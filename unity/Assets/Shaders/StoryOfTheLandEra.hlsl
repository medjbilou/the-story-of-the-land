#ifndef STORY_ERA_INCLUDED
#define STORY_ERA_INCLUDED
// The occupation on the map, June 1967 on (OccupationLayers, C#; tools/occupation_bake.py): the occupied West Bank and Gaza
// Strip washed in the Israeli side's blue, Oslo II's areas from 1995 (Area C the strongest wash, B lighter, A none), the
// wall, Gaza's fence and its access-restricted bands, and the settlements by founding year, all drawn from their true shapes
// (not hex-snapped) out of three textures over the West Bank and Gaza. OccupationLayers sets which parts show on the game's
// date; with no layer (_ScEraGrid.w 0) nothing is sampled. Included by StoryOfTheLandTerrain.hlsl; the terrain calls
// ScEraApply before ScBorderApply. Lines and dots are unlit (Emission), as the borders are; the washes are lit like the ground.

TEXTURE2D(_ScEraTex1);   // r the West Bank, g the Gaza Strip, b Oslo Area A, a Oslo Area B (covered share)
TEXTURE2D(_ScEraTex2);   // r distance to the wall as built, g to the wall not yet built, b to the nearest settlement (x unit),
                         // a Gaza's access-restricted area (1 within 300 m of the fence, 0.5 to 1,000 m)
TEXTURE2D(_ScEraTex3);   // r the nearest settlement's code (texel-exact: year - 1900, + 128 if emptied in August 2005),
                         // g the settlements' built-up areas, b distance to the West Bank's outline, a to Gaza's fence
float4 _ScEraRect;       // x u of the grid's west edge, y v of its south edge, z 1 / its width in u, w 1 / its height in v
float4 _ScEraGrid;       // x texels across, y texels down, z metres per distance unit, w 1 = a layer is given
float4 _ScEraA;          // x occupied (0/1), y Oslo areas (0/1), z Gaza's fence (0/1), w the access-restricted area (0/1)
float4 _ScEraB;          // x settlements founded up to this year (0 none), y 1 = the August 2005 evacuations done,
                         // z built-up areas (0/1), w the wall: 0 none, 1 being built (the route faint), 2 as mapped in 2018
// The destruction of Gaza from October 2023 (GazaDamage, C#; tools/gaza_damage_bake.py): a cell of about 300 m a texel, r the
// UNOSAT image (1..14) by which half its damaged structures were recorded, g the same for destroyed ones (texel-exact), b how
// many were damaged, a the destroyed share (filtered).
TEXTURE2D(_ScWarTex);
float4 _ScWarRect;       // as _ScEraRect, for the damage grid
float4 _ScWarGrid;       // x cells across, y cells down
float4 _ScWar;           // x images recorded by the month's end, y..z the images whose span the month overlaps (smoke; 0 none),
                         // w 1 = on

// Colours (sRGB). The occupation's wash is the blue of the Israeli side (MapBorders.Primary), so the 1948 legend holds;
// the access-restricted area takes the red-orange of the other side's band, hatched so it is told apart without colour.
static const float3 ScEraBlueSrgb = float3(0.29, 0.40, 0.56);
static const float3 ScEraSettleSrgb = float3(0.16, 0.25, 0.47);
static const float3 ScEraPaleSrgb = float3(0.98, 0.97, 0.93);
static const float3 ScEraWallSrgb = float3(0.16, 0.16, 0.18);
static const float3 ScEraAraSrgb = float3(0.75, 0.31, 0.13);
// Wash strengths: the occupied land before Oslo II (and Gaza after it); from 1995 Area C (full Israeli control), Area B
// (Israeli security control) and Area A (none: the Palestinian Authority's areas).
static const float ScEraOccWash = 0.28, ScEraCWash = 0.40, ScEraBWash = 0.18;   // read on the bright hills (captures, 2026-10-22)
// Line half-widths (m) and the least width (px) that keeps each legible at the overview.
static const float ScEraWallHalfM = 70.0, ScEraOutlineHalfM = 45.0, ScEraLineMinPx = 1.3, ScEraOutlineMinPx = 0.9;
// Rubble (damaged), scorched ground (destroyed) and smoke over the cells where destruction is being recorded: restrained,
// a map's tint, never fire or bodies.
static const float3 ScWarRubbleSrgb = float3(0.60, 0.57, 0.53), ScWarScorchSrgb = float3(0.20, 0.18, 0.17), ScWarSmokeSrgb = float3(0.70, 0.70, 0.71);
static const float ScEraDotMinM = 160.0, ScEraDotMaxM = 900.0, ScEraDotPx = 2.6, ScEraRingPx = 0.6;

// Share of a pixel (mpp metres wide) inside a line of half-width halfM (m) at distance d (m).
float ScEraLine(float d, float halfM, float mpp) { return saturate((halfM - d) / mpp + 0.5); }

// Lays colour c at coverage ai over what col and a hold (premultiplied "over").
void ScEraOver(inout float3 col, inout float a, float3 c, float ai)
{
    float na = a + ai * (1.0 - a);
    col = na > 1e-4 ? (col * a * (1.0 - ai) + c * ai) / na : c;
    a = na;
}

// The share of a pixel on the drawn part of a dashed or hatched pattern of period periodM (m) with duty on, box-filtered: far
// off, where the pattern is under a pixel, its mean.
float ScEraDash(float x, float periodM, float on, float mpp)
{
    float ph = frac(x / periodM), w = max(mpp / periodM, 1e-4);
    float c = saturate((on - ph) / w + 0.5) * saturate(ph / w + 0.5);
    return lerp(c, on, saturate(w * 2.0 - 0.5));
}

// mapM: metres east and north of the map's south-west corner; uv the terrain UV; water ScWater's 0 (land) .. 1 (water).
// Call it in uniform control flow: it takes derivatives.
void ScEraApply(float2 uv, float2 mapM, float water, inout float3 baseColor, inout float3 emission)
{
    float mpp = max(max(length(ddx(mapM)), length(ddy(mapM))), 1e-3);   // metres a pixel
    float2 e = (uv - _ScEraRect.xy) * _ScEraRect.zw;
    [branch] if (_ScEraGrid.w > 0.5 && water < 0.999 && all(e > 0.0) && all(e < 1.0))
    {
        float4 t1 = SAMPLE_TEXTURE2D_LOD(_ScEraTex1, sampler_LinearClamp, e, 0);
        float4 t2 = SAMPLE_TEXTURE2D_LOD(_ScEraTex2, sampler_LinearClamp, e, 0);
        float4 t3 = SAMPLE_TEXTURE2D_LOD(_ScEraTex3, sampler_LinearClamp, e, 0);
        float unit = 255.0 * _ScEraGrid.z;   // a distance channel's 1, in metres
        float land = 1.0 - water;

        // The washes (lit): the occupied land, Oslo's areas, the access-restricted bands, the built-up areas.
        float c = saturate(1.0 - t1.b - t1.a);
        float wbWash = lerp(ScEraOccWash, ScEraCWash * c + ScEraBWash * t1.a, _ScEraA.y);
        float wash = _ScEraA.x * (t1.r * wbWash + t1.g * ScEraOccWash) * land;
        baseColor = lerp(baseColor, SRGBToLinear(ScEraBlueSrgb), wash);
        float inner = smoothstep(0.65, 0.85, t2.a), outer = smoothstep(0.15, 0.35, t2.a);
        float hatch = ScEraDash(mapM.x + mapM.y, 180.0, 0.5, mpp);
        float ara = _ScEraA.w * (0.55 * inner + 0.3 * (outer - inner)) * lerp(0.45, 1.0, hatch) * land;
        baseColor = lerp(baseColor, SRGBToLinear(ScEraAraSrgb), ara);
        baseColor = lerp(baseColor, SRGBToLinear(ScEraSettleSrgb), _ScEraB.z * 0.7 * t3.g * land);
        [branch] if (_ScWar.w > 0.5)
        {
            float2 g = (uv - _ScWarRect.xy) * _ScWarRect.zw;
            if (all(g > 0.0) && all(g < 1.0))
            {
                float4 dm = SAMPLE_TEXTURE2D_LOD(_ScWarTex, sampler_LinearClamp, g, 0);
                // The cell's codes, read at a position jittered by up to a cell so the cells' edges fray instead of stepping.
                float2 jitter = float2(ScNoise(mapM / 120.0), ScNoise(mapM / 120.0 + 5.3)) - 0.5;
                float4 code = round(LOAD_TEXTURE2D(_ScWarTex, int2(clamp(g * _ScWarGrid.xy + jitter, 0.0, _ScWarGrid.xy - 1.0))) * 255.0);
                float dens = saturate(dm.b * 1.3) * land;
                float damaged = code.r > 0.5 && code.r <= _ScWar.x ? 1.0 : 0.0;
                float destroyed = code.g > 0.5 && code.g <= _ScWar.x ? 1.0 : 0.0;
                baseColor = lerp(baseColor, SRGBToLinear(ScWarRubbleSrgb), damaged * dens * lerp(0.5, 0.85, ScNoise(mapM / 40.0)));
                baseColor = lerp(baseColor, SRGBToLinear(ScWarScorchSrgb), destroyed * dens * saturate(dm.a * 1.4) * lerp(0.6, 0.9, ScNoise(mapM / 65.0 + 3.1)));
                float fresh = (code.r >= _ScWar.y && code.r <= _ScWar.z) || (code.g >= _ScWar.y && code.g <= _ScWar.z) ? 1.0 : 0.0;
                float plume = smoothstep(0.25, 0.75, ScNoise(mapM / 420.0 + 7.0)) * (0.6 + 0.4 * ScNoise(mapM / 90.0));
                baseColor = lerp(baseColor, SRGBToLinear(ScWarSmokeSrgb), _ScWar.y > 0.5 ? fresh * dens * plume * 0.75 : 0.0);
            }
        }

        // The lines and dots (unlit), bottom to top: the West Bank's outline, Gaza's fence and the wall, the settlements.
        float3 col = 0.0;
        float a = 0.0;
        ScEraOver(col, a, SRGBToLinear(ScEraPaleSrgb), ScEraLine(t3.b * unit, max(ScEraOutlineHalfM, ScEraOutlineMinPx * mpp), mpp) * _ScEraA.x);
        float wallHalf = max(ScEraWallHalfM, ScEraLineMinPx * mpp);
        float fence = ScEraLine(t3.a * unit, wallHalf, mpp) * _ScEraA.z;
        float built = ScEraLine(t2.r * unit, wallHalf, mpp), unbuilt = ScEraLine(t2.g * unit, wallHalf * 0.8, mpp);
        float dash = ScEraDash(mapM.x - mapM.y, 600.0, 0.55, mpp);
        // Being built (2002-2017): OCHA's January 2018 route, faint; as mapped (2018 on): built solid, the rest dashed.
        float wall = _ScEraB.w > 1.5 ? max(built, 0.6 * unbuilt * dash) : (_ScEraB.w > 0.5 ? 0.5 * max(built, unbuilt) : 0.0);
        ScEraOver(col, a, SRGBToLinear(ScEraWallSrgb), max(fence, wall));
        // Settlements: a disc at each point founded by the year shown (gone after August 2005 if emptied then), its radius a
        // few pixels at the overview, within ScEraDotMinM..MaxM, in a pale ring ScEraRingPx wide.
        float code = round(LOAD_TEXTURE2D(_ScEraTex3, int2(e * _ScEraGrid.xy)).r * 255.0);
        float year = 1900.0 + fmod(code, 128.0);
        float shown = code > 0.5 && year <= _ScEraB.x && !(code >= 128.0 && _ScEraB.y > 0.5) ? 1.0 : 0.0;
        float r = clamp(ScEraDotPx * mpp, ScEraDotMinM, ScEraDotMaxM);
        float dd = t2.b * unit;
        ScEraOver(col, a, SRGBToLinear(ScEraPaleSrgb), shown * ScEraLine(dd, r + ScEraRingPx * mpp, mpp));
        ScEraOver(col, a, SRGBToLinear(ScEraSettleSrgb), shown * ScEraLine(dd, r, mpp));
        a *= land;
        baseColor *= 1.0 - a;
        emission = lerp(emission, col, a);
    }
}

#endif

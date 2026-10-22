#ifndef STORY_DETAIL_INCLUDED
#define STORY_DETAIL_INCLUDED
// F6 (owner, 2026-10-19: "good texture and graphics ... like big games"): the ground's close-up detail, laid on the land
// use before the relief shading (StoryOfTheLandTerrain.hlsl), so the colours stay the art style's and only their light varies.
// - Detail: Resources/Map/TerrainDetail.png, the high-passed luminance of three CC0 Poly Haven scans around a 0.5 mean
//   (r aerial_rocks_02, g aerial_ground_rock, b dirt_aerial_02; ASSETS.yaml), rock on the steep faces and stony soil on
//   the rest, at two tilings with offsets (no visible repeat), plus a broad b-channel mottle. The mips fade each tiling to
//   its mean as the view pulls back, so it never shimmers.
// - Terraces (سلاسل): dry-stone risers along the contours of the hill country's moderate slopes, in patches, faded out
//   once they would be closer than a few pixels.
// Desktop tier only: compiled out on WebGL (GLES3 / WebGPU), where shader size, not arithmetic, cost frame time (docs/visual-v1.md
// "Promo track: land use"), and out of every other level's variant by the global keyword _SC_DETAIL (the graph's fragment
// multi_compile; MapShaderGlobals sets it with the strengths): F6 round 3 measured the code alone, skipped by a uniform
// branch, at about 1 ms at the close views. Inside the desktop variant the branch still skips it when Settings or the
// "detail" toggle turn it off.

TEXTURE2D(_ScDetailTex); SAMPLER(sampler_ScDetailTex);
float4 _ScDetail;   // x detail strength, y terrace strength (MapShaderGlobals; 0 = off, the "detail" toggle)

static const float ScDetailNearM = 1500.0, ScDetailFarM = 6100.0;
static const float ScTerraceStepM = 10.0;   // real metres between risers (symbolic: real ones are 1.5-4 m)
static const float ScTerraceWall = 0.06;    // the riser's half-width, in steps (the tread's sunlit lip is twice it)

float3 ScDetail(float3 g, float2 mapM, float3 n, float elevM)
{
#if defined(SHADER_API_GLES3) || defined(SHADER_API_WEBGPU) || !defined(_SC_DETAIL)
    return g;
#else
    // The derivatives first, in uniform control flow, so the branch below may skip everything else: _ScDetail is a global,
    // so every pixel of a draw takes the same side, and with the detail off (the Low tier, the "detail" toggle) the GPU
    // runs none of it.
    float2 dx = ddx(mapM), dy = ddy(mapM);
    float deElev = fwidth(elevM) / ScTerraceStepM;
    [branch] if (_ScDetail.x + _ScDetail.y <= 0.0) return g;
    float slope = 1.0 - saturate(n.y);
    float rockW = smoothstep(0.12, 0.35, slope);
    // Two samples: the far tiling's b channel is the mottle, and the near tiling's b wobbles the terraces (no noise math).
    float4 nearT = SAMPLE_TEXTURE2D_GRAD(_ScDetailTex, sampler_ScDetailTex, mapM / ScDetailNearM, dx / ScDetailNearM, dy / ScDetailNearM);
    float4 farT = SAMPLE_TEXTURE2D_GRAD(_ScDetailTex, sampler_ScDetailTex, mapM / ScDetailFarM + float2(0.37, 0.61), dx / ScDetailFarM, dy / ScDetailFarM);
    float d = (lerp(nearT.g, nearT.r, rockW) - 0.5) * 0.55 + (lerp(farT.g, farT.r, rockW) - 0.5) * 0.45 + (farT.b - 0.5) * 0.5;
    g *= 1.0 + _ScDetail.x * d;

    // Terraces: in the hill country (350-1050 m), on slopes neither flat nor cliff, in patches (the far tiling's mottle).
    float mask = smoothstep(300.0, 420.0, elevM) * (1.0 - smoothstep(950.0, 1100.0, elevM))
        * smoothstep(0.03, 0.08, slope) * (1.0 - smoothstep(0.28, 0.40, slope))
        * smoothstep(0.45, 0.55, farT.b);
    float e = elevM / ScTerraceStepM + (nearT.b - 0.5) * 0.8;   // wobbled: built by hand, not by survey
    float de = max(deElev, 1e-4);   // the wobble's own slope (a 1.5 km tiling's b) is small beside the ground's
    float u = frac(e);                                                        // the riser at 0.5, its tread above it
    float riser = 1.0 - smoothstep(ScTerraceWall - de, ScTerraceWall + de, abs(u - 0.5));
    float lip = smoothstep(0.5 + ScTerraceWall - de, 0.5 + ScTerraceWall + de, u)
        * (1.0 - smoothstep(0.5 + 3.0 * ScTerraceWall - de, 0.5 + 3.0 * ScTerraceWall + de, u));
    float fade = 1.0 - smoothstep(0.12, 0.30, de);                            // gone below about 4 px between risers
    float k = _ScDetail.y * mask * fade;
    g *= 1.0 - 0.24 * riser * k;                                             // the dry-stone wall's shadowed face
    g *= 1.0 + 0.10 * lip * k;                                               // the sunlit edge of the tread above it
    return g;
#endif
}

#endif

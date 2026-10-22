#ifndef STORY_TERRAIN_INCLUDED
#define STORY_TERRAIN_INCLUDED
#include "Assets/Shaders/StoryOfTheLandMap.hlsl"
#include "Assets/Shaders/StoryOfTheLandLand.hlsl"
#include "Assets/Shaders/StoryOfTheLandDetail.hlsl"
#include "Assets/Shaders/StoryOfTheLandEra.hlsl"

// The Custom Function of StoryOfTheLandTerrain.shadergraph (written by tools/terrain_graph.py; keep this signature in step with
// its INPUTS and OUTPUTS). UV is the terrain UV (0..1 over the map); PositionWS and NormalWS are absolute world space;
// Bake is terrain_bake.png (g curvature, b sky-view; its r is not read); Coast is coast.png (0.5 = shore, lower = water);
// Ao is ao.png, the ambient occlusion on its own, uncompressed (BC1 banded it: Task 6 fix round 1); Land is landuse.png
// (tools/land_bake.py: rgb the land's colour, sRGB-decoded by the sampler, a its weight), read by ScLandUse
// (StoryOfTheLandLand.hlsl).
void StoryOfTheLandTerrain_float(float2 UV, float3 PositionWS, float3 NormalWS, UnityTexture2D Bake, UnityTexture2D Coast, UnityTexture2D Ao, UnityTexture2D Land,
    out float3 BaseColor, out float3 NormalOut, out float Occlusion, out float Smoothness, out float Metallic, out float3 Emission)
{
    float3 n = normalize(NormalWS);
    float4 bake = SAMPLE_TEXTURE2D(Bake.tex, Bake.samplerstate, UV);
    bake.r = SAMPLE_TEXTURE2D(Ao.tex, Ao.samplerstate, UV).r;
    float coast = SAMPLE_TEXTURE2D(Coast.tex, Coast.samplerstate, UV).r;
    float2 mapM = UV * _ScMap.xy;
    // ScGround with the land use (StoryOfTheLandLand.hlsl) laid between its colour and its shading: curvature, AO and breakup
    // shade the fields too. The "land" toggle lerps it away (it does not skip the sample: a visual switch, not a price).
    float4 land = SAMPLE_TEXTURE2D(Land.tex, Land.samplerstate, UV);
    float3 ground = ScGroundBase(UV, mapM, n, PositionWS);
    ground = lerp(ground, ScLandUse(ground, land, coast), _ScLandOn);
    float elevM = (PositionWS.y - _ScMap.z) / _ScMap.w;
    ground = ScDetail(ground, mapM, n, elevM);   // F6: rock and soil detail, terraces (StoryOfTheLandDetail.hlsl; desktop only)
    ground = ScGroundShade(ground, mapM, bake);
    float h = _ScHaze.a * ScHazeAmount(PositionWS);                 // land haze, applied after lighting via Emission
    float ao = bake.r * lerp(0.6, 1.0, bake.b);
    float water = ScWater(coast);
    // Water: a black, fully metallic, fully occluded surface with no emission, so Lit outputs 0 there; the water's colour
    // (ScWaterShade, sunlit with its shadows, then the water haze) is added on top, times the same coverage, by the terrain
    // water (Sea.shader's _ScTerrainWater, MapScene.TerrainWaterName): the sum is what this function drew before issue
    // #195. Kept out of here because its size, not its arithmetic, cost about 3 ms a frame at gameplay03 in WebGL even with
    // the water off (perf-investigation). Land keeps its after-lighting haze through Emission.
    ground = ScShore(ground, coast, elevM, n, mapM);   // the beach and the Dead Sea's salt flats (promo-sea)
    // The hex grid (visual v1 Task 8), light and faint on land, thinner towards the overview: the same lines as Sea.shader
    // draws on the water. Computed in uniform control flow: its fwidth needs it.
    float gridLine = ScGridAmount(mapM, PositionWS);
    ground = lerp(ground, SRGBToLinear(ScGridLandSrgb), gridLine * _ScGrid.x);
    #if defined(_SC_DETAIL) && !defined(SHADER_API_GLES3) && !defined(SHADER_API_WEBGPU)
    ground *= ScCloudShade(PositionWS);   // F7: drifting cloud shadows (desktop tier)
    #endif
    BaseColor = ground * (1.0 - h) * (1.0 - water);
    NormalOut = normalize(lerp(n, float3(0, 1, 0), water));
    Occlusion = lerp(lerp(1.0, ao, 0.8), 0.0, water);
    Smoothness = 0.0;
    Metallic = water;
    Emission = _ScHaze.rgb * h * (1.0 - water);
    ScEraApply(UV, mapM, water, BaseColor, Emission);     // the occupation, June 1967 on (StoryOfTheLandEra.hlsl): under the borders
    ScBorderApply(mapM, water, BaseColor, Emission);   // region borders (StoryOfTheLandBorders.hlsl), last: unlit, on land only
}

#endif

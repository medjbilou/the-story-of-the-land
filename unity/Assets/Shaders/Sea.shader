// The border skirt's sea past the map's edge (plan Task 7): the same water as the terrain's own sea, from the same function
// (StoryOfTheLandMap.hlsl ScWaterShade) and coast texture (clamped at the edge), with the same water haze, so no seam shows. Unlit:
// the function lights itself, as the terrain graph's water (emission only) does. DepthOnly and DepthNormals passes keep it
// in the depth prepass on tiers that make one.
// It also shades the water inside the map (issue #195): MapScene's TerrainWater (MapBuilder.BuildWaterMesh, the terrain's
// surface over the water and its shore) is drawn with a copy of Sea.mat that sets _ScTerrainWater, which adds the
// water's colour times its coverage (ScWater) on top of the terrain (additive, no depth write; the land adds 0). The
// terrain graph (StoryOfTheLandTerrain.hlsl) outputs black on the water, so the sum is what it drew before; the water no
// longer weighs on the terrain's own fragment shader, whose size cost about 3 ms a frame at gameplay03.
Shader "StoryOfTheLand/Sea"
{
    Properties
    {
        [NoScaleOffset] _ScCoastTex ("Coast distance (R)", 2D) = "black" {}
        // The skirt's defaults; MapScene's TerrainWater material sets 1, One, One and 0.
        [HideInInspector] _ScTerrainWater ("Terrain water", Float) = 0
        [HideInInspector] _ScSrcBlend ("Source blend", Float) = 1
        [HideInInspector] _ScDstBlend ("Destination blend", Float) = 0
        [HideInInspector] _ScZWrite ("Depth write", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float _ScTerrainWater;   // 1 = the terrain's water (MapScene.TerrainWaterName), 0 = the skirt's sea
        CBUFFER_END
        struct Attributes { float4 positionOS : POSITION; };
        ENDHLSL

        Pass
        {
            Name "Sea"
            Tags { "LightMode" = "UniversalForward" }
            Blend [_ScSrcBlend] [_ScDstBlend]
            ZWrite [_ScZWrite]
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            // The main light's shadows, as the terrain graph's forward pass has them (ScWaterShade samples them).
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Assets/Shaders/StoryOfTheLandMap.hlsl"
            TEXTURE2D(_ScCoastTex); SAMPLER(sampler_ScCoastTex);
            // The terrain water's depth pull (pixels' worth of metres towards the camera; see Vert).
            static const float ScWaterDepthPullPx = 2.0;
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
                o.positionCS = p.positionCS; o.positionWS = p.positionWS;
                // The terrain water (_ScTerrainWater: a uniform, not a keyword, so no extra variants to load). The terrain
                // draws the same surface split and simplified its own way (its LOD keeps within a pixel), so
                // the water's depth is pulled ScWaterDepthPullPx pixels' worth of metres towards the camera along the view
                // ray: the same pixel, never behind the terrain it lies on (a depth offset alone left specks at the
                // overview). Drawn before every other opaque (MapScene.TerrainWaterQueue), so it covers nothing else.
                // Shading keeps the true position.
                // An orthographic camera (MapCapture.TopDown, the top-down sea tests) has parallel rays and pixels the same
                // size at every distance: the pull goes along its forward axis, by its pixel size, or the water would slide
                // across the screen towards the camera's axis.
                float dist = max(length(_WorldSpaceCameraPos - p.positionWS), 1.0);
                bool perspective = IsPerspectiveProjection();
                float3 towards = perspective ? (_WorldSpaceCameraPos - p.positionWS) / dist : -GetViewForwardDir();
                float pixelM = (perspective ? dist : 1.0) * 2.0 / (abs(UNITY_MATRIX_P._m11) * _ScreenParams.y);
                float pull = _ScTerrainWater * min(ScWaterDepthPullPx * pixelM, 0.5 * dist);
                o.positionCS = TransformWorldToHClip(p.positionWS + towards * pull);
                return o;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float2 mapM = input.positionWS.xz - _ScMapOrigin.xz;
                float coast = SAMPLE_TEXTURE2D(_ScCoastTex, sampler_ScCoastTex, saturate(mapM / _ScMap.xy)).r;
                // Every derivative in uniform control flow (the derivatives below and ScWaterShade's).
                float footprintM = max(length(ddx(mapM)), length(ddy(mapM)));
                float2 breakers = ScBreakers(coast, mapM);
                // The hex lattice runs on past the map's edge, exactly as on the terrain (review I3).
                float gridLine = ScGridAmount(mapM, input.positionWS) * _ScGrid.y;
                bool onTerrain = _ScTerrainWater > 0.5;
                // The terrain's coverage (0 on land, a ramp about 140 m wide at the shore) and the rift lakes' depth (salt,
                // fresh); the skirt is all sea at sea level.
                // No discard on the land: a per-pixel discard splits the 2x2 quads along the shore, where the derivatives
                // the water needs come from (the lesson of #306); the land's fragments add colour times 0. The mesh holds
                // only the water and two heightmap samples of shore, so few land fragments run.
                float water = onTerrain ? ScWater(coast) : 1.0;
                float elevM = onTerrain ? (input.positionWS.y - _ScMap.z) / _ScMap.w : 0.0;
                float3 c = ScHazeWater(ScWaterShade(coast, elevM, input.positionWS, mapM, footprintM, breakers), input.positionWS);
                c = lerp(c, SRGBToLinear(ScGridWaterSrgb), gridLine);
                return half4(c * water, onTerrain ? 0.0 : 1.0);   // the terrain water adds; the target's alpha is left as it is
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            float4 Vert(Attributes input) : SV_POSITION { return TransformObjectToHClip(input.positionOS.xyz); }
            half Frag() : SV_Target { return 0; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            float4 Vert(Attributes input) : SV_POSITION { return TransformObjectToHClip(input.positionOS.xyz); }
            half4 Frag() : SV_Target { return half4(0.0, 1.0, 0.0, 0.0); }   // the sea faces up
            ENDHLSL
        }
    }
}

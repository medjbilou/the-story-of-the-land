// F6 round 5 (the owner: "not show and hide and show and hide the buildings"): the Collection's models (URP Lit, a base
// map and a colour) during a 0.4 s swap: URP Lit's own forward pass behind ScFade.hlsl's screen-door clip, _ScFade from a
// property block. ModelFade swaps a copy of a model's material to this shader only while it fades, so no other draw
// discards. In Resources so a player build carries it (ModelFade loads it by its Resources path). Round 6: the shadow caster
// clips with the same screen-door in shadow-map texels, so the shadow thins with the model instead of dropping when the fade
// ends; the depth passes are Lit's.
Shader "StoryOfTheLand/CollectionFade"
{
    Properties
    {
        [MainTexture] _BaseMap ("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor ("Color", Color) = (1, 1, 1, 1)
        _Cutoff ("Alpha cutoff", Range(0, 1)) = 0.5
        _Smoothness ("Smoothness", Range(0, 1)) = 0.5
        _Metallic ("Metallic", Range(0, 1)) = 0
        _ScFade ("Screen-door fade (1 = whole)", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex LitPassVertex
            #pragma fragment FadeFrag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"
            #include "Assets/Shaders/ScFade.hlsl"
            half _ScFade;   // outside UnityPerMaterial: set per renderer by a property block

            void FadeFrag(Varyings input, out half4 outColor : SV_Target0)
            {
                ScFadeClip(input.positionCS.xy, _ScFade);
                LitPassFragment(input, outColor);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex ShadowPassVertex
            #pragma fragment FadeShadowFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            #include "Assets/Shaders/ScFade.hlsl"
            half _ScFade;

            half4 FadeShadowFrag(Varyings input) : SV_TARGET
            {
                ScFadeClip(input.positionCS.xy, _ScFade);
                return ShadowPassFragment(input);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/DEPTHONLY"
        UsePass "Universal Render Pipeline/Lit/DEPTHNORMALS"
    }
}

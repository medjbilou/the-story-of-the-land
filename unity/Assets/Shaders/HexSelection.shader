// The selected hex's outline band (MapScene.Select). Unlit, one colour, drawn after the hex overlay with a depth offset
// towards the camera, so the overlay's lines under the band's edge never z-fight with it, even at the camera's maximum
// distance where a 24-bit depth step is tens of metres (fix round 1, M2). Both faces: the band is seen from every yaw.
// A DepthOnly pass puts it in the depth texture as well (fix round 2, N6).
Shader "StoryOfTheLand/HexSelection"
{
    Properties
    {
        [MainColor] _BaseColor("Color", Color) = (1, 0.95, 0.80, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry+10" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            ZWrite On
            ZTest LEqual
            Offset -1, -4
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };
            Varyings Vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return o;
            }
            half4 Frag(Varyings input) : SV_Target { return _BaseColor; }
            ENDHLSL
        }
        // Writes the band into the depth prepass and the camera depth texture (fix round 2, N6), so depth of field and any
        // other depth reader see the band, not the terrain under it. Same depth offset as the colour pass.
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            Cull Off
            ZWrite On
            ColorMask R
            Offset -1, -4
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            // The same per-material layout as the colour pass, as the SRP Batcher requires of every pass (fix round 3, m-2).
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };
            Varyings Vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return o;
            }
            half Frag(Varyings input) : SV_Target { return input.positionCS.z; }
            ENDHLSL
        }
    }
}

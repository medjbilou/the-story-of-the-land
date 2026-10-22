// Settlement kits and trees (art spike, issues #31 #32 #34 #37). Vertex colour is the sRGB albedo; its alpha picks a
// facade's window pattern (KitMesh.Facade), drawn from uv0 = the wall's (along, up) position in real metres, so storeys and
// windows keep real proportions on symbolic-size buildings, box-filtered so they fade to their mean instead of shimmering
// when a storey is a pixel or two. Lit like the terrain (the sun with its shadows, the trilight ambient through SH; in shade
// the ambient also takes a sun-warm bounce, KitBounce, so a face keeps its own colour) and
// hazed with the terrain's own ScHazeAmount, so kits sit in the same air as the ground. GPU instancing for the trees, with a
// per-instance tint hashed from the instance's origin (no per-instance buffer: WebGL 2 has instanced arrays only).
// Shadows use the sun's own URP bias, unchanged (ApplyShadowBias): KitTests.AKitShadowStartsAtItsWalls finds no gap at
// the walls with it and the hero renders show no acne (#37, review I3), so the kits carry no bias of their own.
Shader "StoryOfTheLand/Kit"
{
    Properties
    {
        _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        _Jitter ("Per-instance tint jitter", Range(0, 0.5)) = 0
        _WindowDark ("Window darkness", Range(0, 1)) = 0.55
        _CityWindowDark ("White City window darkness", Range(0, 1)) = 0.95
        _ScFade ("Screen-door fade (1 = whole)", Range(0, 1)) = 1
        _Sway ("Wind sway (shear per unit height)", Range(0, 0.1)) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half _Jitter, _WindowDark, _CityWindowDark, _ScFade, _Sway;
        CBUFFER_END
        float4 _ScWind;   // F7: x the wind's clock (s), y 1 = the trees sway (VegetationLayer: in play, without reduced motion)

        // F7 (the owner: "keep the game ... not freezed or static"): the trees sway in the wind. A shear that grows with the
        // height above the mesh's foot, about _Sway radians, two gusts of a phase set by where the tree stands, so the grove
        // ripples rather than swinging as one. Vertex-only and shared by every pass, so the shadows sway with the crowns; the
        // kit buildings carry _Sway 0 and stay still.
        float3 ScSway(float3 positionOS)
        {
            float3 origin = TransformObjectToWorld(float3(0, 0, 0));
            float t = _ScWind.x * 1.3 + frac(dot(origin.xz, float2(0.000137, 0.000091))) * 6.2832;
            float bend = _Sway * _ScWind.y * max(positionOS.y, 0.0);
            positionOS.x += bend * (sin(t) + 0.35 * sin(t * 2.3 + 1.7));
            positionOS.z += bend * 0.6 * sin(t * 0.83 + 0.9);
            return positionOS;
        }
        ENDHLSL

        Pass
        {
            Name "Kit"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            // F6: the desktop tier's dressed stone (below) rides on the ground detail's global keyword (MapShaderGlobals), so
            // the Low tier's variant (and the trees', which share this shader) carries none of its code.
            #pragma multi_compile_fragment _ _SC_DETAIL
            #pragma multi_compile_local_fragment _ _SC_FADE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Assets/Shaders/StoryOfTheLandMap.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION; float3 normalOS : NORMAL; half4 color : COLOR; float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1;
                half4 color : TEXCOORD2; float2 uv : TEXCOORD3; half tint : TEXCOORD4;
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                VertexPositionInputs p = GetVertexPositionInputs(ScSway(v.positionOS.xyz));
                o.positionCS = p.positionCS; o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.color = v.color; o.uv = v.uv;
                float3 origin = TransformObjectToWorld(float3(0, 0, 0));
                o.tint = ScHash(origin.xz * 0.0137 + 3.1);
                return o;
            }

            // How much of the ambient in full shade is the sun's bounce (kit shadow colour, refs #32).
            static const half KitBounce = 0.6;

            // A 1D band [a, b] of a period-1 pattern, box-filtered over the pixel's footprint w (in periods).
            float Band(float x, float a, float b, float w)
            {
                float lo = saturate((frac(x) - a) / max(w, 1e-4) + 0.5), hi = saturate((frac(x) - b) / max(w, 1e-4) + 0.5);
                return lo - hi;
            }

            // Window mask (1 = glass) and its mean over a whole storey, for each facade style.
            float Windows(float style, float2 uv, float2 fw, out float mean)
            {
                float storey = 3.3, bay = style > 0.75 ? 2.0 : (style > 0.4 ? 4.2 : 3.0);
                float wv = fw.y / storey, wu = fw.x / bay;
                float a0, a1, b0, b1;
                if (style > 0.75) { a0 = 0.36; a1 = 0.86; b0 = 0.12; b1 = 0.84; }        // white city: tall wide windows, plaster piers between
                else if (style > 0.4) { a0 = 0.40; a1 = 0.72; b0 = 0.38; b1 = 0.62; }   // village / moshava: a few small windows
                else { a0 = 0.36; a1 = 0.74; b0 = 0.34; b1 = 0.64; }                    // stone: arched-height windows
                mean = (a1 - a0) * (b1 - b0);
                float m = Band(uv.y / storey, a0, a1, wv) * Band(uv.x / bay, b0, b1, wu);
                // Fade to the mean once a storey is under about three pixels: no moire at the gameplay zoom. The White City's
                // storeys hold until about two pixels (the box filter keeps them clean there), so its 30 km look keeps its
                // window rows (settlements quick wins, #31).
                float hold = style > 0.75 ? saturate(2.2 - wv * 4.4) : saturate(1.5 - wv * 3.0);
                return lerp(mean, m, hold) * step(0.0, uv.y);
            }

            // F6 round 4: the screen-door fade (ScFade.hlsl). Only the _SC_FADE variant (a material swapped in during a fade)
            // discards: this shader also draws every tree.
            #include "Assets/Shaders/ScFade.hlsl"

            half4 Frag(Varyings i) : SV_Target
            {
                #if defined(_SC_FADE)
                ScFadeClip(i.positionCS.xy, _ScFade);
                #endif
                float3 n = SafeNormalize(i.normalWS);   // never NaN, even for a degenerate face
                half3 albedo = SRGBToLinear(i.color.rgb) * _BaseColor.rgb;
                // Per-instance (tree) tint: brighter or darker, a touch warmer or cooler.
                float t = i.tint - 0.5;
                albedo *= 1.0 + _Jitter * t * 2.0;
                albedo.r *= 1.0 + _Jitter * (frac(i.tint * 7.31) - 0.5);
                float style = i.color.a;
                float2 fw = fwidth(i.uv);   // outside the branch: derivatives need uniform control flow
                if (style > 0.1)
                {
                    float mean;
                    float glass = style > 0.2 ? Windows(style, i.uv, fw, mean) : 0.0;   // a Terrace wall (KitMesh.Facade) has none
                    // White-city balconies: a lighter slab line and its shadow under it, every storey.
                    if (style > 0.75)
                    {
                        float fv = fw.y / 3.3;
                        float slab = Band(i.uv.y / 3.3, 0.28, 0.36, fv), under = Band(i.uv.y / 3.3, 0.36, 0.42, fv);
                        float fade = saturate(2.2 - fv * 4.4);
                        albedo *= 1.0 + 0.10 * lerp(0.08, slab, fade) - 0.45 * lerp(0.06, under, fade);
                    }
                    // The White City's ribbon windows sit deep under the balcony slabs and read near black in promo 03
                    // (settlements quick wins, #31); the stone and village windows keep the shared darkness.
                    albedo *= 1.0 - (style > 0.75 ? _CityWindowDark : _WindowDark) * glass;
                    // Contact darkening in the bottom real 3 m of a wall; not on a terrace wall (Facade.Terrace), which is all
                    // below its ground line (uv.y < 0) and stands in the hill.
                    float terrace = step(style, 0.2);
                    albedo *= lerp(lerp(0.72, 1.0, saturate(i.uv.y / 3.0)), 1.0, terrace);
                    // F6: dressed-stone courses on the stone and village walls (the hill villages' and the old towns' ashlar):
                    // courses 0.55 real m high, blocks 1.1 m long and staggered, each its own shade, mortar joints darker; faded
                    // to their mean, as the windows are, once a course is under about three pixels.
                    #if defined(_SC_DETAIL)
                    if (style <= 0.75)
                    {
                        float course = floor(i.uv.y / 0.55), cv = fw.y / 0.55, cu = fw.x / 1.1;
                        float u = i.uv.x / 1.1 + frac(course * 0.5);   // every other course staggered by half a block
                        float block = Band(i.uv.y / 0.55, 0.07, 1.0, cv) * Band(u, 0.04, 1.0, cu);   // 1 inside a block, 0 on mortar
                        float shade = (0.80 + 0.20 * block) * (0.94 + 0.12 * ScHash(float2(floor(u), course)));
                        albedo *= lerp(1.0, shade, saturate(1.5 - max(cv, cu) * 3.0) * max(step(0.0, i.uv.y), terrace));
                    }
                    #endif
                }
                Light sun = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                float ndl = saturate(dot(n, sun.direction));
                // The ambient takes the sun's warmth where the sun does not reach (the bounce off the sunlit ground; same
                // brightness, the trilight's luminance): with the trilight's own colour, near grey at a wall and sky blue on
                // a roof, a pale face in shade had almost no colour left, and the grade's teal shadows turned it blue-grey.
                float sunlit = ndl * sun.shadowAttenuation;
                #if defined(_SC_DETAIL)
                sunlit *= ScCloudShade(i.positionWS);   // F7: the drifting cloud shadows the terrain has (desktop tier)
                #endif
                half3 sh = SampleSH(n);
                half3 bounce = Luminance(sh) * sun.color / max(Luminance(sun.color), 1e-4);
                half3 lit = albedo * (lerp(sh, bounce, KitBounce * (1.0 - sunlit)) + sun.color * sunlit);
                float h = _ScHaze.a * ScHazeAmount(i.positionWS);
                return half4(lerp(lit, _ScHaze.rgb, h), 1.0);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0
            Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection;
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            float4 Vert(Attributes v) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(v);
                float3 positionWS = TransformObjectToWorld(ScSway(v.positionOS.xyz));
                float3 normalWS = TransformObjectToWorldNormal(v.normalOS);
                return ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection)));
            }
            half4 Frag() : SV_Target { return 0; }
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
            #pragma multi_compile_instancing
            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            float4 Vert(Attributes v) : SV_POSITION { UNITY_SETUP_INSTANCE_ID(v); return TransformObjectToHClip(ScSway(v.positionOS.xyz)); }
            half Frag() : SV_Target { return 0; }
            ENDHLSL
        }
    }
}

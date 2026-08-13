Shader "Custom/New_StylizedLeaves_Verde"
{
    Properties
    {
        _MainTex ("Leaves Texture (RGBA)", 2D) = "white" {}

        [Header(Base Colors)]
        _MainColor ("Main Color", Color) = (0.18, 0.62, 0.25, 1)
        _ShadowColor ("Shadow Color", Color) = (0.05, 0.22, 0.18, 1)
        _HighlightColor ("Highlight Color", Color) = (0.55, 0.85, 0.2, 1)

        [Header(Color Variation)]
        _ColorVariation ("Color Variation Strength", Range(0, 1)) = 0.3
        _ColorVariationScale ("Color Variation Scale", Range(0.1, 5)) = 1.5

        [Header(Z Gradient)]
        _ZGradientColor ("Z Gradient Color Bottom", Color) = (0.04, 0.18, 0.14, 1)
        _ZGradientPos ("Z Gradient Position", Range(-2, 2)) = 0.3
        _ZGradientFactor ("Z Gradient Factor", Range(0, 1)) = 0.04

        [Header(Spherical Shading)]
        _CanopyCenterHeight ("Canopy Center Height", Float) = 1.5
        _FakeNormalBlend ("Spherical Normal Blend", Range(0, 1)) = 0.85
        _ShadowSharpness ("Shadow Sharpness", Range(0.01, 1)) = 0.5
        _ShadowStrength ("Shadow Strength", Range(0, 1)) = 0.6
        _HighlightSharpness ("Highlight Sharpness", Range(0.01, 1)) = 0.5
        _HighlightStrength ("Highlight Strength", Range(0, 1)) = 0.5

        [Header(Light Manager Response)]
        _LightColorInfluence ("Sun Color Influence", Range(0, 1)) = 0.6
        _AmbientInfluence ("Ambient Influence", Range(0, 1)) = 0.25

        [Header(Transparency)]
        _AlphaCutoff ("Alpha Cutoff", Range(0, 1)) = 0.35
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "TransparentCutout"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "AlphaTest"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_instancing
            #pragma multi_compile _ UNITY_SINGLE_PASS_STEREO STEREO_INSTANCING_ON STEREO_MULTIVIEW_ON
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _MainColor;
                float4 _ShadowColor;
                float4 _HighlightColor;
                float4 _ZGradientColor;
                float _ColorVariation;
                float _ColorVariationScale;
                float _ZGradientPos;
                float _ZGradientFactor;
                float _CanopyCenterHeight;
                float _FakeNormalBlend;
                float _ShadowSharpness;
                float _ShadowStrength;
                float _HighlightSharpness;
                float _HighlightStrength;
                float _LightColorInfluence;
                float _AmbientInfluence;
                float _AlphaCutoff;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float3 positionWS   : TEXCOORD1;
                float3 normalWS     : TEXCOORD2;
                float3 sphereNormWS : TEXCOORD3;
                float  fogCoord     : TEXCOORD4;
                float4 shadowCoord  : TEXCOORD5;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float hash2D(float2 p)
            {
                p = frac(p * float2(234.34, 435.345));
                p += dot(p, p + 34.23);
                return frac(p.x * p.y);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.normalWS   = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv         = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.fogCoord   = ComputeFogFactor(OUT.positionCS.z);
                OUT.shadowCoord = TransformWorldToShadowCoord(OUT.positionWS);

                float3 canopyCenterOS = float3(0.0, _CanopyCenterHeight, 0.0);
                float3 sphereNormOS   = normalize(IN.positionOS.xyz - canopyCenterOS);
                OUT.sphereNormWS      = TransformObjectToWorldNormal(sphereNormOS);

                return OUT;
            }

            half4 frag(Varyings IN, bool frontFace : SV_IsFrontFace) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                half4 texSample = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                clip(texSample.a - _AlphaCutoff);

                Light mainLight = GetMainLight(IN.shadowCoord);
                float3 lightDir = normalize(mainLight.direction);

                float3 realNormal = normalize(IN.normalWS) * (frontFace ? 1.0 : -1.0);
                float3 sphereNormal = normalize(IN.sphereNormWS);
                float3 normal = normalize(lerp(realNormal, sphereNormal, _FakeNormalBlend));

                float NdotL = dot(normal, lightDir);

                float shadow = smoothstep(-_ShadowSharpness, _ShadowSharpness, NdotL);
                shadow *= lerp(1.0, mainLight.shadowAttenuation, _ShadowStrength);

                float highlight = smoothstep(1.0 - _HighlightSharpness, 1.0, NdotL) * _HighlightStrength;

                float2 varUV = IN.positionWS.xz * _ColorVariationScale * 0.1;
                float variation = hash2D(floor(varUV * 8.0)) * _ColorVariation;
                float3 variedMain = _MainColor.rgb * (1.0 + variation * 0.5 - _ColorVariation * 0.25);

                float zGradientT = saturate((IN.positionWS.y - _ZGradientPos) * _ZGradientFactor + 0.5);
                float3 gradientColor = lerp(_ZGradientColor.rgb, variedMain, zGradientT);

                float3 shadedColor = lerp(_ShadowColor.rgb, gradientColor, shadow);
                float3 finalColor = lerp(shadedColor, _HighlightColor.rgb, highlight);

                finalColor *= lerp(float3(1, 1, 1), texSample.rgb * 2.0, 0.3);
                finalColor *= lerp(float3(1, 1, 1), mainLight.color, _LightColorInfluence);
                finalColor += SampleSH(normal) * _AmbientInfluence;
                finalColor = saturate(finalColor);
                finalColor = MixFog(finalColor, IN.fogCoord);

                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            Cull Off
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing
            #pragma multi_compile _ UNITY_SINGLE_PASS_STEREO STEREO_INSTANCING_ON STEREO_MULTIVIEW_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _MainColor;
                float4 _ShadowColor;
                float4 _HighlightColor;
                float4 _ZGradientColor;
                float _ColorVariation;
                float _ColorVariationScale;
                float _ZGradientPos;
                float _ZGradientFactor;
                float _CanopyCenterHeight;
                float _FakeNormalBlend;
                float _ShadowSharpness;
                float _ShadowStrength;
                float _HighlightSharpness;
                float _HighlightStrength;
                float _LightColorInfluence;
                float _AmbientInfluence;
                float _AlphaCutoff;
            CBUFFER_END

            float3 _LightDirection;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings ShadowVert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);

                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 normalWS   = TransformObjectToWorldNormal(IN.normalOS);
                positionWS = ApplyShadowBias(positionWS, normalWS, _LightDirection);
                OUT.positionCS = TransformWorldToHClip(positionWS);

                #if UNITY_REVERSED_Z
                    OUT.positionCS.z = min(OUT.positionCS.z, OUT.positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #else
                    OUT.positionCS.z = max(OUT.positionCS.z, OUT.positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #endif

                return OUT;
            }

            half4 ShadowFrag(Varyings input) : SV_TARGET
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                clip(tex.a - _AlphaCutoff);
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}

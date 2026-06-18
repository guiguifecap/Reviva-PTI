Shader "Custom/StylizedLeaves_Yellow"
{
    Properties
    {
        _MainTex ("Leaves Texture (RGBA)", 2D) = "white" {}
        
        [Header(Base Colors)]
        _MainColor ("Main Color", Color) = (0.52, 0.62, 0.08, 1)
        _ShadowColor ("Shadow Color", Color) = (0.20, 0.26, 0.04, 1)
        _HighlightColor ("Highlight Color", Color) = (0.78, 0.88, 0.10, 1)
        
        [Header(Color Variation)]
        _ColorVariation ("Color Variation Strength", Range(0, 1)) = 0.3
        _ColorVariationScale ("Color Variation Scale", Range(0.1, 5)) = 1.5
        
        [Header(Z Gradient)]
        _ZGradientColor ("Z Gradient Color (Bottom)", Color) = (0.14, 0.18, 0.02, 1)
        _ZGradientPos ("Z Gradient Position", Range(-2, 2)) = 0.3
        _ZGradientFactor ("Z Gradient Factor", Range(0, 1)) = 0.04
        
        [Header(Shading)]
        _ShadowSharpness ("Shadow Sharpness", Range(0.01, 1)) = 0.5
        _ShadowStrength ("Shadow Strength", Range(0, 1)) = 0.6
        _HighlightSharpness ("Highlight Sharpness", Range(0.01, 1)) = 0.5
        _HighlightStrength ("Highlight Strength", Range(0, 1)) = 0.5
        
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

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT

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
                float _ShadowSharpness;
                float _ShadowStrength;
                float _HighlightSharpness;
                float _HighlightStrength;
                float _AlphaCutoff;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
            };

            // Hash estático por posição — sem _Time, nunca muda
            float hash2D(float2 p)
            {
                p = frac(p * float2(234.34, 435.345));
                p += dot(p, p + 34.23);
                return frac(p.x * p.y);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.normalWS   = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv         = TRANSFORM_TEX(IN.uv, _MainTex);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // Textura + alpha cutout
                half4 texSample = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                clip(texSample.a - _AlphaCutoff);

                // ---- LUZ (apenas direção + sombra, sem PBR) ----
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(IN.positionWS));
                float3 lightDir = normalize(mainLight.direction);
                float3 normal   = normalize(IN.normalWS);

                float NdotL = dot(normal, lightDir);

                // Sombra suave — cel shading leve
                float shadow = smoothstep(-_ShadowSharpness, _ShadowSharpness, NdotL);
                shadow *= lerp(1.0, mainLight.shadowAttenuation, _ShadowStrength);

                // Highlight suave
                float highlight = smoothstep(1.0 - _HighlightSharpness, 1.0, NdotL) * _HighlightStrength;

                // ---- VARIAÇÃO DE COR ESTÁTICA ----
                float2 varUV = IN.positionWS.xz * _ColorVariationScale * 0.1;
                float variation = hash2D(floor(varUV * 8.0)) * _ColorVariation;
                float3 variedMain = _MainColor.rgb * (1.0 + variation * 0.5 - _ColorVariation * 0.25);

                // ---- GRADIENTE DE ALTURA ----
                float zGradientT = saturate((IN.positionWS.y - _ZGradientPos) * _ZGradientFactor + 0.5);
                float3 gradientColor = lerp(_ZGradientColor.rgb, variedMain, zGradientT);

                // ---- COMPOSIÇÃO FINAL ----
                float3 shadedColor = lerp(_ShadowColor.rgb, gradientColor, shadow);
                float3 finalColor  = lerp(shadedColor, _HighlightColor.rgb, highlight);

                // Tint leve da textura
                finalColor *= lerp(float3(1,1,1), texSample.rgb * 2.0, 0.35);
                finalColor  = saturate(finalColor);

                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }

        // Shadow caster leve
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

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"

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
                float _ShadowSharpness;
                float _ShadowStrength;
                float _HighlightSharpness;
                float _HighlightStrength;
                float _AlphaCutoff;
            CBUFFER_END

            half4 ShadowFrag(Varyings input) : SV_TARGET
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                clip(tex.a - _AlphaCutoff);
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}

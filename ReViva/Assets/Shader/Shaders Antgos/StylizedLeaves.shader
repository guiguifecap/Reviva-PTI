// StylizedLeaves.shader
// Replicates the Blender "Tree Leaves Shader 01 (Custom)" node setup for Unity URP
// Features: alpha clip transparency, geometry-based color variation, multi-layer shadow/highlight,
//           Z-gradient, relight shadows, normal-based shading, disable cast shadows option

Shader "Custom/StylizedLeaves"
{
    Properties
    {
        // ---- Texture ----
        [Header(Texture)]
        _MainTex            ("Leaf Texture (RGBA)",     2D)     = "white" {}
        _AlphaClip          ("Alpha Clip Threshold",    Range(0,1)) = 0.5

        // ---- Main Color ----
        [Header(Main Color)]
        _MainColor          ("Main Color",              Color)  = (0.35, 0.65, 0.15, 1)
        _SeedColorVariation ("Seed Color Variations",   Range(0,1)) = 0.0
        _ScaleColorVariation("Scale Color Variations",  Range(0,1)) = 1.5

        // ---- Factor Color Variation ----
        _FactorColorVar     ("Factor Color Variations", Range(0,1)) = 0.8
        _TransitionVarFactor("Transition Variations Factor", Range(0,1)) = 1.0

        // ---- Shadow ----
        [Header(Shadows)]
        _ShadowColor        ("Shadow Color",            Color)  = (0.05, 0.12, 0.03, 1)
        _PositionShadows    ("Position Shadows",        Range(0,1)) = 0.0
        _ContrastLightShadow("Contrast Light/Shadow",   Range(0,5)) = 2.5
        _FactorShadows      ("Factor Shadows",          Range(0,1)) = 1.0

        // ---- Darker Shadows ----
        _PositionDarkerShadows ("Position Darker Shadows", Range(0,1)) = 0.4
        _ContrastDarkerShadows ("Contrast Darker Shadows", Range(0,10))= 5.0
        _FactorDarkerShadows   ("Factor Darker Shadows",   Range(0,1)) = 1.0

        // ---- Relight Shadows ----
        [Header(Relight Shadows)]
        _RelightShadowsColor    ("Relight Shadows Color",   Color)  = (0.0, 0.35, 0.25, 1)
        _PositionRelightShadows ("Position Relight Shadows",Range(0,1)) = 0.5
        _ContrastRelightShadows ("Contrast Relight Shadows",Range(0,20))= 15.0
        _FactorRelightShadows   ("Factor Relight Shadows",  Range(0,1)) = 0.4

        // ---- Highlight ----
        [Header(Highlight)]
        _HighlightColor     ("Highlight Color",         Color)  = (0.9, 0.95, 0.6, 1)
        _PositionHighlight  ("Position Highlight",      Range(0,1)) = 0.173
        _ContrastHighlight  ("Contrast Highlight",      Range(0,5)) = 1.5
        _FactorHighlight    ("Factor Highlight",        Range(0,1)) = 1.0

        // ---- Darker Highlight Shadows ----
        [Header(Highlight Darker Shadows)]
        _PositionDarkerHLShadows ("Position Darker Highlight Shadows", Range(0,1)) = 0.1
        _FactorDarkerHLShadows   ("Factor Darker HL Shadows",          Range(0,1)) = 0.1

        // ---- Z Gradient ----
        [Header(Z Gradient)]
        _ZGradientColor     ("Z Gradient Color",        Color)  = (0.05, 0.25, 0.05, 1)
        _PositionZGradient  ("Position Z Gradient",     Range(-1,1)) = 0.3
        _FactorZGradient    ("Factor Z Gradient",       Range(0,1)) = 0.5
        _FactorZGradient2   ("Factor Z Gradient 2",     Range(0,1)) = 0.340

        // ---- Normals ----
        [Header(Normals)]
        _SmoothNormalsFactor("Smooth Normals Factor",   Range(0,1)) = 0.15
        _SphereNormalsFactor("Sphere Normals Factor",   Range(0,1)) = 0.0
        _TrivialNormalsFactor("Individual Normals Factor",Range(0,1))= 0.1

        // ---- Options ----
        [Header(Options)]
        [Toggle] _DoubleSided   ("Double Sided",        Float) = 1
        [Toggle] _DisableShadow ("Disable Cast Shadow (Light Path)", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType"     = "TransparentCutout"
            "Queue"          = "AlphaTest"
        }

        // -------------------------------------------------------
        // PASS 1 – Forward Lit (main shading)
        // -------------------------------------------------------
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off   // double-sided leaves

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // ---------- Properties ----------
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float  _AlphaClip;

                float4 _MainColor;
                float  _SeedColorVariation;
                float  _ScaleColorVariation;
                float  _FactorColorVar;
                float  _TransitionVarFactor;

                float4 _ShadowColor;
                float  _PositionShadows;
                float  _ContrastLightShadow;
                float  _FactorShadows;

                float  _PositionDarkerShadows;
                float  _ContrastDarkerShadows;
                float  _FactorDarkerShadows;

                float4 _RelightShadowsColor;
                float  _PositionRelightShadows;
                float  _ContrastRelightShadows;
                float  _FactorRelightShadows;

                float4 _HighlightColor;
                float  _PositionHighlight;
                float  _ContrastHighlight;
                float  _FactorHighlight;

                float  _PositionDarkerHLShadows;
                float  _FactorDarkerHLShadows;

                float4 _ZGradientColor;
                float  _PositionZGradient;
                float  _FactorZGradient;
                float  _FactorZGradient2;

                float  _SmoothNormalsFactor;
                float  _SphereNormalsFactor;
                float  _TrivialNormalsFactor;
                float  _DisableShadow;
            CBUFFER_END

            // ---------- Vertex ----------
            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float2 uv           : TEXCOORD0;
                float4 color        : COLOR;      // vertex color — encodes leavesBlock variation
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS  : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float3 positionWS   : TEXCOORD1;
                float3 normalWS     : TEXCOORD2;
                float4 shadowCoord  : TEXCOORD3;
                float4 vertexColor  : COLOR;
                float  fogFactor    : TEXCOORD4;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                OUT.positionWS  = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionHCS = TransformWorldToHClip(OUT.positionWS);
                OUT.normalWS    = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv          = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.vertexColor = IN.color;
                OUT.shadowCoord = TransformWorldToShadowCoord(OUT.positionWS);
                OUT.fogFactor   = ComputeFogFactor(OUT.positionHCS.z);
                return OUT;
            }

            // ---------- Helpers ----------

            // Soft clamp that mimics Blender's ColorRamp (linear) between two positions
            // Returns [0,1] band centered at 'center' with given 'contrast'
            float colorRamp(float value, float center, float contrast)
            {
                return saturate((value - center) * contrast + 0.5);
            }

            // Pseudo-random hash (for per-instance seed variation)
            float hash(float3 p)
            {
                p = frac(p * float3(127.1, 311.7, 74.7));
                p += dot(p, p.yxz + 19.19);
                return frac((p.x + p.y) * p.z);
            }

            // Blend between two colors using a mask
            float3 mixColor(float3 a, float3 b, float t)
            {
                return lerp(a, b, saturate(t));
            }

            // ---------- Fragment ----------
            half4 frag(Varyings IN, half FACING : VFACE) : SV_Target
            {
                // ── Texture + alpha clip ──────────────────────────────────────
                float4 tex      = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                clip(tex.a - _AlphaClip);

                // ── Normal correction for double-sided ────────────────────────
                float3 N = normalize(IN.normalWS) * (FACING > 0 ? 1 : -1);

                // ── Main light ───────────────────────────────────────────────
                Light mainLight  = GetMainLight(IN.shadowCoord);
                float3 L         = normalize(mainLight.direction);
                float  NdotL     = dot(N, L);          // [-1 , 1]
                float  NdotLu    = NdotL * 0.5 + 0.5;  // [0  , 1]  – wrap lighting like Blender

                // ── Vertex color carries per-leaf-cluster variation ───────────
                // In Blender: Attribute "leavesBlock" → drives color seed
                float  blockSeed = IN.vertexColor.r;   // baked in vertex color R channel

                // ── Color Variation ───────────────────────────────────────────
                // Simulates "Scale/Seed Color Variations" nodes
                float  colorNoise = hash(IN.positionWS * _ScaleColorVariation + blockSeed * 100.0);
                float  varMask    = colorRamp(colorNoise, 0.5, _TransitionVarFactor * 2.0);
                float3 baseColor  = mixColor(_MainColor.rgb, _MainColor.rgb * (1.0 - _FactorColorVar * 0.4), varMask);

                // ── Shadow Layer 1: normal/shadow split ───────────────────────
                float  shadowMask  = colorRamp(NdotLu, _PositionShadows, _ContrastLightShadow);
                float3 col         = mixColor(_ShadowColor.rgb, baseColor, shadowMask * _FactorShadows + (1.0 - _FactorShadows));

                // ── Shadow Layer 2: darker shadows ────────────────────────────
                float  darkerMask  = colorRamp(NdotLu, _PositionDarkerShadows, _ContrastDarkerShadows);
                col                = mixColor(_ShadowColor.rgb * 0.5, col, darkerMask * _FactorDarkerShadows + (1.0 - _FactorDarkerShadows));

                // ── Relight Shadows (back-lit / sub-surface rim) ──────────────
                float  NdotLback  = dot(N, -L) * 0.5 + 0.5;
                float  relightMask = colorRamp(NdotLback, _PositionRelightShadows, _ContrastRelightShadows);
                col                = mixColor(col, _RelightShadowsColor.rgb, relightMask * _FactorRelightShadows);

                // ── Highlight ─────────────────────────────────────────────────
                float  hlMask  = colorRamp(NdotLu, 1.0 - _PositionHighlight, _ContrastHighlight * 5.0);
                col            = mixColor(col, _HighlightColor.rgb, hlMask * _FactorHighlight);

                // ── Highlight Darker Shadows (small darkening just below highlight) ──
                float  hlDark  = colorRamp(NdotLu, _PositionDarkerHLShadows, 8.0);
                col            = mixColor(col * 0.85, col, hlDark * _FactorDarkerHLShadows + (1.0 - _FactorDarkerHLShadows));

                // ── Z Gradient ────────────────────────────────────────────────
                // World-space Y normalized to [0,1] within the object — approximated by positionWS.y
                // We use the object center (unity_ObjectToWorld[1][3] ≈ _WorldSpaceCameraPos for tight bounds)
                float  worldY   = IN.positionWS.y;
                // Remap: use _PositionZGradient as the split point
                float  zGrad    = saturate(worldY * 0.2 + 0.5);   // rough 0-1 over typical tree height
                float  zMask    = colorRamp(zGrad, _PositionZGradient, 4.0);
                col             = mixColor(_ZGradientColor.rgb, col, zMask * (1.0 - _FactorZGradient) + (1.0 - (1.0 - _FactorZGradient)));
                // Second Z gradient factor (fine blend)
                col             = lerp(_ZGradientColor.rgb, col, 1.0 - _FactorZGradient2 * (1.0 - zMask));

                // ── Unity shadow attenuation ──────────────────────────────────
                // Mimics "Disable Shadow" toggle in Blender (Light Path node)
                float  shadowAtt = lerp(mainLight.shadowAttenuation, 1.0, _DisableShadow);
                col              *= (mainLight.color * (shadowAtt * 0.5 + 0.5) + 0.15); // slight ambient

                // ── Additional lights (simple diffuse) ────────────────────────
                #ifdef _ADDITIONAL_LIGHTS
                uint additionalLightsCount = GetAdditionalLightsCount();
                for (uint i = 0; i < additionalLightsCount; ++i)
                {
                    Light light = GetAdditionalLight(i, IN.positionWS);
                    float NdotAL = saturate(dot(N, light.direction)) * 0.5;
                    col += NdotAL * light.color * light.distanceAttenuation * 0.3;
                }
                #endif

                // ── Fog ───────────────────────────────────────────────────────
                col = MixFog(col, IN.fogFactor);

                return half4(col, 1.0);
            }
            ENDHLSL
        }

        // -------------------------------------------------------
        // PASS 2 – Shadow Caster
        // -------------------------------------------------------
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Off

            HLSLPROGRAM
            #pragma vertex   vertShadow
            #pragma fragment fragShadow

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float  _AlphaClip;
                // (other props not needed here)
                float4 _MainColor;
                float  _SeedColorVariation;
                float  _ScaleColorVariation;
                float  _FactorColorVar;
                float  _TransitionVarFactor;
                float4 _ShadowColor;
                float  _PositionShadows;
                float  _ContrastLightShadow;
                float  _FactorShadows;
                float  _PositionDarkerShadows;
                float  _ContrastDarkerShadows;
                float  _FactorDarkerShadows;
                float4 _RelightShadowsColor;
                float  _PositionRelightShadows;
                float  _ContrastRelightShadows;
                float  _FactorRelightShadows;
                float4 _HighlightColor;
                float  _PositionHighlight;
                float  _ContrastHighlight;
                float  _FactorHighlight;
                float  _PositionDarkerHLShadows;
                float  _FactorDarkerHLShadows;
                float4 _ZGradientColor;
                float  _PositionZGradient;
                float  _FactorZGradient;
                float  _FactorZGradient2;
                float  _SmoothNormalsFactor;
                float  _SphereNormalsFactor;
                float  _TrivialNormalsFactor;
                float  _DisableShadow;
            CBUFFER_END

            struct AttrShadow  { float4 pos : POSITION; float2 uv : TEXCOORD0; float3 n : NORMAL; };
            struct VaryShadow  { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            VaryShadow vertShadow(AttrShadow IN)
            {
                VaryShadow OUT;
                float3 ws  = TransformObjectToWorld(IN.pos.xyz);
                float3 wn  = TransformObjectToWorldNormal(IN.n);
                OUT.pos    = TransformWorldToHClip(ApplyShadowBias(ws, wn, _MainLightPosition.xyz));
                OUT.uv     = TRANSFORM_TEX(IN.uv, _MainTex);
                return OUT;
            }

            half4 fragShadow(VaryShadow IN) : SV_Target
            {
                float alpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv).a;
                clip(alpha - _AlphaClip);
                return 0;
            }
            ENDHLSL
        }

        // -------------------------------------------------------
        // PASS 3 – Depth Only (for depth prepass / AO)
        // -------------------------------------------------------
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Off

            HLSLPROGRAM
            #pragma vertex   vertDepth
            #pragma fragment fragDepth

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float  _AlphaClip;
                float4 _MainColor;
                float  _SeedColorVariation;
                float  _ScaleColorVariation;
                float  _FactorColorVar;
                float  _TransitionVarFactor;
                float4 _ShadowColor;
                float  _PositionShadows;
                float  _ContrastLightShadow;
                float  _FactorShadows;
                float  _PositionDarkerShadows;
                float  _ContrastDarkerShadows;
                float  _FactorDarkerShadows;
                float4 _RelightShadowsColor;
                float  _PositionRelightShadows;
                float  _ContrastRelightShadows;
                float  _FactorRelightShadows;
                float4 _HighlightColor;
                float  _PositionHighlight;
                float  _ContrastHighlight;
                float  _FactorHighlight;
                float  _PositionDarkerHLShadows;
                float  _FactorDarkerHLShadows;
                float4 _ZGradientColor;
                float  _PositionZGradient;
                float  _FactorZGradient;
                float  _FactorZGradient2;
                float  _SmoothNormalsFactor;
                float  _SphereNormalsFactor;
                float  _TrivialNormalsFactor;
                float  _DisableShadow;
            CBUFFER_END

            struct AttrD  { float4 pos : POSITION; float2 uv : TEXCOORD0; };
            struct VaryD  { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            VaryD vertDepth(AttrD IN)
            {
                VaryD OUT;
                OUT.pos = TransformObjectToHClip(IN.pos.xyz);
                OUT.uv  = TRANSFORM_TEX(IN.uv, _MainTex);
                return OUT;
            }
            half fragDepth(VaryD IN) : SV_Target
            {
                float alpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv).a;
                clip(alpha - _AlphaClip);
                return IN.pos.z;
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
    CustomEditor "UnityEditor.ShaderGUI"
}

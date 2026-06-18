Shader "Custom/StylizedLeaves"
{
    Properties
    {
        [Header(__ TEXTURE __)]
        _MainTex             ("Leaf Texture (RGBA)",          2D)           = "white" {}
        _AlphaClip           ("Alpha Clip Threshold",         Range(0.001,0.999)) = 0.4

        [Header(__ MAIN COLOR __)]
        // Verde principal: RGB(64,100,55)
        _MainColor           ("Main Color",                   Color)        = (0.252, 0.394, 0.219, 1)
        _ScaleColorVariation ("Scale Color Variations",       Range(0,3))   = 1.5
        _SeedColorVariation  ("Seed Color Variations",        Range(0,10))  = 0.0
        _FactorColorVar      ("Factor Color Variations",      Range(0,1))   = 0.8
        _TransitionVarFactor ("Transition Variations Factor", Range(0,1))   = 1.0

        [Header(__ SHADOWS __)]
        // Sombra escura: RGB(35,78,32)
        _ShadowColor         ("Shadow Color",                 Color)        = (0.139, 0.306, 0.126, 1)
        _PositionShadows     ("Position Shadows",              Range(0,1))   = 0.0
        _ContrastLightShadow ("Contrast Light/Shadow",         Range(0,5))   = 2.5
        _FactorShadows       ("Factor Shadows",                Range(0,1))   = 1.0

        // Sombra media: RGB(40,83,36)
        _DarkerShadowColor   ("Darker Shadow Color",           Color)        = (0.160, 0.327, 0.142, 1)
        _PositionDarkerShadows ("Position Darker Shadows",     Range(0,1))   = 0.4
        _ContrastDarkerShadows ("Contrast Darker Shadows",     Range(0,10))  = 5.0
        _FactorDarkerShadows   ("Factor Darker Shadows",       Range(0,1))   = 1.0

        [Header(__ RELIGHT SHADOWS (backlight) __)]
        // Relight verde mais saturado: RGB(74,118,63)
        _RelightShadowsColor    ("Relight Shadows Color",       Color)        = (0.293, 0.464, 0.247, 1)
        _PositionRelightShadows ("Position Relight Shadows",    Range(0,1))   = 0.5
        _ContrastRelightShadows ("Contrast Relight Shadows",    Range(0,20))  = 15.0
        _FactorRelightShadows   ("Factor Relight Shadows",      Range(0,1))   = 0.4

        [Header(__ HIGHLIGHT __)]
        // Highlight claro: RGB(162,185,112)
        _HighlightColor      ("Highlight Color",                Color)        = (0.638, 0.729, 0.440, 1)
        _PositionHighlight   ("Position Highlight",              Range(0,1))   = 0.175
        _ContrastHighlight   ("Contrast Highlight",              Range(0,5))   = 1.5
        _FactorHighlight     ("Factor Highlight",                Range(0,1))   = 0.8

        [Header(__ Z GRADIENT __)]
        // Base da copa: RGB(41,79,39)
        _ZGradientColor      ("Z Gradient Color",                Color)        = (0.162, 0.313, 0.156, 1)
        _PositionZGradient   ("Position Z Gradient",             Range(-2,2))  = 0.5
        _FactorZGradient     ("Factor Z Gradient",               Range(0,1))   = 0.150
        _FactorZGradient2    ("Factor Z Gradient 2",             Range(0,1))   = 0.060

        [Header(__ NORMALS __)]
        _SmoothNormalsFactor ("Smooth Normals Factor",           Range(0,1))   = 0.150
        _SphereNormalsFactor ("Sphere Normals Factor",           Range(0,1))   = 0.0
        _TrivialNormalsFactor("Individual Normals Factor",       Range(0,1))   = 0.1

        [Header(__ OPTIONS __)]
        [Toggle] _DisableShadow ("Disable Cast Shadow (Light Path)", Float)    = 0
        [Toggle] _WindEnabled   ("Wind Sway",                        Float)    = 0
        _WindStrength         ("Wind Strength",                Range(0,1))   = 0.1
        _WindSpeed            ("Wind Speed",                   Range(0,5))   = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType"     = "TransparentCutout"
            "Queue"          = "AlphaTest"
        }

        // ════════════════════════════════════════════════════════════════════
        // PASS: ForwardLit
        // ════════════════════════════════════════════════════════════════════
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            ZWrite On

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma shader_feature_local _DISABLE_SHADOW_ON
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float  _AlphaClip;

                float4 _MainColor;
                float  _ScaleColorVariation;
                float  _SeedColorVariation;
                float  _FactorColorVar;
                float  _TransitionVarFactor;

                float4 _ShadowColor;
                float  _PositionShadows;
                float  _ContrastLightShadow;
                float  _FactorShadows;

                float4 _DarkerShadowColor;
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

                float4 _ZGradientColor;
                float  _PositionZGradient;
                float  _FactorZGradient;
                float  _FactorZGradient2;

                float  _SmoothNormalsFactor;
                float  _SphereNormalsFactor;
                float  _TrivialNormalsFactor;

                float  _DisableShadow;
                float  _WindEnabled;
                float  _WindStrength;
                float  _WindSpeed;
            CBUFFER_END

            struct Attributes
            {
                float4 posOS  : POSITION;
                float3 normOS : NORMAL;
                float2 uv     : TEXCOORD0;
                float4 color  : COLOR;     // vertex color → emula Attribute "leavesBlock"
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 posHCS  : SV_POSITION;
                float2 uv      : TEXCOORD0;
                float3 posWS   : TEXCOORD1;
                float3 normWS  : TEXCOORD2;
                float4 shadowC : TEXCOORD3;
                float  fog     : TEXCOORD4;
                float4 vcolor  : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // Emula o nó ColorRamp (linear) do Blender
            float ramp(float v, float pos, float contrast)
            {
                return saturate((v - pos) * contrast + 0.5);
            }

            float hash(float3 p)
            {
                p = frac(p * float3(127.1, 311.7, 74.7));
                p += dot(p, p.yxz + 19.19);
                return frac((p.x + p.y) * p.z);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                float4 posOS = IN.posOS;
                if (_WindEnabled > 0.5)
                {
                    float3 wsRaw = TransformObjectToWorld(posOS.xyz);
                    float  wave  = sin(_Time.y * _WindSpeed + wsRaw.x * 0.8 + wsRaw.z * 0.6);
                    posOS.x += wave * _WindStrength * posOS.y * 0.15;
                    posOS.z += wave * _WindStrength * posOS.y * 0.08;
                }

                OUT.posWS  = TransformObjectToWorld(posOS.xyz);
                OUT.posHCS = TransformWorldToHClip(OUT.posWS);
                OUT.normWS = TransformObjectToWorldNormal(IN.normOS);
                OUT.uv     = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.shadowC= TransformWorldToShadowCoord(OUT.posWS);
                OUT.fog    = ComputeFogFactor(OUT.posHCS.z);
                OUT.vcolor = IN.color;
                return OUT;
            }

            half4 frag(Varyings IN, half FACING : VFACE) : SV_Target
            {
                // ── Textura + Alpha clip ─────────────────────────────────────
                float4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                clip(tex.a - _AlphaClip);

                // ── Normal double-sided ──────────────────────────────────────
                float3 N = normalize(IN.normWS) * (FACING > 0 ? 1.0 : -1.0);

                // ── Luz principal ────────────────────────────────────────────
                Light light = GetMainLight(IN.shadowC);
                float3 L    = normalize(light.direction);
                float NdotL = dot(N, L) * 0.5 + 0.5;   // wrap [0,1]

                // ── Cor base + Color Variation (Attribute leavesBlock / seed) ─
                float  seed  = IN.vcolor.r + _SeedColorVariation * 0.1;
                float  noise = hash(floor(IN.posWS * _ScaleColorVariation) + seed * 37.0);
                float  varM  = ramp(noise, 0.5, _TransitionVarFactor * 3.0);
                float3 col   = lerp(_MainColor.rgb * (1.0 - _FactorColorVar * 0.35),
                                    _MainColor.rgb, varM);

                // ── Shadow layer 1 ───────────────────────────────────────────
                float sh1 = ramp(NdotL, _PositionShadows, _ContrastLightShadow);
                col = lerp(_ShadowColor.rgb, col, sh1 * _FactorShadows + (1.0 - _FactorShadows));

                // ── Shadow layer 2 (darker) ───────────────────────────────────
                float sh2 = ramp(NdotL, _PositionDarkerShadows, _ContrastDarkerShadows);
                col = lerp(_DarkerShadowColor.rgb, col, sh2 * _FactorDarkerShadows + (1.0 - _FactorDarkerShadows));

                // ── Relight Shadows (backlight / translucência) ───────────────
                float NdotLback = dot(N, -L) * 0.5 + 0.5;
                float rl = ramp(NdotLback, _PositionRelightShadows, _ContrastRelightShadows);
                col = lerp(col, _RelightShadowsColor.rgb, rl * _FactorRelightShadows);

                // ── Highlight ────────────────────────────────────────────────
                float hl = ramp(NdotL, 1.0 - _PositionHighlight, _ContrastHighlight * 4.0);
                col = lerp(col, _HighlightColor.rgb, hl * _FactorHighlight);

                // ── Z Gradient ───────────────────────────────────────────────
                float zNorm = saturate(IN.posWS.y * 0.15 + 0.5 + _PositionZGradient);
                float zMask = ramp(zNorm, 0.5, 4.0);
                col = lerp(_ZGradientColor.rgb, col, saturate(zMask + (1.0 - _FactorZGradient)));
                col = lerp(_ZGradientColor.rgb, col, saturate(1.0 - _FactorZGradient2 * (1.0 - zMask)));

                // ── Sombra / Light Path (Disable Shadow) ──────────────────────
                #if defined(_DISABLE_SHADOW_ON)
                    float shadowAtt = 1.0;
                #else
                    float shadowAtt = light.shadowAttenuation;
                #endif
                float3 ambient = float3(0.06, 0.09, 0.05);
                col *= (light.color * (shadowAtt * 0.65 + 0.35) + ambient);

                // ── Fog ──────────────────────────────────────────────────────
                col = MixFog(col, IN.fog);

                return half4(col, 1.0);
            }
            ENDHLSL
        }

        // ════════════════════════════════════════════════════════════════════
        // PASS: ShadowCaster
        // ════════════════════════════════════════════════════════════════════
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Off

            HLSLPROGRAM
            #pragma vertex vs
            #pragma fragment fs
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST; float _AlphaClip;
                float4 _MainColor; float _ScaleColorVariation; float _SeedColorVariation;
                float _FactorColorVar; float _TransitionVarFactor;
                float4 _ShadowColor; float _PositionShadows; float _ContrastLightShadow; float _FactorShadows;
                float4 _DarkerShadowColor; float _PositionDarkerShadows; float _ContrastDarkerShadows; float _FactorDarkerShadows;
                float4 _RelightShadowsColor; float _PositionRelightShadows; float _ContrastRelightShadows; float _FactorRelightShadows;
                float4 _HighlightColor; float _PositionHighlight; float _ContrastHighlight; float _FactorHighlight;
                float4 _ZGradientColor; float _PositionZGradient; float _FactorZGradient; float _FactorZGradient2;
                float _SmoothNormalsFactor; float _SphereNormalsFactor; float _TrivialNormalsFactor;
                float _DisableShadow; float _WindEnabled; float _WindStrength; float _WindSpeed;
            CBUFFER_END

            struct A { float4 pos:POSITION; float3 n:NORMAL; float2 uv:TEXCOORD0; };
            struct V { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; };
            V vs(A i) {
                V o;
                float3 ws = TransformObjectToWorld(i.pos.xyz);
                float3 wn = TransformObjectToWorldNormal(i.n);
                o.pos = TransformWorldToHClip(ApplyShadowBias(ws, wn, _MainLightPosition.xyz));
                o.uv  = TRANSFORM_TEX(i.uv, _MainTex);
                return o;
            }
            half4 fs(V i) : SV_Target {
                clip(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).a - _AlphaClip);
                return 0;
            }
            ENDHLSL
        }

        // ════════════════════════════════════════════════════════════════════
        // PASS: DepthOnly
        // ════════════════════════════════════════════════════════════════════
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Off

            HLSLPROGRAM
            #pragma vertex vs
            #pragma fragment fs
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST; float _AlphaClip;
                float4 _MainColor; float _ScaleColorVariation; float _SeedColorVariation;
                float _FactorColorVar; float _TransitionVarFactor;
                float4 _ShadowColor; float _PositionShadows; float _ContrastLightShadow; float _FactorShadows;
                float4 _DarkerShadowColor; float _PositionDarkerShadows; float _ContrastDarkerShadows; float _FactorDarkerShadows;
                float4 _RelightShadowsColor; float _PositionRelightShadows; float _ContrastRelightShadows; float _FactorRelightShadows;
                float4 _HighlightColor; float _PositionHighlight; float _ContrastHighlight; float _FactorHighlight;
                float4 _ZGradientColor; float _PositionZGradient; float _FactorZGradient; float _FactorZGradient2;
                float _SmoothNormalsFactor; float _SphereNormalsFactor; float _TrivialNormalsFactor;
                float _DisableShadow; float _WindEnabled; float _WindStrength; float _WindSpeed;
            CBUFFER_END

            struct A { float4 pos:POSITION; float2 uv:TEXCOORD0; };
            struct V { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; };
            V vs(A i) {
                V o;
                o.pos = TransformObjectToHClip(i.pos.xyz);
                o.uv  = TRANSFORM_TEX(i.uv, _MainTex);
                return o;
            }
            half fs(V i) : SV_Target {
                clip(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).a - _AlphaClip);
                return i.pos.z;
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}

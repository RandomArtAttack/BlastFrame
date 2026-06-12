Shader "Blast Frame/DevTex Triplanar"
{
    Properties
    {
        _MainTex        ("Texture",                     2D)           = "white" {}
        _Color          ("Color",                       Color)        = (1,1,1,1)
        _Tiling         ("World Tiling  (m per tile)",  Float)        = 2.0
        _Metallic       ("Metallic",                    Range(0,1))   = 0.0
        _Smoothness     ("Smoothness",                  Range(0,1))   = 0.5
        _EmissionMap    ("Emission Map",                2D)           = "black" {}
        [HDR] _EmissionColor ("Emission",              Color)        = (0,0,0,0)
        // blend state — set per-material by ImplementFix031
        [HideInInspector] _SrcBlend ("__src", Float) = 1
        [HideInInspector] _DstBlend ("__dst", Float) = 0
        [HideInInspector] _ZWrite   ("__zw",  Float) = 1
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }

        // ---------------------------------------------------------------
        // Forward Lit
        // ---------------------------------------------------------------
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            Blend  [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]

            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment Frag

            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_MainTex);     SAMPLER(sampler_MainTex);
            TEXTURE2D(_EmissionMap); SAMPLER(sampler_EmissionMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float  _Tiling;
                float  _Metallic;
                float  _Smoothness;
                float4 _EmissionColor;
                float4 _MainTex_ST;
                float4 _EmissionMap_ST;
            CBUFFER_END

            struct Attribs
            {
                float4 posOS  : POSITION;
                float3 normOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 posCS  : SV_POSITION;
                float3 posWS  : TEXCOORD0;
                float3 normWS : TEXCOORD1;
                float  fog    : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            half3 Triplanar(TEXTURE2D_PARAM(tex, smp), float3 posWS, float3 normWS, float tiling)
            {
                float3 w = abs(normWS);
                w = pow(max(w, 0.0001), 4);
                w /= dot(w, 1.0);
                half3 xSide = SAMPLE_TEXTURE2D(tex, smp, posWS.zy / tiling).rgb;
                half3 ySide = SAMPLE_TEXTURE2D(tex, smp, posWS.xz / tiling).rgb;
                half3 zSide = SAMPLE_TEXTURE2D(tex, smp, posWS.xy / tiling).rgb;
                return xSide * w.x + ySide * w.y + zSide * w.z;
            }

            Varyings Vert(Attribs IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                Varyings OUT;
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                VertexPositionInputs vpi = GetVertexPositionInputs(IN.posOS.xyz);
                VertexNormalInputs   vni = GetVertexNormalInputs(IN.normOS);
                OUT.posCS  = vpi.positionCS;
                OUT.posWS  = vpi.positionWS;
                OUT.normWS = vni.normalWS;
                OUT.fog    = ComputeFogFactor(vpi.positionCS.z);
                return OUT;
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                float3 normWS = normalize(IN.normWS);

                half3 albedo = Triplanar(TEXTURE2D_ARGS(_MainTex, sampler_MainTex),
                                        IN.posWS, normWS, _Tiling) * _Color.rgb;

                InputData li = (InputData)0;
                li.positionWS             = IN.posWS;
                li.normalWS               = normWS;
                li.viewDirectionWS        = GetWorldSpaceNormalizeViewDir(IN.posWS);
                li.shadowCoord            = TransformWorldToShadowCoord(IN.posWS);
                li.fogCoord               = IN.fog;
                li.bakedGI                = SampleSH(normWS);
                li.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.posCS);

                SurfaceData sd = (SurfaceData)0;
                sd.albedo     = albedo;
                sd.metallic   = _Metallic;
                sd.smoothness = _Smoothness;
                sd.occlusion  = 1.0;
                sd.alpha      = _Color.a;
                sd.emission   = Triplanar(TEXTURE2D_ARGS(_EmissionMap, sampler_EmissionMap),
                                         IN.posWS, normWS, _Tiling) * _EmissionColor.rgb;

                half4 col = UniversalFragmentPBR(li, sd);
                col.rgb = MixFog(col.rgb, IN.fog);
                col.a   = _Color.a;
                return col;
            }
            ENDHLSL
        }

        // ---------------------------------------------------------------
        // Shadow Caster
        // ---------------------------------------------------------------
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ZTest  LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex   ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float  _Tiling;
                float  _Metallic;
                float  _Smoothness;
                float4 _EmissionColor;
                float4 _MainTex_ST;
                float4 _EmissionMap_ST;
            CBUFFER_END

            float3 _LightDirection;
            float3 _LightPosition;

            struct SAttribs
            {
                float4 posOS  : POSITION;
                float3 normOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 ShadowVert(SAttribs IN) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                float3 posWS  = TransformObjectToWorld(IN.posOS.xyz);
                float3 normWS = TransformObjectToWorldNormal(IN.normOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 ld = normalize(_LightPosition - posWS);
                #else
                    float3 ld = _LightDirection;
                #endif
                float4 posCS = TransformWorldToHClip(ApplyShadowBias(posWS, normWS, ld));
                #if UNITY_REVERSED_Z
                    posCS.z = min(posCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    posCS.z = max(posCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return posCS;
            }

            half4 ShadowFrag(float4 posCS : SV_POSITION) : SV_Target { return 0; }
            ENDHLSL
        }

        // ---------------------------------------------------------------
        // Depth Only
        // ---------------------------------------------------------------
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex   DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float  _Tiling;
                float  _Metallic;
                float  _Smoothness;
                float4 _EmissionColor;
                float4 _MainTex_ST;
                float4 _EmissionMap_ST;
            CBUFFER_END

            struct DAttribs
            {
                float4 posOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 DepthVert(DAttribs IN) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                return TransformObjectToHClip(IN.posOS.xyz);
            }

            half DepthFrag(float4 posCS : SV_POSITION) : SV_Target { return 0; }
            ENDHLSL
        }
    }

    Fallback "Hidden/Universal Render Pipeline/FallbackError"
}

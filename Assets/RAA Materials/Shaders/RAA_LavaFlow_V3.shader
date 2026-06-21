Shader "RAA Materials/RAA Lava Flow V.3"
{
    // Standalone URP Unlit lava shader, V.3 = V.1 and V.2 stacked into one.
    //   TOP layer  (V.2 style): emission map * emission color * tint, with its own scroll, and an
    //                           alpha map that controls how much of the top shows over the bottom.
    //   BOTTOM layer (V.1 style): emission map * emission color, with its own scroll. The opaque base.
    // Final RGB = lerp(bottom, top, topAlpha). Output is OPAQUE (the bottom layer fills everything).
    // Every layer has independent texture/tiling, emission color (intensity), and flow/move speed.
    // No relation to the triplanar projection shaders.
    Properties
    {
        // ----- TOP layer (V.2: emission + tint + alpha) -----
        [Header(Top Layer V2)]
        _TopEmissionMap("Top Emission Map", 2D) = "white" {}
        [HDR] _TopEmissionColor("Top Emission Color", Color) = (1,1,1,1)
        _TopTint("Top Tint (used when no texture)", Color) = (1,1,1,1)
        [NoScaleOffset] _TopAlphaMap("Top Alpha Map", 2D) = "white" {}
        _TopMove("Top Move (X,Y units per second)", Vector) = (0,0,0,0)

        // ----- BOTTOM layer (V.1: emission base) -----
        [Header(Bottom Layer V1)]
        [MainTexture] _EmissionMap("Bottom Emission Map", 2D) = "white" {}
        [HDR][MainColor] _EmissionColor("Bottom Emission Color", Color) = (1,1,1,1)
        _BottomMove("Bottom Move (X,Y units per second)", Vector) = (0,0,0,0)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Unlit"
            "IgnoreProjector" = "True"
        }
        LOD 100

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _TopEmissionMap_ST;
            half4  _TopEmissionColor;
            half4  _TopTint;
            float4 _TopMove;         // xy = top-layer UV scroll speed (units/sec)
            float4 _EmissionMap_ST;
            half4  _EmissionColor;
            float4 _BottomMove;      // xy = bottom-layer UV scroll speed (units/sec)
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog

            TEXTURE2D(_TopEmissionMap);
            SAMPLER(sampler_TopEmissionMap);
            TEXTURE2D(_TopAlphaMap);
            SAMPLER(sampler_TopAlphaMap);
            TEXTURE2D(_EmissionMap);
            SAMPLER(sampler_EmissionMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uvTop       : TEXCOORD0;
                float2 uvBottom    : TEXCOORD1;
                float  fogCoord    : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                VertexPositionInputs positionInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionHCS = positionInputs.positionCS;
                OUT.uvTop    = TRANSFORM_TEX(IN.uv, _TopEmissionMap) + _TopMove.xy * _Time.y;
                OUT.uvBottom = TRANSFORM_TEX(IN.uv, _EmissionMap)    + _BottomMove.xy * _Time.y;
                OUT.fogCoord = ComputeFogFactor(positionInputs.positionCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                // Bottom (V.1) base.
                half3 bottom = (SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, IN.uvBottom) * _EmissionColor).rgb;

                // Top (V.2) layer + its alpha (sampled with the top's scrolled UV).
                half3 top   = (SAMPLE_TEXTURE2D(_TopEmissionMap, sampler_TopEmissionMap, IN.uvTop) * _TopEmissionColor * _TopTint).rgb;
                half  aTop  = SAMPLE_TEXTURE2D(_TopAlphaMap, sampler_TopAlphaMap, IN.uvTop).r * _TopTint.a;

                // Composite top over bottom, opaque output.
                half3 color = lerp(bottom, top, aTop);
                color = MixFog(color, IN.fogCoord);
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}

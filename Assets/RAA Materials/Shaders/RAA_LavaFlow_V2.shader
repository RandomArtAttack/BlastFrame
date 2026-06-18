Shader "RAA Materials/RAA Lava Flow V.2"
{
    // Standalone URP Unlit lava shader, V.2. Same as "RAA Lava Flow" (scrolling emission) plus:
    //   - a color Tint that multiplies the emission (so it works even with no texture), and
    //   - an Alpha Map that drives transparency (shader is alpha-blended / transparent).
    // No relation to the triplanar projection shaders.
    Properties
    {
        [MainTexture] _EmissionMap("Emission Map", 2D) = "white" {}
        [HDR][MainColor] _EmissionColor("Emission Color", Color) = (1,1,1,1)
        _Tint("Tint (used when no texture)", Color) = (1,1,1,1)

        [NoScaleOffset] _AlphaMap("Alpha Map", 2D) = "white" {}

        [Header(Move)]
        _Move("Move (X,Y units per second)", Vector) = (0,0,0,0)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Unlit"
            "IgnoreProjector" = "True"
        }
        LOD 100

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _EmissionMap_ST;
            half4  _EmissionColor;
            half4  _Tint;
            float4 _Move;            // xy = UV scroll speed (units/sec)
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }

            // Alpha blending for the alpha map.
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog

            TEXTURE2D(_EmissionMap);
            SAMPLER(sampler_EmissionMap);
            TEXTURE2D(_AlphaMap);
            SAMPLER(sampler_AlphaMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float  fogCoord    : TEXCOORD1;
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
                OUT.uv = TRANSFORM_TEX(IN.uv, _EmissionMap) + _Move.xy * _Time.y;
                OUT.fogCoord = ComputeFogFactor(positionInputs.positionCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                half4 emission = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, IN.uv) * _EmissionColor * _Tint;
                half  alpha = SAMPLE_TEXTURE2D(_AlphaMap, sampler_AlphaMap, IN.uv).r * _Tint.a;

                half3 color = MixFog(emission.rgb, IN.fogCoord);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}

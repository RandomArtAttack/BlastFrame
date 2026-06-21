Shader "Blast Frame/Lava Flow (Vertex Color)"
{
    // Flow direction is read from VERTEX COLOR (R = X dir, G = Y dir), painted with Polybrush.
    // Grey vertex color (0.5, 0.5) = no flow. The base texture UVs are pushed along that
    // direction using a two-phase blend so the scroll never visibly stretches/snaps.
    Properties
    {
        [MainTexture] _BaseMap ("Lava Texture", 2D) = "white" {}
        _BaseTiling ("Base Tiling", Float) = 1.0
        [HDR] _EmissionColor ("Emission Color", Color) = (1, 0.4, 0.05, 1)
        _EmissionStrength ("Emission Strength", Float) = 4.0
        _FlowSpeed ("Flow Speed", Float) = 0.25
        _FlowStrength ("Flow Strength", Float) = 0.5
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "ForwardLava"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;   // <- Polybrush vertex color (flow direction)
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float  _BaseTiling;
                half4  _EmissionColor;
                float  _EmissionStrength;
                float  _FlowSpeed;
                float  _FlowStrength;
            CBUFFER_END

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap) * _BaseTiling;
                OUT.color = IN.color;
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                // Vertex color RG -> signed flow direction. Grey (0.5,0.5) -> zero flow.
                float2 flowDir = (IN.color.rg * 2.0 - 1.0) * _FlowStrength;

                // Two-phase sawtooth so the offset resets without a visible snap.
                float t = _Time.y * _FlowSpeed;
                float phase0 = frac(t);
                float phase1 = frac(t + 0.5);

                float2 uv0 = IN.uv - flowDir * phase0;
                float2 uv1 = IN.uv - flowDir * phase1;

                half4 t0 = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv0);
                half4 t1 = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv1);

                // Crossfade: weight is 0 at a reset, 1 at the midpoint of each phase.
                float blend = abs(phase0 - 0.5) * 2.0;
                half4 lava = lerp(t0, t1, blend);

                half3 col = lava.rgb * _EmissionColor.rgb * _EmissionStrength;
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
}

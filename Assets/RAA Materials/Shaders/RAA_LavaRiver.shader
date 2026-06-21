Shader "RAA Materials/RAA Lava River"
{
    // Standalone URP Unlit lava shader for a flowing RIVER. Uses the SAME two-layer emission/layering
    // as "RAA Lava Flow V.3" (identical property reference names) so a V.3 material's properties can be
    // pasted straight in ("Paste Material Properties") and the textures/colors/emissions match exactly:
    //   TOP layer  (V.2 style): _TopEmissionMap * _TopEmissionColor * _TopTint, blended over the bottom
    //                           by _TopAlphaMap. (Top alpha defaults BLACK = top hidden, so an existing
    //                           single-layer river isn't covered until the top is assigned/pasted.)
    //   BOTTOM layer (V.1 style): _EmissionMap * _EmissionColor. The opaque base.
    // River-specific additions (NOT in V.3, so a paste leaves them at the river's values):
    //   - FLOW MAP drives the scroll of BOTH layers (follows the riverbed you paint), and
    //   - the mesh continuously RIPPLES with concentric waves around a world-space impact point.
    // Final RGB = lerp(bottom, top, topAlpha). Opaque. Assumes a flat, horizontal, subdivided mesh.
    Properties
    {
        // ----- TOP layer (V.2: emission + tint + alpha) -----
        [Header(Top Layer V2)]
        _TopEmissionMap("Top Emission Map", 2D) = "white" {}
        [HDR] _TopEmissionColor("Top Emission Color", Color) = (1,1,1,1)
        _TopTint("Top Tint (used when no texture)", Color) = (1,1,1,1)
        [NoScaleOffset] _TopAlphaMap("Top Alpha Map", 2D) = "black" {}

        // ----- BOTTOM layer (V.1: emission base) -----
        [Header(Bottom Layer V1)]
        [MainTexture] _EmissionMap("Bottom Emission Map", 2D) = "white" {}
        [HDR][MainColor] _EmissionColor("Bottom Emission Color", Color) = (1,1,1,1)

        // ----- Flow (drives both layers along the riverbed) -----
        [Header(Flow)]
        [NoScaleOffset] _FlowMap("Flow Map (RG = direction, grey = none)", 2D) = "grey" {}
        _FlowStrength("Flow Strength (UV distortion)", Float) = 0.25
        _FlowSpeed("Flow Speed (cycles/sec)", Float) = 0.5

        // ----- Ripple at the lavafall impact -----
        [Header(Ripple)]
        // xyz = world-space point the lavafall hits.
        _ImpactPoint("Impact Point (World XYZ)", Vector) = (0,0,0,0)
        _RippleAmplitude("Ripple Amplitude (world units)", Float) = 0.1
        _RippleFrequency("Ripple Frequency (rings/unit)", Float) = 6.0
        _RippleSpeed("Ripple Speed", Float) = 4.0
        _RippleDecay("Ripple Decay (falloff w/ distance)", Float) = 0.5
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
            float4 _EmissionMap_ST;
            half4  _EmissionColor;
            float  _FlowStrength;
            float  _FlowSpeed;
            float4 _ImpactPoint;
            float  _RippleAmplitude;
            float  _RippleFrequency;
            float  _RippleSpeed;
            float  _RippleDecay;
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
            TEXTURE2D(_FlowMap);
            SAMPLER(sampler_FlowMap);

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
                float2 uvFlow      : TEXCOORD2;
                float  fogCoord    : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // Flow-map distortion: two phase-offset samples cross-faded by a triangle wave so the
            // scroll never visibly snaps back when a cycle resets.
            half4 FlowSample(TEXTURE2D_PARAM(tex, smp), float2 baseUV, float2 flow,
                             float phaseA, float phaseB, float blend)
            {
                half4 a = SAMPLE_TEXTURE2D(tex, smp, baseUV - flow * phaseA);
                half4 b = SAMPLE_TEXTURE2D(tex, smp, baseUV - flow * phaseB);
                return lerp(a, b, blend);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                // Continuous concentric ripple around the impact point. Displaces along world up
                // (river is assumed flat/horizontal). Needs a subdivided mesh to be visible.
                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                float dist = length(positionWS.xz - _ImpactPoint.xz);
                float wave = sin(dist * _RippleFrequency - _Time.y * _RippleSpeed);
                positionWS.y += wave * _RippleAmplitude * exp(-dist * _RippleDecay);

                OUT.positionHCS = TransformWorldToHClip(positionWS);
                OUT.uvTop    = TRANSFORM_TEX(IN.uv, _TopEmissionMap);
                OUT.uvBottom = TRANSFORM_TEX(IN.uv, _EmissionMap);
                OUT.uvFlow   = IN.uv;
                OUT.fogCoord = ComputeFogFactor(OUT.positionHCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                // Shared flow vector (one riverbed direction field for both layers).
                float2 flow = (SAMPLE_TEXTURE2D(_FlowMap, sampler_FlowMap, IN.uvFlow).rg * 2.0 - 1.0) * _FlowStrength;
                float t = _Time.y * _FlowSpeed;
                float phaseA = frac(t);
                float phaseB = frac(t + 0.5);
                half  blend = abs(1.0 - 2.0 * phaseA);

                // Bottom (V.1) base.
                half4 bottomTex = FlowSample(TEXTURE2D_ARGS(_EmissionMap, sampler_EmissionMap), IN.uvBottom, flow, phaseA, phaseB, blend);
                half3 bottom = (bottomTex * _EmissionColor).rgb;

                // Top (V.2) layer + its alpha, flowed with the same field so they stay aligned.
                half4 topTex = FlowSample(TEXTURE2D_ARGS(_TopEmissionMap, sampler_TopEmissionMap), IN.uvTop, flow, phaseA, phaseB, blend);
                half3 top    = (topTex * _TopEmissionColor * _TopTint).rgb;
                half4 alphaTex = FlowSample(TEXTURE2D_ARGS(_TopAlphaMap, sampler_TopAlphaMap), IN.uvTop, flow, phaseA, phaseB, blend);
                half  aTop   = alphaTex.r * _TopTint.a;

                half3 color = lerp(bottom, top, aTop);
                color = MixFog(color, IN.fogCoord);
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}

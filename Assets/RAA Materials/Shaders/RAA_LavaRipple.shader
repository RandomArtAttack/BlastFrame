Shader "RAA Materials/Lava Ripple"
{
    // A duplicate of "RAA Lava Flow V.3" (two-layer emission, V.2 over V.1, per-layer Move scroll)
    // PLUS a continuous concentric ripple centered on the mesh itself. No impact point and no flow
    // map — drop a plane with this material wherever you need it and it ripples out from its center.
    //   TOP layer  (V.2 style): _TopEmissionMap * _TopEmissionColor * _TopTint, blended over bottom by
    //                           _TopAlphaMap, own _TopMove scroll.
    //   BOTTOM layer (V.1 style): _EmissionMap * _EmissionColor, own _BottomMove scroll. Opaque base.
    // Final RGB = lerp(bottom, top, topAlpha). Ripple is OBJECT-SPACE: centered at the local origin,
    // distance measured in local XZ, displaced along local up — assumes a flat plane in its XZ.
    // Property reference names match V.3 so "Paste Material Properties" from a V.3 material fills in.
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

        // ----- Ripple (centered on the mesh) -----
        [Header(Ripple)]
        _RippleAmplitude("Ripple Amplitude (local units)", Float) = 0.1
        _RippleAmplitudeRandom("Amplitude Random (plus or minus, per object)", Float) = 0
        _RippleFrequency("Ripple Frequency (rings/unit)", Float) = 6.0
        _RippleSpeed("Ripple Speed", Float) = 4.0
        _RippleDecay("Ripple Decay (falloff w/ distance)", Float) = 0.5
        _RipplePhaseRandom("Phase Desync Per Object (0 = synced, 1 = random)", Range(0,1)) = 1
        _RippleAngularStrength("Angular Height Variation Max (random 0..this per object)", Float) = 0
        _RippleAngularFrequency("Angular Waves Around Ring (whole numbers)", Float) = 3
        _RippleAngularSpeed("Angular Rotation Speed", Float) = 0
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
            float  _RippleAmplitude;
            float  _RippleAmplitudeRandom;
            float  _RippleFrequency;
            float  _RippleSpeed;
            float  _RippleDecay;
            float  _RipplePhaseRandom;
            float  _RippleAngularStrength;
            float  _RippleAngularFrequency;
            float  _RippleAngularSpeed;
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

                // Continuous concentric ripple centered on the mesh: distance from the local origin in
                // the plane's XZ, displaced along local up. Needs a subdivided plane to be visible.
                float3 positionOS = IN.positionOS.xyz;
                float dist = length(positionOS.xz);

                // Per-object randoms hashed from the object's world origin, so separate placed planes
                // (still constant per object / instance) don't ripple in lockstep. Two independent
                // hashes: one desyncs the phase, one varies the amplitude within +/- the random range.
                float3 originWS = GetObjectToWorldMatrix()._m03_m13_m23;
                float randPhase   = frac(sin(dot(originWS, float3(12.9898, 78.233, 37.719))) * 43758.5453);
                float randAmp     = frac(sin(dot(originWS, float3(39.346, 11.135, 83.155))) * 12345.6789);
                float randAngular = frac(sin(dot(originWS, float3(26.651, 53.197, 8.793))) * 27182.8459);

                float amplitude   = _RippleAmplitude + (randAmp * 2.0 - 1.0) * _RippleAmplitudeRandom;
                float phaseOffset = randPhase * 6.2831853 * _RipplePhaseRandom; // up to a full 2*pi cycle

                // Angular wave around the ring so the height isn't uniform around the circumference.
                // Use whole-number _RippleAngularFrequency to avoid a seam at the +/-pi wrap. Per-object
                // strength is random in [0, _RippleAngularStrength] so each ripple is lobed differently
                // (0 max = perfectly circular ring).
                float angularStrength = randAngular * _RippleAngularStrength;
                float angle = atan2(positionOS.z, positionOS.x);
                float angular = sin(angle * _RippleAngularFrequency + _Time.y * _RippleAngularSpeed + phaseOffset);
                float angularMod = 1.0 + angular * angularStrength;

                float wave = sin(dist * _RippleFrequency - _Time.y * _RippleSpeed + phaseOffset);
                positionOS.y += wave * amplitude * exp(-dist * _RippleDecay) * angularMod;

                VertexPositionInputs positionInputs = GetVertexPositionInputs(positionOS);
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
                half3 top  = (SAMPLE_TEXTURE2D(_TopEmissionMap, sampler_TopEmissionMap, IN.uvTop) * _TopEmissionColor * _TopTint).rgb;
                half  aTop = SAMPLE_TEXTURE2D(_TopAlphaMap, sampler_TopAlphaMap, IN.uvTop).r * _TopTint.a;

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

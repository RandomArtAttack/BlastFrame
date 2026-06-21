Shader "RAA Materials/RAA Heat Distortion"
{
    // Refractive heat-haze shader for billboard PARTICLES rising off lava. Samples the scene color
    // (_CameraOpaqueTexture — must be enabled on the URP asset, which it is) at a UV offset driven by a
    // scrolling distortion texture, so whatever is behind the particle wobbles. The offset (and the
    // blend) is scaled by the particle's vertex-color ALPHA, so as each particle fades in/out via
    // Color-over-Lifetime the shimmer eases in/out with no hard pop. Unlit, transparent, no lighting.
    Properties
    {
        [NoScaleOffset] _DistortionMap("Distortion (RG, grey = none)", 2D) = "gray" {}
        _DistortionStrength("Distortion Strength", Float) = 0.02
        _DistortionScroll("Distortion Scroll (X,Y per sec)", Vector) = (0, 0.3, 0, 0)
        _Tiling("Distortion Tiling", Float) = 1.0
        // Procedural soft circle over the whole quad — rounds off the square particle edges with no
        // texture. 0 = hard circle, 1 = fades all the way from the center. Always a circle regardless
        // of distortion tiling (computed from the raw particle UV).
        _Softness("Edge Softness", Range(0,1)) = 0.5
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }
        LOD 100

        Pass
        {
            Name "HeatDistortion"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float  _DistortionStrength;
                float4 _DistortionScroll;
                float  _Tiling;
                float  _Softness;
            CBUFFER_END

            TEXTURE2D(_DistortionMap);
            SAMPLER(sampler_DistortionMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;       // particle color/alpha
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float4 color       : COLOR;
                float4 screenPos   : TEXCOORD1;
                float2 uvAlpha     : TEXCOORD2; // raw particle UV (un-tiled) for the soft mask
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                VertexPositionInputs p = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionHCS = p.positionCS;
                OUT.uv = IN.uv * _Tiling;
                OUT.uvAlpha = IN.uv;            // un-tiled: one soft circle over the whole quad
                OUT.color = IN.color;
                OUT.screenPos = ComputeScreenPos(p.positionCS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                // Procedural soft circle (raw UV) so the square edges round off — no texture. r = 0 at
                // center, 1 at the mid-edge. Combined with the particle's lifetime alpha, this drives
                // BOTH the distortion strength and the blend, so the shimmer fades smoothly at the edge.
                half r = length(IN.uvAlpha - 0.5) * 2.0;
                half mask = 1.0 - smoothstep(saturate(1.0 - _Softness), 1.0, r);
                half a = IN.color.a * mask;

                // Scrolling distortion vector, centered (grey = 0).
                float2 d = SAMPLE_TEXTURE2D(_DistortionMap, sampler_DistortionMap, IN.uv + _DistortionScroll.xy * _Time.y).rg * 2.0 - 1.0;
                float2 offset = d * _DistortionStrength * a;

                float2 screenUV = IN.screenPos.xy / IN.screenPos.w;
                half3 scene = SampleSceneColor(screenUV + offset);

                // Output the refracted scene, blended over the un-refracted background by the soft alpha.
                return half4(scene, a);
            }
            ENDHLSL
        }
    }

    FallBack Off
}

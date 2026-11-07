using UnityEngine;

namespace BlastFrame.Gameplay.Environment
{
    /// <summary>
    /// CPU mirror of the RAA Lava Flow V.4 vertex displacement. Single source of truth — RockBobber
    /// and LavaSubmersionDetector both sample through this so the "visual" (GPU-only) lava surface
    /// can be reconstructed on the CPU (the mesh/collider never move). The bodies are the exact port
    /// of RockBobber's former private CacheV4/SampleNoiseDisp, including the sRGB linearise step.
    /// </summary>
    public static class LavaV4Sampler
    {
        /// <summary>Cached V.4 turbulence params (mirror of RAA_LavaFlow_V4 CBUFFER) read from a
        /// material. IsValid is true only when the noise is CPU-readable and strength is non-zero.</summary>
        public struct V4Params
        {
            public Texture2D NoiseTex;
            public Vector2 Scale, Offset, Move;
            public float Strength;
            public bool Linearize;
            public bool IsValid;
        }

        /// <summary>
        /// Reads _NoiseMap/_NoiseMove/_TurbulenceStrength from a V.4 material. Returns false (and
        /// leaves IsValid false) if the material isn't a V.4 river, has zero strength, or its noise
        /// texture isn't CPU-readable. Logs nothing — the caller logs context (RockBobber re-emits the
        /// "not Read/Write Enabled" warning by inspecting the returned NoiseTex).
        /// </summary>
        public static bool TryCache(Material m, bool noiseIsSRGB, out V4Params p)
        {
            p = default;
            if (m == null || !m.HasProperty("_NoiseMap") || !m.HasProperty("_TurbulenceStrength"))
                return false; // not a V.4 river material

            p.NoiseTex = m.GetTexture("_NoiseMap") as Texture2D;
            p.Scale = m.GetTextureScale("_NoiseMap");
            p.Offset = m.GetTextureOffset("_NoiseMap");
            Vector4 move = m.HasProperty("_NoiseMove") ? m.GetVector("_NoiseMove") : Vector4.zero;
            p.Move = new Vector2(move.x, move.y);
            p.Strength = m.GetFloat("_TurbulenceStrength");

            bool readable = p.NoiseTex != null && p.NoiseTex.isReadable;

            // The GPU sampler converts an sRGB-imported texture to linear in a linear-space project;
            // mirror that on the CPU (GetPixelBilinear returns the raw gamma value) so heights match.
            p.Linearize = noiseIsSRGB && QualitySettings.activeColorSpace == ColorSpace.Linear;

            // Need a readable noise texture and a non-zero strength to mirror the turbulence.
            p.IsValid = readable && Mathf.Abs(p.Strength) > Mathf.Epsilon;
            return p.IsValid;
        }

        /// <summary>
        /// Object-space vertical displacement the GPU applies at a given mesh UV at time t: the
        /// scrolling noise sample * strength. noiseUV mirrors TRANSFORM_TEX(uv,_NoiseMap) + _NoiseMove *
        /// _Time.y (URP's _Time.y == Time.timeSinceLevelLoad). sRGB noise is linearised to match the GPU.
        /// EXACT port of RockBobber.SampleNoiseDisp. Only call when the params are IsValid.
        /// </summary>
        public static float SampleDisp(in V4Params p, Vector2 meshUV, float t)
        {
            float u = meshUV.x * p.Scale.x + p.Offset.x + p.Move.x * t;
            float v = meshUV.y * p.Scale.y + p.Offset.y + p.Move.y * t;
            float n = p.NoiseTex.GetPixelBilinear(u, v).r; // wrap mode handled by the texture
            if (p.Linearize) n = Mathf.GammaToLinearSpace(n);
            return n * p.Strength;
        }
    }
}

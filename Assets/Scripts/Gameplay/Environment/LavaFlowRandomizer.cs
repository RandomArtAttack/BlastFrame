using UnityEngine;

namespace BlastFrame.Gameplay.Environment
{
    /// <summary>
    /// Per-object lava-flow variety. Several lava falls that SHARE one "RAA Lava Flow" material all scroll
    /// in lockstep and look identical. At Start this randomises each lava-flow renderer's texture OFFSET
    /// (its starting frame) and, optionally, jitters its scroll SPEED — applied through a
    /// MaterialPropertyBlock, so it varies the rendered instance WITHOUT cloning or editing the shared
    /// material asset. Detects the lava-flow shader family by its `_BottomMove` property (V.1-V.4).
    ///
    /// Self-contained: only touches its own (and optionally child) Renderers; no scene references, never
    /// writes the material on disk. Variety is seeded by world position, so each fall looks different from
    /// the others but the SAME every play (change Seed to reshuffle).
    /// </summary>
    public class LavaFlowRandomizer : MonoBehaviour
    {
        [Tooltip("Also scan child Renderers (a lava fall built from several meshes, or a group of falls under " +
                 "one parent). Off = only this GameObject's own Renderer.")]
        [SerializeField] private bool includeChildren = true;

        [Tooltip("Randomise each layer's texture OFFSET (its starting frame). The main 'make them look " +
                 "different' knob — leave on.")]
        [SerializeField] private bool randomizeOffset = true;

        [Tooltip("Vary scroll SPEED per object by ±this fraction so falls also DRIFT apart over time, not " +
                 "just start phase-shifted. 0 = identical speed (offset only). 0.15 = ±15%.")]
        [Range(0f, 0.75f)]
        [SerializeField] private float speedJitter = 0.15f;

        [Tooltip("Reshuffle handle: change this for a different set of looks. Variety is otherwise STABLE per " +
                 "object (seeded by world position), so a given fall looks the same every play.")]
        [SerializeField] private int seed = 0;

        // Lava-flow shader property names (the V.1-V.4 family shares these).
        private const string BottomMoveProp = "_BottomMove";
        private const string TopMoveProp    = "_TopMove";
        private const string BottomTexProp  = "_EmissionMap";
        private const string TopTexProp     = "_TopEmissionMap";
        private const string BottomStProp   = "_EmissionMap_ST";
        private const string TopStProp      = "_TopEmissionMap_ST";

        private void Start()
        {
            var renderers = includeChildren ? GetComponentsInChildren<Renderer>(true) : GetComponents<Renderer>();
            if (renderers.Length == 0) return;

            var mpb = new MaterialPropertyBlock();
            foreach (var r in renderers)
            {
                System.Random rng = MakeRng(r.transform.position);
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    Material m = mats[i];
                    if (m == null || !m.HasProperty(BottomMoveProp)) continue; // not a lava-flow material

                    r.GetPropertyBlock(mpb, i); // start from any existing per-renderer block, don't clobber it

                    if (randomizeOffset)
                    {
                        RandomizeOffset(mpb, m, BottomTexProp, BottomStProp, rng);
                        if (m.HasProperty(TopStProp)) RandomizeOffset(mpb, m, TopTexProp, TopStProp, rng);
                    }
                    if (speedJitter > 0f)
                    {
                        JitterMove(mpb, m, BottomMoveProp, rng);
                        JitterMove(mpb, m, TopMoveProp, rng);
                    }

                    r.SetPropertyBlock(mpb, i);
                }
            }
        }

        // Stable per-object seed from world position (each fall differs; same look every play). int math
        // overflow is intentional and deterministic.
        private System.Random MakeRng(Vector3 p)
        {
            unchecked
            {
                int s = seed * 73856093;
                s ^= Mathf.RoundToInt(p.x * 1000f) * 19349663;
                s ^= Mathf.RoundToInt(p.y * 1000f) * 83492791;
                s ^= Mathf.RoundToInt(p.z * 1000f) * 49979693;
                return new System.Random(s);
            }
        }

        // Replace the layer's UV offset (the _ST .zw) with a random one, preserving the designer's tiling (.xy).
        private static void RandomizeOffset(MaterialPropertyBlock mpb, Material m, string texProp, string stProp, System.Random rng)
        {
            Vector2 scale = m.GetTextureScale(texProp);
            mpb.SetVector(stProp, new Vector4(scale.x, scale.y, (float)rng.NextDouble(), (float)rng.NextDouble()));
        }

        // Scale the layer's scroll speed by 1 ± speedJitter.
        private void JitterMove(MaterialPropertyBlock mpb, Material m, string moveProp, System.Random rng)
        {
            if (!m.HasProperty(moveProp)) return;
            float f = 1f + ((float)rng.NextDouble() * 2f - 1f) * speedJitter;
            mpb.SetVector(moveProp, m.GetVector(moveProp) * f);
        }
    }
}

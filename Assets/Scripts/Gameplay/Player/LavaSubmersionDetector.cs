using System;
using UnityEngine;
using BlastFrame.Core.Variables;
using BlastFrame.Gameplay.Environment;

namespace BlastFrame.Gameplay.Player
{
    using BlastFrame.Gameplay; // DamageInfo / DamageKind

    /// <summary>
    /// Detects when the player's HEAD is under the lava river's VISUALLY displaced surface (the GPU
    /// vertex churn of RAA Lava Flow V.4, which never moves the CPU mesh/collider). Each FixedUpdate it
    /// fires a downward ray on the Lava layer from above the head, reconstructs the displaced surface Y
    /// via the shared LavaV4Sampler (same technique RockBobber uses to seat rocks), and compares it to
    /// the head height. While submerged it deals damage over time and raises OnSubmergedChanged so the
    /// HUD can turn the screen opaque orange.
    ///
    /// Self-contained: lives on the Player root, resolves everything at runtime (no cross-object
    /// Inspector references). The lava mesh must be Read/Write Enabled for hit.textureCoord (already
    /// satisfied for lava meshes by Implement Fix 046, which ran for RockBobber).
    /// </summary>
    public class LavaSubmersionDetector : MonoBehaviour
    {
        [Tooltip("Height of the player's eyes/head above the root, in metres. The head point that must " +
                 "go under the lava surface. Match the first-person camera eye height (~1.8).")]
        [SerializeField] private FloatReference eyeHeight = new FloatReference(1.8f);

        [Tooltip("How far ABOVE the head the detection ray starts, in metres. Keep > the wave amplitude " +
                 "so the ray begins above the displaced surface. ~5.")]
        [SerializeField] private FloatReference probeHeight = new FloatReference(5f);

        [Tooltip("How far BELOW the head the ray reaches, in metres — how deep submersion is still " +
                 "detected. ~10.")]
        [SerializeField] private FloatReference probeDepth = new FloatReference(10f);

        [Tooltip("Extra height the head must rise ABOVE the surface before counting as surfaced, in " +
                 "metres. Prevents flicker at the wave boundary. ~0.05.")]
        [SerializeField] private FloatReference exitHysteresis = new FloatReference(0.05f);

        [Tooltip("If the ray misses while submerged, hold the submerged state this long before clearing, " +
                 "in seconds — bridges brief gaps between river pieces. ~0.3.")]
        [SerializeField] private FloatReference missGrace = new FloatReference(0.3f);

        [Tooltip("Damage dealt per tick while the head is submerged. Design default 1.")]
        [SerializeField] private IntReference damagePerTick = new IntReference(1);

        [Tooltip("Seconds between submersion damage ticks. Keep above the player's i-frames (0.5 s) so " +
                 "every tick lands. Design default 0.75.")]
        [SerializeField] private FloatReference tickInterval = new FloatReference(0.75f);

        [Tooltip("Layers the downward ray tests to find the lava surface. Set to the 'Lava' layer. If " +
                 "left at Nothing, the component auto-uses the 'Lava' layer by name.")]
        [SerializeField] private LayerMask lavaMask = 0;

        [Tooltip("Tick if the lava Noise texture is imported as sRGB (the default). In a Linear-space " +
                 "project the CPU sampler must linearise to match the GPU surface exactly. Untick only if " +
                 "the noise texture's import is set to Linear.")]
        [SerializeField] private bool noiseIsSRGB = true;

        public bool IsSubmerged => _submerged;
        public event Action<bool> OnSubmergedChanged;

        private IDamageable _health;
        private int _lavaMask;
        private bool _submerged;
        private float _missTimer;
        private float _nextTick;

        // V.4 params cached per hit-renderer — only re-read when the ray moves onto a different lava piece.
        private Renderer _lastRenderer;
        private Transform _lavaTf;
        private LavaV4Sampler.V4Params _v4;

        private void Awake()
        {
            _health = GetComponent<IDamageable>();
            _lavaMask = lavaMask.value != 0 ? lavaMask.value : LayerMask.GetMask(BlastFrame.Core.LayerNames.Lava);
        }

        private void FixedUpdate()
        {
            Vector3 origin = transform.position + Vector3.up * (eyeHeight.Value + probeHeight.Value);
            float len = probeHeight.Value + probeDepth.Value;

            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, len, _lavaMask,
                    QueryTriggerInteraction.Ignore))
            {
                _missTimer = 0f;

                var renderer = hit.collider.GetComponentInParent<Renderer>();

                if (renderer == null) renderer = hit.collider.GetComponentInChildren<Renderer>(); // explicit: ?? skips Unity's null check
                if (renderer != _lastRenderer)
                {
                    _lastRenderer = renderer;
                    _lavaTf = renderer != null ? renderer.transform : null;
                    LavaV4Sampler.TryCache(renderer != null ? renderer.sharedMaterial : null, noiseIsSRGB, out _v4);
                }

                // Displaced surface Y = undisplaced hit height + the GPU's vertex displacement at the hit
                // mesh UV. Non-V4 lava (waterfall/ripple materials) => 0 displacement, flat hit.point.y.
                float disp = _v4.IsValid
                    ? LavaV4Sampler.SampleDisp(in _v4, hit.textureCoord, Time.timeSinceLevelLoad)
                    : 0f;
                float scaleY = _lavaTf != null ? _lavaTf.lossyScale.y : 1f;
                float surfaceY = hit.point.y + disp * scaleY;

                float headY = transform.position.y + eyeHeight.Value;
                // Enter when the head drops below the surface; require rising above surface+hysteresis to
                // surface again (kills flicker right at the wave boundary).
                bool submerged = _submerged
                    ? headY <= surfaceY + exitHysteresis.Value
                    : headY < surfaceY;
                SetSubmerged(submerged);
            }
            else if (_submerged)
            {
                // Ray missed while under: hold the submerged state briefly (gap between river pieces).
                _missTimer += Time.fixedDeltaTime;
                if (_missTimer >= missGrace.Value) SetSubmerged(false);
            }

            if (_submerged && Time.time >= _nextTick)
            {
                _health?.TakeDamage(new DamageInfo(damagePerTick.Value, DamageKind.Submersion));
                _nextTick = Time.time + tickInterval.Value;
            }
        }

        private void SetSubmerged(bool value)
        {
            if (_submerged == value) return;
            _submerged = value;
            if (value) _nextTick = Time.time; // first tick immediate on submersion
            OnSubmergedChanged?.Invoke(value);
        }
    }
}

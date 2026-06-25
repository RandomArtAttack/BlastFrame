using UnityEngine;

namespace BlastFrame.Gameplay.Player.Movement
{
    /// <summary>
    /// Mantle/ledge-vault assist. When the player is wall-sliding and steering INTO the wall, but the
    /// ledge top sits between their feet and their gun (a near-miss they'd otherwise just slide down),
    /// this gives a small automatic hop sized to clear the lip, plus a forward nudge onto the surface.
    ///
    /// Detection (all must hold): airborne + on a wall, pushing toward it, an upper "gun-height" forward
    /// ray is CLEAR (no wall there — the ledge is below it), a lower forward ray is BLOCKED (the ledge
    /// face), and a downward ray past the lip finds a walkable top within Max Vault Height of the feet.
    /// The pop velocity is computed from the measured clear height + margin against PlayerStats gravity,
    /// so it ALWAYS clears the ledge regardless of its exact height; Vault Up Bonus is the extra "feel"
    /// pop on top. Tall ledges (above Max Vault Height) are ignored — the player wall-jumps those.
    ///
    /// Runs AFTER WallSlideModule so it overrides that tick's slide clamp, and yields to an explicit
    /// jump/wall-jump (skips if JumpFired is already set this tick). No input wiring — it reads WishDir.
    /// Self-contained: only its own siblings (motor Rigidbody/CapsuleCollider, PlayerStats) and a mask.
    /// </summary>
    public class LedgeVaultModule : MonoBehaviour, IMovementModule
    {
        public int Order => MovementOrder.LedgeVault;

        [Tooltip("Surfaces the ledge probes test. Set to the same world/platform layers the player " +
                 "collides with; EXCLUDE the Player and Viewmodel layers.")]
        [SerializeField] private LayerMask vaultMask = ~0;

        [Tooltip("Height above the feet of the 'gun' forward ray that must be CLEAR for a vault, in metres. " +
                 "Put this near eye/gun height (~1.6). The ledge top has to be below this to qualify.")]
        [SerializeField] private float upperProbeHeight = 1.6f;

        [Tooltip("Height above the feet of the low forward ray that must be BLOCKED by the ledge face, in " +
                 "metres. ~0.4. Keep well below Upper Probe Height.")]
        [SerializeField] private float lowerProbeHeight = 0.4f;

        [Tooltip("How far forward (past the capsule) the rays reach to find the wall/ledge, in metres. ~0.7.")]
        [SerializeField] private float probeDistance = 0.7f;

        [Tooltip("Tallest ledge (top height above the feet) that will AUTO-vault, in metres. Keeps it a small " +
                 "assist for near-misses; taller walls are left to the wall-jump. ~0.9.")]
        [SerializeField] private float maxVaultHeight = 0.9f;

        [Tooltip("Shortest ledge that qualifies, in metres — ignores near-level lips the motor's step-up " +
                 "already handles. ~0.1.")]
        [SerializeField] private float minVaultHeight = 0.1f;

        [Tooltip("Max slope angle of the ledge top still treated as standable, in degrees. ~50.")]
        [SerializeField] private float maxLedgeAngle = 50f;

        [Tooltip("How directly the player must steer into the wall to vault (dot of WishDir vs into-wall). " +
                 "0 = any forward lean, 1 = dead-on. ~0.3.")]
        [SerializeField] private float pushIntoWallDot = 0.3f;

        [Tooltip("Extra height added to the computed clear so the feet pass over the lip, in metres. ~0.15.")]
        [SerializeField] private float vaultClearMargin = 0.15f;

        [Tooltip("The 'mini jump' — extra upward velocity on top of just clearing the ledge, in m/s. This is " +
                 "the little pop you feel. ~0.5.")]
        [SerializeField] private float vaultUpBonus = 0.5f;

        [Tooltip("Forward velocity toward the ledge applied on vault so the player lands ON it, in m/s. ~4.")]
        [SerializeField] private float vaultForwardSpeed = 4f;

        [Tooltip("Seconds before another vault can fire, so it doesn't re-trigger every tick. ~0.4.")]
        [SerializeField] private float vaultCooldown = 0.4f;

        private Rigidbody _rb;
        private CapsuleCollider _capsule;
        private PlayerStats _stats;
        private float _cooldownTimer;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _capsule = GetComponent<CapsuleCollider>();
            _stats = GetComponent<PlayerStats>();
        }

        public void Tick(ref MoveState state)
        {
            if (_cooldownTimer > 0f) _cooldownTimer -= state.DeltaTime;

            // Gate: only while wall-sliding (airborne, on a wall, not rising), not already jumping, off cooldown.
            if (state.JumpFired || _cooldownTimer > 0f) return;
            if (state.IsGrounded || !state.IsOnWall || state.Velocity.y > 0.1f) return;

            // Direction into the wall (horizontal). Need a real horizontal normal to probe along.
            Vector3 intoWall = new Vector3(-state.WallNormal.x, 0f, -state.WallNormal.z);
            if (intoWall.sqrMagnitude < 1e-4f) return;
            intoWall.Normalize();

            // Must be steering into the wall (pushing forward).
            if (Vector3.Dot(state.WishDir, intoWall) < pushIntoWallDot) return;

            Vector3 basePos = _rb.position;
            Vector3 upperOrigin = basePos + Vector3.up * upperProbeHeight;
            Vector3 lowerOrigin = basePos + Vector3.up * lowerProbeHeight;

            // Gun height must be CLEAR (the "raycast shows no platform" check) and the low ray BLOCKED.
            if (Physics.Raycast(upperOrigin, intoWall, probeDistance, vaultMask, QueryTriggerInteraction.Ignore))
                return;
            if (!Physics.Raycast(lowerOrigin, intoWall, probeDistance, vaultMask, QueryTriggerInteraction.Ignore))
                return;

            // Find the ledge top just past the lip and confirm it's standable and within reach.
            Vector3 topOrigin = upperOrigin + intoWall * probeDistance;
            if (!Physics.Raycast(topOrigin, Vector3.down, out RaycastHit topHit, upperProbeHeight,
                    vaultMask, QueryTriggerInteraction.Ignore))
                return;
            if (Vector3.Angle(topHit.normal, Vector3.up) > maxLedgeAngle) return;

            float feetY = basePos.y + _capsule.center.y - _capsule.height * 0.5f;
            float clearHeight = topHit.point.y - feetY;
            if (clearHeight < minVaultHeight || clearHeight > maxVaultHeight) return;

            // Pop: enough to clear the lip (+margin) against gravity, plus the feel bonus; nudge forward.
            float upSpeed = Mathf.Sqrt(2f * _stats.Gravity * (clearHeight + vaultClearMargin)) + vaultUpBonus;
            Vector3 forward = intoWall * vaultForwardSpeed;
            state.Velocity = new Vector3(forward.x, upSpeed, forward.z);
            state.JumpFired = true; // release any platform carry, same as a jump
            _cooldownTimer = vaultCooldown;
        }
    }
}

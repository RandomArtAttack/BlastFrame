using System.Collections.Generic;
using UnityEngine;
using BlastFrame.Gameplay.Platforms;

namespace BlastFrame.Gameplay.Player.Movement
{
    /// <summary>
    /// Tracks the platform under the player and rides it: adds the platform's contributed velocity
    /// (moving/rotating carry, treadmill push) while grounded on it, and consumes spring launches.
    /// Because the ride velocity is baked in on the grounded tick a jump fires, the player inherits
    /// the platform's velocity into the jump — but stops inheriting once airborne (snapshot, not
    /// continuous), exactly as designed.
    /// </summary>
    public class PlatformRiderModule : MonoBehaviour, IMovementModule
    {
        public int Order => MovementOrder.PlatformRider;

        [Tooltip("How far below the capsule to probe for a ridable platform, in metres. ~0.3.")]
        [SerializeField] private float probeDistance = 0.3f;

        [Tooltip("Layers platforms live on. Keep broad; only IRidePlatform components contribute.")]
        [SerializeField] private LayerMask platformMask = ~0;

        [Tooltip("Seconds the carry survives a brief loss of grounding while the player is still over the " +
                 "platform. A bobbing platform's ground check flickers for a tick on fast up-strokes; without " +
                 "this grace the moving platform slides out from under the player. ~0.12.")]
        [SerializeField] private float rideGraceTime = 0.12f;

        /// <summary>True while the player is being carried by a platform this tick (grounded or within the
        /// grace window), but NOT on the tick a jump launches. PlayerController reads it so base locomotion
        /// resets horizontal like grounded — otherwise the carry would double-add during a grace tick.</summary>
        public bool IsRiding { get; private set; }

        private CapsuleCollider _capsule;
        private readonly RaycastHit[] _hits = new RaycastHit[8];
        private readonly List<IRidePlatform> _rideBuf = new(); // reused, no per-tick alloc
        private float _graceTimer;     // counts down while coasting through a grounded flicker
        private bool _suppressRide;    // after a jump/launch: don't carry until grounded again

        private void Awake() => _capsule = GetComponent<CapsuleCollider>();

        public void Tick(ref MoveState state)
        {
            IsRiding = false;

            if (!TryProbe(out var collider, out Vector3 point))
            {
                _graceTimer = 0f; // off any platform entirely
                return;
            }

            // Spring launches are checked regardless of grounded state (contact-driven, overrides).
            if (collider.TryGetComponent<ILaunchPlatform>(out var launcher) &&
                launcher.TryConsumeLaunch(out Vector3 launchVel))
            {
                state.Velocity = launchVel;
                _graceTimer = 0f;
                _suppressRide = true; // launched off — don't re-carry mid-air until we land again
                return;
            }

            // Sum EVERY IRidePlatform on the object, not just the first. One object can compose several
            // (a RockBobber bob + a MovingPlatform path, or a Treadmill on a mover) and the player must be
            // carried by ALL of them.
            collider.GetComponents(_rideBuf);
            if (_rideBuf.Count == 0) { _graceTimer = 0f; return; } // not a ride platform (e.g. plain ground)

            Vector3 rideVel = Vector3.zero;
            for (int i = 0; i < _rideBuf.Count; i++)
                rideVel += _rideBuf[i].SampleRideVelocity(point);

            // Carry while grounded OR within a short grace after grounding was lost — a bobbing platform's
            // ground check flickers false for a tick on fast up-strokes, and without the grace the player
            // skips a carry tick and the moving platform slides out from under them. A jump/launch
            // suppresses the carry until we're grounded again so the jump keeps a clean velocity snapshot
            // (taken THIS tick, while still grounded) instead of inheriting platform velocity all the way up.
            bool carry = (state.IsGrounded || _graceTimer > 0f) && !_suppressRide;
            if (carry)
            {
                // Horizontal: additive — base locomotion resets horizontal when riding (grounded or grace),
                // so this contributes exactly one platform-velocity per tick (no double-add).
                state.Velocity.x += rideVel.x;
                state.Velocity.z += rideVel.z;

                // Vertical: MATCH a rising platform, never add. A jump's vy is bigger and wins the Max.
                // Descending platforms need nothing — the motor's ground snap keeps us glued.
                if (rideVel.y > 0f)
                    state.Velocity.y = Mathf.Max(state.Velocity.y, rideVel.y);
            }

            // Bookkeeping for next tick (after the carry, so a jump fired THIS grounded tick still got its
            // snapshot above before we start suppressing).
            if (state.JumpFired) _suppressRide = true;
            else if (state.IsGrounded) _suppressRide = false;

            if (state.IsGrounded) _graceTimer = rideGraceTime;
            else if (carry) _graceTimer -= state.DeltaTime;
            else _graceTimer = 0f;

            // Riding for base locomotion = a steady carry, NOT the tick a jump launches (so the jump's
            // horizontal momentum isn't reset away next tick).
            IsRiding = carry && !state.JumpFired;
        }

        private bool TryProbe(out Collider collider, out Vector3 point)
        {
            collider = null;
            point = transform.position;
            float radius = _capsule != null ? _capsule.radius * 0.9f : 0.35f;

            // Cast from the capsule's BOTTOM sphere centre (same as the motor's ground check) — from
            // the capsule's middle the sphere can never reach the surface under the feet.
            Vector3 origin;
            if (_capsule != null)
            {
                float half = Mathf.Max(0f, _capsule.height * 0.5f - _capsule.radius);
                origin = transform.position + _capsule.center - Vector3.up * half;
            }
            else
            {
                origin = transform.position + Vector3.up * 0.5f;
            }

            int count = Physics.SphereCastNonAlloc(origin, radius, Vector3.down, _hits,
                probeDistance + 0.1f, platformMask, QueryTriggerInteraction.Ignore);

            float min = float.MaxValue;
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                var h = _hits[i];
                if (h.collider == _capsule || h.distance <= 0f) continue;
                if (h.distance < min)
                {
                    min = h.distance;
                    collider = h.collider;
                    point = h.point;
                    found = true;
                }
            }
            return found;
        }
    }
}

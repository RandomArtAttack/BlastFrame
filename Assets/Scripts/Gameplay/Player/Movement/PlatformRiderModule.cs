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

        private CapsuleCollider _capsule;
        private readonly RaycastHit[] _hits = new RaycastHit[8];

        private void Awake() => _capsule = GetComponent<CapsuleCollider>();

        public void Tick(ref MoveState state)
        {
            // Spring launches are checked regardless of grounded state (contact-driven, overrides).
            if (TryProbe(out var collider, out Vector3 point))
            {
                if (collider.TryGetComponent<ILaunchPlatform>(out var launcher) &&
                    launcher.TryConsumeLaunch(out Vector3 launchVel))
                {
                    state.Velocity = launchVel;
                    return;
                }

                if (state.IsGrounded && collider.TryGetComponent<IRidePlatform>(out var ride))
                {
                    state.Velocity += ride.SampleRideVelocity(point);
                }
            }
        }

        private bool TryProbe(out Collider collider, out Vector3 point)
        {
            collider = null;
            point = transform.position;
            float radius = _capsule != null ? _capsule.radius * 0.9f : 0.35f;
            Vector3 origin = transform.position + (_capsule != null ? _capsule.center : Vector3.up) + Vector3.up * 0.1f;

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

using UnityEngine;

namespace BlastFrame.Gameplay.Player
{
    /// <summary>
    /// Custom kinematic Rigidbody motor: owns velocity and performs a collide-and-slide capsule
    /// sweep each call. NOT a CharacterController, NOT force-driven. Exposes grounded/wall state
    /// for movement modules. Uses cached buffers — no per-frame allocations.
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
    public class PlayerMotor : MonoBehaviour
    {
        [Tooltip("Layers the player collides with. Exclude the Player layer itself.")]
        [SerializeField] private LayerMask collisionMask = ~0;

        [Tooltip("Skin width kept between the capsule and surfaces, in metres. ~0.02.")]
        [SerializeField] private float skinWidth = 0.02f;

        [Tooltip("Max slope angle (degrees) treated as ground. Steeper = wall.")]
        [SerializeField] private float maxGroundAngle = 50f;

        private const int MaxBounces = 5;
        private const float GroundProbe = 0.08f;

        private Rigidbody _rb;
        private CapsuleCollider _capsule;
        private readonly RaycastHit[] _hitBuffer = new RaycastHit[8];

        public Vector3 Velocity { get; set; }
        public bool IsGrounded { get; private set; }
        public bool WasGroundedLastTick { get; private set; }
        public bool IsOnWall { get; private set; }
        public Vector3 WallNormal { get; private set; }
        public Vector3 GroundNormal { get; private set; } = Vector3.up;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _capsule = GetComponent<CapsuleCollider>();
            _rb.isKinematic = true;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            _rb.constraints = RigidbodyConstraints.FreezeRotation;
        }

        /// <summary>Sweep-move using the supplied velocity for dt. Returns the post-collision velocity.</summary>
        public Vector3 Move(Vector3 velocity, float dt)
        {
            WasGroundedLastTick = IsGrounded;
            Velocity = velocity;
            IsOnWall = false;
            WallNormal = Vector3.zero;

            Vector3 startPos = _rb.position;
            Vector3 displacement = velocity * dt;
            Vector3 resolved = CollideAndSlide(displacement, startPos, 0, velocity, out velocity);

            _rb.MovePosition(startPos + resolved);
            GroundCheck(startPos + resolved, ref velocity);

            Velocity = velocity;
            return velocity;
        }

        private Vector3 CollideAndSlide(Vector3 vel, Vector3 pos, int depth, Vector3 fullVel, out Vector3 outVel)
        {
            outVel = fullVel;
            if (depth >= MaxBounces || vel.sqrMagnitude < 1e-8f) return Vector3.zero;

            float dist = vel.magnitude + skinWidth;
            Vector3 dir = vel.normalized;

            if (CapsuleCast(pos, dir, dist, out var hit))
            {
                Vector3 snap = dir * Mathf.Max(0f, hit.distance - skinWidth);
                Vector3 leftover = vel - snap;

                bool isGround = Vector3.Angle(hit.normal, Vector3.up) <= maxGroundAngle;
                if (!isGround && Mathf.Abs(hit.normal.y) < 0.5f)
                {
                    IsOnWall = true;
                    WallNormal = hit.normal;
                }

                // Remove the into-surface component from carried velocity for the next tick.
                outVel = Vector3.ProjectOnPlane(fullVel, hit.normal);

                leftover = Vector3.ProjectOnPlane(leftover, hit.normal);
                return snap + CollideAndSlide(leftover, pos + snap, depth + 1, outVel, out outVel);
            }

            return vel;
        }

        private void GroundCheck(Vector3 pos, ref Vector3 velocity)
        {
            GetCapsulePoints(pos, out Vector3 p1, out Vector3 p2, out float radius);
            bool grounded = Physics.SphereCast(p1, radius * 0.95f, Vector3.down,
                out var hit, GroundProbe + skinWidth, collisionMask, QueryTriggerInteraction.Ignore)
                && Vector3.Angle(hit.normal, Vector3.up) <= maxGroundAngle;

            IsGrounded = grounded;
            GroundNormal = grounded ? hit.normal : Vector3.up;

            if (grounded && velocity.y < 0f) velocity.y = 0f;
        }

        private bool CapsuleCast(Vector3 pos, Vector3 dir, float dist, out RaycastHit closest)
        {
            GetCapsulePoints(pos, out Vector3 p1, out Vector3 p2, out float radius);
            int count = Physics.CapsuleCastNonAlloc(p1, p2, radius, dir, _hitBuffer, dist, collisionMask, QueryTriggerInteraction.Ignore);
            closest = default;
            float min = float.MaxValue;
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                var h = _hitBuffer[i];
                if (h.collider == _capsule) continue;
                if (h.distance <= 0f) continue;
                if (h.distance < min)
                {
                    min = h.distance;
                    closest = h;
                    found = true;
                }
            }
            return found;
        }

        private void GetCapsulePoints(Vector3 pos, out Vector3 p1, out Vector3 p2, out float radius)
        {
            radius = _capsule.radius;
            float half = Mathf.Max(0f, _capsule.height * 0.5f - radius);
            Vector3 center = pos + _capsule.center;
            p1 = center + Vector3.up * half;   // top sphere centre
            p2 = center - Vector3.up * half;   // bottom sphere centre
        }

        /// <summary>Teleport without sweeping (spawn / respawn).</summary>
        public void Teleport(Vector3 position)
        {
            _rb.position = position;
            transform.position = position;
            Velocity = Vector3.zero;
        }
    }
}

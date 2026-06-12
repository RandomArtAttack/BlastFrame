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

        [Tooltip("Max ledge height auto-stepped while walking grounded (stairs/low blocks), in metres. ~0.35. 0 disables.")]
        [SerializeField] private float maxStepHeight = 0.35f;

        [Tooltip("Horizontal gap (beyond the capsule) within which an airborne player still counts as 'on a wall' " +
                 "for wall-slide and wall-jump, even without pushing toward it, in metres. ~0.15. 0 disables.")]
        [SerializeField] private float wallDetectDistance = 0.15f;

        [Tooltip("When grounded and descending, the motor looks this far below the feet to stay planted on " +
                 "steps/slopes (prevents micro-hops walking down), in metres. ~0.35. 0 disables snapping.")]
        [SerializeField] private float groundSnapDistance = 0.35f;

        private const int MaxBounces = 5;
        private const float GroundProbe = 0.08f;

        private Rigidbody _rb;
        private CapsuleCollider _capsule;
        private readonly RaycastHit[] _hitBuffer = new RaycastHit[8];
        private readonly Vector3[] _wallProbeDirs = new Vector3[4];

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

        /// <summary>Sweep-move using the supplied velocity for dt. Returns the post-collision velocity.
        /// Horizontal and vertical motion are resolved in separate passes: walls/slopes handled by the
        /// horizontal collide-and-slide, gravity/jump by a straight vertical sweep that never projects
        /// into horizontal. This gives flat-bottom, cylinder-like edge behaviour — a downward contact on
        /// a ledge corner can no longer slingshot the player sideways off the edge.</summary>
        public Vector3 Move(Vector3 velocity, float dt)
        {
            WasGroundedLastTick = IsGrounded;
            Velocity = velocity;
            IsOnWall = false;
            WallNormal = Vector3.zero;

            Vector3 startPos = _rb.position;
            Vector3 inVelocity = velocity;

            // --- Horizontal pass: walls + slopes (no vertical component) ---
            Vector3 horizVel = new Vector3(velocity.x, 0f, velocity.z);
            Vector3 horizDisp = horizVel * dt;
            Vector3 horizResolved = CollideAndSlide(horizDisp, startPos, 0, horizVel, out Vector3 horizOut);

            // Step-up: walking into a low ledge — climb it instead of stopping at it.
            if (WasGroundedLastTick && IsOnWall && maxStepHeight > 0f
                && TryStepUp(startPos, horizDisp, out Vector3 stepped))
            {
                horizResolved = stepped;
                horizOut = new Vector3(inVelocity.x, 0f, inVelocity.z);
                IsOnWall = false;
                WallNormal = Vector3.zero;
            }

            Vector3 posAfterHoriz = startPos + horizResolved;

            // --- Vertical pass: gravity / jump, resolved straight (never redirected horizontally) ---
            float vY = velocity.y;
            float vDist = Mathf.Abs(vY) * dt;

            // Ground snap: when grounded and not rising, reach a little further down to stay glued to
            // steps/slopes. Only commits to the extra distance if ground is actually found there.
            bool snapping = WasGroundedLastTick && vY <= 0.1f && groundSnapDistance > 0f;
            Vector3 vDir = (vY > 0f && !snapping) ? Vector3.up : Vector3.down;
            float castDist = snapping ? Mathf.Max(vDist, groundSnapDistance) : vDist;

            Vector3 vertResolved = vDir * vDist;
            if (castDist > 0f && CapsuleCast(posAfterHoriz, vDir, castDist + skinWidth, out var vHit))
            {
                float allowed = Mathf.Max(0f, vHit.distance - skinWidth);
                if (vDir.y > 0f)
                {
                    vertResolved = Vector3.up * allowed;   // ceiling
                    vY = 0f;
                }
                else
                {
                    vertResolved = Vector3.down * allowed; // ground / step — snap onto it
                    if (Vector3.Angle(vHit.normal, Vector3.up) <= maxGroundAngle) vY = 0f;
                }
            }
            else if (snapping)
            {
                // No ground within snap reach (walked off a real ledge) — fall by gravity only,
                // never teleport down by the extended snap distance.
                vertResolved = vDir * vDist;
            }

            Vector3 endPos = posAfterHoriz + vertResolved;
            velocity = new Vector3(horizOut.x, vY, horizOut.z);

            _rb.MovePosition(endPos);
            GroundCheck(endPos, ref velocity);

            // While airborne, detect a wall we're merely adjacent to (not pushing into) so wall-slide
            // and wall-jump stay reactive. The sweep only finds walls in the direction of travel.
            if (!IsGrounded && !IsOnWall && wallDetectDistance > 0f)
                WallProbe(endPos);

            Velocity = velocity;
            return velocity;
        }

        /// <summary>Casts the capsule a short distance in the four body-horizontal directions to flag
        /// an adjacent vertical wall (sets IsOnWall / WallNormal). Picks the nearest wall hit.</summary>
        private void WallProbe(Vector3 pos)
        {
            _wallProbeDirs[0] = transform.right;
            _wallProbeDirs[1] = -transform.right;
            _wallProbeDirs[2] = transform.forward;
            _wallProbeDirs[3] = -transform.forward;

            GetCapsulePoints(pos, out Vector3 p1, out Vector3 p2, out float radius);
            float dist = wallDetectDistance + skinWidth;

            float nearest = float.MaxValue;
            for (int d = 0; d < 4; d++)
            {
                int count = Physics.CapsuleCastNonAlloc(p1, p2, radius, _wallProbeDirs[d], _hitBuffer, dist, collisionMask, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; i++)
                {
                    var h = _hitBuffer[i];
                    if (h.collider == _capsule) continue;
                    if (Mathf.Abs(h.normal.y) >= 0.5f) continue;                       // floor/ceiling, not a wall
                    if (Vector3.Angle(h.normal, Vector3.up) <= maxGroundAngle) continue; // walkable slope, not a wall
                    if (h.distance < nearest)
                    {
                        nearest = h.distance;
                        IsOnWall = true;
                        WallNormal = h.normal;
                    }
                }
            }
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

        /// <summary>Up → forward → down capsule sweep. Succeeds only if the landing spot is
        /// ground-angle and most of the blocked horizontal motion is recovered.</summary>
        private bool TryStepUp(Vector3 startPos, Vector3 displacement, out Vector3 resolved)
        {
            resolved = Vector3.zero;
            Vector3 horiz = new Vector3(displacement.x, 0f, displacement.z);
            if (horiz.sqrMagnitude < 1e-10f) return false;

            // 1. Up as far as the step height (or the ceiling) allows.
            float up = maxStepHeight;
            if (CapsuleCast(startPos, Vector3.up, up + skinWidth, out var hitUp))
                up = Mathf.Max(0f, hitUp.distance - skinWidth);
            if (up < 0.01f) return false;
            Vector3 raised = startPos + Vector3.up * up;

            // 2. Forward at the raised height.
            float fwdDist = horiz.magnitude;
            Vector3 fwdDir = horiz / fwdDist;
            if (CapsuleCast(raised, fwdDir, fwdDist + skinWidth, out var hitFwd))
            {
                float clear = Mathf.Max(0f, hitFwd.distance - skinWidth);
                if (clear < fwdDist * 0.5f) return false; // still blocked — a wall, not a step
                fwdDist = clear;
            }
            Vector3 fwdPos = raised + fwdDir * fwdDist;

            // 3. Down onto the step surface.
            if (!CapsuleCast(fwdPos, Vector3.down, up + GroundProbe, out var hitDown)) return false;
            if (Vector3.Angle(hitDown.normal, Vector3.up) > maxGroundAngle) return false;

            Vector3 final = fwdPos + Vector3.down * Mathf.Max(0f, hitDown.distance - skinWidth);
            resolved = final - startPos;
            return true;
        }

        private void GroundCheck(Vector3 pos, ref Vector3 velocity)
        {
            GetCapsulePoints(pos, out Vector3 p1, out Vector3 p2, out float radius);
            bool grounded = Physics.SphereCast(p2, radius * 0.95f, Vector3.down,
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

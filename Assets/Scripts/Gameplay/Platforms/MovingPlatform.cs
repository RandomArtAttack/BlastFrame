using System.Collections.Generic;
using UnityEngine;
using BlastFrame.Core;
using BlastFrame.Core.Variables;

namespace BlastFrame.Gameplay.Platforms
{
    /// <summary>
    /// Kinematic Rigidbody platform that moves between an ordered list of child waypoint Transforms.
    /// Supports Cycle (loop) and PingPong traversal. Exposes CurrentVelocity for the rider module to
    /// snapshot at jump time so the player inherits the platform's momentum.
    ///
    /// Can be EXTERNALLY DRIVEN: a sibling that wants to own the final move (e.g. RockBobber, which
    /// layers a lava bob onto this platform's path) calls SetExternallyDriven(true). Then this component
    /// still computes the path and publishes NextPosition + CurrentVelocity, but does NOT call
    /// MovePosition itself — the driver reads NextPosition, adds its own offset, and moves the body.
    /// </summary>
    // Runs before RockBobber (-50) and PlayerController (0) so its NextPosition/CurrentVelocity for this
    // tick are ready when a driver composes them and when the rider reads the carry.
    [DefaultExecutionOrder(-60)]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    public sealed class MovingPlatform : MonoBehaviour, IRidePlatform
    {
        [Tooltip("Child Transform waypoints (empty GameObjects) the platform travels between. " +
                 "Order determines the path. Must be direct children of this GameObject.")]
        [SerializeField] private List<Transform> waypoints = new();

        [Tooltip("Movement speed in metres per second. Use a constant or wire a FloatVariable asset.")]
        [SerializeField] private FloatReference speed = new FloatReference(3f);

        [Tooltip("If on, the platform EASES: it decelerates as it nears each waypoint and accelerates back " +
                 "up to Speed after leaving it (smooth stop-and-go). Off = constant Speed the whole path.")]
        [SerializeField] private bool pathSmoothing = false;

        [Tooltip("Only used when Path Smoothing is on. The accel/decel rate in m/s^2 — HIGHER = sharper, the " +
                 "platform holds full Speed closer to the waypoint then brakes hard; LOWER = gentler, it eases " +
                 "over a longer distance. Tune alongside Speed (the ramp length grows with Speed^2 / this).")]
        [SerializeField] private FloatReference decelerationPower = new FloatReference(8f);

        [Tooltip("Cycle: travels back to the first waypoint after the last. PingPong: reverses at each end. " +
                 "Reuse Loop Teleport: on reaching the last waypoint, INSTANTLY warps back to the first and " +
                 "continues — a one-way conveyor that can vanish into an inaccessible area and reappear at the start.")]
        [SerializeField] private PathMode pathMode = PathMode.PingPong;

        [Tooltip("Waypoint index the platform STARTS on (0 = the first). It then proceeds through the list " +
                 "from there in the normal order, wrapping per Path Mode — so with 10 waypoints you can start " +
                 "at 8 and let it run 8->9 then wrap around. Clamped to the waypoint count.")]
        [SerializeField] private int startIndex = 0;

        [Tooltip("Waypoint index that, when the platform ARRIVES at it, ENABLES the components in 'Enable " +
                 "Components'. Set to -1 to disable this feature. Fires on every arrival (so PingPong/Cycle " +
                 "re-enable on each pass). Initial placement does NOT count as an arrival.")]
        [SerializeField] private int enableAtIndex = -1;

        [Tooltip("Components to ENABLE when the platform reaches Enable At Index. SELF-CONTAINMENT: these must " +
                 "be components on THIS prefab's own hierarchy (children of this platform) — never another " +
                 "scene object. Works on scripts/Behaviours, Colliders and Renderers.")]
        [SerializeField] private List<Component> enableComponents = new();

        [Tooltip("Waypoint index that, when the platform ARRIVES at it, DISABLES the components in 'Disable " +
                 "Components'. Set to -1 to disable this feature. Fires on every arrival.")]
        [SerializeField] private int disableAtIndex = -1;

        [Tooltip("Components to DISABLE when the platform reaches Disable At Index. SELF-CONTAINMENT: these must " +
                 "be components on THIS prefab's own hierarchy (children of this platform) — never another " +
                 "scene object. Works on scripts/Behaviours, Colliders and Renderers.")]
        [SerializeField] private List<Component> disableComponents = new();

        /// <summary>World-space velocity this platform moved at during the last FixedUpdate tick.</summary>
        public Vector3 CurrentVelocity { get; private set; }

        /// <summary>The path position this platform targets this tick. A driver (RockBobber) reads this to
        /// own the XZ travel while supplying its own Y. Updated every FixedUpdate.</summary>
        public Vector3 NextPosition { get; private set; }

        private Rigidbody _rb;
        private Vector3[] _points; // waypoint WORLD positions cached at Start — the waypoints are
                                   // children of this platform, so reading them live makes the target
                                   // move with the platform and it never arrives (carrot on a stick)
        private Vector3 _pathPos;  // path-only position, tracked independently of the Rigidbody so a
                                   // driver's added Y (bob) can't corrupt the path math
        private Vector3 _segmentStart; // world pos of the waypoint the current segment began at — the
                                       // ease-in reference for path smoothing (distance travelled so far)
        private int _targetIndex;
        private int _direction = 1; // +1 or -1 (used by PingPong)
        private bool _externallyDriven; // a sibling owns MovePosition; we only compute the path

        private const float MinSmoothSpeed = 0.1f; // floor so eased motion always closes the last sliver

        /// <summary>Called by a sibling (e.g. RockBobber) that will own the final MovePosition. When true,
        /// this platform computes the path and publishes NextPosition/CurrentVelocity but never moves the
        /// body itself, and drops the phantom vertical from the carry (the driver supplies Y).</summary>
        public void SetExternallyDriven(bool value) => _externallyDriven = value;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.isKinematic = true;
            _rb.useGravity = false;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
        }

        private void Start()
        {
            if (waypoints == null || waypoints.Count < 2)
            {
                Debug.LogWarning($"[MovingPlatform] '{name}' needs at least 2 waypoints.", this);
                enabled = false;
                return;
            }

            // Cache world positions BEFORE the platform moves (moving it drags the child waypoints).
            _points = new Vector3[waypoints.Count];
            for (int i = 0; i < waypoints.Count; i++)
                _points[i] = waypoints[i].position;

            // Start ON the chosen waypoint (clamped) and head to the NEXT one, wrapping per Path Mode.
            // The body ALWAYS spawns at index 'start' — no mode rewrites that.
            int start = Mathf.Clamp(startIndex, 0, _points.Length - 1);
            int last = _points.Length - 1;
            _direction = 1;

            if (pathMode == PathMode.PingPong)
            {
                // Forward, except on the last waypoint where only the downward leg exists.
                if (start >= last) { _direction = -1; _targetIndex = start - 1; }
                else _targetIndex = start + 1;
            }
            else if (pathMode == PathMode.ReuseLoopTeleport)
            {
                // Forward to start+1; from the last waypoint the "next" step IS the warp home (index 0),
                // so target the last index itself and let arrival fire the inline warp on the first tick.
                _targetIndex = start < last ? start + 1 : last;
            }
            else // Cycle: forward; the last waypoint wraps straight back to 0 as a path segment.
            {
                _targetIndex = (start + 1) % _points.Length;
            }

            _rb.position = _points[start];
            _pathPos = _points[start];
            NextPosition = _points[start];
            _segmentStart = _points[start];
        }

        private void FixedUpdate()
        {
            if (_points == null || _points.Length < 2) return;

            // Track the path on its OWN position (not _rb.position) so a driver's added Y can't feed back
            // into the path math.
            Vector3 current = _pathPos;
            Vector3 target = _points[_targetIndex];
            float moveSpeed = pathSmoothing ? SmoothedSpeed(current, target) : speed.Value;
            float maxDist = moveSpeed * Time.fixedDeltaTime;
            Vector3 next = Vector3.MoveTowards(current, target, maxDist);
            bool reachedTarget = Vector3.Distance(next, target) < 0.01f;

            // Reuse Loop (Teleport): the instant we reach the LAST waypoint, snap home to the first and keep
            // going — for a platform that travels into an inaccessible area then warps back to be reused.
            if (reachedTarget && pathMode == PathMode.ReuseLoopTeleport && _targetIndex == _points.Length - 1)
            {
                next = _points[0];
                _pathPos = next;
                NextPosition = next;
                CurrentVelocity = Vector3.zero; // NEVER impart the warp jump as ride velocity (don't fling a rider)
                _rb.position = next;            // hard teleport (direct, not MovePosition). A sibling RockBobber
                                               // detects the XZ jump and snaps too, so the bob doesn't smear.
                ApplyArrival(_points.Length - 1);
                _targetIndex = 1;
                _direction = 1;
                _segmentStart = next; // new segment begins at index 0 (ease-in reference after the warp)
                return;
            }

            Vector3 vel = (next - current) / Time.fixedDeltaTime;
            // When a driver owns Y (bob), don't hand the rider a phantom vertical from the path.
            CurrentVelocity = _externallyDriven ? new Vector3(vel.x, 0f, vel.z) : vel;
            _pathPos = next;
            NextPosition = next;

            if (!_externallyDriven) _rb.MovePosition(next);

            if (reachedTarget)
            {
                _segmentStart = next; // the just-reached waypoint starts the next segment (ease-in reference)
                ApplyArrival(_targetIndex);
                AdvanceTarget();
            }
        }

        /// <summary>Ease-in/ease-out speed for path smoothing. Ramps DOWN as it nears the target waypoint and
        /// UP as it leaves the previous one, via the kinematic v = sqrt(2 * a * d) so 'decelerationPower' is
        /// the accel/decel rate (m/s^2). Whichever end is closer governs the speed; floored at MinSmoothSpeed
        /// so the platform always closes the final sliver instead of crawling asymptotically, and capped at
        /// the configured Speed.</summary>
        private float SmoothedSpeed(Vector3 current, Vector3 target)
        {
            float a = Mathf.Max(0.01f, decelerationPower.Value);
            float distToTarget = Vector3.Distance(current, target);
            float distFromStart = Vector3.Distance(current, _segmentStart);
            float ease = Mathf.Sqrt(2f * a * Mathf.Min(distToTarget, distFromStart));
            return Mathf.Clamp(ease, MinSmoothSpeed, speed.Value);
        }

        /// <summary>Fires the enable/disable component toggles when the platform arrives at a waypoint index.</summary>
        private void ApplyArrival(int index)
        {
            if (index == enableAtIndex)
                for (int i = 0; i < enableComponents.Count; i++)
                    SetEnabled(enableComponents[i], true);

            if (index == disableAtIndex)
                for (int i = 0; i < disableComponents.Count; i++)
                    SetEnabled(disableComponents[i], false);
        }

        /// <summary>Toggles a component's enabled state across the three Unity types that expose one.</summary>
        private static void SetEnabled(Component component, bool value)
        {
            switch (component)
            {
                case null: return;
                case Behaviour b: b.enabled = value; break;
                case Collider c: c.enabled = value; break;
                case Renderer r: r.enabled = value; break;
                default:
                    Debug.LogWarning($"[MovingPlatform] Component '{component.GetType().Name}' has no enabled flag to toggle.", component);
                    break;
            }
        }

        private void AdvanceTarget()
        {
            if (pathMode == PathMode.Cycle)
            {
                _targetIndex = (_targetIndex + 1) % _points.Length;
            }
            else if (pathMode == PathMode.ReuseLoopTeleport)
            {
                _targetIndex++; // the last-waypoint warp is handled inline in FixedUpdate before this runs
            }
            else // PingPong
            {
                _targetIndex += _direction;
                if (_targetIndex >= _points.Length)
                {
                    _direction = -1;
                    _targetIndex = _points.Length - 2;
                }
                else if (_targetIndex < 0)
                {
                    _direction = 1;
                    _targetIndex = 1;
                }
            }
        }

        // IRidePlatform — grounded rider adds this each tick; jump inherits it as a snapshot.
        public Vector3 SampleRideVelocity(Vector3 riderPosition) => CurrentVelocity;
    }
}

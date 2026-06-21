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

        [Tooltip("Cycle: travels back to the first waypoint after the last. PingPong: reverses at each end. " +
                 "Reuse Loop Teleport: on reaching the last waypoint, INSTANTLY warps back to the first and " +
                 "continues — a one-way conveyor that can vanish into an inaccessible area and reappear at the start.")]
        [SerializeField] private PathMode pathMode = PathMode.PingPong;

        [Tooltip("Waypoint index the platform STARTS on (0 = the first). It then proceeds through the list " +
                 "from there in the normal order, wrapping per Path Mode — so with 10 waypoints you can start " +
                 "at 8 and let it run 8->9 then wrap around. Clamped to the waypoint count.")]
        [SerializeField] private int startIndex = 0;

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
        private int _targetIndex;
        private int _direction = 1; // +1 or -1 (used by PingPong)
        private bool _externallyDriven; // a sibling owns MovePosition; we only compute the path

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

            // Start on the chosen waypoint (clamped), then head to the next one per Path Mode.
            int start = Mathf.Clamp(startIndex, 0, _points.Length - 1);
            _direction = 1;

            if (pathMode == PathMode.PingPong && start >= _points.Length - 1)
            {
                _direction = -1;            // starting on the last waypoint: ping-pong can only head back down
                _targetIndex = start - 1;
            }
            else if (pathMode == PathMode.ReuseLoopTeleport && start >= _points.Length - 1)
            {
                start = 0;                  // starting on the last would warp home immediately — just begin first
                _targetIndex = 1;
            }
            else if (pathMode == PathMode.Cycle)
            {
                _targetIndex = (start + 1) % _points.Length;
            }
            else
            {
                _targetIndex = start + 1;   // PingPong/Teleport from a non-last index: head forward
            }

            _rb.position = _points[start];
            _pathPos = _points[start];
            NextPosition = _points[start];
        }

        private void FixedUpdate()
        {
            if (_points == null || _points.Length < 2) return;

            // Track the path on its OWN position (not _rb.position) so a driver's added Y can't feed back
            // into the path math.
            Vector3 current = _pathPos;
            Vector3 target = _points[_targetIndex];
            float maxDist = speed.Value * Time.fixedDeltaTime;
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
                _targetIndex = 1;
                _direction = 1;
                return;
            }

            Vector3 vel = (next - current) / Time.fixedDeltaTime;
            // When a driver owns Y (bob), don't hand the rider a phantom vertical from the path.
            CurrentVelocity = _externallyDriven ? new Vector3(vel.x, 0f, vel.z) : vel;
            _pathPos = next;
            NextPosition = next;

            if (!_externallyDriven) _rb.MovePosition(next);

            if (reachedTarget)
                AdvanceTarget();
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

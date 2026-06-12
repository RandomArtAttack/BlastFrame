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
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    public sealed class MovingPlatform : MonoBehaviour, IRidePlatform
    {
        [Tooltip("Child Transform waypoints (empty GameObjects) the platform travels between. " +
                 "Order determines the path. Must be direct children of this GameObject.")]
        [SerializeField] private List<Transform> waypoints = new();

        [Tooltip("Movement speed in metres per second. Use a constant or wire a FloatVariable asset.")]
        [SerializeField] private FloatReference speed = new FloatReference(3f);

        [Tooltip("Cycle: loops back to the first waypoint after the last. " +
                 "PingPong: reverses direction at each end.")]
        [SerializeField] private PathMode pathMode = PathMode.PingPong;

        /// <summary>World-space velocity this platform moved at during the last FixedUpdate tick.</summary>
        public Vector3 CurrentVelocity { get; private set; }

        private Rigidbody _rb;
        private int _targetIndex;
        private int _direction = 1; // +1 or -1 (used by PingPong)

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

            // Start at waypoint 0.
            _rb.MovePosition(waypoints[0].position);
            _targetIndex = 1;
            _direction = 1;
        }

        private void FixedUpdate()
        {
            if (waypoints == null || waypoints.Count < 2) return;

            Vector3 current = _rb.position;
            Vector3 target = waypoints[_targetIndex].position;
            float maxDist = speed.Value * Time.fixedDeltaTime;
            Vector3 next = Vector3.MoveTowards(current, target, maxDist);

            CurrentVelocity = (next - current) / Time.fixedDeltaTime;
            _rb.MovePosition(next);

            if (Vector3.Distance(next, target) < 0.01f)
                AdvanceTarget();
        }

        private void AdvanceTarget()
        {
            if (pathMode == PathMode.Cycle)
            {
                _targetIndex = (_targetIndex + 1) % waypoints.Count;
            }
            else // PingPong
            {
                _targetIndex += _direction;
                if (_targetIndex >= waypoints.Count)
                {
                    _direction = -1;
                    _targetIndex = waypoints.Count - 2;
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

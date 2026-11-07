using UnityEngine;
using UnityEngine.AI;

namespace BlastFrame.Gameplay.Enemies
{
    /// <summary>
    /// Thin wrapper owning a NavMeshAgent for ground enemies. Behaviors steer through this instead of
    /// touching the agent directly: SetDestination with repath throttling, base speed pulled from
    /// EnemyStats.MoveSpeed (× an optional multiplier for dashes/lunges), Stop/Resume, and HasArrived.
    /// The agent drives the transform, so any sibling Rigidbody is forced kinematic with no
    /// interpolation (interpolation smooths MovePosition, not the agent's direct transform writes).
    /// Behaviors handle their own facing, so agent rotation is disabled.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class EnemyNavMotor : MonoBehaviour
    {
        [Tooltip("Minimum seconds between SetDestination repaths (throttle so behaviors can call it every " +
                 "frame without thrashing the pathfinder).")]
        [SerializeField] private float repathInterval = 0.25f;

        [Tooltip("Extra distance (m) added to the agent's stopping distance when deciding HasArrived.")]
        [SerializeField] private float arriveTolerance = 0.25f;

        private NavMeshAgent _agent;
        private EnemyStats _stats;
        private float _repathTimer;
        private Vector3 _pendingDest;
        private bool _hasPending;
        private float _speedMultiplier = 1f;

        public NavMeshAgent Agent => _agent;
        public bool IsOnNavMesh => _agent != null && _agent.isOnNavMesh;
        public Vector3 Velocity => _agent != null ? _agent.velocity : Vector3.zero;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _stats = GetComponent<EnemyStats>();

            if (TryGetComponent<Rigidbody>(out var rb))
            {
                rb.isKinematic = true;                       // the agent owns the transform
                rb.interpolation = RigidbodyInterpolation.None;
                rb.useGravity = false;
            }

            _agent.updateRotation = false;                   // behaviors face the player themselves
            _agent.updateUpAxis = true;
            ApplyBaseSpeed();
        }

        /// <summary>Re-reads EnemyStats.MoveSpeed × the current multiplier onto the agent.</summary>
        public void ApplyBaseSpeed()
        {
            if (_agent != null && _stats != null)
                _agent.speed = _stats.MoveSpeed * _speedMultiplier;
        }

        /// <summary>Temporary speed scaling (dash / lunge). 1 = normal. Re-applies immediately.</summary>
        public void SetSpeedMultiplier(float multiplier)
        {
            _speedMultiplier = Mathf.Max(0f, multiplier);
            ApplyBaseSpeed();
        }

        /// <summary>Queue a destination. Applied at most once per repathInterval.</summary>
        public void SetDestination(Vector3 destination)
        {
            _pendingDest = destination;
            _hasPending = true;
            if (_repathTimer <= 0f) FlushPending();
        }

        public void Stop()
        {
            if (_agent != null && _agent.isOnNavMesh) _agent.isStopped = true;
        }

        public void Resume()
        {
            if (_agent != null && _agent.isOnNavMesh) _agent.isStopped = false;
        }

        public bool HasArrived
        {
            get
            {
                if (_agent == null || !_agent.isOnNavMesh || _agent.pathPending) return false;
                return _agent.remainingDistance <= _agent.stoppingDistance + arriveTolerance;
            }
        }

        private void Update()
        {
            if (_repathTimer > 0f) _repathTimer -= Time.deltaTime;
            if (_hasPending && _repathTimer <= 0f) FlushPending();
        }

        private void FlushPending()
        {
            if (_agent == null || !_agent.isOnNavMesh) return;
            _agent.SetDestination(_pendingDest);
            _hasPending = false;
            _repathTimer = repathInterval;
        }
    }
}

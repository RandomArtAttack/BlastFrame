using UnityEngine;
using BlastFrame.Core.Pooling;
using BlastFrame.Core.Services;
using BlastFrame.Core.Variables;

namespace BlastFrame.Gameplay.Projectiles
{
    /// <summary>
    /// Pooled enemy missile. Starts slow and accelerates along its initial direction until it
    /// reaches maxSpeed, then maintains that speed until it expires or hits something.
    /// On trigger collision with the player's IDamageable, deals damage and despawns.
    /// PlayerShooter calls Initialize(damage, direction) immediately after Spawn().
    /// Requires a kinematic Rigidbody and a trigger Collider on the same GameObject.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class EnemyMissile : MonoBehaviour, IPoolable
    {
        [Tooltip("Speed at the moment of launch, in m/s.")]
        [SerializeField] private FloatReference startSpeed = new FloatReference(4f);

        [Tooltip("Rate at which the missile accelerates, in m/s².")]
        [SerializeField] private FloatReference acceleration = new FloatReference(14f);

        [Tooltip("Maximum speed the missile can reach, in m/s.")]
        [SerializeField] private FloatReference maxSpeed = new FloatReference(28f);

        [Tooltip("Seconds before the missile despawns if it hits nothing.")]
        [SerializeField] private FloatReference lifetime = new FloatReference(6f);

        private IPoolManager _pool;
        private int _damage;
        private Vector3 _direction;
        private float _currentSpeed;
        private float _remainingLifetime;
        private bool _despawned;

        // ----- MonoBehaviour lifecycle ---------------------------------------------------------

        private void Start()
        {
            _pool = ServiceLocator.Get<IPoolManager>();
        }

        private void FixedUpdate()
        {
            if (_despawned) return;

            // Accelerate up to maxSpeed.
            _currentSpeed = Mathf.MoveTowards(_currentSpeed, maxSpeed.Value,
                acceleration.Value * Time.fixedDeltaTime);

            // Translate in the initial direction (kinematic — no physics forces).
            transform.position += _direction * (_currentSpeed * Time.fixedDeltaTime);

            // Lifetime countdown.
            _remainingLifetime -= Time.fixedDeltaTime;
            if (_remainingLifetime <= 0f) Despawn();
        }

        // ----- IPoolable -----------------------------------------------------------------------

        public void OnSpawn()
        {
            _despawned = false;
            _currentSpeed = startSpeed.Value;
            _remainingLifetime = lifetime.Value;
        }

        public void OnDespawn()
        {
            _direction = Vector3.zero;
            _currentSpeed = 0f;
        }

        // ----- Public API ----------------------------------------------------------------------

        /// <summary>
        /// Called by the turret behavior immediately after the pool returns this object.
        /// Sets the damage and travel direction (normalized by this method).
        /// </summary>
        public void Initialize(int damage, Vector3 direction)
        {
            _damage = damage;
            _direction = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.forward;
            // Align the visual to the travel direction.
            if (_direction.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(_direction, Vector3.up);
        }

        // ----- Collision -----------------------------------------------------------------------

        private void OnTriggerEnter(Collider other)
        {
            if (_despawned) return;

            // Ignore sibling missiles — avoid friendly-fire between simultaneous spawns.
            if (other.TryGetComponent<EnemyMissile>(out _)) return;

            // Walk up the hierarchy for an IDamageable (the player's capsule is on the root).
            var damageable = other.GetComponentInParent<IDamageable>();
            damageable?.TakeDamage(_damage);

            Despawn();
        }

        // ----- Private -------------------------------------------------------------------------

        private void Despawn()
        {
            if (_despawned) return;
            _despawned = true;
            _pool?.Despawn(gameObject);
        }
    }
}

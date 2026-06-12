using UnityEngine;
using BlastFrame.Core.Pooling;
using BlastFrame.Core.Services;

namespace BlastFrame.Gameplay.Projectiles
{
    /// <summary>
    /// Pooled ballistic projectile that follows a physics arc under gravity. The launching behavior
    /// supplies a pre-solved launchVelocity (via Initialize) that lands the projectile on the
    /// desired target ground point. On impact with the player's IDamageable, deals damage then
    /// despawns. On impact with any other solid surface, despawns without dealing damage.
    /// Requires a kinematic Rigidbody + trigger Collider on the same GameObject.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class ArcProjectile : MonoBehaviour, IPoolable
    {
        [Tooltip("Seconds before the projectile despawns if it hits nothing.")]
        [SerializeField] private float lifetime = 8f;

        private IPoolManager _pool;
        private int _damage;
        private Vector3 _velocity;
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

            // Apply gravity each tick (kinematic — we integrate manually).
            _velocity += Physics.gravity * Time.fixedDeltaTime;

            // Move by current velocity.
            transform.position += _velocity * Time.fixedDeltaTime;

            // Orient the projectile along the current velocity direction for a natural arc look.
            if (_velocity.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.LookRotation(_velocity.normalized, Vector3.up);

            // Lifetime countdown.
            _remainingLifetime -= Time.fixedDeltaTime;
            if (_remainingLifetime <= 0f) Despawn();
        }

        // ----- IPoolable -----------------------------------------------------------------------

        public void OnSpawn()
        {
            _despawned = false;
            _remainingLifetime = lifetime;
        }

        public void OnDespawn()
        {
            _velocity = Vector3.zero;
        }

        // ----- Public API ----------------------------------------------------------------------

        /// <summary>
        /// Called by EnemyBehaviorArcPredict immediately after the pool returns this object.
        /// Sets the damage and the pre-solved launch velocity (direction + magnitude).
        /// </summary>
        public void Initialize(int damage, Vector3 launchVelocity)
        {
            _damage = damage;
            _velocity = launchVelocity;

            if (launchVelocity.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(launchVelocity.normalized, Vector3.up);
        }

        // ----- Collision -----------------------------------------------------------------------

        private void OnTriggerEnter(Collider other)
        {
            if (_despawned) return;

            // Ignore other arc projectiles — no friendly-fire between simultaneous volleys.
            if (other.TryGetComponent<ArcProjectile>(out _)) return;

            // Walk up the hierarchy for an IDamageable (player capsule is on the root).
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

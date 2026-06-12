using UnityEngine;
using BlastFrame.Core;
using BlastFrame.Core.Pooling;
using BlastFrame.Core.Services;

namespace BlastFrame.Gameplay.Projectiles
{
    /// <summary>
    /// Pooled ballistic projectile that follows a physics arc under (optionally scaled) gravity.
    /// The launching behavior supplies a pre-solved launchVelocity (via Initialize) that lands the
    /// projectile on the desired target ground point. On impact with ANY solid surface it spawns a
    /// pooled ArcExplosion at the impact point — damage comes from the explosion's blast radius
    /// (tuned on the ArcExplosion prefab), not from direct contact.
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
        private float _gravityScale = 1f;
        private float _remainingLifetime;
        private float _collisionGraceUntil;
        private bool _despawned;

        // ----- MonoBehaviour lifecycle ---------------------------------------------------------

        private void Start()
        {
            _pool = ServiceLocator.Get<IPoolManager>();
        }

        private void FixedUpdate()
        {
            if (_despawned) return;

            // Apply (scaled) gravity each tick (kinematic — we integrate manually).
            _velocity += Physics.gravity * (_gravityScale * Time.fixedDeltaTime);

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
            _collisionGraceUntil = Time.time + 0.15f;
        }

        public void OnDespawn()
        {
            _velocity = Vector3.zero;
        }

        // ----- Public API ----------------------------------------------------------------------

        /// <summary>
        /// Called by EnemyBehaviorArcPredict immediately after the pool returns this object.
        /// Sets damage, the pre-solved launch velocity, and the gravity multiplier (must match the
        /// gravity the launcher's ballistic solver assumed, or the shot will miss).
        /// </summary>
        public void Initialize(int damage, Vector3 launchVelocity, float gravityScale = 1f)
        {
            _damage = damage;
            _velocity = launchVelocity;
            _gravityScale = gravityScale;

            if (launchVelocity.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(launchVelocity.normalized, Vector3.up);
        }

        // ----- Collision -----------------------------------------------------------------------

        private void OnTriggerEnter(Collider other)
        {
            if (_despawned) return;
            if (Time.time < _collisionGraceUntil) return;

            // Ignore other arc projectiles — no friendly-fire between simultaneous volleys.
            if (other.TryGetComponent<ArcProjectile>(out _)) return;

            // Detonate: the pooled explosion applies blast damage to everything in its radius
            // (radius + damage live on the ArcExplosion prefab) — no direct contact damage here.
            _pool?.Spawn(PoolIds.ArcExplosion, transform.position, Quaternion.identity);

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

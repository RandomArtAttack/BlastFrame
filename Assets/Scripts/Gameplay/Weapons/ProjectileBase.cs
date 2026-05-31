using UnityEngine;
using BlastFrame.Core;
using BlastFrame.Core.Pooling;
using BlastFrame.Core.Services;
using BlastFrame.Core.Variables;

namespace BlastFrame.Gameplay.Weapons
{
    /// <summary>
    /// Abstract base for pooled projectiles. Provides:
    ///   - Forward movement in FixedUpdate (speed set by subclass via SetSpeed).
    ///   - Lifetime countdown — despawns itself when it expires.
    ///   - IPoolable wiring (OnSpawn restarts lifetime, OnDespawn clears velocity).
    /// Subclasses call SetSpeed() from Initialize() and override HandleHit() to apply damage /
    /// spawn effects before the base auto-despawns.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public abstract class ProjectileBase : MonoBehaviour, IPoolable
    {
        [Tooltip("How many seconds the projectile travels before despawning if it hits nothing.")]
        [SerializeField] private FloatReference lifetime = new FloatReference(4f);

        protected IPoolManager Pool { get; private set; }

        private float _speed;
        private float _remainingLifetime;
        private bool _despawned;

        // ----- IPoolable -----------------------------------------------------------------------

        public virtual void OnSpawn()
        {
            _remainingLifetime = lifetime.Value;
            _despawned = false;
        }

        public virtual void OnDespawn()
        {
            _speed = 0f;
        }

        // ----- MonoBehaviour lifecycle ---------------------------------------------------------

        private void Start()
        {
            Pool = ServiceLocator.Get<IPoolManager>();
        }

        private void FixedUpdate()
        {
            if (_despawned) return;

            // Move forward.
            transform.Translate(Vector3.forward * (_speed * Time.fixedDeltaTime), Space.Self);

            // Lifetime countdown.
            _remainingLifetime -= Time.fixedDeltaTime;
            if (_remainingLifetime <= 0f) Despawn();
        }

        // ----- Protected API for subclasses ----------------------------------------------------

        /// <summary>Called from Initialize() to set the forward speed for this shot.</summary>
        protected void SetSpeed(float speed) => _speed = speed;

        /// <summary>Despawn this projectile. Safe to call multiple times (idempotent).</summary>
        protected void Despawn()
        {
            if (_despawned) return;
            _despawned = true;
            Pool?.Despawn(gameObject);
        }
    }
}

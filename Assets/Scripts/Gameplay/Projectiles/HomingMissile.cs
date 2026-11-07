using UnityEngine;
using BlastFrame.Core;
using BlastFrame.Core.Entities;
using BlastFrame.Core.Pooling;
using BlastFrame.Core.Services;
using BlastFrame.Core.Variables;

namespace BlastFrame.Gameplay.Projectiles
{
    /// <summary>
    /// Pooled homing missile fired by the Missiler. Launches along a supplied direction (typically
    /// upward, in a spread), flies straight for homingDelay, then curves toward the player with a
    /// clamped turn rate while accelerating up to cruise speed. Detonates (pooled ArcExplosion — the
    /// enemy blast that damages the player) on reaching the player, hitting solid geometry, or on
    /// lifetime end.
    ///
    /// Shootable: implements IDamageable with 1 HP. A PlayerProjectile hit calls TakeDamage → the
    /// missile pops harmlessly (pooled player Explosion, whose damage mask excludes the Player) and
    /// despawns. Sits on the EnemyWeapon layer: player projectiles can hit it (PlayerWeapon × EnemyWeapon
    /// is on) yet it passes through enemies (EnemyWeapon × Enemy is off). Kinematic Rigidbody + trigger
    /// collider. Resets all homing/velocity state on OnSpawn.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class HomingMissile : MonoBehaviour, IPoolable, IDamageable
    {
        [Tooltip("Registry used to find the player to home toward.")]
        [SerializeField] private EntityRegistrySO entityRegistry;

        [Tooltip("Cruise speed (m/s) the missile accelerates up to.")]
        [SerializeField] private FloatReference cruiseSpeed = new FloatReference(16f);

        [Tooltip("Speed (m/s) at launch, before it accelerates to cruise.")]
        [SerializeField] private FloatReference launchSpeed = new FloatReference(6f);

        [Tooltip("Acceleration (m/s^2) toward cruise speed.")]
        [SerializeField] private FloatReference acceleration = new FloatReference(18f);

        [Tooltip("Maximum turn rate (deg/sec) while homing. Lower = wider, more dodgeable arcs.")]
        [SerializeField] private FloatReference turnDegPerSec = new FloatReference(140f);

        [Tooltip("Seconds after launch it flies straight (up-and-out) before it starts homing.")]
        [SerializeField] private FloatReference homingDelay = new FloatReference(0.45f);

        [Tooltip("Distance (m) to the player at which it detonates.")]
        [SerializeField] private FloatReference detonateRange = new FloatReference(1.3f);

        [Tooltip("Seconds before it self-detonates if it never reaches the player.")]
        [SerializeField] private FloatReference lifetime = new FloatReference(7f);

        private const float CollisionGrace = 0.1f;
        private const float PlayerAimHeight = 0.8f;

        private IPoolManager _pool;
        private Vector3 _dir;
        private float _currentSpeed;
        private float _remainingLife;
        private float _homeTimer;
        private float _graceUntil;
        private bool _dead;

        public int CurrentHealth { get; private set; }
        public bool IsDead => _dead || CurrentHealth <= 0;

        // ----- MonoBehaviour lifecycle ---------------------------------------------------------

        private void Start()
        {
            _pool = ServiceLocator.Get<IPoolManager>();
        }

        private void FixedUpdate()
        {
            if (_dead) return;
            float dt = Time.fixedDeltaTime;

            _homeTimer -= dt;
            if (_homeTimer <= 0f && entityRegistry != null && entityRegistry.HasPlayer)
            {
                var target = entityRegistry.PlayerTransform.position + Vector3.up * PlayerAimHeight;
                var toTarget = target - transform.position;
                if (toTarget.sqrMagnitude <= detonateRange.Value * detonateRange.Value) { Detonate(); return; }
                if (toTarget.sqrMagnitude > 0.0001f)
                {
                    var maxRad = turnDegPerSec.Value * Mathf.Deg2Rad * dt;
                    _dir = Vector3.RotateTowards(_dir, toTarget.normalized, maxRad, 0f).normalized;
                }
            }

            _currentSpeed = Mathf.MoveTowards(_currentSpeed, cruiseSpeed.Value, acceleration.Value * dt);
            transform.position += _dir * (_currentSpeed * dt);
            if (_dir.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(_dir, Vector3.up);

            _remainingLife -= dt;
            if (_remainingLife <= 0f) Detonate();
        }

        // ----- IPoolable -----------------------------------------------------------------------

        public void OnSpawn()
        {
            _dead = false;
            CurrentHealth = 1;
            _currentSpeed = launchSpeed.Value;
            _remainingLife = lifetime.Value;
            _homeTimer = homingDelay.Value;
            _graceUntil = Time.time + CollisionGrace;
            _dir = transform.forward;
        }

        public void OnDespawn()
        {
            _dir = Vector3.zero;
            _currentSpeed = 0f;
            _dead = true;
        }

        // ----- Public API ----------------------------------------------------------------------

        /// <summary>Called by the launcher immediately after Spawn(): initial launch direction. Blast
        /// damage comes from the pooled ArcExplosion spawned on detonation, not from this projectile.</summary>
        public void Initialize(Vector3 launchDir)
        {
            _dir = launchDir.sqrMagnitude > 0.0001f ? launchDir.normalized : transform.forward;
            transform.rotation = Quaternion.LookRotation(_dir, Vector3.up);
        }

        // ----- IDamageable (shot down by the player) -------------------------------------------

        public void TakeDamage(int amount)
        {
            if (_dead || amount <= 0) return;
            CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
            if (CurrentHealth == 0) PopHarmless();
        }

        // ----- Collision -----------------------------------------------------------------------

        private void OnTriggerEnter(Collider other)
        {
            if (_dead || Time.time < _graceUntil) return;

            // Non-damageable trigger volumes (hazard zones, secret triggers) are not solid.
            if (other.isTrigger && other.GetComponentInParent<IDamageable>() == null) return;

            // A player projectile handles the shoot-down via TakeDamage on us — don't double-react.
            if (other.GetComponentInParent<Weapons.ProjectileBase>() != null) return;

            // Ignore sibling missiles.
            if (other.TryGetComponent<HomingMissile>(out _)) return;

            Detonate();
        }

        // ----- Private -------------------------------------------------------------------------

        private void PopHarmless()
        {
            if (_dead) return;
            if (_pool == null) ServiceLocator.TryGet(out _pool);
            // HomingPop is a dedicated damage-0 VFX: harmless to the player AND to enemies. (Reusing the
            // player Explosion here would splash the Enemy layer; reusing ArcExplosion would splash the
            // player — so neither existing blast is a safe "shot-down" pop.)
            _pool?.Spawn(PoolIds.HomingPop, transform.position, Quaternion.identity);
            Despawn();
        }

        private void Detonate()
        {
            if (_dead) return;
            if (_pool == null) ServiceLocator.TryGet(out _pool);
            // ArcExplosion is the enemy blast (mask excludes Enemy) — it damages the player.
            _pool?.Spawn(PoolIds.ArcExplosion, transform.position, Quaternion.identity);
            Despawn();
        }

        private void Despawn()
        {
            if (_dead) return;
            _dead = true;
            if (_pool == null) ServiceLocator.TryGet(out _pool);
            _pool?.Despawn(gameObject);
        }
    }
}

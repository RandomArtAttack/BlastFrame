using UnityEngine;
using BlastFrame.Core;
using BlastFrame.Gameplay.Weapons;

namespace BlastFrame.Gameplay.Projectiles
{
    /// <summary>
    /// Pooled player projectile. Moves forward kinematically (trigger collider + kinematic
    /// Rigidbody). On trigger enter: damages the first IDamageable found on the collider or its
    /// ancestors, spawns an AoE explosion from the pool if this was a charged shot, then despawns.
    /// PlayerShooter calls Initialize() immediately after Spawn().
    /// </summary>
    public class PlayerProjectile : ProjectileBase
    {
        private int _damage;
        private bool _aoe;
        private bool _hit;

        // ----- IPoolable -----------------------------------------------------------------------

        public override void OnSpawn()
        {
            base.OnSpawn();
            _hit = false;
        }

        public override void OnDespawn()
        {
            base.OnDespawn();
            _hit = false;
        }

        // ----- Public API ----------------------------------------------------------------------

        /// <summary>
        /// Called by PlayerShooter immediately after the pool returns this object.
        /// Sets damage, visual scale, AoE flag, and forward speed.
        /// </summary>
        public void Initialize(int damage, float size, bool aoe, float speed)
        {
            _damage = damage;
            _aoe = aoe;
            transform.localScale = Vector3.one * size;
            SetSpeed(speed);
        }

        // ----- Collision -----------------------------------------------------------------------

        private void OnTriggerEnter(Collider other)
        {
            if (_hit) return;

            // Skip other player projectiles.
            if (other.TryGetComponent<PlayerProjectile>(out _)) return;

            _hit = true;

            // Damage the first IDamageable on the collider or any of its parents.
            var damageable = other.GetComponentInParent<IDamageable>();
            damageable?.TakeDamage(_damage);

            // AoE explosion.
            if (_aoe && Pool != null)
            {
                var explosionGo = Pool.Spawn(PoolIds.Explosion, transform.position, Quaternion.identity);
                // AoeExplosion.OnSpawn() handles the radial damage — no further init needed.
            }

            Despawn();
        }
    }
}

using UnityEngine;
using BlastFrame.Core;
using BlastFrame.Core.Services;
using BlastFrame.Core.Variables;
using BlastFrame.Gameplay.Weapons;
using BlastFrame.Gameplay.Projectiles;

namespace BlastFrame.Gameplay.Player
{
    /// <summary>
    /// Sits on the player Camera child. Subscribes to ChargeShot.OnReleased (sibling component).
    /// On release: spawns a pooled PlayerProjectile from the muzzle (this transform), initialises it
    /// with damage / size / AoE flags scaled by the charge value, then returns. No runtime
    /// Instantiate — the pool pre-warmed the projectiles at scene load.
    /// </summary>
    [RequireComponent(typeof(ChargeShot))]
    public class PlayerShooter : MonoBehaviour
    {
        [Tooltip("Base damage dealt by a tap (charge ≈ 0) shot.")]
        [SerializeField] private IntReference baseDamage = new IntReference(1);

        [Tooltip("Extra damage added at full charge (stacks on top of baseDamage). Full charge = baseDamage + chargedDamageBonus.")]
        [SerializeField] private IntReference chargedDamageBonus = new IntReference(4);

        [Tooltip("Charge threshold (0..1) above which the shot triggers an AoE explosion.")]
        [SerializeField] private FloatReference aoeChargeThreshold = new FloatReference(0.5f);

        [Tooltip("Projectile size multiplier at zero charge.")]
        [SerializeField] private FloatReference minProjectileSize = new FloatReference(0.25f);

        [Tooltip("Projectile size multiplier at full charge.")]
        [SerializeField] private FloatReference maxProjectileSize = new FloatReference(1f);

        [Tooltip("Forward speed applied to tap-fire projectiles (m/s). Fully charged shots use chargedProjectileSpeed.")]
        [SerializeField] private FloatReference tapProjectileSpeed = new FloatReference(30f);

        [Tooltip("Forward speed applied to charged projectiles (m/s). Slightly slower for heavier feel.")]
        [SerializeField] private FloatReference chargedProjectileSpeed = new FloatReference(22f);

        private IPoolManager _pool;
        private Transform _muzzle;       // this transform — camera IS the muzzle

        private void Awake()
        {
            _muzzle = transform;
        }

        private void Start()
        {
            _pool = ServiceLocator.Get<IPoolManager>();
            GetComponent<ChargeShot>().OnReleased += HandleReleased;
        }

        private void OnDestroy()
        {
            if (TryGetComponent<ChargeShot>(out var cs))
                cs.OnReleased -= HandleReleased;
        }

        private void HandleReleased(float charge)
        {
            var go = _pool.Spawn(BlastFrame.Core.PoolIds.PlayerProjectile, _muzzle.position, _muzzle.rotation);
            if (go == null) return;

            if (!go.TryGetComponent<PlayerProjectile>(out var proj)) return;

            bool aoe = charge >= aoeChargeThreshold.Value;
            int dmg = baseDamage.Value + Mathf.RoundToInt(chargedDamageBonus.Value * charge);
            float size = Mathf.Lerp(minProjectileSize.Value, maxProjectileSize.Value, charge);
            float speed = Mathf.Lerp(tapProjectileSpeed.Value, chargedProjectileSpeed.Value, charge);

            proj.Initialize(dmg, size, aoe, speed);
        }
    }
}

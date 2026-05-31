using UnityEngine;
using BlastFrame.Core;
using BlastFrame.Core.Pooling;
using BlastFrame.Core.Services;
using BlastFrame.Core.Variables;
using BlastFrame.Gameplay.Projectiles;

namespace BlastFrame.Gameplay.Enemies
{
    /// <summary>
    /// Composable behavior: rotates the turret root toward the player (within Stats.Range) and
    /// fires an EnemyMissile from the muzzle at Stats.FireRate. Requires a sibling EnemyCore +
    /// EnemyStats and a muzzle child Transform. Player is resolved via the EntityRegistry —
    /// never via Find().
    /// </summary>
    public class EnemyBehaviorMissileTurret : EnemyBehaviorBase
    {
        [Tooltip("Child Transform at the barrel tip from which missiles are spawned. Must be a child of this GameObject.")]
        [SerializeField] private Transform muzzle;

        [Tooltip("Degrees per second the turret rotates toward the player.")]
        [SerializeField] private FloatReference rotationSpeed = new FloatReference(120f);

        [Tooltip("Vertical angle offset applied to the launch direction so missiles arc slightly upward. Degrees.")]
        [SerializeField] private FloatReference launchAngleOffset = new FloatReference(5f);

        private IPoolManager _pool;
        private float _fireCooldown;
        private bool _dead;

        // -- Reuse buffer for range check --------------------------------------------------------
        private static readonly Collider[] s_overlapBuffer = new Collider[4];

        protected override void Awake()
        {
            base.Awake();
            Core.OnDeath += OnCoreDeathHandler;
        }

        private void OnDestroy()
        {
            Core.OnDeath -= OnCoreDeathHandler;
        }

        private void Start()
        {
            _pool = ServiceLocator.Get<IPoolManager>();
        }

        private void FixedUpdate()
        {
            if (_dead || !HasPlayer) return;

            var playerPos = Player.position;
            var toPlayer = playerPos - transform.position;

            // Range check — avoid sqrt: compare squared distances.
            var rangeSq = Stats.Range * Stats.Range;
            if (toPlayer.sqrMagnitude > rangeSq) return;

            // Rotate turret toward player (Y-axis only keeps the turret level).
            var targetDir = new Vector3(toPlayer.x, 0f, toPlayer.z);
            if (targetDir.sqrMagnitude > 0.001f)
            {
                var targetRot = Quaternion.LookRotation(targetDir, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, targetRot, rotationSpeed.Value * Time.fixedDeltaTime);
            }

            // Fire cooldown.
            _fireCooldown -= Time.fixedDeltaTime;
            if (_fireCooldown <= 0f)
            {
                Fire();
                _fireCooldown = Stats.FireRate > 0f ? 1f / Stats.FireRate : 1f;
            }
        }

        private void Fire()
        {
            if (_pool == null) return;

            var spawnPos = muzzle != null ? muzzle.position : transform.position;

            // Direction from muzzle toward player with a slight upward offset for feel.
            var toPlayer = Player.position - spawnPos;
            if (toPlayer.sqrMagnitude < 0.001f) return;

            var baseDir = toPlayer.normalized;
            var rightAxis = Vector3.Cross(baseDir, Vector3.up).normalized;
            if (rightAxis.sqrMagnitude < 0.001f) rightAxis = Vector3.right;
            var launchDir = Quaternion.AngleAxis(-launchAngleOffset.Value, rightAxis) * baseDir;

            var spawnRot = Quaternion.LookRotation(launchDir, Vector3.up);

            var go = _pool.Spawn(PoolIds.EnemyMissile, spawnPos, spawnRot);
            if (go == null) return;

            if (go.TryGetComponent<EnemyMissile>(out var missile))
                missile.Initialize(Stats.Damage, launchDir);
        }

        private void OnCoreDeathHandler()
        {
            _dead = true;
        }
    }
}

using UnityEngine;
using BlastFrame.Core;
using BlastFrame.Core.Pooling;
using BlastFrame.Core.Services;
using BlastFrame.Core.Variables;
using BlastFrame.Gameplay.Projectiles;

namespace BlastFrame.Gameplay.Enemies
{
    /// <summary>
    /// Ground unit that holds a preferred distance band (a fraction of Range) from the player using
    /// EnemyNavMotor — advancing when too far, backing off when too close — and lobs a pooled
    /// ArcProjectile on a ballistic arc onto the player's current position. Non-predictive aim by
    /// default. Fires only with line of sight; fire rate from EnemyStats.
    /// </summary>
    [RequireComponent(typeof(EnemyNavMotor))]
    public class EnemyBehaviorLobber : EnemyBehaviorBase
    {
        [Tooltip("Child Transform (barrel tip) arc projectiles spawn from.")]
        [SerializeField] private Transform muzzle;

        [Tooltip("Preferred hold distance as a fraction of Range (the centre of the band).")]
        [SerializeField] private FloatReference preferredRangeFactor = new FloatReference(0.75f);

        [Tooltip("Half-width of the hold band as a fraction of Range. Inside the band the Lobber stops " +
                 "and fires; outside it repositions to the preferred distance.")]
        [SerializeField] private FloatReference rangeBandFactor = new FloatReference(0.15f);

        [Tooltip("Multiplier on Physics.gravity for the lobbed projectile. Must match the ArcProjectile " +
                 "it fires. Lower = floatier, higher arc.")]
        [SerializeField] private FloatReference projectileGravityScale = new FloatReference(1f);

        [Tooltip("Degrees/second the body yaws to face the player.")]
        [SerializeField] private FloatReference faceTurnSpeed = new FloatReference(240f);

        private IPoolManager _pool;
        private EnemyNavMotor _nav;
        private Collider _playerCollider;
        private float _fireCooldown;
        private bool _dead;

        protected override void Awake()
        {
            base.Awake();
            _nav = GetComponent<EnemyNavMotor>();
            Core.OnDeath += OnCoreDeath;
        }

        private void OnDestroy() => Core.OnDeath -= OnCoreDeath;

        private void Start() => _pool = ServiceLocator.Get<IPoolManager>();

        private void Update()
        {
            if (_dead || !HasPlayer) return;

            var pos = transform.position;
            var playerPos = Player.position;
            var toPlayer = playerPos - pos;
            float range = Stats.Range;
            float dt = Time.deltaTime;

            _fireCooldown = Mathf.Max(_fireCooldown - dt, 0f);
            FacePlayer(pos, playerPos, dt);

            float dist = new Vector3(toPlayer.x, 0f, toPlayer.z).magnitude;
            float preferred = range * preferredRangeFactor.Value;
            float band = range * rangeBandFactor.Value;

            // Reposition to the preferred distance along the current bearing, or hold and fire.
            if (Mathf.Abs(dist - preferred) > band)
            {
                var bearing = (pos - playerPos);
                bearing.y = 0f;
                if (bearing.sqrMagnitude < 0.01f) bearing = -transform.forward;
                var desired = playerPos + bearing.normalized * preferred;
                _nav.Resume();
                _nav.SetDestination(desired);
            }
            else
            {
                _nav.Stop();
            }

            if (toPlayer.sqrMagnitude > range * range) return;

            CachePlayerCollider();
            if (_fireCooldown <= 0f &&
                EnemyLineOfSight.HasLineOfSight(transform, muzzle != null ? muzzle.position : pos, Player, _playerCollider))
            {
                Fire(playerPos);
                _fireCooldown = Stats.FireRate > 0f ? 1f / Stats.FireRate : 1f;
            }
        }

        private void FacePlayer(Vector3 pos, Vector3 playerPos, float dt)
        {
            var flat = new Vector3(playerPos.x - pos.x, 0f, playerPos.z - pos.z);
            if (flat.sqrMagnitude < 0.001f) return;
            var target = Quaternion.LookRotation(flat, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, faceTurnSpeed.Value * dt);
        }

        private void Fire(Vector3 targetPos)
        {
            if (_pool == null) return;
            var spawn = muzzle != null ? muzzle.position : transform.position;
            if (!TrySolveArc(spawn, targetPos, Stats.ProjectileSpeed, projectileGravityScale.Value, out var vel))
                return;

            var go = _pool.Spawn(PoolIds.ArcProjectile, spawn, Quaternion.LookRotation(vel.normalized, Vector3.up));
            if (go != null && go.TryGetComponent<ArcProjectile>(out var proj))
                proj.Initialize(Stats.Damage, vel, projectileGravityScale.Value);
        }

        // High-arc ballistic solve for the launch velocity that lands a projectile of the given speed on
        // targetPos under gravity × gravityScale. Falls back to a 45° max-range shot when unreachable.
        private static bool TrySolveArc(Vector3 from, Vector3 to, float speed, float gravityScale, out Vector3 velocity)
        {
            velocity = Vector3.zero;
            if (speed <= 0.001f) return false;

            var flat = new Vector3(to.x - from.x, 0f, to.z - from.z);
            float R = flat.magnitude;
            var horizDir = R > 0.001f ? flat / R : Vector3.forward;
            float g = Mathf.Abs(Physics.gravity.y) * Mathf.Max(gravityScale, 0.01f);
            float dy = to.y - from.y;
            float v2 = speed * speed;

            float disc = v2 * v2 - g * (g * R * R + 2f * dy * v2);
            float angle;
            if (disc < 0f || R < 0.001f)
            {
                angle = 45f * Mathf.Deg2Rad; // unreachable — best-range lob
            }
            else
            {
                float highTan = (v2 + Mathf.Sqrt(disc)) / (g * R);
                angle = Mathf.Max(Mathf.Atan(highTan), 45f * Mathf.Deg2Rad);
            }

            velocity = horizDir * (speed * Mathf.Cos(angle)) + Vector3.up * (speed * Mathf.Sin(angle));
            return true;
        }

        private void CachePlayerCollider()
        {
            if (Player == null) return;
            _playerCollider = PlayerCollider; // base re-resolves when the nearest player changes
        }

        private void OnCoreDeath()
        {
            _dead = true;
            if (_nav != null) _nav.Stop();
        }
    }
}

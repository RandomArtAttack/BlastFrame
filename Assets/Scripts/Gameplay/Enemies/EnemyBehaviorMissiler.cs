using UnityEngine;
using BlastFrame.Core;
using BlastFrame.Core.Pooling;
using BlastFrame.Core.Services;
using BlastFrame.Core.Variables;
using BlastFrame.Gameplay.Projectiles;

namespace BlastFrame.Gameplay.Enemies
{
    /// <summary>
    /// Slow, tanky vehicle. The chassis creeps toward the player via EnemyNavMotor and stops at
    /// stopDistanceFactor × Range. A child Turret transform tracks the player. Two attacks on
    /// independent cooldowns, both from the turret muzzle and gated on line of sight:
    ///   (a) a high-speed explosive shot — a straight, zero-gravity pooled ArcProjectile at
    ///       explosiveSpeedFactor × ProjectileSpeed that explodes on impact (ArcExplosion);
    ///   (b) a volley of pooled HomingMissiles launched upward in a spread that then home.
    /// </summary>
    [RequireComponent(typeof(EnemyNavMotor))]
    public class EnemyBehaviorMissiler : EnemyBehaviorBase
    {
        [Tooltip("Child Transform that rotates to track the player (the turret).")]
        [SerializeField] private Transform turret;

        [Tooltip("Child Transform (under the turret) shots and missiles spawn from.")]
        [SerializeField] private Transform muzzle;

        [Tooltip("Chassis stops advancing at this fraction of Range from the player.")]
        [SerializeField] private FloatReference stopDistanceFactor = new FloatReference(0.7f);

        [Tooltip("Degrees/second the turret rotates to track the player.")]
        [SerializeField] private FloatReference turretRotSpeed = new FloatReference(120f);

        [Tooltip("Seconds between explosive straight shots.")]
        [SerializeField] private FloatReference explosiveCooldown = new FloatReference(2.5f);

        [Tooltip("Explosive shot speed = ProjectileSpeed × this.")]
        [SerializeField] private FloatReference explosiveSpeedFactor = new FloatReference(2f);

        [Tooltip("Seconds between homing volleys.")]
        [SerializeField] private FloatReference volleyCooldown = new FloatReference(5f);

        [Tooltip("Homing missiles per volley.")]
        [SerializeField] private IntReference volleyCount = new IntReference(4);

        [Tooltip("Tilt (degrees from straight up) the volley missiles are fanned out at launch.")]
        [SerializeField] private FloatReference volleySpreadDegrees = new FloatReference(28f);

        [Tooltip("Seconds between individual missiles within one volley.")]
        [SerializeField] private FloatReference volleySpacing = new FloatReference(0.1f);

        private const float PlayerAimHeight = 0.9f;

        private IPoolManager _pool;
        private EnemyNavMotor _nav;
        private Collider _playerCollider;
        private float _explosiveTimer;
        private float _volleyTimer;
        private int _volleyRemaining;
        private float _volleySpaceTimer;
        private bool _dead;

        protected override void Awake()
        {
            base.Awake();
            _nav = GetComponent<EnemyNavMotor>();
            Core.OnDeath += OnCoreDeath;
        }

        private void OnDestroy() => Core.OnDeath -= OnCoreDeath;

        private void Start()
        {
            _pool = ServiceLocator.Get<IPoolManager>();
            _explosiveTimer = explosiveCooldown.Value;
            _volleyTimer = volleyCooldown.Value;
        }

        private void Update()
        {
            if (_dead || !HasPlayer) return;
            float dt = Time.deltaTime;
            var pos = transform.position;
            var playerPos = Player.position;
            float dist = Vector3.Distance(pos, playerPos);
            float range = Stats.Range;

            // --- Chassis advance / hold -----------------------------------------------------------
            float stopDist = range * stopDistanceFactor.Value;
            if (dist > stopDist)
            {
                var bearing = (pos - playerPos); bearing.y = 0f;
                if (bearing.sqrMagnitude < 0.01f) bearing = -transform.forward;
                _nav.Resume();
                _nav.SetDestination(playerPos + bearing.normalized * stopDist);
            }
            else _nav.Stop();

            // --- Turret tracking ------------------------------------------------------------------
            var aimPoint = playerPos + Vector3.up * PlayerAimHeight;
            if (turret != null)
            {
                var dir = aimPoint - turret.position;
                if (dir.sqrMagnitude > 0.0001f)
                {
                    var target = Quaternion.LookRotation(dir.normalized, Vector3.up);
                    turret.rotation = Quaternion.RotateTowards(turret.rotation, target, turretRotSpeed.Value * dt);
                }
            }

            if (dist > range) return;
            CachePlayerCollider();
            var muzzlePos = muzzle != null ? muzzle.position : pos;
            bool los = EnemyLineOfSight.HasLineOfSight(transform, muzzlePos, Player, _playerCollider);

            // --- Attack A: explosive straight shot ------------------------------------------------
            _explosiveTimer = Mathf.Max(_explosiveTimer - dt, 0f);
            if (_explosiveTimer <= 0f && los && _volleyRemaining <= 0)
            {
                FireExplosive(muzzlePos, aimPoint);
                _explosiveTimer = explosiveCooldown.Value;
            }

            // --- Attack B: homing volley ----------------------------------------------------------
            _volleyTimer = Mathf.Max(_volleyTimer - dt, 0f);
            if (_volleyTimer <= 0f && los && _volleyRemaining <= 0)
            {
                _volleyRemaining = Mathf.Max(1, volleyCount.Value);
                _volleySpaceTimer = 0f;
                _volleyTimer = volleyCooldown.Value;
            }
            if (_volleyRemaining > 0)
            {
                _volleySpaceTimer -= dt;
                if (_volleySpaceTimer <= 0f)
                {
                    LaunchOneMissile(muzzlePos);
                    _volleyRemaining--;
                    _volleySpaceTimer = volleySpacing.Value;
                }
            }
        }

        private void FireExplosive(Vector3 muzzlePos, Vector3 aimPoint)
        {
            if (_pool == null) return;
            var dir = aimPoint - muzzlePos;
            if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;
            dir.Normalize();
            var vel = dir * (Stats.ProjectileSpeed * explosiveSpeedFactor.Value);

            var go = _pool.Spawn(PoolIds.ArcProjectile, muzzlePos, Quaternion.LookRotation(dir, Vector3.up));
            if (go != null && go.TryGetComponent<ArcProjectile>(out var proj))
                proj.Initialize(Stats.Damage, vel, 0f); // gravityScale 0 → straight line, explodes on impact
        }

        private void LaunchOneMissile(Vector3 muzzlePos)
        {
            if (_pool == null) return;
            // Fan around a vertical cone: azimuth spread across the volley, tilted volleySpreadDegrees off vertical.
            int idx = Mathf.Max(1, volleyCount.Value) - _volleyRemaining;
            float az = (360f / Mathf.Max(1, volleyCount.Value)) * idx * Mathf.Deg2Rad;
            var horiz = new Vector3(Mathf.Cos(az), 0f, Mathf.Sin(az));
            float tilt = volleySpreadDegrees.Value * Mathf.Deg2Rad;
            var dir = (Vector3.up * Mathf.Cos(tilt) + horiz * Mathf.Sin(tilt)).normalized;

            var go = _pool.Spawn(PoolIds.HomingMissile, muzzlePos, Quaternion.LookRotation(dir, Vector3.up));
            if (go != null && go.TryGetComponent<HomingMissile>(out var missile))
                missile.Initialize(dir);
        }

        private void CachePlayerCollider()
        {
            if (_playerCollider != null || Player == null) return;
            _playerCollider = Player.GetComponent<Collider>() ?? Player.GetComponentInChildren<Collider>();
        }

        private void OnCoreDeath()
        {
            _dead = true;
            if (_nav != null) _nav.Stop();
        }
    }
}

using UnityEngine;
using BlastFrame.Core;
using BlastFrame.Core.Pooling;
using BlastFrame.Core.Services;
using BlastFrame.Core.Variables;
using BlastFrame.Gameplay.Projectiles;

namespace BlastFrame.Gameplay.Enemies
{
    /// <summary>
    /// Composable behavior: rotates the turret body to face the player (yaw), pitches a barrel
    /// pivot to visually match the ballistic launch angle (pitch), then fires a pooled ArcProjectile
    /// that lands on the predicted target ground point under gravity.
    /// </summary>
    public class EnemyBehaviorArcPredict : EnemyBehaviorBase
    {
        [Tooltip("Child Transform at the barrel tip from which arc projectiles spawn.")]
        [SerializeField] private Transform muzzle;

        [Tooltip("Child Transform of the barrel that rotates on its local X-axis to visually " +
                 "track the launch angle. Pivot point is the transform's own origin. " +
                 "Optional — leave empty for a turret with no moving barrel.")]
        [SerializeField] private Transform barrelPivot;

        [Tooltip("Degrees per second the turret body yaws toward its aim point.")]
        [SerializeField] private FloatReference rotationSpeed = new FloatReference(90f);

        [Tooltip("Degrees per second the barrel pitches toward the computed launch angle.")]
        [SerializeField] private FloatReference barrelPitchSpeed = new FloatReference(120f);

        [Tooltip("Dot product threshold above which the player is considered to be moving " +
                 "'roughly straight' and leading is applied. Range [0,1].")]
        [SerializeField] private FloatReference straightThreshold = new FloatReference(0.85f);

        [Tooltip("Minimum horizontal distance to the target before the turret fires (metres).")]
        [SerializeField] private FloatReference minFireDistance = new FloatReference(2f);

        [Tooltip("Number of iterations for the iterative lead-time solve (3-5 is plenty).")]
        [SerializeField] private IntReference predictIterations = new IntReference(3);

        [Tooltip("Multiplier on Physics.gravity for this turret's projectiles. 1 = normal, " +
                 "0.2 = floaty lob with long hang time. The ballistic solver and the projectile " +
                 "both use this value so shots stay accurate.")]
        [SerializeField] private FloatReference projectileGravityScale = new FloatReference(1f);

        [Tooltip("Maximum seconds of player-movement lead applied to aim prediction. Caps the " +
                 "runaway lead that slow, high arcs would otherwise produce (e.g. predicting the " +
                 "player several rooms away — or behind the turret when they run toward it).")]
        [SerializeField] private FloatReference maxLeadTime = new FloatReference(1.5f);

        private static readonly RaycastHit[] s_losBuffer = new RaycastHit[32];

        private IPoolManager _pool;
        private Collider _playerCollider;
        private float _fireCooldown;
        private float _currentBarrelPitchDeg;
        private bool _dead;

        // Velocity sampling — two position snapshots.
        private Vector3 _prevPlayerPos;
        private Vector3 _playerVelocity;

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
            if (HasPlayer) _prevPlayerPos = Player.position;
        }

        private void FixedUpdate()
        {
            if (_dead || !HasPlayer) return;

            // --- Sample player velocity -----------------------------------------------------------
            var playerPos = Player.position;
            if (Time.fixedDeltaTime > 0f)
                _playerVelocity = (playerPos - _prevPlayerPos) / Time.fixedDeltaTime;
            _prevPlayerPos = playerPos;

            var toPlayer = playerPos - transform.position;
            if (toPlayer.sqrMagnitude > Stats.Range * Stats.Range) return;

            // --- Predict aim target ---------------------------------------------------------------
            var targetPos = PredictTarget(playerPos);

            // --- Turret yaw: rotate body on Y toward target ---------------------------------------
            var flatDir = new Vector3(targetPos.x - transform.position.x, 0f, targetPos.z - transform.position.z);
            if (flatDir.sqrMagnitude > 0.001f)
            {
                var targetRot = Quaternion.LookRotation(flatDir, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, targetRot, rotationSpeed.Value * Time.fixedDeltaTime);
            }

            // --- Barrel pitch: tilt on local X toward ballistic launch angle ----------------------
            var targetPitchDeg = -ComputeLaunchAngle(targetPos) * Mathf.Rad2Deg;
            _currentBarrelPitchDeg = Mathf.MoveTowards(
                _currentBarrelPitchDeg, targetPitchDeg, barrelPitchSpeed.Value * Time.fixedDeltaTime);
            if (barrelPivot != null)
                barrelPivot.localEulerAngles = new Vector3(_currentBarrelPitchDeg, 0f, 0f);

            // --- Fire (only with line of sight to any part of the player) -------------------------
            _fireCooldown = Mathf.Max(_fireCooldown - Time.fixedDeltaTime, 0f);
            if (_fireCooldown <= 0f && HasLineOfSight())
            {
                Fire(targetPos);
                _fireCooldown = Stats.FireRate > 0f ? 1f / Stats.FireRate : 1f;
            }
        }

        // True if an unobstructed ray exists from the turret center to ANY sampled point on the
        // player's collider bounds (center, head, feet, left edge, right edge) — so a player only
        // partially poking out from cover is still a valid target.
        private bool HasLineOfSight()
        {
            var player = Player;
            if (player == null) return false;

            if (_playerCollider == null)
            {
                _playerCollider = player.GetComponent<Collider>();
                if (_playerCollider == null) _playerCollider = player.GetComponentInChildren<Collider>();
            }

            var origin = transform.position;
            var b = _playerCollider != null
                ? _playerCollider.bounds
                : new Bounds(player.position, new Vector3(1f, 2f, 1f));

            var toCenter = b.center - origin;
            var side = Vector3.Cross(toCenter, Vector3.up);
            side = side.sqrMagnitude > 0.001f ? side.normalized : Vector3.right;
            var lateral = Mathf.Max(b.extents.x, b.extents.z) * 0.9f;
            var vertical = b.extents.y * 0.9f;

            return IsPointVisible(origin, b.center, player)
                || IsPointVisible(origin, b.center + Vector3.up * vertical, player)
                || IsPointVisible(origin, b.center - Vector3.up * vertical, player)
                || IsPointVisible(origin, b.center + side * lateral, player)
                || IsPointVisible(origin, b.center - side * lateral, player);
        }

        private bool IsPointVisible(Vector3 origin, Vector3 point, Transform player)
        {
            var delta = point - origin;
            var dist = delta.magnitude;
            if (dist < 0.001f) return true;
            var dir = delta / dist;

            int n = Physics.RaycastNonAlloc(origin, dir, s_losBuffer, dist, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var t = s_losBuffer[i].transform;
                if (t.IsChildOf(transform) || t.IsChildOf(player)) continue; // self / target — not blockers
                if (s_losBuffer[i].distance < dist - 0.05f) return false;    // solid geometry in the way
            }
            return true;
        }

        // Returns the ground-level target position — either predicted or current player foot.
        private Vector3 PredictTarget(Vector3 playerPos)
        {
            var spawnPos = muzzle != null ? muzzle.position : transform.position;
            var launchSpeed = Stats.ProjectileSpeed;

            var flatVel = new Vector3(_playerVelocity.x, 0f, _playerVelocity.z);
            var groundTarget = new Vector3(playerPos.x, playerPos.y, playerPos.z);

            if (flatVel.magnitude < 0.5f) return groundTarget;

            var leadPos = groundTarget;
            for (int i = 0; i < predictIterations.Value; i++)
            {
                var flatDist = new Vector3(leadPos.x - spawnPos.x, 0f, leadPos.z - spawnPos.z).magnitude;
                if (launchSpeed < 0.001f) break;

                // Travel time of a ballistic arc: t = R / (v·cosθ), using the actual high-arc angle
                // this shot will be fired at (the iterative loop re-solves as the lead point moves).
                // Capped at maxLeadTime so slow lobs never predict absurdly far ahead.
                var cosTheta = Mathf.Max(Mathf.Cos(ComputeLaunchAngle(leadPos)), 0.1f);
                var travelTime = Mathf.Clamp(flatDist / (launchSpeed * cosTheta), 0f, maxLeadTime.Value);
                var predicted = new Vector3(
                    playerPos.x + _playerVelocity.x * travelTime,
                    playerPos.y,
                    playerPos.z + _playerVelocity.z * travelTime);

                var toPredicted = new Vector3(predicted.x - spawnPos.x, 0f, predicted.z - spawnPos.z);
                if (toPredicted.sqrMagnitude > 0.001f && flatVel.sqrMagnitude > 0.001f)
                {
                    if (Vector3.Dot(flatVel.normalized, toPredicted.normalized) >= straightThreshold.Value)
                        leadPos = predicted;
                    else
                        break;
                }
                else
                {
                    leadPos = predicted;
                }
            }

            return leadPos;
        }

        // Solves the HIGH-arc ballistic launch angle (radians) required to land on targetPos.
        // Ballistic solve: tanθ = (v² ± √(v⁴ − g(gR² + 2·dy·v²))) / (gR). The two roots straddle
        // 45° — taking the + root gives the lobbed mortar arc, which is ≥ 45° for any target at or
        // above max-range. Clamped to ≥ 45° for the steep-downhill edge case, and falls back to 45°
        // (max range) when the target is unreachable at the current projectile speed.
        private float ComputeLaunchAngle(Vector3 targetPos)
        {
            const float minAngle = 45f * Mathf.Deg2Rad;

            var spawnPos = muzzle != null ? muzzle.position : transform.position;
            var flatOffset = new Vector3(targetPos.x - spawnPos.x, 0f, targetPos.z - spawnPos.z);
            var R = flatOffset.magnitude;
            if (R < 0.001f) return minAngle;

            var g  = Mathf.Abs(Physics.gravity.y) * Mathf.Max(projectileGravityScale.Value, 0.01f);
            var dy = targetPos.y - spawnPos.y;
            var v  = Stats.ProjectileSpeed;
            var v2 = v * v;

            var disc = v2 * v2 - g * (g * R * R + 2f * dy * v2);
            if (disc < 0f) return minAngle;

            var highTan = (v2 + Mathf.Sqrt(disc)) / (g * R);
            return Mathf.Max(Mathf.Atan(highTan), minAngle);
        }

        private void Fire(Vector3 targetPos)
        {
            if (_pool == null) return;

            var spawnPos  = muzzle != null ? muzzle.position : transform.position;
            var flatOffset = new Vector3(targetPos.x - spawnPos.x, 0f, targetPos.z - spawnPos.z);
            if (flatOffset.magnitude < minFireDistance.Value) return;

            // Constant muzzle velocity along the barrel's CURRENT physical facing — body yaw and
            // barrel pitch decide where the shot goes. The ballistic solver only steers the barrel;
            // it never bypasses it. A barrel still swinging toward the target fires off-aim, which
            // is exactly the readable behavior we want.
            var aimDir = muzzle != null ? muzzle.forward
                       : barrelPivot != null ? barrelPivot.forward
                       : transform.forward;
            var launchVel = aimDir * Stats.ProjectileSpeed;

            var go = _pool.Spawn(PoolIds.ArcProjectile, spawnPos,
                                 Quaternion.LookRotation(aimDir, Vector3.up));
            if (go == null) return;

            if (go.TryGetComponent<ArcProjectile>(out var proj))
                proj.Initialize(Stats.Damage, launchVel, projectileGravityScale.Value);
        }

        private void OnCoreDeathHandler() => _dead = true;
    }
}

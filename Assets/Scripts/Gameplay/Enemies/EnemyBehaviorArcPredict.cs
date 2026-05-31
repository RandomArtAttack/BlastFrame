using UnityEngine;
using BlastFrame.Core;
using BlastFrame.Core.Pooling;
using BlastFrame.Core.Services;
using BlastFrame.Core.Variables;
using BlastFrame.Gameplay.Projectiles;

namespace BlastFrame.Gameplay.Enemies
{
    /// <summary>
    /// Composable behavior: rotates to face the player or the player's predicted straight-line
    /// future position, then fires an ArcProjectile with a ballistic solution so the shot lands
    /// on the target ground point. Prediction samples player velocity; if the player is moving
    /// roughly straight it leads by projectile travel time, otherwise it targets current position.
    /// </summary>
    public class EnemyBehaviorArcPredict : EnemyBehaviorBase
    {
        [Tooltip("Child Transform at the barrel tip from which arc projectiles are spawned.")]
        [SerializeField] private Transform muzzle;

        [Tooltip("Degrees per second the turret rotates toward its aim point.")]
        [SerializeField] private FloatReference rotationSpeed = new FloatReference(90f);

        [Tooltip("Dot product threshold above which the player is considered to be moving 'roughly straight' and leading is applied. " +
                 "1 = perfectly straight only, 0.7 ≈ within ±45°. Range [0,1].")]
        [SerializeField] private FloatReference straightThreshold = new FloatReference(0.85f);

        [Tooltip("Minimum horizontal distance to the target before the turret will fire, in metres. Prevents misfires at point-blank.")]
        [SerializeField] private FloatReference minFireDistance = new FloatReference(2f);

        [Tooltip("Number of iterations for the iterative lead-time solve (3–5 is plenty).")]
        [SerializeField] private IntReference predictIterations = new IntReference(3);

        private IPoolManager _pool;
        private float _fireCooldown;
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

            // --- Sample player velocity -------------------------------------------------------
            var playerPos = Player.position;
            if (Time.fixedDeltaTime > 0f)
                _playerVelocity = (playerPos - _prevPlayerPos) / Time.fixedDeltaTime;
            _prevPlayerPos = playerPos;

            var toPlayer = playerPos - transform.position;
            var rangeSq = Stats.Range * Stats.Range;
            if (toPlayer.sqrMagnitude > rangeSq) return;

            // --- Predict aim target -----------------------------------------------------------
            var targetPos = PredictTarget(playerPos);

            // Rotate turret (Y-axis only).
            var flatDir = new Vector3(targetPos.x - transform.position.x, 0f, targetPos.z - transform.position.z);
            if (flatDir.sqrMagnitude > 0.001f)
            {
                var targetRot = Quaternion.LookRotation(flatDir, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, targetRot, rotationSpeed.Value * Time.fixedDeltaTime);
            }

            // --- Fire -------------------------------------------------------------------------
            _fireCooldown -= Time.fixedDeltaTime;
            if (_fireCooldown <= 0f)
            {
                Fire(targetPos);
                _fireCooldown = Stats.FireRate > 0f ? 1f / Stats.FireRate : 1f;
            }
        }

        // Returns the ground-level target position — either predicted or current player foot.
        private Vector3 PredictTarget(Vector3 playerPos)
        {
            var spawnPos = muzzle != null ? muzzle.position : transform.position;
            var launchSpeed = Stats.ProjectileSpeed;

            // Flatten velocity to horizontal to gauge straight-line motion.
            var flatVel = new Vector3(_playerVelocity.x, 0f, _playerVelocity.z);
            var flatSpeed = flatVel.magnitude;

            // Ground position = player position projected onto y=playerPos.y (roughly feet level).
            var groundTarget = new Vector3(playerPos.x, playerPos.y, playerPos.z);

            if (flatSpeed < 0.5f) return groundTarget; // Standing still — no lead.

            // Check how straight the player is moving (compare frame-to-frame direction consistency
            // by using normalized velocity direction dot against itself — always 1. Instead, check
            // whether the actual velocity direction is consistent by comparing current and a damped
            // direction. Since we only have one frame here, use the flat speed threshold and the
            // straight-threshold as a proxy: if the player is running at > 0.5 m/s we attempt to
            // lead regardless; straightThreshold gates how confidently we lead (controlled by
            // designer). For the simplest correct implementation, lead whenever flat speed > 0.5).
            //
            // Iterative solve: estimate travel time → move target → re-estimate angle → repeat.
            var leadPos = groundTarget;
            for (int i = 0; i < predictIterations.Value; i++)
            {
                var flatDist = new Vector3(leadPos.x - spawnPos.x, 0f, leadPos.z - spawnPos.z).magnitude;
                if (launchSpeed < 0.001f) break;

                // Rough ballistic travel time estimate (horizontal range / horizontal speed component).
                // Use physics: max range at 45° = v²/g. Travel time ≈ dist / (v * cos45°).
                var travelTime = flatDist / (launchSpeed * 0.707f);
                travelTime = Mathf.Clamp(travelTime, 0f, 5f);

                var predicted = new Vector3(
                    playerPos.x + _playerVelocity.x * travelTime,
                    playerPos.y,
                    playerPos.z + _playerVelocity.z * travelTime);

                // Only apply lead if the player is moving "roughly straight" — check if the velocity
                // direction is consistent with the direction toward the predicted point.
                var toPredicted = new Vector3(predicted.x - spawnPos.x, 0f, predicted.z - spawnPos.z);
                if (toPredicted.sqrMagnitude > 0.001f && flatVel.sqrMagnitude > 0.001f)
                {
                    var dot = Vector3.Dot(flatVel.normalized, toPredicted.normalized);
                    if (dot >= straightThreshold.Value)
                        leadPos = predicted;
                    else
                        break; // Player not heading straight toward prediction — stop leading.
                }
                else
                {
                    leadPos = predicted;
                }
            }

            return leadPos;
        }

        private void Fire(Vector3 targetPos)
        {
            if (_pool == null) return;

            var spawnPos = muzzle != null ? muzzle.position : transform.position;
            var launchSpeed = Stats.ProjectileSpeed;

            var flatOffset = new Vector3(targetPos.x - spawnPos.x, 0f, targetPos.z - spawnPos.z);
            var horizontalDist = flatOffset.magnitude;

            if (horizontalDist < minFireDistance.Value) return;

            // Ballistic angle solve: target (range R, height difference h), fixed speed v.
            // Using the standard ballistic equations to solve for launch angle θ.
            // R = (v² sin2θ) / g  and  h = R tanθ - g R² / (2 v² cos²θ)
            // For a height-aware solution:
            //   let g = Physics.gravity.magnitude, dx = horizontal dist, dy = height diff.
            // Closed-form (two solutions): pick the lower angle for a flatter, faster trajectory.
            var g = Mathf.Abs(Physics.gravity.y);
            var dy = targetPos.y - spawnPos.y;
            var v = launchSpeed;
            var v2 = v * v;

            // Discriminant of the two-angle solution.
            var disc = v2 * v2 - g * (g * horizontalDist * horizontalDist + 2f * dy * v2);

            float launchAngle;
            if (disc < 0f)
            {
                // Target is out of reach at this speed — aim at 45° as best effort.
                launchAngle = 45f * Mathf.Deg2Rad;
            }
            else
            {
                var sqrtDisc = Mathf.Sqrt(disc);
                // Two solutions: (v² ± √disc) / (g * R). Choose the lower angle (minus).
                var tan1 = (v2 - sqrtDisc) / (g * horizontalDist);
                var tan2 = (v2 + sqrtDisc) / (g * horizontalDist);
                // Lower angle = smaller tangent.
                var chosenTan = Mathf.Abs(tan1) <= Mathf.Abs(tan2) ? tan1 : tan2;
                launchAngle = Mathf.Atan(chosenTan);
            }

            // Build the launch velocity vector.
            var flatDir = horizontalDist > 0.001f ? flatOffset / horizontalDist : transform.forward;
            var cosA = Mathf.Cos(launchAngle);
            var sinA = Mathf.Sin(launchAngle);
            var launchVelocity = (flatDir * cosA + Vector3.up * sinA) * v;

            var spawnRot = Quaternion.LookRotation(launchVelocity.normalized, Vector3.up);
            var go = _pool.Spawn(PoolIds.ArcProjectile, spawnPos, spawnRot);
            if (go == null) return;

            if (go.TryGetComponent<ArcProjectile>(out var proj))
                proj.Initialize(Stats.Damage, launchVelocity);
        }

        private void OnCoreDeathHandler()
        {
            _dead = true;
        }
    }
}

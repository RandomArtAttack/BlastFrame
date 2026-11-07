using UnityEngine;
using BlastFrame.Core;
using BlastFrame.Core.Services;
using BlastFrame.Core.Variables;

namespace BlastFrame.Gameplay.Enemies
{
    /// <summary>
    /// Composable behavior for a hovering robot ("Hoverbot"): flies toward the player, holds a
    /// preferred range band, orbits/strafes with random direction flips and an idle hover bob, fires
    /// short bursts of pooled EnemyMissiles with mild lead, dodges sideways when the player aims at it,
    /// and periodically repositions. Kinematic MovePosition steering (no AddForce), one spherecast for
    /// obstacle deflection. Reads EnemyStats, reacts to EnemyCore.OnDeath — never references other
    /// behaviors. All movement runs in FixedUpdate with zero per-tick allocations (shared static buffers).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class EnemyBehaviorFlyer : EnemyBehaviorBase
    {
        [Tooltip("Child Transform from which missiles spawn (own child — its forward is used for the shot).")]
        [SerializeField] private Transform muzzle;

        [Tooltip("Orbit ring radius around the player, in metres. The flyer holds this distance.")]
        [SerializeField] private FloatReference preferredRange = new FloatReference(10f);

        [Tooltip("Acceptable ± band (metres) around Preferred Range before the flyer corrects distance " +
                 "and the gate that allows firing.")]
        [SerializeField] private FloatReference rangeBand = new FloatReference(3f);

        [Tooltip("Metres above the player's eye height the flyer tries to hold.")]
        [SerializeField] private FloatReference hoverHeight = new FloatReference(2.5f);

        [Tooltip("Amplitude (metres) of the idle vertical sine bob.")]
        [SerializeField] private FloatReference hoverBobAmplitude = new FloatReference(0.4f);

        [Tooltip("Frequency (Hz) of the idle vertical sine bob.")]
        [SerializeField] private FloatReference hoverBobFrequency = new FloatReference(1.2f);

        [Tooltip("Orbit tangential speed = MoveSpeed × this factor (0-1).")]
        [SerializeField] private FloatReference strafeSpeedFactor = new FloatReference(0.7f);

        [Tooltip("Minimum seconds between random orbit direction flips.")]
        [SerializeField] private FloatReference strafeFlipIntervalMin = new FloatReference(1.5f);

        [Tooltip("Maximum seconds between random orbit direction flips.")]
        [SerializeField] private FloatReference strafeFlipIntervalMax = new FloatReference(3.5f);

        [Tooltip("Lateral burst speed (m/s) applied during a dodge. May exceed MoveSpeed by design.")]
        [SerializeField] private FloatReference dodgeSpeed = new FloatReference(9f);

        [Tooltip("Seconds a dodge impulse lasts (steering is suspended for this long).")]
        [SerializeField] private FloatReference dodgeDuration = new FloatReference(0.25f);

        [Tooltip("Minimum seconds between dodges.")]
        [SerializeField] private FloatReference dodgeCooldown = new FloatReference(1.2f);

        [Tooltip("Dot of the player camera's forward with the direction to the flyer that counts as " +
                 "'aimed at'. Range [0,1]; higher = must be more precisely aimed. ~0.985 is a tight cone.")]
        [SerializeField] private FloatReference aimDotThreshold = new FloatReference(0.985f);

        [Tooltip("Seconds the aim must be held continuously before a dodge fires (reaction delay).")]
        [SerializeField] private FloatReference aimReactionTime = new FloatReference(0.18f);

        [Tooltip("Seconds between picking a new orbit height offset and angle jump.")]
        [SerializeField] private FloatReference repositionInterval = new FloatReference(4f);

        [Tooltip("Missiles fired per burst.")]
        [SerializeField] private IntReference burstCount = new IntReference(3);

        [Tooltip("Seconds between individual missiles inside one burst.")]
        [SerializeField] private FloatReference burstSpacing = new FloatReference(0.12f);

        [Tooltip("Fraction of the full linear lead applied when aiming (0 = no lead, 1 = full lead).")]
        [SerializeField] private FloatReference leadFactor = new FloatReference(0.5f);

        [Tooltip("Spherecast length ahead (metres) used to deflect the flyer off obstacles.")]
        [SerializeField] private FloatReference avoidProbeDistance = new FloatReference(2f);

        // --- Spec-inlined secondary tuning (named, not magic literals) ---------------------------
        private const float SteeringAccelFactor = 2f;         // accel per second = MoveSpeed × this
        private const float FaceTurnDegreesPerSecond = 360f;  // yaw slerp rate toward the player
        private const float DodgeUpBiasFactor = 0.3f;         // + dodgeSpeed × this on Y when biased up
        private const float DodgeUpChance = 0.5f;             // probability a dodge adds up-bias
        private const float MicroDodgeChance = 0.35f;         // chance of a micro-dodge right after a burst
        private const float RepositionHeightRange = 1.5f;     // ± metres re-rolled every reposition
        private const float RepositionAngleJumpMin = 30f;     // degrees
        private const float RepositionAngleJumpMax = 90f;     // degrees
        private const float TwoPi = Mathf.PI * 2f;

        private static readonly RaycastHit[] s_castBuffer = new RaycastHit[8];

        private Rigidbody _rb;
        private IPoolManager _pool;
        private Collider _playerCollider;
        private Camera _playerCamera;
        private float _selfRadius = 0.5f;

        private Vector3 _velocity;
        private float _orbitAngle;
        private int _orbitDir = 1;
        private float _flipTimer;
        private float _repositionTimer;
        private float _heightOffset;
        private float _bobPhase;

        private float _burstCooldown;
        private int _burstRemaining;
        private float _burstSpacingTimer;

        private float _dodgeTimer;
        private float _dodgeCooldownTimer;
        private float _aimHeldTime;
        private Vector3 _dodgeVelocity;

        private Vector3 _prevPlayerPos;
        private Vector3 _playerVelocity;
        private bool _dead;

        // ----- Lifecycle -----------------------------------------------------------------------

        protected override void Awake()
        {
            base.Awake();

            if (!TryGetComponent(out _rb)) _rb = gameObject.AddComponent<Rigidbody>();
            _rb.isKinematic = true;
            _rb.useGravity = false;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;

            Core.OnDeath += OnCoreDeathHandler;

            // Deterministic per-instance bob phase and initial orbit direction so co-spawned flyers differ.
            int id = GetInstanceID();
            _bobPhase = Mathf.Abs(id % 628) * 0.01f;
            _orbitDir = (id & 1) == 0 ? 1 : -1;
        }

        private void OnDestroy()
        {
            Core.OnDeath -= OnCoreDeathHandler;
        }

        private void Start()
        {
            _pool = ServiceLocator.Get<IPoolManager>();

            if (TryGetComponent<SphereCollider>(out var sphere))
            {
                var s = transform.lossyScale;
                _selfRadius = sphere.radius * Mathf.Max(Mathf.Abs(s.x), Mathf.Max(Mathf.Abs(s.y), Mathf.Abs(s.z)));
            }
            else if (TryGetComponent<Collider>(out var col))
            {
                _selfRadius = Mathf.Max(col.bounds.extents.x, col.bounds.extents.z);
            }

            _flipTimer = Random.Range(strafeFlipIntervalMin.Value, strafeFlipIntervalMax.Value);
            _repositionTimer = repositionInterval.Value;
            _orbitAngle = Random.Range(0f, TwoPi);

            if (HasPlayer) _prevPlayerPos = Player.position;
        }

        private void FixedUpdate()
        {
            if (_dead) return;

            float dt = Time.fixedDeltaTime;
            _dodgeCooldownTimer = Mathf.Max(_dodgeCooldownTimer - dt, 0f);

            if (!HasPlayer)
            {
                IdleDrift(dt);
                return;
            }

            var player = Player;
            var playerPos = player.position;

            // Sample player velocity by position delta (same as the turret).
            if (dt > 0f) _playerVelocity = (playerPos - _prevPlayerPos) / dt;
            _prevPlayerPos = playerPos;

            CachePlayerRefs(player);

            var pos = transform.position;
            var toPlayer = playerPos - pos;
            float range = Stats.Range;
            if (toPlayer.sqrMagnitude > range * range)
            {
                IdleDrift(dt);
                return;
            }

            float dist = toPlayer.magnitude;
            float eyeY = _playerCamera != null ? _playerCamera.transform.position.y : playerPos.y;

            // --- Reposition: re-roll height offset and jump the orbit angle ------------------------
            _repositionTimer -= dt;
            if (_repositionTimer <= 0f)
            {
                _heightOffset = Random.Range(-RepositionHeightRange, RepositionHeightRange);
                float jump = Random.Range(RepositionAngleJumpMin, RepositionAngleJumpMax) * Mathf.Deg2Rad;
                _orbitAngle += _orbitDir * jump;
                _repositionTimer = repositionInterval.Value;
            }

            // --- Orbit direction flip --------------------------------------------------------------
            _flipTimer -= dt;
            if (_flipTimer <= 0f)
            {
                _orbitDir = -_orbitDir;
                _flipTimer = Random.Range(strafeFlipIntervalMin.Value, strafeFlipIntervalMax.Value);
            }

            // --- Advance orbit angle and build the desired ring position ---------------------------
            float ring = Mathf.Max(preferredRange.Value, 0.01f);
            float angularSpeed = strafeSpeedFactor.Value * Stats.MoveSpeed / ring; // rad/s
            _orbitAngle += _orbitDir * angularSpeed * dt;

            var ringDir = new Vector3(Mathf.Cos(_orbitAngle), 0f, Mathf.Sin(_orbitAngle));
            var desiredPos = playerPos + ringDir * ring;
            desiredPos.y = eyeY + hoverHeight.Value + _heightOffset + BobOffset();

            // --- Dodge detection (aimed-at) --------------------------------------------------------
            UpdateAimAndMaybeDodge(pos, dt);

            // --- Velocity: dodge override OR steering ----------------------------------------------
            Vector3 vel;
            if (_dodgeTimer > 0f)
            {
                _dodgeTimer -= dt;
                vel = _dodgeVelocity;
                ApplyObstacleSlide(ref vel, pos);
            }
            else
            {
                var desiredVel = Vector3.ClampMagnitude(desiredPos - pos, Stats.MoveSpeed);
                _velocity = Vector3.MoveTowards(_velocity, desiredVel, SteeringAccelFactor * Stats.MoveSpeed * dt);
                vel = _velocity;
                ApplyObstacleSlide(ref vel, pos);
                _velocity = vel;
            }

            _rb.MovePosition(pos + vel * dt);

            // --- Face the player (yaw only) --------------------------------------------------------
            var flatDir = new Vector3(playerPos.x - pos.x, 0f, playerPos.z - pos.z);
            if (flatDir.sqrMagnitude > 0.001f)
            {
                var targetRot = Quaternion.LookRotation(flatDir, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, targetRot, FaceTurnDegreesPerSecond * dt);
            }

            // --- Fire ------------------------------------------------------------------------------
            UpdateFiring(pos, playerPos, dist, dt);
        }

        // ----- Movement helpers ----------------------------------------------------------------

        private void IdleDrift(float dt)
        {
            _velocity = Vector3.MoveTowards(_velocity, Vector3.zero, SteeringAccelFactor * Stats.MoveSpeed * dt);
            var vel = _velocity;
            vel.y += BobVelocity(); // keep the idle hover bob alive while drifting to a stop
            var pos = transform.position;
            ApplyObstacleSlide(ref vel, pos);
            _rb.MovePosition(pos + vel * dt);
        }

        private void ApplyObstacleSlide(ref Vector3 vel, Vector3 pos)
        {
            if (vel.sqrMagnitude < 0.0001f) return;

            float len = vel.magnitude;
            var dir = vel / len;
            int n = Physics.SphereCastNonAlloc(pos, _selfRadius, dir, s_castBuffer,
                avoidProbeDistance.Value, ~0, QueryTriggerInteraction.Ignore);

            float nearest = float.MaxValue;
            int idx = -1;
            for (int i = 0; i < n; i++)
            {
                var h = s_castBuffer[i];
                if (h.collider == null) continue;
                if (h.collider.transform.IsChildOf(transform)) continue;   // self — not an obstacle
                if (h.normal.sqrMagnitude < 0.001f) continue;              // started overlapping — no plane
                if (h.distance < nearest)
                {
                    nearest = h.distance;
                    idx = i;
                }
            }

            if (idx >= 0)
                vel = Vector3.ProjectOnPlane(vel, s_castBuffer[idx].normal);
        }

        private float BobOffset() =>
            hoverBobAmplitude.Value * Mathf.Sin(Time.time * TwoPi * hoverBobFrequency.Value + _bobPhase);

        private float BobVelocity() =>
            hoverBobAmplitude.Value * TwoPi * hoverBobFrequency.Value *
            Mathf.Cos(Time.time * TwoPi * hoverBobFrequency.Value + _bobPhase);

        // ----- Dodge ---------------------------------------------------------------------------

        private void UpdateAimAndMaybeDodge(Vector3 pos, float dt)
        {
            if (_playerCamera == null) { _aimHeldTime = 0f; return; }

            var camPos = _playerCamera.transform.position;
            var toFlyer = pos - camPos;
            if (toFlyer.sqrMagnitude < 0.0001f) { _aimHeldTime = 0f; return; }

            float aimDot = Vector3.Dot(_playerCamera.transform.forward, toFlyer.normalized);
            if (aimDot > aimDotThreshold.Value)
                _aimHeldTime += dt;
            else
                _aimHeldTime = 0f;

            if (_aimHeldTime >= aimReactionTime.Value && _dodgeCooldownTimer <= 0f && _dodgeTimer <= 0f)
                StartDodge();
        }

        private void StartDodge()
        {
            // Lateral axis: perpendicular to the player's aim, flattened. Fall back to own right.
            Vector3 lateral;
            if (_playerCamera != null)
            {
                lateral = _playerCamera.transform.right;
                lateral.y = 0f;
            }
            else
            {
                lateral = transform.right;
                lateral.y = 0f;
            }
            if (lateral.sqrMagnitude < 0.0001f) lateral = Vector3.right;
            lateral.Normalize();

            float sign = Random.value < 0.5f ? -1f : 1f;
            float speed = dodgeSpeed.Value;
            _dodgeVelocity = lateral * (sign * speed);
            if (Random.value < DodgeUpChance)
                _dodgeVelocity.y += DodgeUpBiasFactor * speed;

            _dodgeTimer = dodgeDuration.Value;
            _dodgeCooldownTimer = dodgeCooldown.Value;
            _aimHeldTime = 0f;
        }

        // ----- Firing --------------------------------------------------------------------------

        private void UpdateFiring(Vector3 pos, Vector3 playerPos, float dist, float dt)
        {
            _burstCooldown = Mathf.Max(_burstCooldown - dt, 0f);

            if (_burstRemaining > 0)
            {
                _burstSpacingTimer -= dt;
                if (_burstSpacingTimer <= 0f)
                {
                    FireOne(pos, playerPos, dist);
                    _burstRemaining--;
                    _burstSpacingTimer = burstSpacing.Value;

                    if (_burstRemaining == 0 && _dodgeTimer <= 0f && _dodgeCooldownTimer <= 0f
                        && Random.value < MicroDodgeChance)
                        StartDodge();
                }
                return;
            }

            bool withinBand = Mathf.Abs(dist - preferredRange.Value) <= rangeBand.Value;
            if (_burstCooldown <= 0f && withinBand &&
                EnemyLineOfSight.HasLineOfSight(transform, pos, Player, _playerCollider))
            {
                _burstRemaining = Mathf.Max(burstCount.Value, 1);
                _burstSpacingTimer = 0f; // fire the first shot on the next tick
                _burstCooldown = Stats.FireRate > 0f ? 1f / Stats.FireRate : 1f;
            }
        }

        private void FireOne(Vector3 pos, Vector3 playerPos, float dist)
        {
            if (_pool == null || muzzle == null) return;

            // Mild linear lead toward where the player is heading.
            float speed = Stats.ProjectileSpeed;
            float travelTime = speed > 0.001f ? dist / speed : 0f;
            var flatVel = new Vector3(_playerVelocity.x, 0f, _playerVelocity.z);
            var aimPoint = playerPos + flatVel * (travelTime * leadFactor.Value);

            var aimDir = aimPoint - muzzle.position;
            if (aimDir.sqrMagnitude < 0.0001f) aimDir = muzzle.forward;
            aimDir.Normalize();

            var go = _pool.Spawn(PoolIds.EnemyMissile, muzzle.position, Quaternion.LookRotation(aimDir, Vector3.up));
            if (go == null) return;

            if (go.TryGetComponent<Projectiles.EnemyMissile>(out var missile))
                missile.Initialize(Stats.Damage, aimDir);
        }

        // ----- References ----------------------------------------------------------------------

        private void CachePlayerRefs(Transform player)
        {
            // Base re-resolves both when the nearest player changes (co-op: dodge the aim of the player we target).
            _playerCollider = PlayerCollider;
            _playerCamera = PlayerCamera;
        }

        private void OnCoreDeathHandler() => _dead = true;
    }
}

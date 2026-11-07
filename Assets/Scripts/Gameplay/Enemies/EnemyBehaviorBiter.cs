using UnityEngine;
using BlastFrame.Core.Variables;

namespace BlastFrame.Gameplay.Enemies
{
    /// <summary>
    /// Small, fast melee chaser. NavMesh-chases the player via EnemyNavMotor but offsets its
    /// destination sideways with a smooth sine wobble so it weaves instead of running a straight line
    /// (the weave shrinks as it closes). Within lungeRange it briefly bursts agent speed toward the
    /// player; within biteRange it deals contact damage through the DamageInfo pipeline (source = its
    /// own position, so the directional damage indicator points at it) on a per-target cooldown.
    /// </summary>
    [RequireComponent(typeof(EnemyNavMotor))]
    public class EnemyBehaviorBiter : EnemyBehaviorBase
    {
        [Tooltip("Distance (m) at which it starts a lunge (brief speed burst).")]
        [SerializeField] private FloatReference lungeRange = new FloatReference(2.5f);

        [Tooltip("Agent speed multiplier during a lunge.")]
        [SerializeField] private FloatReference lungeSpeedMultiplier = new FloatReference(2.5f);

        [Tooltip("Seconds a lunge speed burst lasts.")]
        [SerializeField] private FloatReference lungeDuration = new FloatReference(0.3f);

        [Tooltip("Seconds between allowed lunges.")]
        [SerializeField] private FloatReference lungeCooldown = new FloatReference(1.5f);

        [Tooltip("Distance (m) at which a bite lands (contact damage).")]
        [SerializeField] private FloatReference biteRange = new FloatReference(1.6f);

        [Tooltip("Seconds between bites on the same target.")]
        [SerializeField] private FloatReference biteCooldown = new FloatReference(1f);

        [Tooltip("Peak sideways weave offset (m) applied to the chase destination when far from the player.")]
        [SerializeField] private FloatReference weaveAmplitude = new FloatReference(2.5f);

        [Tooltip("Weave oscillation frequency (Hz).")]
        [SerializeField] private FloatReference weaveFrequency = new FloatReference(1.4f);

        [Tooltip("Degrees/second the body yaws to face the player.")]
        [SerializeField] private FloatReference faceTurnSpeed = new FloatReference(360f);

        private EnemyNavMotor _nav;
        private IDamageable _playerDamageable;
        private float _weavePhase;
        private float _biteTimer;
        private float _lungeCdTimer;
        private float _lungeTimer;
        private bool _dead;

        protected override void Awake()
        {
            base.Awake();
            _nav = GetComponent<EnemyNavMotor>();
            Core.OnDeath += OnCoreDeath;
            _weavePhase = Mathf.Abs(GetInstanceID() % 628) * 0.01f;
        }

        private void OnDestroy() => Core.OnDeath -= OnCoreDeath;

        private void Update()
        {
            if (_dead || !HasPlayer) return;
            float dt = Time.deltaTime;
            var pos = transform.position;
            var playerPos = Player.position;
            var toPlayer = playerPos - pos;
            float dist = toPlayer.magnitude;

            _biteTimer = Mathf.Max(_biteTimer - dt, 0f);
            _lungeCdTimer = Mathf.Max(_lungeCdTimer - dt, 0f);
            if (_lungeTimer > 0f)
            {
                _lungeTimer -= dt;
                if (_lungeTimer <= 0f) _nav.SetSpeedMultiplier(1f);
            }

            FacePlayer(pos, playerPos, dt);

            // --- Weaving chase destination --------------------------------------------------------
            var flat = new Vector3(toPlayer.x, 0f, toPlayer.z);
            var forward = flat.sqrMagnitude > 0.0001f ? flat.normalized : transform.forward;
            var side = Vector3.Cross(Vector3.up, forward); // right of the approach vector
            // Weave shrinks as it closes so it actually converges on the target.
            float closeFactor = Mathf.Clamp01(dist / Mathf.Max(0.01f, lungeRange.Value * 2f));
            float weave = Mathf.Sin(Time.time * weaveFrequency.Value * Mathf.PI * 2f + _weavePhase)
                          * weaveAmplitude.Value * closeFactor;
            _nav.Resume();
            _nav.SetDestination(playerPos + side * weave);

            // --- Lunge ----------------------------------------------------------------------------
            if (dist <= lungeRange.Value && _lungeCdTimer <= 0f && _lungeTimer <= 0f)
            {
                _nav.SetSpeedMultiplier(lungeSpeedMultiplier.Value);
                _lungeTimer = lungeDuration.Value;
                _lungeCdTimer = lungeCooldown.Value;
            }

            // --- Bite -----------------------------------------------------------------------------
            if (dist <= biteRange.Value && _biteTimer <= 0f)
            {
                CachePlayerDamageable();
                if (_playerDamageable != null && !_playerDamageable.IsDead)
                {
                    _playerDamageable.TakeDamage(new DamageInfo(Stats.Damage, pos, DamageKind.Contact));
                    _biteTimer = biteCooldown.Value;
                }
            }
        }

        private void FacePlayer(Vector3 pos, Vector3 playerPos, float dt)
        {
            var flat = new Vector3(playerPos.x - pos.x, 0f, playerPos.z - pos.z);
            if (flat.sqrMagnitude < 0.001f) return;
            var target = Quaternion.LookRotation(flat, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, faceTurnSpeed.Value * dt);
        }

        private void CachePlayerDamageable()
        {
            if (Player == null) return;
            _playerDamageable = PlayerDamageable; // base re-resolves when the nearest player changes
        }

        private void OnCoreDeath()
        {
            _dead = true;
            if (_nav != null) { _nav.SetSpeedMultiplier(1f); _nav.Stop(); }
        }
    }
}

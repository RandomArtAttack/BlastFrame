using UnityEngine;
using BlastFrame.Core;
using BlastFrame.Core.Pooling;
using BlastFrame.Core.Services;
using BlastFrame.Core.Variables;
using BlastFrame.Gameplay.Projectiles;

namespace BlastFrame.Gameplay.Enemies
{
    /// <summary>
    /// Flying bomber with an ammo loop. Flies (direct kinematic steering + smooth noise wander, like the
    /// Flyer) to a hover point above the player, drops its single bomb (a pooled ArcProjectile falling
    /// under gravity that explodes on impact), then flies BACK to its home point to rearm — a brief
    /// reload pause — before hunting again. A small child sphere is toggled on/off to show the carried
    /// bomb. Home point is the spawn position, or a child "Roost" transform if assigned. No NavMesh.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class EnemyBehaviorBat : EnemyBehaviorBase
    {
        private enum State { Hunting, Returning, Reloading }

        [Tooltip("Child Transform the bomb is released from (under the bat).")]
        [SerializeField] private Transform dropPoint;

        [Tooltip("Small child sphere toggled on/off to show the carried bomb. Optional.")]
        [SerializeField] private Transform bombVisual;

        [Tooltip("Optional child Transform used as the home/roost point. If empty, the spawn position is home.")]
        [SerializeField] private Transform roost;

        [Tooltip("Metres above the player the bat tries to hover while hunting.")]
        [SerializeField] private FloatReference hoverHeight = new FloatReference(8f);

        [Tooltip("Horizontal distance (m) to directly-above-the-player within which it releases the bomb.")]
        [SerializeField] private FloatReference dropTolerance = new FloatReference(1.5f);

        [Tooltip("Seconds spent rearming at the home point before hunting again.")]
        [SerializeField] private FloatReference reloadTime = new FloatReference(1f);

        [Tooltip("Distance (m) to the home point that counts as 'arrived' for reloading.")]
        [SerializeField] private FloatReference homeArriveTolerance = new FloatReference(1.5f);

        [Tooltip("Multiplier on Physics.gravity for the dropped bomb (matches the ArcProjectile it spawns).")]
        [SerializeField] private FloatReference bombGravityScale = new FloatReference(1f);

        [Tooltip("Amplitude (m) of the smooth 3D wander added to the target position.")]
        [SerializeField] private FloatReference wanderAmplitude = new FloatReference(1.5f);

        [Tooltip("Frequency (Hz) of the wander.")]
        [SerializeField] private FloatReference wanderFrequency = new FloatReference(0.5f);

        private const float SteeringAccelFactor = 2f;
        private const float FaceTurnDegreesPerSecond = 240f;

        private Rigidbody _rb;
        private IPoolManager _pool;
        private Collider _playerCollider;
        private Vector3 _homePoint;
        private Vector3 _velocity;
        private float _wanderSeed;
        private State _state = State.Hunting;
        private float _reloadTimer;
        private bool _hasBomb = true;
        private bool _dead;

        protected override void Awake()
        {
            base.Awake();
            if (!TryGetComponent(out _rb)) _rb = gameObject.AddComponent<Rigidbody>();
            _rb.isKinematic = true;
            _rb.useGravity = false;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            Core.OnDeath += OnCoreDeath;
            _wanderSeed = Mathf.Abs(GetInstanceID() % 1000) * 0.137f;
        }

        private void OnDestroy() => Core.OnDeath -= OnCoreDeath;

        private void Start()
        {
            _pool = ServiceLocator.Get<IPoolManager>();
            _homePoint = roost != null ? roost.position : transform.position;
            SetBombVisual(true);
        }

        private void FixedUpdate()
        {
            if (_dead) return;
            float dt = Time.fixedDeltaTime;
            var pos = transform.position;

            if (!HasPlayer)
            {
                Steer(pos, _homePoint, dt);
                return;
            }

            var playerPos = Player.position;
            Vector3 targetPos;

            switch (_state)
            {
                case State.Hunting:
                    targetPos = playerPos + Vector3.up * hoverHeight.Value + Wander();
                    Steer(pos, targetPos, dt);
                    FaceHorizontal(pos, playerPos, dt);

                    float horizDist = new Vector3(playerPos.x - pos.x, 0f, playerPos.z - pos.z).magnitude;
                    if (_hasBomb && horizDist <= dropTolerance.Value && HasLineOfSightToPlayer(pos))
                        DropBomb();
                    break;

                case State.Returning:
                    Steer(pos, _homePoint + Wander(), dt);
                    FaceHorizontal(pos, _homePoint, dt);
                    if (Vector3.Distance(pos, _homePoint) <= homeArriveTolerance.Value)
                    {
                        _reloadTimer = reloadTime.Value;
                        _state = State.Reloading;
                    }
                    break;

                case State.Reloading:
                    Steer(pos, _homePoint, dt);
                    _reloadTimer -= dt;
                    if (_reloadTimer <= 0f)
                    {
                        _hasBomb = true;
                        SetBombVisual(true);
                        _state = State.Hunting;
                    }
                    break;
            }
        }

        private Vector3 Wander()
        {
            float t = Time.time;
            return new Vector3(
                Mathf.PerlinNoise(_wanderSeed, t * wanderFrequency.Value) - 0.5f,
                Mathf.PerlinNoise(_wanderSeed + 13f, t * wanderFrequency.Value) - 0.5f,
                Mathf.PerlinNoise(_wanderSeed + 29f, t * wanderFrequency.Value) - 0.5f) * (2f * wanderAmplitude.Value);
        }

        private void Steer(Vector3 pos, Vector3 targetPos, float dt)
        {
            var desiredVel = Vector3.ClampMagnitude(targetPos - pos, Stats.MoveSpeed);
            _velocity = Vector3.MoveTowards(_velocity, desiredVel, SteeringAccelFactor * Stats.MoveSpeed * dt);
            _rb.MovePosition(pos + _velocity * dt);
        }

        private void FaceHorizontal(Vector3 pos, Vector3 lookTarget, float dt)
        {
            var flat = new Vector3(lookTarget.x - pos.x, 0f, lookTarget.z - pos.z);
            if (flat.sqrMagnitude < 0.001f) return;
            var target = Quaternion.LookRotation(flat, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, FaceTurnDegreesPerSecond * dt);
        }

        private void DropBomb()
        {
            if (_pool == null) return;
            var spawn = dropPoint != null ? dropPoint.position : transform.position + Vector3.down * 0.5f;
            var go = _pool.Spawn(PoolIds.ArcProjectile, spawn, Quaternion.LookRotation(Vector3.down, Vector3.forward));
            if (go != null && go.TryGetComponent<ArcProjectile>(out var proj))
                proj.Initialize(Stats.Damage, Vector3.zero, bombGravityScale.Value); // pure gravity drop

            _hasBomb = false;
            SetBombVisual(false);
            _state = State.Returning;
        }

        private bool HasLineOfSightToPlayer(Vector3 pos)
        {
            if (Player != null)
                _playerCollider = PlayerCollider; // base re-resolves when the nearest player changes
            return EnemyLineOfSight.HasLineOfSight(transform, pos, Player, _playerCollider);
        }

        private void SetBombVisual(bool on)
        {
            if (bombVisual != null) bombVisual.gameObject.SetActive(on);
        }

        private void OnCoreDeath() => _dead = true;
    }
}

using UnityEngine;
using UnityEngine.AI;
using BlastFrame.Core;
using BlastFrame.Core.Pooling;
using BlastFrame.Core.Services;
using BlastFrame.Core.Variables;
using BlastFrame.Gameplay.Projectiles;

namespace BlastFrame.Gameplay.Enemies
{
    /// <summary>
    /// Ground cover-user. Samples a ring of NavMesh candidates and hides at the nearest one where line
    /// of sight to the player is BLOCKED. Every attackInterval it pops out to a sampled position WITH
    /// line of sight, fires a short burst of pooled straight shots (EnemyMissile), then returns to a
    /// fresh cover point. Occasionally dashes (agent speed ×dashSpeedMultiplier briefly); if the player
    /// gets closer than playerCloseDistance it dashes to a farther cover point. All via EnemyNavMotor.
    /// </summary>
    [RequireComponent(typeof(EnemyNavMotor))]
    public class EnemyBehaviorSkirmisher : EnemyBehaviorBase
    {
        private enum State { ToCover, InCover, ToPeek, Firing }

        [Tooltip("Child Transform (muzzle) straight shots spawn from.")]
        [SerializeField] private Transform muzzle;

        [Tooltip("Seconds hidden in cover before popping out to attack.")]
        [SerializeField] private FloatReference attackInterval = new FloatReference(3f);

        [Tooltip("Shots per burst when it pops out.")]
        [SerializeField] private IntReference burstCount = new IntReference(3);

        [Tooltip("Seconds between shots inside one burst.")]
        [SerializeField] private FloatReference burstSpacing = new FloatReference(0.14f);

        [Tooltip("Eye height (m) above the sampled ground point used for line-of-sight tests.")]
        [SerializeField] private FloatReference eyeHeight = new FloatReference(1.2f);

        [Tooltip("Number of ring candidates sampled when searching for cover / a firing spot.")]
        [SerializeField] private IntReference ringSamples = new IntReference(12);

        [Tooltip("Seconds between allowed dashes.")]
        [SerializeField] private FloatReference dashCooldown = new FloatReference(6f);

        [Tooltip("Agent speed multiplier while dashing.")]
        [SerializeField] private FloatReference dashSpeedMultiplier = new FloatReference(3f);

        [Tooltip("Seconds a dash lasts.")]
        [SerializeField] private FloatReference dashDuration = new FloatReference(0.3f);

        [Tooltip("If the player comes within this distance (m) the Skirmisher dashes to a farther cover point.")]
        [SerializeField] private FloatReference playerCloseDistance = new FloatReference(6f);

        [Tooltip("Degrees/second the body yaws to face the player.")]
        [SerializeField] private FloatReference faceTurnSpeed = new FloatReference(240f);

        private const float PeekTimeout = 4f;

        private IPoolManager _pool;
        private EnemyNavMotor _nav;
        private Collider _playerCollider;
        private State _state = State.ToCover;
        private float _attackTimer;
        private float _peekTimer;
        private int _burstRemaining;
        private float _burstTimer;
        private float _dashCdTimer;
        private float _dashTimer;
        private bool _hasTarget;
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
            _attackTimer = attackInterval.Value;
        }

        private void Update()
        {
            if (_dead || !HasPlayer) return;
            float dt = Time.deltaTime;
            var pos = transform.position;
            var playerPos = Player.position;

            CachePlayerCollider();
            FacePlayer(pos, playerPos, dt);

            _dashCdTimer = Mathf.Max(_dashCdTimer - dt, 0f);
            if (_dashTimer > 0f)
            {
                _dashTimer -= dt;
                if (_dashTimer <= 0f) _nav.SetSpeedMultiplier(1f);
            }

            // Player crowds us → dash to a farther cover point.
            float dist = Vector3.Distance(pos, playerPos);
            if (dist < playerCloseDistance.Value && _dashCdTimer <= 0f)
            {
                if (TryFindCover(pos, playerPos, requireFarther: true, out var far))
                {
                    _nav.Resume();
                    _nav.SetDestination(far);
                    StartDash();
                    _state = State.ToCover;
                    _hasTarget = true;
                }
            }

            switch (_state)
            {
                case State.ToCover:
                    if (!_hasTarget)
                    {
                        if (TryFindCover(pos, playerPos, requireFarther: false, out var cover))
                        {
                            _nav.Resume();
                            _nav.SetDestination(cover);
                            _hasTarget = true;
                        }
                    }
                    else if (_nav.HasArrived)
                    {
                        _nav.Stop();
                        _hasTarget = false;
                        _attackTimer = attackInterval.Value;
                        _state = State.InCover;
                    }
                    break;

                case State.InCover:
                    _attackTimer -= dt;
                    if (_attackTimer <= 0f)
                    {
                        if (TryFindPeek(pos, playerPos, out var peek))
                        {
                            _nav.Resume();
                            _nav.SetDestination(peek);
                            _peekTimer = PeekTimeout;
                            _state = State.ToPeek;
                        }
                        else
                        {
                            _attackTimer = attackInterval.Value * 0.5f; // retry soon
                        }
                    }
                    break;

                case State.ToPeek:
                    _peekTimer -= dt;
                    bool los = EnemyLineOfSight.HasLineOfSight(transform, MuzzleOrEye(pos), Player, _playerCollider);
                    if ((_nav.HasArrived && los) || (_peekTimer <= 0f && los))
                    {
                        _nav.Stop();
                        _burstRemaining = Mathf.Max(1, burstCount.Value);
                        _burstTimer = 0f;
                        _state = State.Firing;
                    }
                    else if (_peekTimer <= 0f)
                    {
                        _hasTarget = false;
                        _state = State.ToCover; // could not get a shot — re-cover
                    }
                    break;

                case State.Firing:
                    _burstTimer -= dt;
                    if (_burstTimer <= 0f && _burstRemaining > 0)
                    {
                        if (EnemyLineOfSight.HasLineOfSight(transform, MuzzleOrEye(pos), Player, _playerCollider))
                            FireStraight(playerPos);
                        _burstRemaining--;
                        _burstTimer = burstSpacing.Value;
                    }
                    if (_burstRemaining <= 0)
                    {
                        _hasTarget = false;
                        _state = State.ToCover;
                    }
                    break;
            }
        }

        private Vector3 MuzzleOrEye(Vector3 pos) =>
            muzzle != null ? muzzle.position : pos + Vector3.up * eyeHeight.Value;

        private void StartDash()
        {
            _nav.SetSpeedMultiplier(dashSpeedMultiplier.Value);
            _dashTimer = dashDuration.Value;
            _dashCdTimer = dashCooldown.Value;
        }

        // Ring-sample NavMesh points; pick the nearest (to self) walkable point whose LOS to the player
        // is BLOCKED. When requireFarther, only accept points farther from the player than we are now.
        private bool TryFindCover(Vector3 self, Vector3 playerPos, bool requireFarther, out Vector3 result)
        {
            result = self;
            int n = Mathf.Max(4, ringSamples.Value);
            float range = Stats.Range;
            float currentPlayerDist = Vector3.Distance(self, playerPos);
            float bestScore = float.MaxValue;
            bool found = false;

            for (int i = 0; i < n; i++)
            {
                float ang = (Mathf.PI * 2f / n) * i;
                // Two radii per angle for more candidates.
                for (int r = 0; r < 2; r++)
                {
                    float radius = Mathf.Lerp(range * 0.4f, range * 0.9f, r);
                    var probe = self + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * radius;
                    if (!NavMesh.SamplePosition(probe, out var hit, 3f, NavMesh.AllAreas)) continue;
                    var cand = hit.position;
                    if (requireFarther && Vector3.Distance(cand, playerPos) <= currentPlayerDist + 0.5f) continue;

                    var eye = cand + Vector3.up * eyeHeight.Value;
                    bool blocked = !EnemyLineOfSight.HasLineOfSight(transform, eye, Player, _playerCollider);
                    if (!blocked) continue;

                    float score = Vector3.Distance(self, cand);
                    if (requireFarther) score = -Vector3.Distance(cand, playerPos); // prefer farthest from player
                    if (score < bestScore)
                    {
                        bestScore = score;
                        result = cand;
                        found = true;
                    }
                }
            }
            return found;
        }

        // Ring-sample NavMesh points; pick the nearest walkable point WITH LOS to the player.
        private bool TryFindPeek(Vector3 self, Vector3 playerPos, out Vector3 result)
        {
            result = self;
            int n = Mathf.Max(4, ringSamples.Value);
            float range = Stats.Range;
            float best = float.MaxValue;
            bool found = false;

            for (int i = 0; i < n; i++)
            {
                float ang = (Mathf.PI * 2f / n) * i;
                float radius = range * 0.6f;
                var probe = self + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * radius;
                if (!NavMesh.SamplePosition(probe, out var hit, 3f, NavMesh.AllAreas)) continue;
                var cand = hit.position;
                var eye = cand + Vector3.up * eyeHeight.Value;
                if (!EnemyLineOfSight.HasLineOfSight(transform, eye, Player, _playerCollider)) continue;

                float d = Vector3.Distance(self, cand);
                if (d < best) { best = d; result = cand; found = true; }
            }
            return found;
        }

        private void FireStraight(Vector3 playerPos)
        {
            if (_pool == null || muzzle == null) return;
            var dir = (playerPos + Vector3.up * 0.8f) - muzzle.position;
            if (dir.sqrMagnitude < 0.0001f) dir = muzzle.forward;
            dir.Normalize();

            var go = _pool.Spawn(PoolIds.EnemyMissile, muzzle.position, Quaternion.LookRotation(dir, Vector3.up));
            if (go != null && go.TryGetComponent<EnemyMissile>(out var missile))
                missile.Initialize(Stats.Damage, dir);
        }

        private void FacePlayer(Vector3 pos, Vector3 playerPos, float dt)
        {
            var flat = new Vector3(playerPos.x - pos.x, 0f, playerPos.z - pos.z);
            if (flat.sqrMagnitude < 0.001f) return;
            var target = Quaternion.LookRotation(flat, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, faceTurnSpeed.Value * dt);
        }

        private void CachePlayerCollider()
        {
            if (_playerCollider != null || Player == null) return;
            _playerCollider = Player.GetComponent<Collider>() ?? Player.GetComponentInChildren<Collider>();
        }

        private void OnCoreDeath()
        {
            _dead = true;
            if (_nav != null) { _nav.SetSpeedMultiplier(1f); _nav.Stop(); }
        }
    }
}

using System;
using UnityEngine;
using BlastFrame.Core;
using BlastFrame.Core.Events;
using BlastFrame.Input;

namespace BlastFrame.Gameplay.Player.Movement
{
    /// <summary>
    /// Ground-initiated dash: a burst of horizontal speed in the input/look direction for
    /// dashDuration, then a cooldown. Overrides horizontal velocity ONLY while on a surface (grounded or
    /// riding a platform), so a dash-jump preserves the dash speed into the arc (dash-jump carry) and then
    /// steers/bleeds with normal air control instead of staying locked to the dash direction. Riding (not
    /// raw grounded) is used so a bobbing rock's flickering ground check doesn't chop the dash short.
    /// Raises events for the cooldown ring UI.
    /// </summary>
    public class DashModule : MonoBehaviour, IMovementModule
    {
        public int Order => MovementOrder.Dash;

        [Tooltip("Optional event raised the instant a dash starts (SFX / VFX / UI flash).")]
        [SerializeField] private GameEventSO onDashStartedEvent;

        [Tooltip("Optional float event raised with cooldown readiness 0..1 (1 = ready) for the UI ring.")]
        [SerializeField] private FloatGameEventSO onCooldownChangedEvent;

        private PlayerStats _stats;
        private IPlayerInput _input;
        private PlatformRiderModule _rider; // sibling (optional) — its IsRiding survives bobbing-rock ground flicker

        private bool _dashQueued;
        private float _dashTimer;
        private float _cooldownTimer;
        private Vector3 _dashDir;

        public bool IsDashing => _dashTimer > 0f;
        public bool IsReady => _cooldownTimer <= 0f;

        /// <summary>Cooldown readiness 0..1 (1 = ready). Mirrors onCooldownChangedEvent for direct subscribers.</summary>
        public event Action<float> OnCooldownChanged;
        public event Action OnDashStarted;

        private void Awake()
        {
            _stats = GetComponent<PlayerStats>();
            _rider = GetComponent<PlatformRiderModule>();
        }

        private void Start()
        {
            _input = ServiceLocator.Get<IPlayerInput>();
            _input.OnDashPressed += OnDashPressed;
            _input.OnDashReleased += OnDashReleased;
        }

        private void OnDestroy()
        {
            if (_input != null)
            {
                _input.OnDashPressed -= OnDashPressed;
                _input.OnDashReleased -= OnDashReleased;
            }
        }

        private void OnDashPressed() => _dashQueued = true;

        // Releasing the dash button ends the dash early (duration is a cap, not a fixed length).
        // Leaves horizontal velocity untouched so momentum bleeds out like a naturally-expired dash.
        private void OnDashReleased() => _dashTimer = 0f;

        public void Tick(ref MoveState state)
        {
            float dt = state.DeltaTime;

            // "On a surface" = truly grounded OR riding a platform. IsRiding rides through a bobbing rock's
            // ground-check flicker (it has its own grace window) and only drops the tick you jump off — so the
            // dash carries cleanly on a bobbing rock yet still releases to air control on a real dash-jump.
            bool onSurface = state.IsGrounded || (_rider != null && _rider.IsRiding);

            if (_dashQueued && IsReady && onSurface && !IsDashing)
            {
                Vector3 dir = state.WishDir.sqrMagnitude > 0.01f
                    ? state.WishDir
                    : (state.LookYaw * Vector3.forward);
                dir.y = 0f;
                _dashDir = dir.normalized;
                _dashTimer = _stats.DashDuration;
                _cooldownTimer = _stats.DashCooldown;
                onDashStartedEvent?.Raise();
                OnDashStarted?.Invoke();
            }
            _dashQueued = false;

            if (IsDashing)
            {
                // Hard-override horizontal ONLY while on a surface (grounded or riding). The instant a dash-jump
                // (or dashing off a ledge) puts the player genuinely airborne, stop asserting dash velocity —
                // the dash speed already in Velocity carries into the arc (momentum preserved) and normal air
                // control takes over, so the player can steer mid-air. Using onSurface (not raw IsGrounded)
                // means a bobbing rock's flickering ground check no longer chops the dash short.
                if (onSurface)
                {
                    Vector3 dash = _dashDir * _stats.DashSpeed;
                    state.Velocity.x = dash.x;
                    state.Velocity.z = dash.z;
                }
                _dashTimer -= dt;
            }

            if (_cooldownTimer > 0f)
            {
                _cooldownTimer -= dt;
                float readiness = 1f - Mathf.Clamp01(_cooldownTimer / Mathf.Max(0.0001f, _stats.DashCooldown));
                onCooldownChangedEvent?.Raise(readiness);
                OnCooldownChanged?.Invoke(readiness);
                if (_cooldownTimer <= 0f)
                {
                    onCooldownChangedEvent?.Raise(1f);
                    OnCooldownChanged?.Invoke(1f);
                }
            }
        }
    }
}

using System;
using UnityEngine;
using BlastFrame.Core;
using BlastFrame.Core.Events;
using BlastFrame.Input;

namespace BlastFrame.Gameplay.Player.Movement
{
    /// <summary>
    /// Ground-initiated dash: a burst of horizontal speed in the input/look direction for
    /// dashDuration, then a cooldown. Overrides only horizontal velocity, so a jump fired mid-dash
    /// keeps the dash speed into the arc (dash-jump carry). Raises events for the cooldown ring UI.
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

        private bool _dashQueued;
        private float _dashTimer;
        private float _cooldownTimer;
        private Vector3 _dashDir;

        public bool IsDashing => _dashTimer > 0f;
        public bool IsReady => _cooldownTimer <= 0f;

        /// <summary>Cooldown readiness 0..1 (1 = ready). Mirrors onCooldownChangedEvent for direct subscribers.</summary>
        public event Action<float> OnCooldownChanged;
        public event Action OnDashStarted;

        private void Awake() => _stats = GetComponent<PlayerStats>();

        private void Start()
        {
            _input = ServiceLocator.Get<IPlayerInput>();
            _input.OnDashPressed += OnDashPressed;
        }

        private void OnDestroy()
        {
            if (_input != null) _input.OnDashPressed -= OnDashPressed;
        }

        private void OnDashPressed() => _dashQueued = true;

        public void Tick(ref MoveState state)
        {
            float dt = state.DeltaTime;

            if (_dashQueued && IsReady && state.IsGrounded && !IsDashing)
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
                Vector3 dash = _dashDir * _stats.DashSpeed;
                state.Velocity.x = dash.x;
                state.Velocity.z = dash.z;
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

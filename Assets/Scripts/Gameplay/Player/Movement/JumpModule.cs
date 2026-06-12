using UnityEngine;
using BlastFrame.Core;
using BlastFrame.Input;

namespace BlastFrame.Gameplay.Player.Movement
{
    /// <summary>
    /// Variable-height jump with coyote time and a jump buffer. Only fires when grounded (or within
    /// coyote) — wall jumps are owned by WallSlideModule, so the two never conflict. Releasing jump
    /// while still rising cuts upward velocity for a short hop. Does NOT zero horizontal velocity
    /// (preserves dash-jump / platform momentum).
    /// </summary>
    public class JumpModule : MonoBehaviour, IMovementModule
    {
        public int Order => MovementOrder.Jump;

        private PlayerStats _stats;
        private IPlayerInput _input;

        private float _coyoteTimer;
        private float _bufferTimer;
        private bool _rising;       // currently in the rising phase of an active jump
        private bool _cutQueued;    // release happened while rising → cut next tick

        private void Awake() => _stats = GetComponent<PlayerStats>();

        private void Start()
        {
            _input = ServiceLocator.Get<IPlayerInput>();
            _input.OnJumpPressed += OnJumpPressed;
            _input.OnJumpReleased += OnJumpReleased;
        }

        private void OnDestroy()
        {
            if (_input == null) return;
            _input.OnJumpPressed -= OnJumpPressed;
            _input.OnJumpReleased -= OnJumpReleased;
        }

        private void OnJumpPressed() => _bufferTimer = _stats.JumpBuffer;

        private void OnJumpReleased()
        {
            if (_rising) _cutQueued = true;
        }

        public void Tick(ref MoveState state)
        {
            float dt = state.DeltaTime;

            if (state.IsGrounded) _coyoteTimer = _stats.CoyoteTime;
            else _coyoteTimer -= dt;

            _bufferTimer -= dt;

            if (_bufferTimer > 0f && _coyoteTimer > 0f)
            {
                state.Velocity.y = _stats.JumpForce;
                _bufferTimer = 0f;
                _coyoteTimer = 0f;
                _rising = true;
            }

            if (_cutQueued)
            {
                if (_rising && state.Velocity.y > 0f) state.Velocity.y *= 0.5f;
                _cutQueued = false;
            }

            if (_rising && state.Velocity.y <= 0f) _rising = false;
        }
    }
}

using UnityEngine;
using BlastFrame.Core;
using BlastFrame.Input;

namespace BlastFrame.Gameplay.Player.Movement
{
    /// <summary>
    /// When airborne and pressing into a wall, clamps fall speed to a slow slide. Jumping while
    /// wall-sliding launches up-and-away from the wall, with a brief steering lockout so the player
    /// actually leaves the wall before air control resumes. Dashing while on a wall does the same
    /// launch but with dash-speed horizontal momentum (a stronger kick). Independent of JumpModule
    /// and DashModule (only acts when on a wall and airborne, where those modules do nothing).
    /// </summary>
    public class WallSlideModule : MonoBehaviour, IMovementModule
    {
        public int Order => MovementOrder.WallSlide;

        [Tooltip("Seconds after a wall jump during which air steering is suppressed. ~0.2.")]
        [SerializeField] private float wallJumpLockout = 0.2f;

        private PlayerStats _stats;
        private IPlayerInput _input;

        private bool _sliding;
        private bool _jumpQueued;
        private bool _dashJumpQueued;
        private float _lockoutTimer;
        private Vector3 _lockoutHoriz;

        private void Awake() => _stats = GetComponent<PlayerStats>();

        private void Start()
        {
            _input = ServiceLocator.Get<IPlayerInput>();
            _input.OnJumpPressed += OnJumpPressed;
            _input.OnDashPressed += OnDashPressed;
        }

        private void OnDestroy()
        {
            if (_input == null) return;
            _input.OnJumpPressed -= OnJumpPressed;
            _input.OnDashPressed -= OnDashPressed;
        }

        private void OnJumpPressed()
        {
            if (_sliding) _jumpQueued = true;
        }

        private void OnDashPressed()
        {
            if (_sliding) _dashJumpQueued = true;
        }

        public void Tick(ref MoveState state)
        {
            float dt = state.DeltaTime;

            _sliding = !state.IsGrounded && state.IsOnWall;

            bool dashJump = _dashJumpQueued;
            bool normalJump = _jumpQueued;
            _jumpQueued = false;
            _dashJumpQueued = false;

            if ((dashJump || normalJump) && state.IsOnWall && !state.IsGrounded)
            {
                // Dash-wall-jump carries dash speed away from the wall; a plain wall jump uses the
                // lighter wall-jump push. Both launch at the same upward velocity.
                float awaySpeed = dashJump ? _stats.DashSpeed : _stats.WallJumpAway;
                Vector3 away = state.WallNormal * awaySpeed;
                state.Velocity = new Vector3(away.x, _stats.WallJumpUp, away.z);
                _lockoutTimer = wallJumpLockout;
                _lockoutHoriz = new Vector3(away.x, 0f, away.z);
                _sliding = false;
            }

            if (_lockoutTimer > 0f)
            {
                _lockoutTimer -= dt;
                state.Velocity.x = _lockoutHoriz.x;
                state.Velocity.z = _lockoutHoriz.z;
            }
            else if (_sliding && state.Velocity.y < -_stats.WallSlideSpeed)
            {
                state.Velocity.y = -_stats.WallSlideSpeed;
            }
        }
    }
}

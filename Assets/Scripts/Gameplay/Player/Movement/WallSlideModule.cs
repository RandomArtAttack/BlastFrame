using UnityEngine;
using BlastFrame.Core;
using BlastFrame.Input;

namespace BlastFrame.Gameplay.Player.Movement
{
    /// <summary>
    /// When airborne and pressing into a wall, clamps fall speed to a slow slide. Jumping while
    /// wall-sliding launches up-and-away from the wall, with a brief steering lockout so the player
    /// actually leaves the wall before air control resumes. Independent of JumpModule (only acts
    /// when on a wall and airborne, where JumpModule does nothing).
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
        private float _lockoutTimer;
        private Vector3 _lockoutHoriz;

        private void Awake() => _stats = GetComponent<PlayerStats>();

        private void Start()
        {
            _input = ServiceLocator.Get<IPlayerInput>();
            _input.OnJumpPressed += OnJumpPressed;
        }

        private void OnDestroy()
        {
            if (_input != null) _input.OnJumpPressed -= OnJumpPressed;
        }

        private void OnJumpPressed()
        {
            if (_sliding) _jumpQueued = true;
        }

        public void Tick(ref MoveState state)
        {
            float dt = state.DeltaTime;

            bool pushingIntoWall = state.IsOnWall && Vector3.Dot(state.WishDir, -state.WallNormal) > 0.1f;
            _sliding = !state.IsGrounded && pushingIntoWall;

            if (_jumpQueued && state.IsOnWall && !state.IsGrounded)
            {
                Vector3 away = state.WallNormal * _stats.WallJumpAway;
                state.Velocity = new Vector3(away.x, _stats.WallJumpUp, away.z);
                _lockoutTimer = wallJumpLockout;
                _lockoutHoriz = new Vector3(away.x, 0f, away.z);
                _sliding = false;
            }
            _jumpQueued = false;

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

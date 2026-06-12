using UnityEngine;
using BlastFrame.Core.Variables;

namespace BlastFrame.Gameplay.Player
{
    /// <summary>
    /// Tunable movement stats for the player, as Float/Int References so designers can type a
    /// constant or plug a shared Variable asset. Movement modules read these via GetComponent.
    /// </summary>
    public class PlayerStats : MonoBehaviour
    {
        [Header("Locomotion")]
        [Tooltip("Ground move speed in m/s. ~7 for a snappy platformer FPS.")]
        [SerializeField] private FloatReference moveSpeed = new FloatReference(7f);

        [Tooltip("Air steering acceleration in m/s^2. Higher = more air control. ~30.")]
        [SerializeField] private FloatReference airControl = new FloatReference(30f);

        [Tooltip("Gravity acceleration in m/s^2 (positive). ~24 for tight jumps.")]
        [SerializeField] private FloatReference gravity = new FloatReference(24f);

        [Tooltip("Horizontal air drag per second applied to carried momentum when no stick input. " +
                 "Keep LOW (~0.2) — platform/wall-jump/dash momentum must survive a full jump arc; " +
                 "2+ visibly eats inherited velocity mid-air.")]
        [SerializeField] private FloatReference airDrag = new FloatReference(0.2f);

        [Header("Jump")]
        [Tooltip("Initial upward velocity on jump in m/s. ~9 clears a 2m obstacle when held.")]
        [SerializeField] private FloatReference jumpForce = new FloatReference(9f);

        [Tooltip("Seconds after leaving ground a jump still counts (coyote time). ~0.25.")]
        [SerializeField] private FloatReference coyoteTime = new FloatReference(0.25f);

        [Tooltip("Seconds a buffered jump press persists before landing. ~0.1.")]
        [SerializeField] private FloatReference jumpBuffer = new FloatReference(0.1f);

        [Header("Dash")]
        [Tooltip("Dash speed in m/s.")]
        [SerializeField] private FloatReference dashSpeed = new FloatReference(15.4f);

        [Tooltip("Dash duration in seconds.")]
        [SerializeField] private FloatReference dashDuration = new FloatReference(0.36f);

        [Tooltip("Dash cooldown in seconds (design default 5s).")]
        [SerializeField] private FloatReference dashCooldown = new FloatReference(5f);

        [Header("Wall")]
        [Tooltip("Clamped downward slide speed while wall-sliding, m/s. ~3.")]
        [SerializeField] private FloatReference wallSlideSpeed = new FloatReference(1.5f);

        [Tooltip("Upward velocity of a wall jump, m/s. ~9.")]
        [SerializeField] private FloatReference wallJumpUp = new FloatReference(9f);

        [Tooltip("Away-from-wall velocity of a wall jump, m/s. ~8.")]
        [SerializeField] private FloatReference wallJumpAway = new FloatReference(8f);

        public float MoveSpeed => moveSpeed;
        public float AirControl => airControl;
        public float Gravity => gravity;
        public float AirDrag => airDrag;
        public float JumpForce => jumpForce;
        public float CoyoteTime => coyoteTime;
        public float JumpBuffer => jumpBuffer;
        public float DashSpeed => dashSpeed;
        public float DashDuration => dashDuration;
        public float DashCooldown => dashCooldown;
        public float WallSlideSpeed => wallSlideSpeed;
        public float WallJumpUp => wallJumpUp;
        public float WallJumpAway => wallJumpAway;
    }
}

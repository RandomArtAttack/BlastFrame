using UnityEngine;

namespace BlastFrame.Gameplay.Player.Movement
{
    /// <summary>
    /// Per-tick working state passed by reference through every movement module. Modules mutate
    /// Velocity; PlayerController reads it back and feeds the motor. This lets modules layer in
    /// independently — add a module component and it joins the pipeline automatically.
    /// </summary>
    public struct MoveState
    {
        public Vector3 Velocity;     // working velocity (m/s), mutated by modules
        public Vector2 MoveInput;    // raw planar input: x = strafe, y = forward
        public Vector3 WishDir;      // world-space desired horizontal move direction (camera-relative)
        public bool IsGrounded;
        public bool IsOnWall;
        public Vector3 WallNormal;
        public Vector3 GroundNormal;
        public float DeltaTime;
        public Quaternion LookYaw;   // camera yaw, for orienting movement/impulses
    }

    /// <summary>
    /// A composable movement behavior on the Player root. Lower Order runs earlier each tick.
    /// Modules read PlayerStats/IPlayerInput themselves in Awake/Start — never via the Inspector.
    /// </summary>
    public interface IMovementModule
    {
        int Order { get; }
        void Tick(ref MoveState state);
    }

    /// <summary>Shared module ordering so layered modules interact predictably.</summary>
    public static class MovementOrder
    {
        public const int Dash = 10;          // overrides horizontal, suppresses gravity while active
        public const int Jump = 20;          // adds vertical; does NOT zero horizontal (dash-jump carry)
        public const int WallSlide = 30;     // clamps fall speed / wall jump
        public const int PlatformRider = 40; // adds inherited platform velocity last
    }
}

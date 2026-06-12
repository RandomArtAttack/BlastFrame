using System.Collections.Generic;
using UnityEngine;
using BlastFrame.Core;
using BlastFrame.Core.Entities;
using BlastFrame.Input;
using BlastFrame.Gameplay.Player.Movement;

namespace BlastFrame.Gameplay.Player
{
    /// <summary>
    /// Orchestrates input → base locomotion → movement modules → kinematic motor each FixedUpdate.
    /// Base ground/air locomotion + gravity live here; Jump/Dash/WallSlide/PlatformRider modules
    /// layer on by implementing IMovementModule (discovered via GetComponents). Registers the
    /// player in the EntityRegistrySO so behaviors find it without Find().
    /// </summary>
    [RequireComponent(typeof(PlayerMotor), typeof(PlayerStats))]
    public class PlayerController : MonoBehaviour
    {
        [Tooltip("Runtime registry the player registers itself into so enemies/turrets can target it.")]
        [SerializeField] private EntityRegistrySO entityRegistry;

        private PlayerMotor _motor;
        private PlayerStats _stats;
        private IPlayerInput _input;
        private readonly List<IMovementModule> _modules = new List<IMovementModule>();

        private void Awake()
        {
            _motor = GetComponent<PlayerMotor>();
            _stats = GetComponent<PlayerStats>();

            GetComponents(_modules);
            _modules.Sort((a, b) => a.Order.CompareTo(b.Order));
        }

        private void OnEnable() => entityRegistry?.RegisterPlayer(transform);
        private void OnDisable() => entityRegistry?.UnregisterPlayer(transform);

        private void Start() => _input = ServiceLocator.Get<IPlayerInput>();

        private void FixedUpdate()
        {
            if (_input == null) return;
            float dt = Time.fixedDeltaTime;

            Vector2 moveInput = _input.Move;
            Vector3 wishDir = transform.right * moveInput.x + transform.forward * moveInput.y;
            wishDir.y = 0f;
            if (wishDir.sqrMagnitude > 1f) wishDir.Normalize();

            var state = new MoveState
            {
                Velocity = _motor.Velocity,
                MoveInput = moveInput,
                WishDir = wishDir,
                IsGrounded = _motor.IsGrounded,
                IsOnWall = _motor.IsOnWall,
                WallNormal = _motor.WallNormal,
                GroundNormal = _motor.GroundNormal,
                DeltaTime = dt,
                LookYaw = transform.rotation
            };

            ApplyBaseLocomotion(ref state, dt);

            for (int i = 0; i < _modules.Count; i++) _modules[i].Tick(ref state);

            _motor.Move(state.Velocity, dt);
        }

        private void ApplyBaseLocomotion(ref MoveState state, float dt)
        {
            Vector3 horiz = new Vector3(state.Velocity.x, 0f, state.Velocity.z);
            Vector3 target = state.WishDir * _stats.MoveSpeed;

            if (state.IsGrounded)
            {
                horiz = target; // snappy ground control
            }
            else
            {
                // Air: steer toward target while bleeding excess momentum (dash-jump carry).
                horiz = Vector3.MoveTowards(horiz, target, _stats.AirControl * dt);
            }

            float vy = state.Velocity.y - _stats.Gravity * dt;
            state.Velocity = new Vector3(horiz.x, vy, horiz.z);
        }
    }
}

using UnityEngine;
using UnityEngine.InputSystem;
using BlastFrame.Core;
using BlastFrame.Input;
using BlastFrame.Gameplay.Player;
using BlastFrame.Gameplay.Player.Movement;

namespace BlastFrame.Debugging
{
    /// <summary>
    /// DEBUG-only movement module: when enabled, lets the player jump in mid-air (multi-jump) so you
    /// can reach anywhere for testing. Joins the normal movement pipeline as an IMovementModule
    /// (PlayerController discovers it via GetComponents) and runs right after JumpModule. Grounded
    /// jumps still go through JumpModule untouched — this only handles the EXTRA airborne jumps, so
    /// there is no double-apply. Attach to the Player root alongside the other movement modules.
    /// </summary>
    public class PlayerTestMode : MonoBehaviour, IMovementModule
    {
        // Just after JumpModule (20) so a grounded press is consumed there and only air presses land here.
        public int Order => MovementOrder.Jump + 5;

        [Tooltip("Master switch. When ON, jump works in mid-air (multi-jump). Toggle live with the key below.")]
        [SerializeField] private bool enabledTestMode = true;

        [Tooltip("Keyboard key to toggle test mode on/off at runtime. Default F9.")]
        [SerializeField] private Key toggleKey = Key.F9;

        [Tooltip("Max air jumps before touching ground. 0 = unlimited (default). Resets on landing.")]
        [SerializeField] private int maxAirJumps = 0;

        [Tooltip("Multiplier on PlayerStats.JumpForce for air jumps. 1 = same as a normal jump.")]
        [SerializeField] private float airJumpForceMultiplier = 1f;

        private PlayerStats _stats;
        private IPlayerInput _input;

        private bool _jumpQueued;   // set on press, consumed next Tick (one-shot)
        private int _airJumpsUsed;

        private void Awake() => _stats = GetComponent<PlayerStats>();

        private void Start()
        {
            _input = ServiceLocator.Get<IPlayerInput>();
            _input.OnJumpPressed += OnJumpPressed;
        }

        private void OnDestroy()
        {
            if (_input == null) return;
            _input.OnJumpPressed -= OnJumpPressed;
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb[toggleKey].wasPressedThisFrame)
            {
                enabledTestMode = !enabledTestMode;
                UnityEngine.Debug.Log($"[PlayerTestMode] Multi-jump {(enabledTestMode ? "ENABLED" : "disabled")}");
            }
        }

        private void OnJumpPressed() => _jumpQueued = true;

        public void Tick(ref MoveState state)
        {
            bool queued = _jumpQueued;
            _jumpQueued = false; // one-shot: never carry a press into a later tick

            if (state.IsGrounded)
            {
                _airJumpsUsed = 0;   // landed — refill air jumps
                return;              // grounded jump is JumpModule's job
            }

            if (!enabledTestMode || !queued) return;
            if (maxAirJumps > 0 && _airJumpsUsed >= maxAirJumps) return;

            state.Velocity.y = _stats.JumpForce * airJumpForceMultiplier;
            _airJumpsUsed++;
        }
    }
}

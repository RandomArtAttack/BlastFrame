using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Users;

namespace BlastFrame.Input
{
    /// <summary>
    /// One player's live input. Wraps a private <see cref="InputSystem_Actions"/> instance, optionally
    /// paired to a specific set of devices (null = read every device, which preserves the original
    /// single-player behaviour). NOT a MonoBehaviour — the owning <see cref="PlayerInputHandler"/> polls
    /// it each frame via <see cref="Poll"/>. Implements <see cref="IPlayerInput"/> so any existing
    /// consumer can treat a per-player reader exactly like the old global handler.
    /// </summary>
    public sealed class PlayerInputReader : IPlayerInput, IDisposable
    {
        public int PlayerIndex { get; }

        private readonly InputSystem_Actions _actions;
        private InputUser _user;
        private bool _hasUser;
        private bool _enabled = true;

        public Vector2 Move { get; private set; }
        public Vector2 Look { get; private set; }
        public bool LookFromGamepad { get; private set; }
        public bool JumpHeld { get; private set; }
        public bool FireHeld { get; private set; }

        public event Action OnJumpPressed;
        public event Action OnJumpReleased;
        public event Action OnDashPressed;
        public event Action OnDashReleased;
        public event Action OnFirePressed;
        public event Action OnFireReleased;
        public event Action OnInteract;

        /// <param name="playerIndex">0 = P1, 1 = P2, ...</param>
        /// <param name="devices">Devices this reader is restricted to; null/empty = read all devices
        /// (single-player default — keyboard+mouse+any gamepad).</param>
        public PlayerInputReader(int playerIndex, InputDevice[] devices)
        {
            PlayerIndex = playerIndex;
            _actions = new InputSystem_Actions();
            SetDevices(devices);

            _actions.Player.Enable();
            _actions.Player.Jump.started += OnJumpStarted;
            _actions.Player.Jump.canceled += OnJumpCanceled;
            _actions.Player.Sprint.started += OnDashStarted;
            _actions.Player.Sprint.canceled += OnDashCanceled;
            _actions.Player.Attack.started += OnFireStarted;
            _actions.Player.Attack.canceled += OnFireCanceled;
            _actions.Player.Interact.started += OnInteractStarted;
        }

        /// <summary>Restrict this reader to a device set (P2 = one gamepad, P1 = keyboard+mouse once a
        /// second device joins) or pass null/empty to read every device again.</summary>
        public void SetDevices(InputDevice[] devices)
        {
            if (devices != null && devices.Length > 0)
            {
                // Pair the devices to a dedicated InputUser (Input System join-management best practice)
                // and hard-restrict this action set to exactly those devices.
                if (!_hasUser)
                {
                    _user = InputUser.PerformPairingWithDevice(devices[0]);
                    for (int i = 1; i < devices.Length; i++)
                        _user = InputUser.PerformPairingWithDevice(devices[i], _user);
                    _user.AssociateActionsWithUser(_actions);
                    _hasUser = true;
                }
                _actions.devices = devices;
            }
            else
            {
                _actions.devices = default; // null => no restriction, read all devices
            }
        }

        /// <summary>Sample continuous action values. Called once per frame by the owner.</summary>
        public void Poll()
        {
            if (!_enabled)
            {
                Move = Vector2.zero;
                Look = Vector2.zero;
                return;
            }

            Move = _actions.Player.Move.ReadValue<Vector2>();
            Look = _actions.Player.Look.ReadValue<Vector2>();

            // Route look sensitivity by the device actually driving the action this frame (a
            // connected-but-idle gamepad must not hijack mouse sensitivity). activeControl is null at
            // rest — keep the last known device in that case.
            var lookControl = _actions.Player.Look.activeControl;
            if (lookControl != null)
                LookFromGamepad = lookControl.device is Gamepad;

            JumpHeld = _actions.Player.Jump.IsPressed();
            FireHeld = _actions.Player.Attack.IsPressed();
        }

        private void OnJumpStarted(InputAction.CallbackContext _) { if (_enabled) OnJumpPressed?.Invoke(); }
        private void OnJumpCanceled(InputAction.CallbackContext _) { if (_enabled) OnJumpReleased?.Invoke(); }
        private void OnDashStarted(InputAction.CallbackContext _) { if (_enabled) OnDashPressed?.Invoke(); }
        private void OnDashCanceled(InputAction.CallbackContext _) { if (_enabled) OnDashReleased?.Invoke(); }
        private void OnFireStarted(InputAction.CallbackContext _) { if (_enabled) OnFirePressed?.Invoke(); }
        private void OnFireCanceled(InputAction.CallbackContext _) { if (_enabled) OnFireReleased?.Invoke(); }
        private void OnInteractStarted(InputAction.CallbackContext _) { if (_enabled) OnInteract?.Invoke(); }

        public void SetInputEnabled(bool enabled)
        {
            _enabled = enabled;
            JumpHeld = false;
            FireHeld = false;
        }

        public void Dispose()
        {
            _actions.Player.Jump.started -= OnJumpStarted;
            _actions.Player.Jump.canceled -= OnJumpCanceled;
            _actions.Player.Sprint.started -= OnDashStarted;
            _actions.Player.Sprint.canceled -= OnDashCanceled;
            _actions.Player.Attack.started -= OnFireStarted;
            _actions.Player.Attack.canceled -= OnFireCanceled;
            _actions.Player.Interact.started -= OnInteractStarted;
            _actions.Player.Disable();
            _actions.Dispose();

            if (_hasUser && _user.valid)
                _user.UnpairDevicesAndRemoveUser();
            _hasUser = false;
        }
    }
}

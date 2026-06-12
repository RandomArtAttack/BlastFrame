using System;
using UnityEngine;
using BlastFrame.Core;

namespace BlastFrame.Input
{
    /// <summary>Clean input contract consumed by gameplay. Nothing else touches the generated class.</summary>
    public interface IPlayerInput
    {
        Vector2 Move { get; }
        Vector2 Look { get; }
        bool LookFromGamepad { get; }
        bool JumpHeld { get; }
        bool FireHeld { get; }
        event Action OnJumpPressed;
        event Action OnJumpReleased;
        event Action OnDashPressed;
        event Action OnFirePressed;
        event Action OnFireReleased;
        event Action OnInteract;
        void SetInputEnabled(bool enabled);
    }

    /// <summary>
    /// Wraps the generated InputSystem_Actions class (Project: enable "Generate C# Class" on
    /// Assets/InputSystem_Actions.inputactions). Exposes clean C# events + value getters. Jump
    /// exposes BOTH press and release for variable jump height. Registered as IPlayerInput.
    /// Lives in the Core scene; gameplay gets it via ServiceLocator in Start (never Awake).
    /// </summary>
    public class PlayerInputHandler : MonoBehaviour, IPlayerInput
    {
        private InputSystem_Actions _actions;
        private bool _enabled = true;

        public Vector2 Move { get; private set; }
        public Vector2 Look { get; private set; }
        public bool LookFromGamepad { get; private set; }
        public bool JumpHeld { get; private set; }
        public bool FireHeld { get; private set; }

        public event Action OnJumpPressed;
        public event Action OnJumpReleased;
        public event Action OnDashPressed;
        public event Action OnFirePressed;
        public event Action OnFireReleased;
        public event Action OnInteract;

        private void Awake()
        {
            _actions = new InputSystem_Actions();
            ServiceLocator.Register<IPlayerInput>(this);
        }

        private void OnEnable()
        {
            if (_actions == null) return;
            _actions.Player.Enable();

            _actions.Player.Jump.started += OnJumpStarted;
            _actions.Player.Jump.canceled += OnJumpCanceled;
            _actions.Player.Sprint.started += OnDashStarted;
            _actions.Player.Attack.started += OnFireStarted;
            _actions.Player.Attack.canceled += OnFireCanceled;
            _actions.Player.Interact.started += OnInteractStarted;
        }

        private void OnDisable()
        {
            if (_actions == null) return;
            _actions.Player.Jump.started -= OnJumpStarted;
            _actions.Player.Jump.canceled -= OnJumpCanceled;
            _actions.Player.Sprint.started -= OnDashStarted;
            _actions.Player.Attack.started -= OnFireStarted;
            _actions.Player.Attack.canceled -= OnFireCanceled;
            _actions.Player.Interact.started -= OnInteractStarted;
            _actions.Player.Disable();
        }

        private void OnDestroy() => ServiceLocator.Unregister<IPlayerInput>(this);

        private void Update()
        {
            if (!_enabled)
            {
                Move = Vector2.zero;
                Look = Vector2.zero;
                return;
            }
            Move = _actions.Player.Move.ReadValue<Vector2>();
            Look = _actions.Player.Look.ReadValue<Vector2>();

            // Route look sensitivity by the device actually driving the action this frame
            // (a connected-but-idle gamepad must NOT hijack mouse sensitivity). activeControl is
            // null when the action is at rest — keep the last known device in that case.
            var lookControl = _actions.Player.Look.activeControl;
            if (lookControl != null)
                LookFromGamepad = lookControl.device is UnityEngine.InputSystem.Gamepad;

            JumpHeld = _actions.Player.Jump.IsPressed();
            FireHeld = _actions.Player.Attack.IsPressed();
        }

        private void OnJumpStarted(UnityEngine.InputSystem.InputAction.CallbackContext _) { if (_enabled) OnJumpPressed?.Invoke(); }
        private void OnJumpCanceled(UnityEngine.InputSystem.InputAction.CallbackContext _) { if (_enabled) OnJumpReleased?.Invoke(); }
        private void OnDashStarted(UnityEngine.InputSystem.InputAction.CallbackContext _) { if (_enabled) OnDashPressed?.Invoke(); }
        private void OnFireStarted(UnityEngine.InputSystem.InputAction.CallbackContext _) { if (_enabled) OnFirePressed?.Invoke(); }
        private void OnFireCanceled(UnityEngine.InputSystem.InputAction.CallbackContext _) { if (_enabled) OnFireReleased?.Invoke(); }
        private void OnInteractStarted(UnityEngine.InputSystem.InputAction.CallbackContext _) { if (_enabled) OnInteract?.Invoke(); }

        public void SetInputEnabled(bool enabled)
        {
            _enabled = enabled;
            JumpHeld = false;
            FireHeld = false;
        }
    }
}

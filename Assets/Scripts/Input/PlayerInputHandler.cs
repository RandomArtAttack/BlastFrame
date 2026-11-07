using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
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
        event Action OnDashReleased;
        event Action OnFirePressed;
        event Action OnFireReleased;
        event Action OnInteract;
        void SetInputEnabled(bool enabled);
    }

    /// <summary>
    /// Player-index-aware input source. Hands out the <see cref="IPlayerInput"/> for player N. P1 (index
    /// 0) is always present and is ALSO registered as the global <see cref="IPlayerInput"/> service for
    /// back-compat; local co-op adds P2 (index 1) at runtime via <see cref="AddPlayer"/>.
    /// </summary>
    public interface IPlayerInputProvider
    {
        int PlayerCount { get; }
        IPlayerInput GetInput(int playerIndex);
        int AddPlayer(InputDevice[] devices);
        void RemovePlayer(int playerIndex);
        void SetPlayerDevices(int playerIndex, InputDevice[] devices);
    }

    /// <summary>
    /// Owns one <see cref="PlayerInputReader"/> per local player and polls them each frame. Registers
    /// reader 0 (P1) as the global <see cref="IPlayerInput"/> service (so every existing consumer keeps
    /// working unchanged) and registers itself as <see cref="IPlayerInputProvider"/> so co-op can add a
    /// second player. Lives in the Core scene; gameplay resolves its per-player input via
    /// <see cref="PlayerInputResolver"/> in Start (never Awake).
    /// </summary>
    public class PlayerInputHandler : MonoBehaviour, IPlayerInputProvider
    {
        private readonly List<PlayerInputReader> _readers = new List<PlayerInputReader>();

        public int PlayerCount => _readers.Count;

        private void Awake()
        {
            // P1 with null devices = reads all devices (keyboard+mouse+any gamepad) — original behaviour.
            var p1 = new PlayerInputReader(0, null);
            _readers.Add(p1);
            ServiceLocator.Register<IPlayerInput>(p1);
            ServiceLocator.Register<IPlayerInputProvider>(this);
        }

        private void Update()
        {
            for (int i = 0; i < _readers.Count; i++) _readers[i].Poll();
        }

        private void OnDestroy()
        {
            if (_readers.Count > 0) ServiceLocator.Unregister<IPlayerInput>(_readers[0]);
            ServiceLocator.Unregister<IPlayerInputProvider>(this);
            for (int i = 0; i < _readers.Count; i++) _readers[i].Dispose();
            _readers.Clear();
        }

        public IPlayerInput GetInput(int playerIndex)
            => (playerIndex >= 0 && playerIndex < _readers.Count) ? _readers[playerIndex] : null;

        public int AddPlayer(InputDevice[] devices)
        {
            int index = _readers.Count;
            _readers.Add(new PlayerInputReader(index, devices));
            return index;
        }

        public void RemovePlayer(int playerIndex)
        {
            // P1 is permanent; only additional players are removable.
            if (playerIndex <= 0 || playerIndex >= _readers.Count) return;
            _readers[playerIndex].Dispose();
            _readers.RemoveAt(playerIndex);
        }

        public void SetPlayerDevices(int playerIndex, InputDevice[] devices)
        {
            if (playerIndex < 0 || playerIndex >= _readers.Count) return;
            _readers[playerIndex].SetDevices(devices);
        }
    }
}

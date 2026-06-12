using System;
using UnityEngine;
using BlastFrame.Core;
using BlastFrame.Core.Variables;
using BlastFrame.Input;

namespace BlastFrame.Gameplay.Weapons
{
    /// <summary>
    /// Tracks fire-button charge state (0..1) and exposes it for the HUD and PlayerShooter.
    /// Sits on the player Camera alongside PlayerShooter. Gets IPlayerInput from ServiceLocator
    /// in Start, subscribes to OnFirePressed / OnFireReleased, and ramps Charge01 over chargeTime
    /// while the fire button is held. On release, raises OnReleased with the final charge value and
    /// resets to 0. PlayerShooter subscribes to OnReleased and handles the actual spawn.
    /// </summary>
    public class ChargeShot : MonoBehaviour, IChargeReadout
    {
        [Tooltip("Seconds to go from 0 to full charge (1.0). 0.8 – 1.5 is a good range.")]
        [SerializeField] private FloatReference chargeTime = new FloatReference(1f);

        // IChargeReadout
        public float Charge01 { get; private set; }
        public event Action<float> OnChargeChanged;

        /// <summary>Raised on fire-button release with the charge at that moment (0..1).
        /// PlayerShooter subscribes to this to fire the projectile.</summary>
        public event Action<float> OnReleased;

        private IPlayerInput _input;
        private bool _charging;

        private void Start()
        {
            _input = ServiceLocator.Get<IPlayerInput>();
            _input.OnFirePressed += HandleFirePressed;
            _input.OnFireReleased += HandleFireReleased;
        }

        private void OnDestroy()
        {
            if (_input == null) return;
            _input.OnFirePressed -= HandleFirePressed;
            _input.OnFireReleased -= HandleFireReleased;
        }

        private void Update()
        {
            if (!_charging) return;

            float dt = Time.deltaTime;
            float rate = chargeTime.Value > 0f ? dt / chargeTime.Value : 1f;
            float prev = Charge01;
            Charge01 = Mathf.Min(1f, Charge01 + rate);

            // Raise only when the value actually changed.
            if (!Mathf.Approximately(Charge01, prev))
                OnChargeChanged?.Invoke(Charge01);
        }

        private void HandleFirePressed()
        {
            _charging = true;
            SetCharge(0f);
        }

        private void HandleFireReleased()
        {
            _charging = false;
            float fired = Charge01;
            SetCharge(0f);
            OnReleased?.Invoke(fired);
        }

        private void SetCharge(float value)
        {
            Charge01 = value;
            OnChargeChanged?.Invoke(Charge01);
        }
    }
}

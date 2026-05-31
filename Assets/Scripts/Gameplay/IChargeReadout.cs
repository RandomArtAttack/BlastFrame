using System;

namespace BlastFrame.Gameplay
{
    /// <summary>
    /// Read-only charge state for the HUD charge bar. ChargeShot implements it; ChargeBarUI finds
    /// it on the player via the registry and subscribes — no direct dependency on the weapon type.
    /// </summary>
    public interface IChargeReadout
    {
        /// <summary>Current charge, 0..1.</summary>
        float Charge01 { get; }

        /// <summary>Raised whenever charge changes (0..1).</summary>
        event Action<float> OnChargeChanged;
    }
}

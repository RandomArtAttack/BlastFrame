using UnityEngine;

namespace BlastFrame.Gameplay.Platforms
{
    /// <summary>
    /// A platform the player can ride. Returns the velocity to ADD to a grounded rider this tick
    /// (moving/rotating carry, treadmill push). Implemented by concrete platform components.
    /// </summary>
    public interface IRidePlatform
    {
        Vector3 SampleRideVelocity(Vector3 riderPosition);
    }

    /// <summary>A platform that launches the player on contact (SpringBoard), overriding velocity.</summary>
    public interface ILaunchPlatform
    {
        bool TryConsumeLaunch(out Vector3 launchVelocity);
    }
}

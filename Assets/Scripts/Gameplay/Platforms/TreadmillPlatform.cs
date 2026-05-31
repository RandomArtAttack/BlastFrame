using UnityEngine;
using BlastFrame.Core.Variables;

namespace BlastFrame.Gameplay.Platforms
{
    /// <summary>
    /// A static platform surface that pushes anything standing on it in a configured direction.
    /// SampleRideVelocity returns pushDirection * force each tick, which PlatformRiderModule
    /// adds to the grounded player's velocity.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class TreadmillPlatform : MonoBehaviour, IRidePlatform
    {
        [Tooltip("World-space direction to push the rider. Automatically normalised at runtime. " +
                 "E.g. (1,0,0) pushes along +X, (-1,0,0) pushes along -X.")]
        [SerializeField] private Vector3 pushDirection = Vector3.forward;

        [Tooltip("Push speed in metres per second added to the rider per tick. " +
                 "Use a constant or wire a FloatVariable asset.")]
        [SerializeField] private FloatReference force = new FloatReference(4f);

        // IRidePlatform — returns the treadmill push velocity each tick.
        public Vector3 SampleRideVelocity(Vector3 riderPosition)
        {
            Vector3 dir = pushDirection.sqrMagnitude > 0f ? pushDirection.normalized : Vector3.forward;
            return dir * force.Value;
        }
    }
}

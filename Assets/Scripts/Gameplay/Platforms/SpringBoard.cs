using UnityEngine;
using BlastFrame.Core.Variables;

namespace BlastFrame.Gameplay.Platforms
{
    /// <summary>
    /// Launches the player (or any ILaunchPlatform consumer) when they land on this surface.
    /// TryConsumeLaunch fires once per landing and resets only after the rider leaves, so the
    /// player is launched once per contact rather than every physics tick.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class SpringBoard : MonoBehaviour, ILaunchPlatform
    {
        [Tooltip("World-space direction to launch the rider. Automatically normalised at runtime. " +
                 "E.g. (0,1,0) launches straight up.")]
        [SerializeField] private Vector3 launchDirection = Vector3.up;

        [Tooltip("Launch speed in metres per second. Use a constant or wire a FloatVariable asset.")]
        [SerializeField] private FloatReference force = new FloatReference(12f);

        // True once consumed this contact; cleared when the rider leaves the trigger/collider.
        private bool _consumed;
        private float _lastProbeTime = float.NegativeInfinity;

        // ILaunchPlatform — called by PlatformRiderModule each tick it probes this collider.
        public bool TryConsumeLaunch(out Vector3 launchVelocity)
        {
            // Re-arm when the rider's probe skipped at least one physics tick: the contact ended. The exit
            // callbacks below never fire for the kinematic player against a static board (no contact pair),
            // which made every springboard single-use.
            if (Time.fixedTime - _lastProbeTime > Time.fixedDeltaTime * 1.5f) _consumed = false;
            _lastProbeTime = Time.fixedTime;

            if (_consumed)
            {
                launchVelocity = Vector3.zero;
                return false;
            }

            _consumed = true;
            Vector3 dir = launchDirection.sqrMagnitude > 0f ? launchDirection.normalized : Vector3.up;
            launchVelocity = dir * force.Value;
            return true;
        }

        // Reset debounce when the player moves far enough away that the probe no longer hits us.
        // We use OnCollisionExit (kinematic probe is a SphereCast, not a trigger) to detect separation.
        // Because the player uses a kinematic Rigidbody sweeping against our collider, we get
        // OnCollisionExit when they leave. If the Collider is set to Is Trigger, use OnTriggerExit.
        private void OnCollisionExit(Collision other)
        {
            _consumed = false;
        }

        private void OnTriggerExit(Collider other)
        {
            _consumed = false;
        }
    }
}

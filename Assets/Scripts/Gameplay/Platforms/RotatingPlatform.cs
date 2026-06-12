using UnityEngine;
using BlastFrame.Core.Variables;

namespace BlastFrame.Gameplay.Platforms
{
    /// <summary>
    /// Kinematic Rigidbody platform that rotates about a configurable local axis at a constant
    /// angular rate. While the player is grounded on it, SampleRideVelocity returns the
    /// instantaneous linear tangential velocity at the rider's contact point (omega x r), which
    /// the PlatformRiderModule adds to the player so they're carried along and inherit that
    /// velocity on jump.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    public sealed class RotatingPlatform : MonoBehaviour, IRidePlatform
    {
        [Tooltip("Local axis to rotate around. (0,1,0) = spin about Y, (1,0,0) = tip about X, etc.")]
        [SerializeField] private Vector3 rotationAxis = Vector3.up;

        [Tooltip("Rotation speed in degrees per second (positive = counter-clockwise by right-hand rule). " +
                 "Use a constant or wire a FloatVariable asset.")]
        [SerializeField] private FloatReference degreesPerSecond = new FloatReference(45f);

        private Rigidbody _rb;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.isKinematic = true;
            _rb.useGravity = false;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
        }

        private void FixedUpdate()
        {
            float angle = degreesPerSecond.Value * Time.fixedDeltaTime;
            Quaternion delta = Quaternion.AngleAxis(angle, transform.TransformDirection(rotationAxis.normalized));
            _rb.MoveRotation(delta * _rb.rotation);
        }

        // IRidePlatform — returns tangential linear velocity v = omega × r
        // omega is the world-space angular velocity vector; r is from platform centre to rider.
        public Vector3 SampleRideVelocity(Vector3 riderPosition)
        {
            Vector3 worldAxis = transform.TransformDirection(rotationAxis.normalized);
            float radiansPerSec = degreesPerSecond.Value * Mathf.Deg2Rad;
            Vector3 omega = worldAxis * radiansPerSec;
            Vector3 r = riderPosition - _rb.position;
            return Vector3.Cross(omega, r);
        }
    }
}

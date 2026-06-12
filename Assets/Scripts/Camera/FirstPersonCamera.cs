using UnityEngine;
using BlastFrame.Core;
using BlastFrame.Core.Variables;
using BlastFrame.Input;

namespace BlastFrame.CameraRig
{
    /// <summary>
    /// Direct mouse/right-stick first-person look (no Cinemachine). Yaw rotates the player body
    /// (this component sits on the camera but rotates its parent root for yaw), pitch rotates the
    /// camera locally with a clamp. Reads look from IPlayerInput. Cache this camera elsewhere —
    /// never Camera.main at runtime.
    /// </summary>
    public class FirstPersonCamera : MonoBehaviour
    {
        [Tooltip("The body transform to yaw (the Player root). Auto-resolved to the parent if null.")]
        [SerializeField] private Transform body;

        [Tooltip("Look sensitivity multiplier. ~0.1 for mouse delta scaling.")]
        [SerializeField] private FloatReference sensitivity = new FloatReference(0.1f);

        [Tooltip("Minimum pitch (look down) in degrees.")]
        [SerializeField] private float minPitch = -85f;

        [Tooltip("Maximum pitch (look up) in degrees.")]
        [SerializeField] private float maxPitch = 85f;

        [Tooltip("Invert vertical look.")]
        [SerializeField] private bool invertY;

        private IPlayerInput _input;
        private float _pitch;

        private void Awake()
        {
            if (body == null && transform.parent != null) body = transform.parent;
        }

        private void Start() => _input = ServiceLocator.Get<IPlayerInput>();

        private void LateUpdate()
        {
            if (_input == null) return;
            Vector2 look = _input.Look * sensitivity;

            if (body != null) body.Rotate(Vector3.up, look.x, Space.Self);

            _pitch += invertY ? look.y : -look.y;
            _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);
            transform.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }
    }
}

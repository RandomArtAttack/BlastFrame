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

        [Tooltip("Look sensitivity multiplier for mouse (pixel delta). ~0.1.")]
        [SerializeField] private FloatReference sensitivity = new FloatReference(0.1f);

        [Tooltip("Degrees of rotation per frame at full stick deflection for controller. ~3 = 180°/s at 60fps. Tune up/down in Inspector.")]
        [SerializeField] private FloatReference controllerSensitivity = new FloatReference(2.4f);

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
            Vector2 look = _input.LookFromGamepad
                ? _input.Look * (float)controllerSensitivity
                : _input.Look * (float)sensitivity;

            if (body != null) body.Rotate(Vector3.up, look.x, Space.Self);

            _pitch += invertY ? look.y : -look.y;
            _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);
            transform.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }
    }
}

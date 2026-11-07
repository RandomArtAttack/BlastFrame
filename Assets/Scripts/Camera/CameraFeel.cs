using UnityEngine;
using BlastFrame.Core.Variables;
using BlastFrame.Gameplay.Player;
using BlastFrame.Gameplay.Player.Movement;

namespace BlastFrame.CameraRig
{
    /// <summary>
    /// Camera "juice" layer for the first-person rig: dash FOV kick, speed FOV, landing dip,
    /// wall-slide lean and strafe roll. Driven entirely by existing motor/module state read via
    /// GetComponentInParent — no mechanics changes, no Inspector cross-object wiring. Every effect
    /// magnitude is a FloatReference; setting it to 0 disables that effect (the camera then behaves
    /// exactly as before this component existed).
    ///
    /// Composition: runs LAST in LateUpdate (DefaultExecutionOrder 100) so it layers AFTER
    /// FirstPersonCamera (which owns pitch/yaw) and CameraShake (which owns positional shake). Roll
    /// is post-multiplied onto FirstPersonCamera's look rotation (never written into its look state);
    /// the landing dip is added on top of whatever position CameraShake left. FOV is applied to this
    /// camera only — the stacked overlay gun camera keeps its constant FOV during a dash by design.
    /// NO head-bob: it fights precision platforming and is intentionally omitted.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [DefaultExecutionOrder(100)]
    public class CameraFeel : MonoBehaviour
    {
        [Header("Dash FOV kick")]
        [Tooltip("Degrees of FOV added while a dash is held. 0 disables. ~12.")]
        [SerializeField] private FloatReference dashFovKick = new FloatReference(12f);

        [Tooltip("Seconds to ramp the dash FOV kick IN on dash start. ~0.06.")]
        [SerializeField] private FloatReference dashFovRiseTime = new FloatReference(0.06f);

        [Tooltip("Seconds to decay the dash FOV kick back OUT on dash end. ~0.2.")]
        [SerializeField] private FloatReference dashFovDecayTime = new FloatReference(0.2f);

        [Header("Speed FOV")]
        [Tooltip("Max additive FOV from moving faster than run speed (keeps dash-jump chains feeling " +
                 "fast after the dash ends). 0 disables. ~4.")]
        [SerializeField] private FloatReference speedFovMax = new FloatReference(4f);

        [Tooltip("Horizontal speed ABOVE run speed (m/s) that maps to the full speed-FOV bonus. ~8.")]
        [SerializeField] private FloatReference speedFovRange = new FloatReference(8f);

        [Tooltip("Smoothing time (seconds) for the speed-FOV bonus so it doesn't pop. ~0.1.")]
        [SerializeField] private FloatReference speedFovSmoothTime = new FloatReference(0.1f);

        [Header("Landing dip")]
        [Tooltip("Max downward camera dip (metres) on the hardest landing. 0 disables. ~0.25.")]
        [SerializeField] private FloatReference landDipMax = new FloatReference(0.25f);

        [Tooltip("Impact fall speed (m/s) below which NO dip happens (normal hops don't dip). ~6.")]
        [SerializeField] private FloatReference landDipDeadZone = new FloatReference(6f);

        [Tooltip("Impact fall speed (m/s) that produces the full dip. ~18.")]
        [SerializeField] private FloatReference landDipFullSpeed = new FloatReference(18f);

        [Tooltip("Spring frequency (Hz) for the critically-damped dip recovery. ~8.")]
        [SerializeField] private FloatReference landDipSpringHz = new FloatReference(8f);

        [Header("Wall-slide lean")]
        [Tooltip("Degrees of camera roll away from the wall while wall-sliding. 0 disables. ~5.")]
        [SerializeField] private FloatReference wallLeanDegrees = new FloatReference(5f);

        [Tooltip("Smoothing time (seconds) for the wall lean in/out. ~0.15.")]
        [SerializeField] private FloatReference wallLeanSmoothTime = new FloatReference(0.15f);

        [Header("Strafe roll")]
        [Tooltip("Degrees of subtle camera roll opposite lateral movement while grounded. 0 disables. ~1.5.")]
        [SerializeField] private FloatReference strafeRollDegrees = new FloatReference(1.5f);

        [Tooltip("Smoothing time (seconds) for the strafe roll. ~0.12.")]
        [SerializeField] private FloatReference strafeRollSmoothTime = new FloatReference(0.12f);

        private Camera _cam;
        private PlayerMotor _motor;
        private DashModule _dash;
        private WallSlideModule _wallSlide;
        private PlayerStats _stats;
        private CameraShake _shake;

        private float _baseFov;
        private Vector3 _restLocalPos;

        // Dash FOV kick state.
        private float _dashFov;
        // Speed FOV state.
        private float _speedFov;
        private float _speedFovVel;
        // Landing dip spring state (displacement + velocity, metres).
        private float _dip;
        private float _dipVel;
        private bool _wasGrounded;
        private float _prevAirVelY;
        // Roll state (degrees).
        private float _wallLean;
        private float _strafeRoll;

        private void Awake()
        {
            _cam = GetComponent<Camera>();
            _motor = GetComponentInParent<PlayerMotor>();
            _dash = GetComponentInParent<DashModule>();
            _wallSlide = GetComponentInParent<WallSlideModule>();
            _stats = GetComponentInParent<PlayerStats>();
            _shake = GetComponent<CameraShake>();
        }

        private void Start()
        {
            _baseFov = _cam.fieldOfView;
            _restLocalPos = transform.localPosition;
            _wasGrounded = _motor != null && _motor.IsGrounded;
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            UpdateFov(dt);
            UpdateDip(dt);
            UpdateRoll(dt);

            Apply();
        }

        private void UpdateFov(float dt)
        {
            // Dash kick: hold while dashing, decay otherwise, with separate rise/decay time constants.
            float dashTarget = (_dash != null && _dash.IsDashing) ? (float)dashFovKick : 0f;
            float dashTau = dashTarget > _dashFov ? (float)dashFovRiseTime : (float)dashFovDecayTime;
            _dashFov = ExpApproach(_dashFov, dashTarget, dashTau, dt);

            // Speed FOV: additive, scaled by horizontal speed above run speed.
            float speedTarget = 0f;
            if (_motor != null && _stats != null && (float)speedFovMax != 0f)
            {
                Vector3 v = _motor.Velocity;
                float horiz = new Vector2(v.x, v.z).magnitude;
                float excess = horiz - _stats.MoveSpeed;
                float range = Mathf.Max(0.01f, (float)speedFovRange);
                speedTarget = Mathf.Clamp01(excess / range) * (float)speedFovMax;
            }
            _speedFov = SmoothDampScalar(_speedFov, speedTarget, ref _speedFovVel, (float)speedFovSmoothTime, dt);
        }

        private void UpdateDip(float dt)
        {
            bool grounded = _motor != null && _motor.IsGrounded;

            // Rising edge: airborne -> grounded. Inject a downward displacement scaled by impact speed.
            if (grounded && !_wasGrounded && (float)landDipMax != 0f)
            {
                float impact = Mathf.Max(0f, -_prevAirVelY); // fall speed at the last airborne tick
                float dead = (float)landDipDeadZone;
                float full = Mathf.Max(dead + 0.01f, (float)landDipFullSpeed);
                if (impact > dead)
                {
                    float t = Mathf.Clamp01((impact - dead) / (full - dead));
                    _dip = -t * (float)landDipMax; // negative = down
                    _dipVel = 0f;
                }
            }

            if (!grounded) _prevAirVelY = _motor != null ? _motor.Velocity.y : 0f;
            _wasGrounded = grounded;

            // Critically-damped spring back to 0.
            float hz = Mathf.Max(0f, (float)landDipSpringHz);
            if (hz > 0f && (_dip != 0f || _dipVel != 0f))
            {
                float omega = 2f * Mathf.PI * hz;
                float accel = -(omega * omega) * _dip - 2f * omega * _dipVel;
                _dipVel += accel * dt;
                _dip += _dipVel * dt;
                if (Mathf.Abs(_dip) < 0.0001f && Mathf.Abs(_dipVel) < 0.0001f)
                {
                    _dip = 0f;
                    _dipVel = 0f;
                }
            }
            else
            {
                _dip = 0f;
                _dipVel = 0f;
            }
        }

        private void UpdateRoll(float dt)
        {
            // Wall-slide lean: roll away from the wall while sliding.
            float leanTarget = 0f;
            if (_motor != null && _wallSlide != null && _wallSlide.IsSliding && (float)wallLeanDegrees != 0f)
            {
                // WallNormal points out of the wall toward the player. Roll so the top of the view
                // tilts away from the wall (i.e. toward the side the wall is on).
                float side = Vector3.Dot(_motor.WallNormal, transform.right); // >0: wall to the left, <0: wall to the right
                leanTarget = Mathf.Sign(side) * (float)wallLeanDegrees;
                if (side == 0f) leanTarget = 0f;
            }
            _wallLean = ExpApproach(_wallLean, leanTarget, (float)wallLeanSmoothTime, dt);

            // Strafe roll: subtle roll opposite lateral movement while grounded.
            float strafeTarget = 0f;
            if (_motor != null && _stats != null && _motor.IsGrounded && (float)strafeRollDegrees != 0f)
            {
                float lateral = Vector3.Dot(_motor.Velocity, transform.right);
                float lateralN = Mathf.Clamp(lateral / Mathf.Max(0.01f, _stats.MoveSpeed), -1f, 1f);
                strafeTarget = -lateralN * (float)strafeRollDegrees;
            }
            _strafeRoll = ExpApproach(_strafeRoll, strafeTarget, (float)strafeRollSmoothTime, dt);
        }

        private void Apply()
        {
            _cam.fieldOfView = _baseFov + _dashFov + _speedFov;

            // Roll: post-multiply onto FirstPersonCamera's look rotation (it re-sets pure pitch each
            // LateUpdate before this runs, so this never accumulates).
            float roll = _wallLean + _strafeRoll;
            if (roll != 0f)
                transform.localRotation = transform.localRotation * Quaternion.Euler(0f, 0f, roll);

            // Dip: add on top of the current local position. If CameraShake is present it overwrites
            // localPosition to base+shake each frame (so a plain add never drifts); if absent we own
            // the position and reconstruct it from the captured rest pose.
            if (_shake != null)
                transform.localPosition += Vector3.up * _dip;
            else
                transform.localPosition = _restLocalPos + Vector3.up * _dip;
        }

        /// <summary>Framerate-independent exponential approach toward target with time-constant tau
        /// (seconds). tau <= 0 snaps.</summary>
        private static float ExpApproach(float current, float target, float tau, float dt)
        {
            if (tau <= 0f) return target;
            float k = 1f - Mathf.Exp(-dt / tau);
            return current + (target - current) * k;
        }

        /// <summary>Critically-damped smoothing (Unity SmoothDamp) for a scalar. smoothTime <= 0 snaps.</summary>
        private static float SmoothDampScalar(float current, float target, ref float vel, float smoothTime, float dt)
        {
            if (smoothTime <= 0f) { vel = 0f; return target; }
            return Mathf.SmoothDamp(current, target, ref vel, smoothTime, Mathf.Infinity, dt);
        }
    }
}

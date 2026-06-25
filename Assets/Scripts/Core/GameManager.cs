using UnityEngine;

namespace BlastFrame.Core
{
    /// <summary>
    /// Application-level settings owner in the Core scene: frame rate, cursor lock. Not a singleton
    /// and not DontDestroyOnLoad — it lives in Core, which is always loaded.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        [Tooltip("Target frame rate for the game. 60 = capped at 60 FPS; -1 = platform default. " +
                 "Ignored while VSync is on (the display refresh rate caps the frame rate instead).")]
        [SerializeField] private int targetFrameRate = 60;

        [Tooltip("VSync: 0 = off (use targetFrameRate cap instead), 1 = sync to every display refresh, " +
                 "2 = every second refresh. VSync is the only real tearing fix; if look feels laggy on " +
                 "a TV, enable the TV's Game Mode before blaming VSync.")]
        [SerializeField] private int vSyncCount = 1;

        [Tooltip("Align the physics step to the display refresh rate at startup so the fixed timestep " +
                 "divides evenly into render frames. This kills the classic 50Hz-physics vs 60Hz-render " +
                 "'beat' judder that VSync exposes (some render frames get a physics step, some don't).")]
        [SerializeField] private bool alignPhysicsToRefreshRate = true;

        [Tooltip("Lowest physics rate (Hz) to settle on when aligning. The largest exact integer divisor " +
                 "of the refresh rate that stays at or above this is chosen, e.g. 144Hz -> 72Hz, 240Hz -> " +
                 "60Hz, 60Hz -> 60Hz. Keeps physics in a sane range while still dividing render evenly.")]
        [SerializeField] private float minPhysicsRate = 50f;

        [Tooltip("Lock and hide the cursor on start (gameplay). Disable for menu-only scenes.")]
        [SerializeField] private bool lockCursorOnStart = true;

        private void Awake()
        {
            QualitySettings.vSyncCount = vSyncCount;
            // When VSync is on, let the refresh rate cap frames; targetFrameRate only applies uncapped.
            Application.targetFrameRate = vSyncCount == 0 ? targetFrameRate : -1;

            if (alignPhysicsToRefreshRate) AlignPhysicsToRefreshRate();
        }

        /// <summary>
        /// Sets Time.fixedDeltaTime so the physics rate is an exact integer divisor of the display
        /// refresh rate. With VSync on, render frames are spaced at 1/refresh; a physics rate that
        /// evenly divides it means every render frame contains the same whole number of physics steps,
        /// which removes the FixedUpdate cadence judder (rigidbody interpolation then stays smooth).
        /// </summary>
        private void AlignPhysicsToRefreshRate()
        {
            double refresh = Screen.currentResolution.refreshRateRatio.value;
            if (refresh < 1.0) refresh = 60.0; // some platforms report 0/garbage — fall back to 60Hz.

            // Largest N such that refresh / N >= minPhysicsRate, i.e. the highest divisor that still
            // keeps physics at or above the floor. 60/1=60, 144/2=72, 240/4=60.
            int divisor = Mathf.Max(1, Mathf.FloorToInt((float)(refresh / Mathf.Max(1f, minPhysicsRate))));
            float physicsRate = (float)(refresh / divisor);
            Time.fixedDeltaTime = 1f / physicsRate;
        }

        private void Start()
        {
            if (lockCursorOnStart)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }
}

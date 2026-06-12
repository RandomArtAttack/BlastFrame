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

        [Tooltip("Lock and hide the cursor on start (gameplay). Disable for menu-only scenes.")]
        [SerializeField] private bool lockCursorOnStart = true;

        private void Awake()
        {
            QualitySettings.vSyncCount = vSyncCount;
            // When VSync is on, let the refresh rate cap frames; targetFrameRate only applies uncapped.
            Application.targetFrameRate = vSyncCount == 0 ? targetFrameRate : -1;
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

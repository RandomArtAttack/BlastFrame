using UnityEngine;

namespace BlastFrame.Core
{
    /// <summary>
    /// Application-level settings owner in the Core scene: frame rate, cursor lock. Not a singleton
    /// and not DontDestroyOnLoad — it lives in Core, which is always loaded.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        [Tooltip("Target frame rate for the game. 144 for high-refresh play; -1 = platform default.")]
        [SerializeField] private int targetFrameRate = 144;

        [Tooltip("Lock and hide the cursor on start (gameplay). Disable for menu-only scenes.")]
        [SerializeField] private bool lockCursorOnStart = true;

        private void Awake()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = targetFrameRate;
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

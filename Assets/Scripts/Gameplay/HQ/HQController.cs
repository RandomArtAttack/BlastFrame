using UnityEngine;
using BlastFrame.Core;
using BlastFrame.Core.Events;
using BlastFrame.Core.Services;

namespace BlastFrame.Gameplay.HQ
{
    /// <summary>
    /// Light orchestrator for the HQ scene. Exposes a run-start hook (called by the run-start
    /// portal UI button) that hands off to IRunManager. Holds a reference to the permanent
    /// upgrade registry for HQ-scene systems that need it (e.g. future intro cinematics).
    /// Does not register as a service — it is a scene-local orchestrator only.
    /// </summary>
    public class HQController : MonoBehaviour
    {
        [Tooltip("All permanent upgrades available in this run's shop. Populated by Fix019 / the Inspector.")]
        [SerializeField] private PermanentUpgradeRegistrySO upgradeRegistry;

        [Tooltip("Difficulty to use when starting a run. Shown in a UI picker; defaulted to Easy for prototyping.")]
        [SerializeField] private Difficulty startDifficulty = Difficulty.Easy;

        [Tooltip("Level index (0-8) to start the run on. Typically the player's highest unlocked level.")]
        [SerializeField] private int startLevelIndex;

        [Tooltip("Optional: GameEventSO raised when the player triggers run-start from HQ.")]
        [SerializeField] private GameEventSO onRunStartRequested;

        /// <summary>Registry of available permanent upgrades — readable by other HQ-scene components.</summary>
        public PermanentUpgradeRegistrySO UpgradeRegistry => upgradeRegistry;

        /// <summary>
        /// Called by the run-start portal (UI button or trigger volume OnInteract).
        /// Delegates to IRunManager — HQController never knows how a run is set up.
        /// </summary>
        public void RequestRunStart()
        {
            if (!ServiceLocator.TryGet<IRunManager>(out var runManager))
            {
                Debug.LogWarning("[HQController] IRunManager not registered — cannot start run.");
                return;
            }

            onRunStartRequested?.Raise();
            runManager.StartRun(startLevelIndex, startDifficulty);
        }

        /// <summary>Set difficulty from UI (e.g. a difficulty-picker dropdown).</summary>
        public void SetDifficulty(Difficulty difficulty) => startDifficulty = difficulty;

        /// <summary>Set level index from UI (e.g. a level-select panel).</summary>
        public void SetLevelIndex(int levelIndex) => startLevelIndex = levelIndex;
    }
}

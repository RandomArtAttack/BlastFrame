using UnityEngine;
using BlastFrame.Core;
using BlastFrame.Core.Events;
using BlastFrame.Core.Services;

namespace BlastFrame.Gameplay.Levels
{
    /// <summary>
    /// Owns the active run: tracks whether a run is in progress, the chosen difficulty, and the
    /// current level/room indices. Implements <see cref="IRunManager"/> so other systems can reach
    /// it via <c>ServiceLocator.Get&lt;IRunManager&gt;()</c> without a direct reference.
    ///
    /// Registers as IRunManager in Awake. Acquires optional services (IGameStateMachine,
    /// ISceneLoader, ICurrencyManager) in Start via TryGet so that missing services during
    /// prototyping do not crash the game.
    ///
    /// All GameEventSO fields are null-safe — assign the assets you need; leave the rest empty
    /// while features are still being built.
    /// </summary>
    public sealed class RunManager : MonoBehaviour, IRunManager
    {
        // ---- Events -------------------------------------------------------------------------

        [Tooltip("Raised when a new run is started via StartRun(). Wire a GameEventSO asset here. " +
                 "Listeners: AudioManager (music swap), HUD, LevelController.")]
        [SerializeField] private GameEventSO _onRunStarted;

        [Tooltip("Raised when the player dies and returns to HQ via EndRun(true). " +
                 "Listeners: HQ scene loader, death screen UI, AudioManager.")]
        [SerializeField] private GameEventSO _onReturnedToHQ;

        [Tooltip("Raised when all rooms in the current level are cleared and the boss is defeated. " +
                 "Listeners: RunManager (internally advances level), HQ, AudioManager.")]
        [SerializeField] private GameEventSO _onLevelCleared;

        // ---- Runtime state ------------------------------------------------------------------

        private bool _runActive;
        private Difficulty _difficulty;
        private int _currentLevelIndex;
        private int _currentRoomIndex;

        // Cached optional service references (resolved in Start).
        private IGameStateMachine _stateMachine;
        private ISceneLoader _sceneLoader;
        private ICurrencyManager _currencyManager;

        // ---- IRunManager interface ----------------------------------------------------------

        /// <inheritdoc/>
        public bool RunActive => _runActive;

        /// <inheritdoc/>
        public Difficulty Difficulty => _difficulty;

        /// <inheritdoc/>
        public int CurrentLevelIndex => _currentLevelIndex;

        /// <inheritdoc/>
        public int CurrentRoomIndex => _currentRoomIndex;

        // ---- Lifecycle ----------------------------------------------------------------------

        private void Awake()
        {
            ServiceLocator.Register<IRunManager>(this);
        }

        private void Start()
        {
            // Optional services — TryGet so the game boots even if these systems aren't built yet.
            ServiceLocator.TryGet(out _stateMachine);
            ServiceLocator.TryGet(out _sceneLoader);
            ServiceLocator.TryGet(out _currencyManager);
        }

        private void OnDestroy()
        {
            ServiceLocator.Unregister<IRunManager>(this);
        }

        // ---- IRunManager implementation ----------------------------------------------------

        /// <summary>
        /// Starts a new run at the given level with the chosen difficulty. Transitions game state
        /// to Run and raises onRunStarted. Safe to call while no run is active.
        /// </summary>
        public void StartRun(int levelIndex, Difficulty difficulty)
        {
            _runActive = true;
            _currentLevelIndex = levelIndex;
            _currentRoomIndex = 0;
            _difficulty = difficulty;

            _stateMachine?.TransitionTo(GameState.Run);
            _onRunStarted?.Raise();

            Debug.Log($"[RunManager] Run started — Level {levelIndex}, Difficulty {difficulty}.");
        }

        /// <summary>
        /// Ends the current run. If <paramref name="died"/> is true, transitions to Death state,
        /// raises onReturnedToHQ, and attempts to load the HQ scene additively (guarded — HQ scene
        /// may not exist yet during prototyping). If false, treat as a clean level completion path
        /// (call CompleteLevelInternal for the win flow instead).
        /// </summary>
        public void EndRun(bool died)
        {
            if (!_runActive)
            {
                Debug.LogWarning("[RunManager] EndRun called but no run is active.");
                return;
            }

            _runActive = false;

            if (died)
            {
                _stateMachine?.TransitionTo(GameState.Death);
                _onReturnedToHQ?.Raise();
                ReturnToHQAsync();
            }
            else
            {
                // Non-death ending (e.g. game-over win condition) — callers drive the flow via events.
                Debug.Log("[RunManager] Run ended cleanly (not died).");
            }
        }

        // ---- Room/Level helpers ------------------------------------------------------------

        /// <summary>
        /// Advances to the next room in the current level. LevelController calls this after
        /// confirming the room is cleared (via event or direct call).
        /// </summary>
        public void AdvanceRoom()
        {
            if (!_runActive) return;
            _currentRoomIndex++;
            Debug.Log($"[RunManager] Advanced to room {_currentRoomIndex} in level {_currentLevelIndex}.");
        }

        /// <summary>
        /// Marks the current level as complete, raises onLevelCleared, then advances the level
        /// index. The caller is responsible for actually loading the next level scene.
        /// </summary>
        public void CompleteLevel()
        {
            if (!_runActive) return;
            _onLevelCleared?.Raise();
            _currentLevelIndex++;
            _currentRoomIndex = 0;
            Debug.Log($"[RunManager] Level complete. Next level index: {_currentLevelIndex}.");
        }

        // ---- Private helpers ---------------------------------------------------------------

        private async void ReturnToHQAsync()
        {
            if (_sceneLoader == null)
            {
                Debug.LogWarning("[RunManager] ISceneLoader not available — cannot load HQ scene. " +
                                 "Implement SceneLoader or run the feature when ready.");
                return;
            }

            if (_sceneLoader.IsLoaded(SceneNames.HQ))
            {
                // HQ already loaded (e.g. still in memory); nothing to do.
                return;
            }

            try
            {
                await _sceneLoader.LoadAdditiveAsync(SceneNames.HQ);
                Debug.Log("[RunManager] HQ scene loaded additively after death.");
            }
            catch (System.Exception ex)
            {
                // HQ scene may not exist yet during early development — log a warning, don't crash.
                Debug.LogWarning($"[RunManager] Could not load HQ scene '{SceneNames.HQ}': {ex.Message}");
            }
        }
    }
}

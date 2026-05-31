using UnityEngine;
using BlastFrame.Core;
using BlastFrame.Core.Events;
using BlastFrame.Core.Services;

namespace BlastFrame.Gameplay.Levels
{
    /// <summary>
    /// Lives in a level scene (not Core). Given a <see cref="LevelDefinitionSO"/>, drives room
    /// progression within that level: tracks which room is active, advances on a cleared signal,
    /// and raises level-wide events. Does not reference Room feature types directly to avoid
    /// cross-feature compile dependencies — room progression is communicated via GameEventSO.
    ///
    /// Listens to <see cref="_onEnemiesCleared"/> (raised by a room-level enemy counter or
    /// trigger volume) to know when the current room is done. When all rooms are cleared it raises
    /// <see cref="_onLevelCleared"/> and calls RunManager.CompleteLevel() if available.
    ///
    /// Prototype-simple: no boss/mini-boss phase wiring in this class — those are separate
    /// GameObjects in the level scene that raise their own events.
    /// </summary>
    public sealed class LevelController : MonoBehaviour
    {
        [Tooltip("The LevelDefinitionSO asset for this scene. Provides levelIndex, roomCount, and " +
                 "difficulty-scaling values. Must match the index used by RunManager.")]
        [SerializeField] private LevelDefinitionSO _levelDefinition;

        [Tooltip("GameEventSO raised by an enemy-counter or trigger volume in a room when all " +
                 "enemies are defeated and the room objective is met. LevelController listens to " +
                 "this to advance room index. Leave null to advance rooms manually via AdvanceRoom().")]
        [SerializeField] private GameEventSO _onEnemiesCleared;

        [Tooltip("GameEventSO raised by this LevelController when the current room index changes " +
                 "(i.e. a room has been cleared and the next one begins). " +
                 "Listeners: UI, audio, room setup logic.")]
        [SerializeField] private GameEventSO _onRoomCleared;

        [Tooltip("GameEventSO raised when all rooms in this level are cleared. " +
                 "Listeners: RunManager (via CompleteLevel), HQ transition logic, AudioManager.")]
        [SerializeField] private GameEventSO _onLevelCleared;

        // ---- Runtime state ------------------------------------------------------------------

        private int _currentRoomIndex;

        // ---- Properties ---------------------------------------------------------------------

        /// <summary>Zero-based index of the room currently active in this level.</summary>
        public int CurrentRoomIndex => _currentRoomIndex;

        /// <summary>The LevelDefinitionSO assigned to this controller.</summary>
        public LevelDefinitionSO LevelDefinition => _levelDefinition;

        // ---- Lifecycle ----------------------------------------------------------------------

        private void Start()
        {
            if (_levelDefinition == null)
            {
                Debug.LogError("[LevelController] No LevelDefinitionSO assigned — level will not function correctly.", this);
                return;
            }

            _currentRoomIndex = 0;

            if (_onEnemiesCleared != null)
                _onEnemiesCleared.Register(OnEnemiesCleared);

            Debug.Log($"[LevelController] Level '{_levelDefinition.DisplayName}' (index {_levelDefinition.LevelIndex}) started. " +
                      $"Room count: {_levelDefinition.RoomCount}.");
        }

        private void OnDestroy()
        {
            if (_onEnemiesCleared != null)
                _onEnemiesCleared.Unregister(OnEnemiesCleared);
        }

        // ---- Public API --------------------------------------------------------------------

        /// <summary>
        /// Manually advances to the next room. Can be called from editor tooling or test scripts
        /// when no automatic enemies-cleared event is wired.
        /// </summary>
        public void AdvanceRoom()
        {
            HandleRoomCleared();
        }

        // ---- Private logic -----------------------------------------------------------------

        private void OnEnemiesCleared()
        {
            HandleRoomCleared();
        }

        private void HandleRoomCleared()
        {
            if (_levelDefinition == null) return;

            _onRoomCleared?.Raise();

            _currentRoomIndex++;

            if (_currentRoomIndex >= _levelDefinition.RoomCount)
            {
                // All rooms done — notify the run layer.
                _onLevelCleared?.Raise();

                if (ServiceLocator.TryGet<IRunManager>(out var runManager))
                    runManager.CompleteLevel();

                Debug.Log($"[LevelController] All {_levelDefinition.RoomCount} rooms cleared in level '{_levelDefinition.DisplayName}'.");
            }
            else
            {
                Debug.Log($"[LevelController] Room cleared. Advancing to room index {_currentRoomIndex} " +
                          $"of {_levelDefinition.RoomCount} in '{_levelDefinition.DisplayName}'.");
            }
        }
    }
}

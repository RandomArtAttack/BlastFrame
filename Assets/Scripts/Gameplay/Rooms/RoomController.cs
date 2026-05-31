using System.Collections.Generic;
using UnityEngine;
using BlastFrame.Core.Events;

namespace BlastFrame.Gameplay.Rooms
{
    /// <summary>
    /// A room slot in a level scene. Holds exactly 3 <see cref="RoomVariantSO"/> references and
    /// a child SpawnAnchor Transform. On initialization it uses <see cref="RoomVariantSelector"/>
    /// to deterministically pick one variant (based on run seed + room index) and instantiates
    /// that variant's prefab at the anchor.
    ///
    /// LevelController calls <see cref="Initialize"/> with the run seed before the room is entered.
    /// If no external driver calls Initialize, the fallback in Start uses the serialized
    /// <see cref="_fallbackSeed"/> so the room still works in isolation (e.g. test play).
    ///
    /// No per-room bespoke code — all layout variability is purely prefab + SO driven.
    /// </summary>
    public sealed class RoomController : MonoBehaviour
    {
        [Tooltip("Exactly 3 RoomVariantSO assets (one per variant). The selector picks one per run. " +
                 "Validation will warn if the count is not exactly 3.")]
        [SerializeField] private List<RoomVariantSO> _variants = new List<RoomVariantSO>();

        [Tooltip("Child Transform used as the position/rotation anchor when the chosen variant prefab " +
                 "is instantiated. Must be a direct child of this GameObject (self-contained).")]
        [SerializeField] private Transform _spawnAnchor;

        [Tooltip("Seed used when LevelController has not called Initialize() before Start fires. " +
                 "Useful for testing a specific variant in isolation. 0 = use constant 0 as seed.")]
        [SerializeField] private int _fallbackSeed = 0;

        [Tooltip("Zero-based index of this room slot within its level. Used together with the run " +
                 "seed to make each room's variant pick independent from other rooms.")]
        [SerializeField] private int _roomIndex = 0;

        [Tooltip("Optional event raised when this room is cleared (all enemies defeated, objective met). " +
                 "Wire a GameEventSO asset here; leave null if no listeners need to know.")]
        [SerializeField] private GameEventSO _onRoomCleared;

        private GameObject _spawnedVariant;
        private bool _initialized;

        /// <summary>
        /// Called by LevelController (or another external driver) before the room becomes active.
        /// Picks and instantiates the variant. Idempotent — subsequent calls are ignored.
        /// </summary>
        /// <param name="seed">The run's integer seed, unique per run.</param>
        public void Initialize(int seed)
        {
            if (_initialized) return;
            SpawnVariant(seed);
        }

        /// <summary>
        /// Raises the onRoomCleared event. Call this from room-completion logic (e.g. an enemy
        /// counter component, a trigger volume) when all objectives in the room are met.
        /// </summary>
        public void NotifyRoomCleared()
        {
            _onRoomCleared?.Raise();
        }

        private void Start()
        {
            // Fallback: if LevelController hasn't called Initialize yet, self-initialize so the
            // room works in standalone test play.
            if (!_initialized)
                SpawnVariant(_fallbackSeed);
        }

        private void SpawnVariant(int seed)
        {
            _initialized = true;

            if (!ValidateVariants()) return;

            var chosen = RoomVariantSelector.Pick(_variants, seed, _roomIndex);
            if (chosen == null)
            {
                Debug.LogError($"[RoomController] '{name}': RoomVariantSelector returned null.", this);
                return;
            }

            if (chosen.VariantPrefab == null)
            {
                Debug.LogError($"[RoomController] '{name}': Selected variant '{chosen.Id}' has no prefab assigned.", this);
                return;
            }

            Transform anchor = _spawnAnchor != null ? _spawnAnchor : transform;
            _spawnedVariant = Instantiate(chosen.VariantPrefab, anchor.position, anchor.rotation);
            _spawnedVariant.name = $"{chosen.Id}_Instance";

            Debug.Log($"[RoomController] '{name}' spawned variant '{chosen.Id}' (seed={seed}, roomIndex={_roomIndex}).");
        }

        private bool ValidateVariants()
        {
            if (_variants == null || _variants.Count == 0)
            {
                Debug.LogError($"[RoomController] '{name}': No variants assigned.", this);
                return false;
            }

            if (_variants.Count != 3)
            {
                Debug.LogWarning($"[RoomController] '{name}': Expected exactly 3 variants, found {_variants.Count}. " +
                                 "Selection still works but designer intent is 3-per-slot.", this);
            }

            for (int i = 0; i < _variants.Count; i++)
            {
                if (_variants[i] == null)
                {
                    Debug.LogError($"[RoomController] '{name}': Variant at index {i} is null.", this);
                    return false;
                }
            }

            return true;
        }

        private void OnValidate()
        {
            if (_variants != null && _variants.Count != 3)
                Debug.LogWarning($"[RoomController] '{name}': Should have exactly 3 variants (currently {_variants.Count}).", this);
        }

        private void OnDestroy()
        {
            if (_spawnedVariant != null)
                Destroy(_spawnedVariant);
        }
    }
}

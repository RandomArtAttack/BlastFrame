using UnityEngine;

namespace BlastFrame.Gameplay.Rooms
{
    /// <summary>
    /// Designer-authored recipe for a single room layout variant. Holds the prefab that is
    /// instantiated into the scene when this variant is selected for a run. The prefab must be
    /// fully self-contained: geometry, hazards, enemy spawn points, and any hidden secrets
    /// are all authored inside it — no external scene object wiring allowed.
    /// </summary>
    [CreateAssetMenu(fileName = "NewRoomVariant", menuName = "Blast Frame/Rooms/Room Variant")]
    public sealed class RoomVariantSO : ScriptableObject
    {
        [Tooltip("Unique string identifier for this variant. Must not change once authored — " +
                 "used by seeded selection logic and future save state. Example: 'Level01_Room02_VariantA'.")]
        [SerializeField] private string _id;

        [Tooltip("The self-contained room layout prefab to instantiate at the RoomController's spawn anchor. " +
                 "Must contain all geometry, hazards, enemy spawn points, and secrets. No references to other scene objects.")]
        [SerializeField] private GameObject _variantPrefab;

        /// <summary>Unique identifier for this variant.</summary>
        public string Id => _id;

        /// <summary>The self-contained prefab instantiated when this variant is selected.</summary>
        public GameObject VariantPrefab => _variantPrefab;
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace BlastFrame.Gameplay.Areas
{
    /// <summary>Every area in the game, so saved ids resolve to definitions and the effect graph can be derived.
    /// One asset project-wide (Assets/ScriptableObjects/Areas/AreaRegistry).</summary>
    [CreateAssetMenu(fileName = "AreaRegistry", menuName = "Blast Frame/Areas/Area Registry")]
    public sealed class AreaRegistrySO : ScriptableObject
    {
        [Tooltip("All AreaDefinitionSO assets (9 for the full game). Order is the HQ display order.")]
        [SerializeField] private List<AreaDefinitionSO> areas = new List<AreaDefinitionSO>();

        public IReadOnlyList<AreaDefinitionSO> Areas => areas;

        public AreaDefinitionSO Find(string id)
        {
            for (int i = 0; i < areas.Count; i++)
                if (areas[i] != null && areas[i].Id == id) return areas[i];
            return null;
        }

        /// <summary>Convenience: derive current world state from a completed-id set.</summary>
        public AreaWorldState Derive(ICollection<string> completedIds) => AreaGraph.Derive(areas, completedIds);

#if UNITY_EDITOR
        public void EditorSetAreas(List<AreaDefinitionSO> list) => areas = list;
#endif
    }
}

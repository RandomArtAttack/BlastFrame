using System;
using System.Collections.Generic;
using UnityEngine;

namespace BlastFrame.Core.Seeding
{
    [Serializable]
    public class WeightedEntry<T> where T : UnityEngine.Object
    {
        [Tooltip("The thing this entry picks (room layout, enemy definition, variant profile…).")]
        public T item;

        [Tooltip("Relative chance. 2 is twice as likely as 1; 0 disables the entry without deleting it.")]
        [Min(0f)] public float weight = 1f;
    }

    /// <summary>
    /// DESIGN.md §6: one reusable seeded weighted roll for every layer of variability (room layout, spawn
    /// choice, enemy variant). Give it a roll from <see cref="ISeedService"/>; the same roll always returns the
    /// same entry. Subclass once per item type (Unity cannot create assets of an open generic type).
    /// </summary>
    public abstract class VariantSetSO<T> : ScriptableObject where T : UnityEngine.Object
    {
        [Tooltip("Weighted entries. Weights are relative; entries with weight 0 or no item are never picked.")]
        [SerializeField] private List<WeightedEntry<T>> entries = new List<WeightedEntry<T>>();

        public IReadOnlyList<WeightedEntry<T>> Entries => entries;

        /// <summary>Deterministic weighted pick. Null if nothing is pickable.</summary>
        public T Pick(uint roll)
        {
            float total = 0f;
            for (int i = 0; i < entries.Count; i++)
                if (Pickable(entries[i])) total += entries[i].weight;
            if (total <= 0f) return null;

            float target = SeedService.Value01From(roll) * total;
            T last = null;
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (!Pickable(e)) continue;
                last = e.item;
                target -= e.weight;
                if (target < 0f) return e.item;
            }
            return last; // float round-off on the final entry
        }

        public T Pick(ISeedService seeds, string areaId, int roomIndex, string slotId) =>
            Pick(seeds.Roll(areaId, roomIndex, slotId));

        private static bool Pickable(WeightedEntry<T> e) => e != null && e.item != null && e.weight > 0f;

#if UNITY_EDITOR
        /// <summary>Editor/test hook to author entries without an Inspector.</summary>
        public void EditorSetEntries(List<WeightedEntry<T>> list) => entries = list;
#endif
    }
}

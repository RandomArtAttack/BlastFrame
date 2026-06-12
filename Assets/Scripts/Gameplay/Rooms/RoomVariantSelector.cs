using System.Collections.Generic;
using UnityEngine;

namespace BlastFrame.Gameplay.Rooms
{
    /// <summary>
    /// Stateless utility that deterministically picks one <see cref="RoomVariantSO"/> from a list
    /// given a run seed and a room index. The same seed + roomIndex pair always returns the same
    /// variant, so a run is fully reproducible from its seed alone.
    ///
    /// No MonoBehaviour: this is a pure static helper. RoomController calls it on initialization.
    /// </summary>
    public static class RoomVariantSelector
    {
        /// <summary>
        /// Deterministically selects one variant from <paramref name="variants"/> using a seeded
        /// hash of <paramref name="seed"/> and <paramref name="roomIndex"/>.
        ///
        /// The hash mixes the seed and room index so each room in a run gets a statistically
        /// independent draw without needing a shared Random state. Identical inputs always
        /// produce identical output — no side effects, no shared state.
        /// </summary>
        /// <param name="variants">Pool of variants to choose from. Must be non-null and non-empty.</param>
        /// <param name="seed">Run seed (integer, assigned at run start).</param>
        /// <param name="roomIndex">Zero-based index of this room slot within the level.</param>
        /// <returns>The selected <see cref="RoomVariantSO"/>.</returns>
        public static RoomVariantSO Pick(IReadOnlyList<RoomVariantSO> variants, int seed, int roomIndex)
        {
            if (variants == null || variants.Count == 0)
            {
                Debug.LogError("[RoomVariantSelector] variants list is null or empty.");
                return null;
            }

            // Mix seed and roomIndex to get a per-room deterministic hash.
            // Using the Wang hash style to spread bits across the int range.
            int hash = seed ^ unchecked(roomIndex * (int)2654435761u);
            hash ^= hash >> 16;
            hash *= unchecked((int)0x45d9f3b);
            hash ^= hash >> 16;

            int index = Mathf.Abs(hash) % variants.Count;
            return variants[index];
        }
    }
}

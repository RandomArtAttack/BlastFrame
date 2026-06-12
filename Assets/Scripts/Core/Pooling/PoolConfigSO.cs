using System.Collections.Generic;
using UnityEngine;
using BlastFrame.Core.Entities;

namespace BlastFrame.Core.Pooling
{
    /// <summary>Per-prefab pool sizing. Pool sizes are never hardcoded — they live here.</summary>
    [CreateAssetMenu(fileName = "PoolConfig", menuName = "Blast Frame/Pooling/Pool Config")]
    public class PoolConfigSO : ScriptableObject
    {
        [System.Serializable]
        public class PoolEntry
        {
            [Tooltip("Entity definition whose prefab this pool spawns. Its Id is the pool key.")]
            public EntityDefinitionSO definition;

            [Tooltip("Number of instances to pre-warm at scene load.")]
            public int prewarmCount = 16;

            [Tooltip("How many extra instances to add when the pool is exhausted.")]
            public int expandIncrement = 8;
        }

        [Tooltip("Every pool to pre-warm at boot. One entry per pooled prefab.")]
        [SerializeField] private List<PoolEntry> entries = new List<PoolEntry>();

        public IReadOnlyList<PoolEntry> Entries => entries;
    }
}

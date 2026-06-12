using System.Collections.Generic;
using UnityEngine;
using BlastFrame.Core.Services;

namespace BlastFrame.Core.Pooling
{
    /// <summary>
    /// Builds and owns every pool from a PoolConfigSO, pre-warmed at scene load. Registers itself
    /// as IPoolManager. Spawn by pool id; pooled objects return themselves via Despawn(gameObject).
    /// </summary>
    public class PoolManager : MonoBehaviour, IPoolManager
    {
        [Tooltip("Pool sizing config — one entry per pooled prefab. Assign the project PoolConfig asset.")]
        [SerializeField] private PoolConfigSO config;

        private readonly Dictionary<string, IPool> _pools = new Dictionary<string, IPool>();

        private void Awake()
        {
            ServiceLocator.Register<IPoolManager>(this);
            BuildPools();
        }

        private void OnDestroy() => ServiceLocator.Unregister<IPoolManager>(this);

        private void BuildPools()
        {
            if (config == null) return;
            foreach (var entry in config.Entries)
            {
                if (entry?.definition?.Prefab == null) continue;
                var id = entry.definition.Id;
                if (_pools.ContainsKey(id)) continue;
                var holder = new GameObject($"Pool_{id}").transform;
                holder.SetParent(transform, false);
                _pools[id] = new Pool<Transform>(entry.definition.Prefab, holder, id, entry.prewarmCount, entry.expandIncrement);
            }
        }

        public GameObject Spawn(string poolId, Vector3 position, Quaternion rotation)
        {
            if (_pools.TryGetValue(poolId, out var pool)) return pool.Get(position, rotation);
            Debug.LogError($"[PoolManager] No pool registered for id '{poolId}'.");
            return null;
        }

        public void Despawn(GameObject instance)
        {
            if (instance == null) return;
            if (instance.TryGetComponent<PooledMarker>(out var marker) && _pools.TryGetValue(marker.PoolId, out var pool))
            {
                pool.Return(instance);
                return;
            }
            Debug.LogWarning($"[PoolManager] Despawn called on non-pooled object '{instance.name}' — destroying instead.");
            Destroy(instance);
        }
    }
}

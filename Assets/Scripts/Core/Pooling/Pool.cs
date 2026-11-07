using System.Collections.Generic;
using UnityEngine;

namespace BlastFrame.Core.Pooling
{
    /// <summary>Non-generic facade so the manager can store heterogeneous pools in one dictionary.</summary>
    public interface IPool
    {
        GameObject Get(Vector3 position, Quaternion rotation);
        void Return(GameObject instance);
    }

    /// <summary>
    /// Single generic, reusable pool — NOT a per-object-type implementation. Pre-warms at
    /// construction; expands by a configured increment and logs a warning when exhausted; never
    /// silently drops objects. Every pooled object's IPoolable hooks fire on spawn/despawn.
    /// </summary>
    public class Pool<T> : IPool where T : Component
    {
        private readonly GameObject _prefab;
        private readonly Transform _parent;
        private readonly string _poolId;
        private readonly int _expandIncrement;
        private readonly Stack<GameObject> _available = new Stack<GameObject>();
        private readonly HashSet<GameObject> _inPool = new HashSet<GameObject>();
        private readonly Dictionary<GameObject, IPoolable[]> _hooks = new Dictionary<GameObject, IPoolable[]>();

        public Pool(GameObject prefab, Transform parent, string poolId, int prewarm, int expandIncrement)
        {
            _prefab = prefab;
            _parent = parent;
            _poolId = poolId;
            _expandIncrement = Mathf.Max(1, expandIncrement);
            Expand(Mathf.Max(0, prewarm));
        }

        private void Expand(int count)
        {
            for (int i = 0; i < count; i++)
            {
                var instance = Object.Instantiate(_prefab, _parent);
                if (!instance.TryGetComponent<PooledMarker>(out var marker)) marker = instance.AddComponent<PooledMarker>();
                marker.PoolId = _poolId;
                instance.SetActive(false);
                _available.Push(instance);
                _inPool.Add(instance);
                _hooks[instance] = instance.GetComponentsInChildren<IPoolable>(true); // cached: no per-spawn array
            }
        }

        public GameObject Get(Vector3 position, Quaternion rotation)
        {
            if (_available.Count == 0)
            {
                Debug.LogWarning($"[Pool:{_poolId}] exhausted — expanding by {_expandIncrement}.");
                Expand(_expandIncrement);
            }

            var instance = _available.Pop();
            _inPool.Remove(instance);
            instance.transform.SetPositionAndRotation(position, rotation);
            instance.SetActive(true);

            var poolables = Hooks(instance);
            for (int i = 0; i < poolables.Length; i++) poolables[i].OnSpawn();

            return instance;
        }

        public void Return(GameObject instance)
        {
            if (instance == null) return;
            if (!_inPool.Add(instance))
            {
                // Already back in the pool (e.g. two hits despawned it in one tick). Pushing it again would
                // hand the same object out twice.
                Debug.LogWarning($"[Pool:{_poolId}] '{instance.name}' returned twice — ignored.");
                return;
            }

            var poolables = Hooks(instance);
            for (int i = 0; i < poolables.Length; i++) poolables[i].OnDespawn();

            instance.SetActive(false);
            instance.transform.SetParent(_parent, false);
            _available.Push(instance);
        }

        private IPoolable[] Hooks(GameObject instance)
        {
            if (!_hooks.TryGetValue(instance, out var hooks))
                _hooks[instance] = hooks = instance.GetComponentsInChildren<IPoolable>(true);
            return hooks;
        }
    }

    /// <summary>Stamped on each pooled instance so the manager can route Despawn back to its pool.</summary>
    public class PooledMarker : MonoBehaviour
    {
        [System.NonSerialized] public string PoolId;
    }
}

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
                var marker = instance.GetComponent<PooledMarker>() ?? instance.AddComponent<PooledMarker>();
                marker.PoolId = _poolId;
                instance.SetActive(false);
                _available.Push(instance);
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
            instance.transform.SetPositionAndRotation(position, rotation);
            instance.SetActive(true);

            var poolables = instance.GetComponentsInChildren<IPoolable>(true);
            for (int i = 0; i < poolables.Length; i++) poolables[i].OnSpawn();

            return instance;
        }

        public void Return(GameObject instance)
        {
            if (instance == null) return;

            var poolables = instance.GetComponentsInChildren<IPoolable>(true);
            for (int i = 0; i < poolables.Length; i++) poolables[i].OnDespawn();

            instance.SetActive(false);
            instance.transform.SetParent(_parent, false);
            _available.Push(instance);
        }
    }

    /// <summary>Stamped on each pooled instance so the manager can route Despawn back to its pool.</summary>
    public class PooledMarker : MonoBehaviour
    {
        [System.NonSerialized] public string PoolId;
    }
}

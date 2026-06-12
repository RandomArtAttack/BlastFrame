using System.Collections.Generic;
using UnityEngine;

namespace BlastFrame.Core.Entities
{
    /// <summary>
    /// Runtime set of active entities, replacing FindObjectsOfType. The player registers itself
    /// here; behaviors acquire the player via PlayerTransform instead of Find(). Never store
    /// persistent state — this is a live, runtime-populated list cleared between runs.
    /// </summary>
    [CreateAssetMenu(fileName = "EntityRegistry", menuName = "Blast Frame/Entities/Entity Registry")]
    public class EntityRegistrySO : ScriptableObject
    {
        private readonly List<Transform> _enemies = new List<Transform>();

        [System.NonSerialized] private Transform _player;

        public Transform PlayerTransform => _player;
        public bool HasPlayer => _player != null;
        public IReadOnlyList<Transform> Enemies => _enemies;

        public void RegisterPlayer(Transform player) => _player = player;

        public void UnregisterPlayer(Transform player)
        {
            if (_player == player) _player = null;
        }

        public void RegisterEnemy(Transform enemy)
        {
            if (!_enemies.Contains(enemy)) _enemies.Add(enemy);
        }

        public void UnregisterEnemy(Transform enemy) => _enemies.Remove(enemy);

        private void OnDisable()
        {
            // Clear runtime state when assets unload (play-mode exit) so nothing leaks into edit mode.
            _player = null;
            _enemies.Clear();
        }
    }
}

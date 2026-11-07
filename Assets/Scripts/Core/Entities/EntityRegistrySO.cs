using System.Collections.Generic;
using UnityEngine;

namespace BlastFrame.Core.Entities
{
    /// <summary>
    /// Runtime set of active entities, replacing FindObjectsOfType. Players register themselves here;
    /// behaviors acquire a player via PlayerTransform / GetNearestPlayer instead of Find(). Supports
    /// N local players (co-op): PlayerTransform stays the FIRST-registered player (P1) for back-compat,
    /// while GetNearestPlayer picks the closest live player for enemy targeting. Never store persistent
    /// state — this is a live, runtime-populated list cleared between runs.
    /// </summary>
    [CreateAssetMenu(fileName = "EntityRegistry", menuName = "Blast Frame/Entities/Entity Registry")]
    public class EntityRegistrySO : ScriptableObject
    {
        private readonly List<Transform> _enemies = new List<Transform>();

        [System.NonSerialized] private readonly List<Transform> _players = new List<Transform>();

        /// <summary>The first-registered player (P1). Back-compat: single-player systems read this.</summary>
        public Transform PlayerTransform => _players.Count > 0 ? _players[0] : null;
        public bool HasPlayer => _players.Count > 0;
        public IReadOnlyList<Transform> Players => _players;
        public IReadOnlyList<Transform> Enemies => _enemies;

        public void RegisterPlayer(Transform player)
        {
            if (player != null && !_players.Contains(player)) _players.Add(player);
        }

        public void UnregisterPlayer(Transform player) => _players.Remove(player);

        /// <summary>Nearest live player to a world point (≤2 entries in co-op — a trivial loop).
        /// Skips destroyed entries. Returns null when no player is registered.</summary>
        public Transform GetNearestPlayer(Vector3 from)
        {
            Transform nearest = null;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < _players.Count; i++)
            {
                Transform p = _players[i];
                if (p == null) continue;
                float sqr = (p.position - from).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    nearest = p;
                }
            }
            return nearest;
        }

        public void RegisterEnemy(Transform enemy)
        {
            if (!_enemies.Contains(enemy)) _enemies.Add(enemy);
        }

        public void UnregisterEnemy(Transform enemy) => _enemies.Remove(enemy);

        private void OnDisable()
        {
            // Clear runtime state when assets unload (play-mode exit) so nothing leaks into edit mode.
            _players.Clear();
            _enemies.Clear();
        }
    }
}

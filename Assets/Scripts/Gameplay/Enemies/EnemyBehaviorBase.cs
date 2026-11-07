using UnityEngine;
using BlastFrame.Core.Entities;

namespace BlastFrame.Gameplay.Enemies
{
    /// <summary>
    /// Base for composable enemy behaviors. Caches sibling EnemyStats / EnemyCore and the
    /// EntityRegistry, and resolves the player target from the registry (never Find()). Behaviors
    /// react to EnemyCore events and read EnemyStats — they never reference each other.
    /// </summary>
    [RequireComponent(typeof(EnemyCore), typeof(EnemyStats))]
    public abstract class EnemyBehaviorBase : MonoBehaviour
    {
        [Tooltip("Registry used to find the player target at runtime.")]
        [SerializeField] protected EntityRegistrySO entityRegistry;

        protected EnemyStats Stats { get; private set; }
        protected EnemyCore Core { get; private set; }

        // Nearest live player to THIS enemy (co-op targets whichever player is closer; single-player is
        // unchanged since there is only one). Resolved per access — the registry loops ≤2 entries.
        protected Transform Player => entityRegistry != null ? entityRegistry.GetNearestPlayer(transform.position) : null;
        protected bool HasPlayer => entityRegistry != null && entityRegistry.HasPlayer;

        protected virtual void Awake()
        {
            Stats = GetComponent<EnemyStats>();
            Core = GetComponent<EnemyCore>();
        }
    }
}

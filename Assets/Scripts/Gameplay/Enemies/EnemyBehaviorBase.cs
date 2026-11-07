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

        // Components of the CURRENT target player, re-resolved whenever the nearest player changes. Behaviors used
        // to cache these once from whichever player they saw first — in co-op a Biter then measured range to P2 but
        // damaged P1, and line-of-sight used the wrong player's bounds.
        private Transform _refsFor;
        private Collider _playerCollider;
        private IDamageable _playerDamageable;
        private Camera _playerCamera;

        protected Collider PlayerCollider { get { RefreshPlayerRefs(); return _playerCollider; } }
        protected IDamageable PlayerDamageable { get { RefreshPlayerRefs(); return _playerDamageable; } }
        protected Camera PlayerCamera { get { RefreshPlayerRefs(); return _playerCamera; } }

        private void RefreshPlayerRefs()
        {
            var p = Player;
            if (p == _refsFor) return;
            _refsFor = p;
            _playerCollider = null;
            _playerDamageable = null;
            _playerCamera = null;
            if (p == null) return;
            if (!p.TryGetComponent(out _playerCollider)) _playerCollider = p.GetComponentInChildren<Collider>();
            if (!p.TryGetComponent(out _playerDamageable)) _playerDamageable = p.GetComponentInParent<IDamageable>();
            _playerCamera = p.GetComponentInChildren<Camera>(true); // never Camera.main
        }

        protected virtual void Awake()
        {
            Stats = GetComponent<EnemyStats>();
            Core = GetComponent<EnemyCore>();
        }
    }
}

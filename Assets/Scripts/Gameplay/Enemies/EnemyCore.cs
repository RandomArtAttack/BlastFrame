using System;
using UnityEngine;
using BlastFrame.Core;
using BlastFrame.Core.Events;
using BlastFrame.Core.Entities;
using BlastFrame.Core.Pooling;
using BlastFrame.Core.Services;

namespace BlastFrame.Gameplay.Enemies
{
    /// <summary>
    /// Health / damage / death / reward / registry membership for an enemy. Behavior components
    /// subscribe to OnDamaged / OnDeath — they never reference each other. On death: grants reward
    /// currency (if a currency service exists), raises an optional GameEventSO, and returns to its
    /// pool (or destroys if not pooled). MiniBossCore / BossCore extend this contract.
    /// </summary>
    [RequireComponent(typeof(EnemyStats))]
    public class EnemyCore : MonoBehaviour, IDamageable
    {
        [Tooltip("Runtime registry this enemy adds itself to so the player/systems can find it.")]
        [SerializeField] private EntityRegistrySO entityRegistry;

        [Tooltip("Optional event raised when any enemy dies (audio, kill counter).")]
        [SerializeField] private GameEventSO onEnemyKilledEvent;

        protected EnemyStats Stats { get; private set; }

        public int CurrentHealth { get; protected set; }
        public bool IsDead => CurrentHealth <= 0;

        public event Action<int> OnDamaged;
        public event Action OnDeath;

        protected virtual void Awake()
        {
            Stats = GetComponent<EnemyStats>();
        }

        protected virtual void OnEnable()
        {
            CurrentHealth = Stats.Health;
            entityRegistry?.RegisterEnemy(transform);
        }

        protected virtual void OnDisable()
        {
            entityRegistry?.UnregisterEnemy(transform);
        }

        public virtual void TakeDamage(int amount)
        {
            if (amount <= 0 || IsDead) return;
            CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
            OnDamaged?.Invoke(CurrentHealth);
            if (CurrentHealth == 0) Die();
        }

        protected virtual void Die()
        {
            OnDeath?.Invoke();
            onEnemyKilledEvent?.Raise();

            if (ServiceLocator.TryGet<ICurrencyManager>(out var currency))
                currency.Add(Stats.RewardCurrency);

            entityRegistry?.UnregisterEnemy(transform);

            if (TryGetComponent<PooledMarker>(out _) && ServiceLocator.TryGet<IPoolManager>(out var pool))
                pool.Despawn(gameObject);
            else
                Destroy(gameObject);
        }
    }
}

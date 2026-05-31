using System;
using UnityEngine;
using BlastFrame.Core.Variables;
using BlastFrame.Core.Events;

namespace BlastFrame.Gameplay.Player
{
    using BlastFrame.Gameplay;
    /// <summary>
    /// Player hit points (start 5). Raises C# events for HUD and an optional GameEventSO for the
    /// wider system (audio, camera shake). Damage clamps to 0 = death.
    /// </summary>
    public class PlayerHealth : MonoBehaviour, IDamageable
    {
        [Tooltip("Starting / max health. Design default is 5.")]
        [SerializeField] private IntReference startHealth = new IntReference(5);

        [Tooltip("Optional event raised when the player takes damage (audio/camera shake react).")]
        [SerializeField] private GameEventSO onDamagedEvent;

        [Tooltip("Optional event raised when the player dies.")]
        [SerializeField] private GameEventSO onDeathEvent;

        public int Current { get; private set; }
        public int Max { get; private set; }
        public bool IsDead => Current <= 0;

        /// <summary>(current, max)</summary>
        public event Action<int, int> OnHealthChanged;
        public event Action OnDeath;

        private void Awake()
        {
            Max = startHealth;
            Current = Max;
        }

        private void Start() => OnHealthChanged?.Invoke(Current, Max);

        public void TakeDamage(int amount)
        {
            if (amount <= 0 || IsDead) return;
            Current = Mathf.Max(0, Current - amount);
            OnHealthChanged?.Invoke(Current, Max);
            onDamagedEvent?.Raise();
            if (Current == 0)
            {
                OnDeath?.Invoke();
                onDeathEvent?.Raise();
            }
        }

        public void Heal(int amount)
        {
            if (amount <= 0 || IsDead) return;
            Current = Mathf.Min(Max, Current + amount);
            OnHealthChanged?.Invoke(Current, Max);
        }

        public void ResetHealth()
        {
            Current = Max;
            OnHealthChanged?.Invoke(Current, Max);
        }
    }
}

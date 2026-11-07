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

        [Tooltip("Seconds of invulnerability after taking any damage. 0 disables.")]
        [SerializeField] private FloatReference invulnerabilitySeconds = new FloatReference(0.5f);

        public int Current { get; private set; }
        public int Max { get; private set; }
        public bool IsDead => Current <= 0;
        public bool IsInvulnerable => Time.time < _invulnUntil;

        private float _invulnUntil;

        /// <summary>(current, max)</summary>
        public event Action<int, int> OnHealthChanged;
        public event Action OnDeath;
        public event Action<DamageInfo> OnDamagedInfo;

        private void Awake()
        {
            Max = startHealth;
            Current = Max;
        }

        private void Start() => OnHealthChanged?.Invoke(Current, Max);

        public void TakeDamage(int amount) => TakeDamage(new DamageInfo(amount, DamageKind.Unknown));

        public void TakeDamage(in DamageInfo info)
        {
            if (info.Amount <= 0 || IsDead || IsInvulnerable) return;
            Current = Mathf.Max(0, Current - info.Amount);
            OnHealthChanged?.Invoke(Current, Max);
            onDamagedEvent?.Raise();
            _invulnUntil = Time.time + invulnerabilitySeconds.Value;
            OnDamagedInfo?.Invoke(info);
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

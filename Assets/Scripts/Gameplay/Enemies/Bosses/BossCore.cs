using System.Collections.Generic;
using UnityEngine;
using BlastFrame.Core.Events;
using BlastFrame.Gameplay.Enemies;

namespace BlastFrame.Gameplay.Enemies.Bosses
{
    /// <summary>
    /// Full boss variant of EnemyCore. Identical phase contract to <see cref="MiniBossCore"/> but
    /// raises an additional level-unlock event on death. Bosses typically have more phases and
    /// higher health than mini-bosses.
    ///
    /// Phase rules: see <see cref="MiniBossCore"/> — same contract applies.
    /// On death: instantiate drop prefab, raise weapon-unlock string event, raise OnBossDefeated,
    /// raise OnLevelUnlocked.
    /// </summary>
    public class BossCore : EnemyCore
    {
        [Tooltip("Ordered list of phases (highest healthFraction first). Each phase names which EnemyBehaviorBase siblings to ENABLE; all others are disabled.")]
        [SerializeField] private List<BossPhase> phases = new List<BossPhase>();

        [Tooltip("Prefab instantiated at the boss's position on death (e.g. a PowerupPickup prefab). Leave empty for no drop.")]
        [SerializeField] private GameObject dropPrefab;

        [Tooltip("Parameterless event raised when this boss is defeated (room-clear, mini-boss equivalent hook, etc.).")]
        [SerializeField] private GameEventSO onBossDefeated;

        [Tooltip("Parameterless event raised when this boss dies to unlock the next level. Wire an OnLevelUnlocked GameEventSO asset here.")]
        [SerializeField] private GameEventSO onLevelUnlocked;

        [Tooltip("String event raised with the weapon/ability ID granted on death. Wire a StringGameEventSO asset here.")]
        [SerializeField] private StringGameEventSO onWeaponUnlocked;

        [Tooltip("The weapon or ability ID string granted when this boss dies (e.g. \"weapon_charge_cannon\"). Must match the ID registered in the unlock registry.")]
        [SerializeField] private string weaponUnlockId = string.Empty;

        private EnemyBehaviorBase[] _allBehaviors;
        private int _currentPhaseIndex = -1;
        private int _maxHealth;

        protected override void Awake()
        {
            base.Awake();
            _allBehaviors = GetComponents<EnemyBehaviorBase>();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            _maxHealth = Stats.Health;
            _currentPhaseIndex = -1;
            OnDamaged += HandleDamaged;
            EvaluatePhases(CurrentHealth);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            OnDamaged -= HandleDamaged;
        }

        private void HandleDamaged(int remainingHealth)
        {
            EvaluatePhases(remainingHealth);
        }

        private void EvaluatePhases(int remainingHealth)
        {
            if (phases == null || phases.Count == 0 || _maxHealth <= 0) return;

            float fraction = (float)remainingHealth / _maxHealth;

            int targetIndex = 0;
            for (int i = phases.Count - 1; i >= 0; i--)
            {
                if (fraction <= phases[i].HealthFraction)
                {
                    targetIndex = i;
                    break;
                }
            }

            if (targetIndex == _currentPhaseIndex) return;
            _currentPhaseIndex = targetIndex;
            ApplyPhase(phases[targetIndex]);
            Debug.Log($"[BossCore] {gameObject.name} entered phase {targetIndex} at {fraction * 100f:F0}% health.");
        }

        private void ApplyPhase(BossPhase phase)
        {
            if (_allBehaviors == null) return;

            var enableSet = phase.BehaviorsToEnable;
            foreach (var b in _allBehaviors)
            {
                if (b == null) continue;
                bool shouldEnable = false;
                for (int i = 0; i < enableSet.Count; i++)
                {
                    if (enableSet[i] == b) { shouldEnable = true; break; }
                }
                b.enabled = shouldEnable;
            }
        }

        protected override void Die()
        {
            if (dropPrefab != null)
                Object.Instantiate(dropPrefab, transform.position + Vector3.up * 0.5f, Quaternion.identity);

            if (!string.IsNullOrEmpty(weaponUnlockId))
                onWeaponUnlocked?.Raise(weaponUnlockId);

            onBossDefeated?.Raise();
            onLevelUnlocked?.Raise();

            base.Die();
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using BlastFrame.Core.Events;
using BlastFrame.Gameplay.Enemies;

namespace BlastFrame.Gameplay.Enemies.Bosses
{
    /// <summary>
    /// Mini-boss variant of EnemyCore. Adds multi-phase behavior swapping driven by health
    /// thresholds. On death: instantiates a drop prefab at the boss position and raises
    /// OnBossDefeated + a string weapon/ability-unlock event — no compile dependency on the
    /// Powerups feature (the drop is a plain GameObject prefab reference).
    ///
    /// Phase rules:
    ///  - <see cref="phases"/> must be ordered from highest healthFraction to lowest.
    ///  - When health crosses a threshold, all EnemyBehaviorBase siblings are disabled except
    ///    those listed in that phase's behaviorsToEnable.
    ///  - Phase 0 (healthFraction = 1) activates on spawn so the boss starts in a known state.
    /// </summary>
    public class MiniBossCore : EnemyCore
    {
        [Tooltip("Ordered list of phases (highest healthFraction first). Each phase names which EnemyBehaviorBase siblings to ENABLE; all others are disabled.")]
        [SerializeField] private List<BossPhase> phases = new List<BossPhase>();

        [Tooltip("Prefab instantiated at the boss's position on death (e.g. a PowerupPickup prefab). Leave empty for no drop.")]
        [SerializeField] private GameObject dropPrefab;

        [Tooltip("Parameterless event raised when this mini-boss is defeated (triggers level-unlock logic, room-clear logic, etc.).")]
        [SerializeField] private GameEventSO onBossDefeated;

        [Tooltip("String event raised with the weapon/ability ID granted on death. Wire a StringGameEventSO asset here.")]
        [SerializeField] private StringGameEventSO onWeaponUnlocked;

        [Tooltip("The weapon or ability ID string granted when this mini-boss dies (e.g. \"weapon_rocket\"). Must match the ID registered in the unlock registry.")]
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
            // Apply the first applicable phase immediately so behaviors start in a known state.
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

            // Walk phases from last (lowest threshold) to first (highest) to find the deepest
            // phase whose threshold is >= current fraction. This is intentionally the LOWEST
            // threshold that is still <= fraction, i.e. the most advanced phase reached.
            int targetIndex = 0; // default: first phase (full-health behaviors)
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
            Debug.Log($"[MiniBossCore] {gameObject.name} entered phase {targetIndex} at {fraction * 100f:F0}% health.");
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
            // Spawn drop before calling base (which may destroy/despawn the GameObject).
            if (dropPrefab != null)
                Object.Instantiate(dropPrefab, transform.position + Vector3.up * 0.5f, Quaternion.identity);

            if (!string.IsNullOrEmpty(weaponUnlockId))
                onWeaponUnlocked?.Raise(weaponUnlockId);

            onBossDefeated?.Raise();

            base.Die();
        }
    }
}

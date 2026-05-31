using System;
using System.Collections.Generic;
using UnityEngine;
using BlastFrame.Gameplay.Enemies;

namespace BlastFrame.Gameplay.Enemies.Bosses
{
    /// <summary>
    /// Describes one phase of a boss fight. When CurrentHealth falls at or below
    /// <see cref="healthFraction"/> × MaxHealth, <see cref="behaviorsToEnable"/> are enabled and all
    /// behaviors NOT in the list are disabled. Phases are evaluated highest-to-lowest threshold so
    /// the designer stacks them in descending order (phase 0 = full health, last phase = near death).
    /// </summary>
    [Serializable]
    public class BossPhase
    {
        [Tooltip("Health fraction (0–1) at or below which this phase activates. E.g. 0.5 = 50 % health.")]
        [SerializeField] private float healthFraction = 1f;

        [Tooltip("Behavior components on the same GameObject to ENABLE when this phase is active. All other EnemyBehaviorBase siblings are disabled.")]
        [SerializeField] private List<EnemyBehaviorBase> behaviorsToEnable = new List<EnemyBehaviorBase>();

        public float HealthFraction => healthFraction;
        public IReadOnlyList<EnemyBehaviorBase> BehaviorsToEnable => behaviorsToEnable;
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

namespace BlastFrame.Gameplay.Areas
{
    public enum AreaTier { Easy, Medium, Hard, Extreme }

    /// <summary>The finite, extensible effect vocabulary of DESIGN.md §8.2. Add new kinds at the END only —
    /// the enum is serialized by value.</summary>
    public enum AreaEffectType
    {
        UnlockArea = 0,        // target area becomes enterable
        AlterSpawnTable = 1,   // target area applies alteration `key` (e.g. "flooded", "no_turrets")
        OpenShortcut = 2,      // target area opens shortcut `key`
        ApplyBossWeakness = 3, // target area's boss takes bonus damage from weapon `key`
        PriceCut = 4,          // HQ shop prices drop by `amount` percent (target unused)
    }

    [Serializable]
    public struct AreaEffect
    {
        [Tooltip("What this effect does when the SOURCE area is completed.")]
        public AreaEffectType type;

        [Tooltip("Id of the area this effect changes (AreaDefinitionSO.Id). Unused for PriceCut.")]
        public string targetAreaId;

        [Tooltip("Effect parameter: alteration id (AlterSpawnTable), shortcut id (OpenShortcut), weapon id " +
                 "(ApplyBossWeakness). Leave empty for UnlockArea / PriceCut.")]
        public string key;

        [Tooltip("Numeric parameter: percent for PriceCut (10 = 10% off); bonus damage multiplier for " +
                 "ApplyBossWeakness (2 = double). Unused otherwise.")]
        public float amount;
    }

    /// <summary>
    /// One area (DESIGN.md §8). Completing it runs <see cref="OnComplete"/> against OTHER areas — the Mega Man
    /// weakness web. Area state is never stored: it is re-derived from the save's completedAreaIds by
    /// <see cref="AreaGraph.Derive"/> every time an area is entered.
    /// </summary>
    [CreateAssetMenu(fileName = "NewArea", menuName = "Blast Frame/Areas/Area Definition")]
    public sealed class AreaDefinitionSO : ScriptableObject
    {
        [Tooltip("Unique, permanent id saved in completedAreaIds. Never rename once players have saves. e.g. 'lava_works'.")]
        [SerializeField] private string id;

        [Tooltip("Name shown at HQ / run-start panel.")]
        [SerializeField] private string displayName = "New Area";

        [Tooltip("Intended difficulty tier (guides which edges open when — not a hard gate).")]
        [SerializeField] private AreaTier tier = AreaTier.Easy;

        [Tooltip("Enterable from a fresh save, before any area is completed.")]
        [SerializeField] private bool startsUnlocked;

        [Tooltip("Scene loaded additively for this area. Must match a constant in SceneNames.")]
        [SerializeField] private string sceneName;

        [Tooltip("Effects applied to other areas once THIS area is completed.")]
        [SerializeField] private List<AreaEffect> onComplete = new List<AreaEffect>();

        public string Id => id;
        public string DisplayName => displayName;
        public AreaTier Tier => tier;
        public bool StartsUnlocked => startsUnlocked;
        public string SceneName => sceneName;
        public IReadOnlyList<AreaEffect> OnComplete => onComplete;

#if UNITY_EDITOR
        public void EditorInit(string newId, bool unlocked, List<AreaEffect> effects, AreaTier newTier = AreaTier.Easy)
        {
            id = newId;
            displayName = newId;
            startsUnlocked = unlocked;
            onComplete = effects ?? new List<AreaEffect>();
            tier = newTier;
        }
#endif
    }
}

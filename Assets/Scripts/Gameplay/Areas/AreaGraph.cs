using System.Collections.Generic;
using UnityEngine;

namespace BlastFrame.Gameplay.Areas
{
    /// <summary>Derived (never saved) state of one area for the current completed set.</summary>
    public sealed class AreaState
    {
        public AreaDefinitionSO Definition;
        public bool Unlocked;
        public bool Completed;
        public readonly HashSet<string> Alterations = new HashSet<string>();
        public readonly HashSet<string> Shortcuts = new HashSet<string>();
        /// <summary>weaponId → damage multiplier against this area's boss (highest wins).</summary>
        public readonly Dictionary<string, float> BossWeaknesses = new Dictionary<string, float>();
        /// <summary>Areas whose completion unlocked this one (for UI: "opened by …").</summary>
        public readonly List<string> UnlockedBy = new List<string>();
    }

    public sealed class AreaWorldState
    {
        public readonly Dictionary<string, AreaState> Areas = new Dictionary<string, AreaState>();
        /// <summary>Total HQ price cut, percent, clamped to [0, 90].</summary>
        public float PriceCutPercent;

        public AreaState Get(string id) => id != null && Areas.TryGetValue(id, out var s) ? s : null;
    }

    /// <summary>
    /// DESIGN.md §8.2 applier. Pure function of (all area definitions, completed-area ids): every effect whose
    /// SOURCE area is completed is applied to its target. Recomputed fresh on area enter; nothing is cached in
    /// save data, so editing an area's effects re-shapes every existing save consistently.
    /// </summary>
    public static class AreaGraph
    {
        public const float MaxPriceCutPercent = 90f;

        public static AreaWorldState Derive(IReadOnlyList<AreaDefinitionSO> areas, ICollection<string> completedIds)
        {
            var world = new AreaWorldState();
            foreach (var a in areas)
            {
                if (a == null || string.IsNullOrEmpty(a.Id) || world.Areas.ContainsKey(a.Id)) continue;
                world.Areas[a.Id] = new AreaState
                {
                    Definition = a,
                    Unlocked = a.StartsUnlocked,
                    Completed = completedIds != null && completedIds.Contains(a.Id),
                };
            }

            foreach (var src in world.Areas.Values)
            {
                if (!src.Completed) continue;
                src.Unlocked = true; // you can't have completed an area you never entered
                foreach (var fx in src.Definition.OnComplete)
                {
                    if (fx.type == AreaEffectType.PriceCut)
                    {
                        world.PriceCutPercent += Mathf.Max(0f, fx.amount);
                        continue;
                    }
                    var target = world.Get(fx.targetAreaId);
                    if (target == null) continue; // reported by Validate
                    switch (fx.type)
                    {
                        case AreaEffectType.UnlockArea:
                            target.Unlocked = true;
                            target.UnlockedBy.Add(src.Definition.Id);
                            break;
                        case AreaEffectType.AlterSpawnTable:
                            if (!string.IsNullOrEmpty(fx.key)) target.Alterations.Add(fx.key);
                            break;
                        case AreaEffectType.OpenShortcut:
                            if (!string.IsNullOrEmpty(fx.key)) target.Shortcuts.Add(fx.key);
                            break;
                        case AreaEffectType.ApplyBossWeakness:
                            if (string.IsNullOrEmpty(fx.key)) break;
                            float mult = fx.amount > 0f ? fx.amount : 2f;
                            target.BossWeaknesses[fx.key] = target.BossWeaknesses.TryGetValue(fx.key, out var m) ? Mathf.Max(m, mult) : mult;
                            break;
                    }
                }
            }
            world.PriceCutPercent = Mathf.Clamp(world.PriceCutPercent, 0f, MaxPriceCutPercent);
            return world;
        }

        /// <summary>Authoring problems: duplicate/empty ids, effects pointing at unknown or self areas, missing
        /// params, no starting area, and areas no completion order can ever unlock.</summary>
        public static List<string> Validate(IReadOnlyList<AreaDefinitionSO> areas)
        {
            var problems = new List<string>();
            var byId = new Dictionary<string, AreaDefinitionSO>();
            foreach (var a in areas)
            {
                if (a == null) { problems.Add("Registry contains an empty slot."); continue; }
                if (string.IsNullOrWhiteSpace(a.Id)) { problems.Add($"{a.name}: empty id."); continue; }
                if (byId.ContainsKey(a.Id)) { problems.Add($"{a.name}: duplicate id '{a.Id}' (also {byId[a.Id].name})."); continue; }
                byId[a.Id] = a;
            }

            bool anyStart = false;
            foreach (var a in byId.Values)
            {
                anyStart |= a.StartsUnlocked;
                if (string.IsNullOrWhiteSpace(a.SceneName)) problems.Add($"{a.Id}: no scene name.");
                for (int i = 0; i < a.OnComplete.Count; i++)
                {
                    var fx = a.OnComplete[i];
                    string where = $"{a.Id}.onComplete[{i}] {fx.type}";
                    if (fx.type == AreaEffectType.PriceCut)
                    {
                        if (fx.amount <= 0f) problems.Add($"{where}: amount must be > 0 (percent).");
                        continue;
                    }
                    if (string.IsNullOrWhiteSpace(fx.targetAreaId)) { problems.Add($"{where}: no target area."); continue; }
                    if (!byId.ContainsKey(fx.targetAreaId)) problems.Add($"{where}: unknown target '{fx.targetAreaId}'.");
                    else if (fx.targetAreaId == a.Id) problems.Add($"{where}: targets itself.");
                    if (fx.type != AreaEffectType.UnlockArea && string.IsNullOrWhiteSpace(fx.key))
                        problems.Add($"{where}: needs a key (alteration / shortcut / weapon id).");
                }
            }
            if (byId.Count > 0 && !anyStart) problems.Add("No area has startsUnlocked — a fresh save can't enter anything.");

            // Reachability: start set, then repeatedly "complete" every unlocked area.
            var unlocked = new HashSet<string>();
            foreach (var a in byId.Values) if (a.StartsUnlocked) unlocked.Add(a.Id);
            bool grew = true;
            while (grew)
            {
                grew = false;
                foreach (var id in new List<string>(unlocked))
                foreach (var fx in byId[id].OnComplete)
                    if (fx.type == AreaEffectType.UnlockArea && fx.targetAreaId != null && byId.ContainsKey(fx.targetAreaId) && unlocked.Add(fx.targetAreaId))
                        grew = true;
            }
            foreach (var a in byId.Values)
                if (!unlocked.Contains(a.Id)) problems.Add($"{a.Id}: can never be unlocked (no UnlockArea path from a starting area).");
            return problems;
        }
    }
}

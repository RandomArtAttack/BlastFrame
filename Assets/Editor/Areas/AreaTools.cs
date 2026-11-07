using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using BlastFrame.EditorTools.LevelDesign;
using BlastFrame.Gameplay.Areas;
using BlastFrame.Gameplay.Rooms;

namespace BlastFrame.EditorTools.Areas
{
    /// <summary>Wizards + validation for the area effect graph (DESIGN.md §8) and seeded variant sets (§6).</summary>
    public static class AreaTools
    {
        private const string AreaFolder = "Assets/ScriptableObjects/Areas";
        private const string RegistryPath = AreaFolder + "/AreaRegistry.asset";
        private const string SetFolder = "Assets/ScriptableObjects/RoomVariants";

        [MenuItem("Tools/Blast Frame/Areas/Create Area Definition")]
        private static void CreateArea()
        {
            Directory.CreateDirectory(AreaFolder);
            var registry = LoadOrCreateRegistry();
            int n = registry.Areas.Count + 1;
            var path = AssetDatabase.GenerateUniqueAssetPath($"{AreaFolder}/Area{n:00}.asset");
            var area = ScriptableObject.CreateInstance<AreaDefinitionSO>();
            bool first = registry.Areas.Count == 0;
            area.EditorInit(Path.GetFileNameWithoutExtension(path).ToLowerInvariant(), first, new List<AreaEffect>());
            AssetDatabase.CreateAsset(area, path);

            var list = registry.Areas.ToList();
            list.Add(area);
            registry.EditorSetAreas(list);
            EditorUtility.SetDirty(registry);
            AssetDatabase.SaveAssets();
            Selection.activeObject = area;
            EditorGUIUtility.PingObject(area);
            EditorUtility.DisplayDialog("Area created",
                $"Created {path} and added it to the AreaRegistry.\n\nSet its id (permanent — saved in player files), scene name, " +
                "and onComplete effects (UnlockArea / AlterSpawnTable / OpenShortcut / ApplyBossWeakness / PriceCut)." +
                (first ? "\n\nIt is the first area, so it starts unlocked." : ""), "OK");
        }

        [MenuItem("Tools/Blast Frame/Areas/Validate Area Graph")]
        private static void Validate()
        {
            var registry = AssetDatabase.LoadAssetAtPath<AreaRegistrySO>(RegistryPath);
            var issues = new List<LintIssue>();
            if (registry == null)
                issues.Add(new LintIssue(LintSeverity.Info, $"No AreaRegistry at {RegistryPath} yet — run Tools/Blast Frame/Areas/Create Area Definition."));
            else
            {
                foreach (var p in AreaGraph.Validate(registry.Areas)) issues.Add(new LintIssue(LintSeverity.Error, p, registry));
                var orphans = AssetDatabase.FindAssets("t:" + nameof(AreaDefinitionSO))
                    .Select(g => AssetDatabase.LoadAssetAtPath<AreaDefinitionSO>(AssetDatabase.GUIDToAssetPath(g)))
                    .Where(a => a != null && !registry.Areas.Contains(a));
                foreach (var o in orphans) issues.Add(new LintIssue(LintSeverity.Warning, $"{o.name} is not in the AreaRegistry.", o));
                if (issues.Count == 0) issues.Add(new LintIssue(LintSeverity.Info, $"Area graph OK ({registry.Areas.Count} areas)."));
            }
            LintResultsWindow.Show("Area Graph", issues);
        }

        [MenuItem("Tools/Blast Frame/Rooms/Create Room Variant Set")]
        private static void CreateRoomVariantSet()
        {
            Directory.CreateDirectory(SetFolder);
            var path = AssetDatabase.GenerateUniqueAssetPath($"{SetFolder}/RoomVariantSet.asset");
            var set = ScriptableObject.CreateInstance<RoomVariantSetSO>();
            // Pre-fill with the selected RoomVariantSOs, weight 1 each.
            var picked = Selection.objects.OfType<RoomVariantSO>().ToList();
            set.EditorSetEntries(picked.Select(v => new Core.Seeding.WeightedEntry<RoomVariantSO> { item = v, weight = 1f }).ToList());
            AssetDatabase.CreateAsset(set, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = set;
            EditorGUIUtility.PingObject(set);
            Debug.Log($"[AreaTools] Created {path} with {picked.Count} selected room variant(s).");
        }

        private static AreaRegistrySO LoadOrCreateRegistry()
        {
            var reg = AssetDatabase.LoadAssetAtPath<AreaRegistrySO>(RegistryPath);
            if (reg != null) return reg;
            reg = ScriptableObject.CreateInstance<AreaRegistrySO>();
            AssetDatabase.CreateAsset(reg, RegistryPath);
            return reg;
        }
    }
}

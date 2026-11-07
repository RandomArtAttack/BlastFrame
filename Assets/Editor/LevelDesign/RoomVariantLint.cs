using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using BlastFrame.Gameplay.Player;
using BlastFrame.Gameplay.Rooms;

namespace BlastFrame.EditorTools.LevelDesign
{
    public enum LintSeverity { Info, Warning, Error }

    public sealed class LintIssue
    {
        public LintSeverity Severity;
        public string Message;
        public Object Context;
        public LintIssue(LintSeverity s, string m, Object c = null) { Severity = s; Message = m; Context = c; }
    }

    /// <summary>
    /// Data + reachability checks for the room-variant framework (CLAUDE.md "Room Variant Framework"):
    /// every RoomController holds exactly 3 non-null variants; variant ids are present and unique; each variant
    /// prefab exists, has geometry, contains no player/camera/core-scene objects, and (headless) every Exit /
    /// Secret RoomMarker inside it is reachable from its Entry marker.
    /// </summary>
    public static class RoomVariantLint
    {
        [MenuItem("Tools/Blast Frame/Level Design/Lint Room Variants")]
        private static void RunMenu()
        {
            var issues = Run(interactive: true);
            if (issues == null) return;
            LintResultsWindow.Show("Room Variant Lint", issues);
        }

        /// <summary>Null if the user cancelled the save prompt.</summary>
        public static List<LintIssue> Run(bool interactive)
        {
            var issues = new List<LintIssue>();
            CheckSceneSlots(issues);
            var variants = LoadAllVariants();
            CheckVariantData(variants, issues);

            var profile = ReachMeasureMenu.LoadFresh(out bool stale);
            if (profile == null || stale)
            {
                profile = ReachMeasureMenu.MeasureAndCache(interactive);
                if (profile == null) return interactive ? null : issues;
            }

            var prefabs = variants.Where(v => v.VariantPrefab != null).Select(v => (v, v.VariantPrefab)).ToList();
            if (prefabs.Count == 0) return issues;

            var scene = IsolatedSimScene.TryEnter(interactive);
            if (scene == null) return interactive ? null : issues;
            try
            {
                for (int i = 0; i < prefabs.Count; i++)
                {
                    var (so, prefab) = prefabs[i];
                    if (interactive) EditorUtility.DisplayProgressBar("Room variant lint", prefab.name, (float)i / prefabs.Count);
                    CheckVariantReach(so, prefab, profile, issues);
                }
            }
            finally
            {
                if (interactive) EditorUtility.ClearProgressBar();
                scene.Dispose();
            }

            if (issues.Count == 0) issues.Add(new LintIssue(LintSeverity.Info, "No problems found."));
            return issues;
        }

        public static List<RoomVariantSO> LoadAllVariants() =>
            AssetDatabase.FindAssets("t:" + nameof(RoomVariantSO))
                .Select(g => AssetDatabase.LoadAssetAtPath<RoomVariantSO>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(v => v != null).ToList();

        private static void CheckSceneSlots(List<LintIssue> issues)
        {
            for (int s = 0; s < SceneManager.sceneCount; s++)
            {
                var scene = SceneManager.GetSceneAt(s);
                if (!scene.isLoaded) continue;
                foreach (var root in scene.GetRootGameObjects())
                foreach (var rc in root.GetComponentsInChildren<RoomController>(true))
                {
                    var so = new SerializedObject(rc);
                    var list = so.FindProperty("_variants");
                    int count = list?.arraySize ?? 0;
                    if (count != 3)
                        issues.Add(new LintIssue(LintSeverity.Error, $"{scene.name}/{rc.name}: {count} variants (must be exactly 3).", rc));
                    for (int i = 0; i < count; i++)
                        if (list.GetArrayElementAtIndex(i).objectReferenceValue == null)
                            issues.Add(new LintIssue(LintSeverity.Error, $"{scene.name}/{rc.name}: variant slot {i} is empty.", rc));
                    var anchor = so.FindProperty("_spawnAnchor")?.objectReferenceValue as Transform;
                    if (anchor != null && anchor.parent != rc.transform)
                        issues.Add(new LintIssue(LintSeverity.Warning, $"{scene.name}/{rc.name}: spawn anchor is not a direct child (self-containment rule).", rc));
                }
            }
        }

        public static void CheckVariantData(List<RoomVariantSO> variants, List<LintIssue> issues)
        {
            var byId = new Dictionary<string, RoomVariantSO>();
            foreach (var v in variants)
            {
                if (string.IsNullOrWhiteSpace(v.Id))
                    issues.Add(new LintIssue(LintSeverity.Error, $"{v.name}: empty variant id.", v));
                else if (byId.TryGetValue(v.Id, out var other))
                    issues.Add(new LintIssue(LintSeverity.Error, $"{v.name}: id '{v.Id}' duplicates {other.name}.", v));
                else byId[v.Id] = v;

                var prefab = v.VariantPrefab;
                if (prefab == null)
                {
                    issues.Add(new LintIssue(LintSeverity.Error, $"{v.name}: no variant prefab assigned.", v));
                    continue;
                }
                if (prefab.GetComponentsInChildren<Collider>(true).Length == 0)
                    issues.Add(new LintIssue(LintSeverity.Warning, $"{v.name}: prefab '{prefab.name}' has no colliders (no walkable geometry).", prefab));
                if (prefab.GetComponentInChildren<PlayerController>(true) != null)
                    issues.Add(new LintIssue(LintSeverity.Error, $"{v.name}: prefab contains a Player — the player lives in Core.", prefab));
                if (prefab.GetComponentInChildren<Camera>(true) != null)
                    issues.Add(new LintIssue(LintSeverity.Warning, $"{v.name}: prefab contains a Camera.", prefab));
                if (prefab.GetComponentInChildren<AudioListener>(true) != null)
                    issues.Add(new LintIssue(LintSeverity.Warning, $"{v.name}: prefab contains an AudioListener.", prefab));
                if (prefab.GetComponentInChildren<UnityEngine.EventSystems.EventSystem>(true) != null)
                    issues.Add(new LintIssue(LintSeverity.Error, $"{v.name}: prefab contains an EventSystem (belongs in Core).", prefab));
            }
        }

        private static void CheckVariantReach(RoomVariantSO so, GameObject prefab, ReachProfile profile, List<LintIssue> issues)
        {
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            try
            {
                var markers = inst.GetComponentsInChildren<RoomMarker>(true);
                if (!markers.Any(m => m.Kind == RoomMarkerKind.Entry))
                    issues.Add(new LintIssue(LintSeverity.Info, $"{so.name}: no Entry RoomMarker — reachability measured from the largest island.", prefab));

                var r = ReachabilityAnalyzer.Analyze(profile, 0.5f, null, "");
                foreach (var (marker, island, cls) in r.Markers)
                {
                    if (marker.Kind == RoomMarkerKind.Entry) continue;
                    if (cls == ReachClass.Unreachable)
                        issues.Add(new LintIssue(LintSeverity.Error, $"{so.name}: {marker.Kind} '{marker.Label}' is UNREACHABLE from {r.StartDescription}.", prefab));
                    else if (cls == ReachClass.PerfectJump || cls == ReachClass.PerfectDashJump)
                        issues.Add(new LintIssue(LintSeverity.Warning, $"{so.name}: {marker.Kind} '{marker.Label}' needs a frame-perfect {(cls == ReachClass.PerfectJump ? "jump" : "dash jump")}.", prefab));
                }
                int unreachable = r.Islands.Count(i => i.Class == ReachClass.Unreachable && i.Nodes.Count * 0.25f >= 1f);
                if (unreachable > 0)
                    issues.Add(new LintIssue(LintSeverity.Info, $"{so.name}: {unreachable} standable area(s) ≥1 m² can't be reached (fine if decorative). Open the prefab and run Reachability Lint to see them.", prefab));
            }
            finally
            {
                Object.DestroyImmediate(inst);
            }
        }
    }

    /// <summary>Plain list of lint issues with ping buttons.</summary>
    public sealed class LintResultsWindow : EditorWindow
    {
        private List<LintIssue> _issues = new List<LintIssue>();
        private Vector2 _scroll;

        public static void Show(string title, List<LintIssue> issues)
        {
            var w = GetWindow<LintResultsWindow>(title);
            w._issues = issues.OrderByDescending(i => i.Severity).ToList();
            w.Repaint();
            foreach (var i in w._issues)
            {
                string line = $"[{title}] {i.Severity}: {i.Message}";
                if (i.Severity == LintSeverity.Error) Debug.LogError(line, i.Context);
                else if (i.Severity == LintSeverity.Warning) Debug.LogWarning(line, i.Context);
                else Debug.Log(line, i.Context);
            }
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (var i in _issues)
            {
                var type = i.Severity == LintSeverity.Error ? MessageType.Error
                    : i.Severity == LintSeverity.Warning ? MessageType.Warning : MessageType.Info;
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.HelpBox(i.Message, type);
                    if (i.Context != null && GUILayout.Button("Ping", GUILayout.Width(44), GUILayout.Height(38)))
                        EditorGUIUtility.PingObject(i.Context);
                }
            }
            EditorGUILayout.EndScrollView();
        }
    }
}

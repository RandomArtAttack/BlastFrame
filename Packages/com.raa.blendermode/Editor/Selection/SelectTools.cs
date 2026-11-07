using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RAA.BlenderMode
{
    /// <summary>
    /// Whole-selection operations: A select-all (visible objects across all loaded scenes or the current
    /// prefab stage), Alt+A deselect-all, Ctrl+I invert. Stock Unity LMB click-selection is deliberately
    /// left untouched. See DESIGN.md §7.
    /// </summary>
    [InitializeOnLoad]
    public static class SelectTools
    {
        static SelectTools() { Register(); }

        static bool _registered;

        /// <summary>Register the select-all / deselect / invert action handlers (idempotent).</summary>
        public static void Register()
        {
            if (_registered) return;
            _registered = true;

            ActionRegistry.Register(Actions.SelectAll, (sv, e) => { SelectAll(); return true; });
            ActionRegistry.Register(Actions.SelectNone, (sv, e) => { DeselectAll(); return true; });
            ActionRegistry.Register(Actions.SelectInvert, (sv, e) => { InvertSelection(); return true; });
        }

        /// <summary>Select every visible, editable object in the active scenes / prefab stage.</summary>
        public static void SelectAll()
        {
            List<GameObject> all = EnumerateSelectable(true);
            Selection.objects = all.ToArray();
        }

        /// <summary>Clear the current selection.</summary>
        public static void DeselectAll()
        {
            Selection.activeObject = null;
            Selection.objects = System.Array.Empty<Object>();
        }

        /// <summary>Select everything visible that is not currently selected.</summary>
        public static void InvertSelection()
        {
            var current = new HashSet<GameObject>(Selection.gameObjects);
            List<GameObject> all = EnumerateSelectable(true);
            var result = new List<Object>(all.Count);
            for (int i = 0; i < all.Count; i++)
                if (!current.Contains(all[i])) result.Add(all[i]);
            Selection.objects = result.ToArray();
        }

        /// <summary>
        /// Enumerate all GameObjects in the current editing context (prefab stage if open, else every loaded
        /// scene). When <paramref name="respectVisibility"/> is set, objects hidden via the
        /// <see cref="SceneVisibilityManager"/> are skipped, as are editor-only / non-editable objects.
        /// </summary>
        public static List<GameObject> EnumerateSelectable(bool respectVisibility)
        {
            var list = new List<GameObject>();
            SceneVisibilityManager svm = SceneVisibilityManager.instance;

            PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null)
            {
                GameObject[] roots = stage.scene.GetRootGameObjects();
                for (int i = 0; i < roots.Length; i++)
                    Collect(roots[i].transform, list, svm, respectVisibility);
                return list;
            }

            for (int s = 0; s < SceneManager.sceneCount; s++)
            {
                Scene scene = SceneManager.GetSceneAt(s);
                if (!scene.isLoaded) continue;
                GameObject[] roots = scene.GetRootGameObjects();
                for (int i = 0; i < roots.Length; i++)
                    Collect(roots[i].transform, list, svm, respectVisibility);
            }
            return list;
        }

        static void Collect(Transform t, List<GameObject> list, SceneVisibilityManager svm, bool respectVisibility)
        {
            GameObject go = t.gameObject;
            bool editorOnly = (go.hideFlags & (HideFlags.HideInHierarchy | HideFlags.DontSave | HideFlags.NotEditable)) != 0;
            if (!editorOnly && (!respectVisibility || !svm.IsHidden(go)))
                list.Add(go);

            int n = t.childCount;
            for (int i = 0; i < n; i++)
                Collect(t.GetChild(i), list, svm, respectVisibility);
        }
    }
}

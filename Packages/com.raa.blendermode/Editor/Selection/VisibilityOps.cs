using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace RAA.BlenderMode
{
    /// <summary>
    /// Blender-style visibility operations built on the <see cref="SceneVisibilityManager"/>: H hides the
    /// selection, Shift+H hides everything except the selection (and its hierarchy chain), Alt+H unhides all.
    /// See DESIGN.md §7.
    /// </summary>
    [InitializeOnLoad]
    public static class VisibilityOps
    {
        static VisibilityOps() { Register(); }

        static bool _registered;

        /// <summary>Register the hide/unhide action handlers (idempotent).</summary>
        public static void Register()
        {
            if (_registered) return;
            _registered = true;

            ActionRegistry.Register(Actions.HideSelected, (sv, e) => { HideSelected(); return true; });
            ActionRegistry.Register(Actions.HideUnselected, (sv, e) => { HideUnselected(); return true; });
            ActionRegistry.Register(Actions.UnhideAll, (sv, e) => { UnhideAll(); return true; });
        }

        /// <summary>Hide the current selection (with descendants).</summary>
        public static void HideSelected()
        {
            GameObject[] sel = Selection.gameObjects;
            if (sel == null || sel.Length == 0) return;
            SceneVisibilityManager.instance.Hide(sel, true);
        }

        /// <summary>Hide everything that is not selected, keeping the selection and its ancestors/descendants visible.</summary>
        public static void HideUnselected()
        {
            var keep = new HashSet<GameObject>();
            foreach (GameObject go in Selection.gameObjects)
            {
                if (go == null) continue;
                for (Transform t = go.transform; t != null; t = t.parent) keep.Add(t.gameObject);
                Transform[] descendants = go.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < descendants.Length; i++) keep.Add(descendants[i].gameObject);
            }

            List<GameObject> all = SelectTools.EnumerateSelectable(false);
            var hide = new List<GameObject>();
            for (int i = 0; i < all.Count; i++)
                if (!keep.Contains(all[i])) hide.Add(all[i]);

            if (hide.Count > 0)
                SceneVisibilityManager.instance.Hide(hide.ToArray(), false);
        }

        /// <summary>Unhide every object (and exit isolation if active).</summary>
        public static void UnhideAll()
        {
            SceneVisibilityManager svm = SceneVisibilityManager.instance;
            if (svm.IsCurrentStageIsolated())
                svm.ExitIsolation();

            List<GameObject> all = SelectTools.EnumerateSelectable(false);
            if (all.Count > 0)
                svm.Show(all.ToArray(), true);
        }
    }
}

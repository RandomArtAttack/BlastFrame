using UnityEditor;
using UnityEngine;

namespace RAA.BlenderMode
{
    /// <summary>
    /// Object-level operations: X / Delete delete the selection (instant or with a confirm popup per prefs),
    /// Ctrl+P parents the selection under the active object keeping world transforms, Alt+P clears the parent
    /// keeping world transforms. All operations are undoable. See DESIGN.md §7.
    /// </summary>
    [InitializeOnLoad]
    public static class ObjectOps
    {
        static ObjectOps() { Register(); }

        static bool _registered;

        /// <summary>Register the delete / parent action handlers (idempotent).</summary>
        public static void Register()
        {
            if (_registered) return;
            _registered = true;

            ActionRegistry.Register(Actions.Delete, (sv, e) => DeleteSelected());
            ActionRegistry.Register(Actions.DeleteAlt, (sv, e) => DeleteSelected());
            ActionRegistry.Register(Actions.Parent, (sv, e) => ParentSelected());
            ActionRegistry.Register(Actions.ClearParent, (sv, e) => ClearParentSelected());
        }

        /// <summary>Delete the current selection (undoable); optionally confirm first.</summary>
        public static bool DeleteSelected()
        {
            GameObject[] sel = Selection.gameObjects;
            if (sel == null || sel.Length == 0) return false;

            if (BlenderModePrefs.DeleteConfirm)
            {
                string msg = sel.Length == 1
                    ? "Delete \"" + sel[0].name + "\"?"
                    : "Delete " + sel.Length + " objects?";
                if (!EditorUtility.DisplayDialog("Delete", msg, "Delete", "Cancel"))
                    return true; // consumed the key, user cancelled
            }

            int group = UndoScope.Begin("Delete");
            for (int i = 0; i < sel.Length; i++)
                if (sel[i] != null) Undo.DestroyObjectImmediate(sel[i]);
            UndoScope.Collapse(group);
            return true;
        }

        /// <summary>Parent all selected objects (except the active one) under the active object, keeping world transforms.</summary>
        public static bool ParentSelected()
        {
            Transform active = Selection.activeTransform;
            Transform[] sel = Selection.transforms;
            if (active == null || sel == null || sel.Length < 2) return false;

            int group = UndoScope.Begin("Parent");
            for (int i = 0; i < sel.Length; i++)
            {
                Transform t = sel[i];
                if (t == null || t == active) continue;
                if (IsAncestor(t, active)) continue; // cannot parent under one of your own descendants
                Undo.SetTransformParent(t, active, "Parent");
            }
            UndoScope.Collapse(group);
            return true;
        }

        /// <summary>Clear the parent of all selected objects, keeping world transforms.</summary>
        public static bool ClearParentSelected()
        {
            Transform[] sel = Selection.transforms;
            if (sel == null || sel.Length == 0) return false;

            int group = UndoScope.Begin("Clear Parent");
            for (int i = 0; i < sel.Length; i++)
            {
                Transform t = sel[i];
                if (t != null && t.parent != null)
                    Undo.SetTransformParent(t, null, "Clear Parent");
            }
            UndoScope.Collapse(group);
            return true;
        }

        static bool IsAncestor(Transform ancestor, Transform node)
        {
            for (Transform t = node; t != null; t = t.parent)
                if (t == ancestor) return true;
            return false;
        }
    }
}

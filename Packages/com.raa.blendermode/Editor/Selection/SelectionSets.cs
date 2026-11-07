using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace RAA.BlenderMode
{
    /// <summary>
    /// Named, per-scene selection sets. Members are stored as <see cref="GlobalObjectId"/> strings so they
    /// survive domain reloads and scene reopen; stale ids are skipped on recall. Numbered shortcuts
    /// Ctrl+Shift+1..9 store / Shift+1..9 recall the sets "Set 1".."Set 9"; arbitrary named sets are managed
    /// from the N-panel Sets tab. See DESIGN.md §10.
    /// </summary>
    [InitializeOnLoad]
    public static class SelectionSets
    {
        static SelectionSets() { Register(); }

        static bool _registered;

        /// <summary>Register the numbered store/recall action handlers (idempotent).</summary>
        public static void Register()
        {
            if (_registered) return;
            _registered = true;

            for (int i = 1; i <= 9; i++)
            {
                int n = i;
                ActionRegistry.Register(Actions.SetStorePrefix + n, (sv, e) => { Store("Set " + n); return true; });
                ActionRegistry.Register(Actions.SetRecallPrefix + n, (sv, e) => { Recall("Set " + n); return true; });
            }
        }

        static List<SelectionSet> Sets => SceneStateStore.instance.ActiveData().selectionSets;

        /// <summary>All selection sets for the active scene.</summary>
        public static IReadOnlyList<SelectionSet> All => Sets;

        static List<string> CaptureCurrent()
        {
            var ids = new List<string>();
            foreach (GameObject go in Selection.gameObjects)
            {
                if (go == null) continue;
                ids.Add(GlobalObjectId.GetGlobalObjectIdSlow(go).ToString());
            }
            return ids;
        }

        static SelectionSet FindOrCreate(string name)
        {
            SelectionSet s = Sets.Find(x => x.name == name);
            if (s == null)
            {
                s = new SelectionSet { name = name };
                Sets.Add(s);
            }
            return s;
        }

        /// <summary>Store the current selection as the set <paramref name="name"/> (replacing its members).</summary>
        public static void Store(string name)
        {
            SelectionSet s = FindOrCreate(name);
            s.objectIds = CaptureCurrent();
            Persist();
        }

        /// <summary>Add the current selection to the set (union).</summary>
        public static void AddToSet(string name)
        {
            SelectionSet s = FindOrCreate(name);
            var set = new HashSet<string>(s.objectIds);
            foreach (string id in CaptureCurrent()) set.Add(id);
            s.objectIds = new List<string>(set);
            Persist();
        }

        /// <summary>Remove the current selection from the set.</summary>
        public static void RemoveFromSet(string name)
        {
            SelectionSet s = Sets.Find(x => x.name == name);
            if (s == null) return;
            var remove = new HashSet<string>(CaptureCurrent());
            s.objectIds.RemoveAll(id => remove.Contains(id));
            Persist();
        }

        /// <summary>Select the set's members (skipping any that no longer resolve).</summary>
        public static void Recall(string name)
        {
            SelectionSet s = Sets.Find(x => x.name == name);
            if (s == null) return;

            var objs = new List<Object>();
            for (int i = 0; i < s.objectIds.Count; i++)
            {
                if (GlobalObjectId.TryParse(s.objectIds[i], out GlobalObjectId gid))
                {
                    Object o = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(gid);
                    if (o != null) objs.Add(o);
                }
            }
            Selection.objects = objs.ToArray();
        }

        /// <summary>Delete a set entirely.</summary>
        public static void Delete(string name)
        {
            Sets.RemoveAll(x => x.name == name);
            Persist();
        }

        /// <summary>Rename a set (no-op if the old name is missing).</summary>
        public static void Rename(string oldName, string newName)
        {
            if (string.IsNullOrEmpty(newName)) return;
            SelectionSet s = Sets.Find(x => x.name == oldName);
            if (s != null) { s.name = newName; Persist(); }
        }

        static void Persist() => SceneStateStore.instance.Persist();
    }
}

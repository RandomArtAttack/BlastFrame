using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace RAA.BlenderMode
{
    /// <summary>
    /// Camera bookmarks: Ctrl+Alt+1..9 store the current view (pivot / rotation / size / ortho); Alt+1..9
    /// recall it with an animated <see cref="SceneView.LookAt(Vector3,Quaternion,float,bool)"/>. Nine slots
    /// per scene are persisted in <see cref="SceneStateStore"/>. See DESIGN.md §10.
    /// </summary>
    [InitializeOnLoad]
    public static class CameraBookmarks
    {
        static CameraBookmarks() { Register(); }

        static bool _registered;

        /// <summary>Number of bookmark slots per scene.</summary>
        public const int SlotCount = 9;

        /// <summary>Register the store/recall action handlers for all nine slots (idempotent).</summary>
        public static void Register()
        {
            if (_registered) return;
            _registered = true;

            for (int i = 1; i <= SlotCount; i++)
            {
                int n = i;
                ActionRegistry.Register(Actions.BookmarkStorePrefix + n, (sv, e) => { Store(sv, n); return true; });
                ActionRegistry.Register(Actions.BookmarkRecallPrefix + n, (sv, e) => Recall(sv, n));
            }
        }

        static List<CameraBookmark> Slots
        {
            get
            {
                PerSceneData d = SceneStateStore.instance.ActiveData();
                while (d.bookmarks.Count < SlotCount)
                    d.bookmarks.Add(new CameraBookmark { name = "View " + (d.bookmarks.Count + 1) });
                return d.bookmarks;
            }
        }

        /// <summary>The nine bookmark slots for the active scene.</summary>
        public static IReadOnlyList<CameraBookmark> Bookmarks => Slots;

        /// <summary>Capture the current view into slot <paramref name="slot"/> (1..9).</summary>
        public static void Store(SceneView sv, int slot)
        {
            if (sv == null || slot < 1 || slot > SlotCount) return;
            CameraBookmark b = Slots[slot - 1];
            b.pivot = sv.pivot;
            b.rotationEuler = sv.rotation.eulerAngles;
            b.size = sv.size;
            b.orthographic = sv.orthographic;
            b.assigned = true;
            if (string.IsNullOrEmpty(b.name)) b.name = "View " + slot;
            SceneStateStore.instance.Persist();
            Hud.Notify(sv, "Stored view " + slot);
        }

        /// <summary>Recall slot <paramref name="slot"/> with an animated transition (no-op if unassigned).</summary>
        public static bool Recall(SceneView sv, int slot)
        {
            if (sv == null || slot < 1 || slot > SlotCount) return false;
            CameraBookmark b = Slots[slot - 1];
            if (!b.assigned) { Hud.Notify(sv, "View " + slot + " is empty"); return true; }
            sv.LookAt(b.pivot, Quaternion.Euler(b.rotationEuler), b.size, b.orthographic);
            return true;
        }

        /// <summary>Rename a bookmark slot.</summary>
        public static void Rename(int slot, string name)
        {
            if (slot < 1 || slot > SlotCount) return;
            Slots[slot - 1].name = string.IsNullOrEmpty(name) ? "View " + slot : name;
            SceneStateStore.instance.Persist();
        }

        /// <summary>Clear a bookmark slot.</summary>
        public static void Clear(int slot)
        {
            if (slot < 1 || slot > SlotCount) return;
            Slots[slot - 1].assigned = false;
            SceneStateStore.instance.Persist();
        }
    }
}

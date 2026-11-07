using UnityEditor;
using UnityEngine;

namespace RAA.BlenderMode
{
    /// <summary>Cached local transform (position/rotation/scale) for silent cancel-restore.</summary>
    public struct Trs
    {
        public Vector3 localPosition;
        public Quaternion localRotation;
        public Vector3 localScale;

        public static Trs From(Transform t)
        {
            return new Trs
            {
                localPosition = t.localPosition,
                localRotation = t.localRotation,
                localScale = t.localScale
            };
        }

        public void ApplyTo(Transform t)
        {
            t.localPosition = localPosition;
            t.localRotation = localRotation;
            t.localScale = localScale;
        }
    }

    /// <summary>
    /// Undo helpers implementing the design's rule: one collapsed undo group per confirmed operation, and
    /// a silent (no undo entry) restore of cached transforms on cancel.
    /// </summary>
    public static class UndoScope
    {
        /// <summary>Open a new undo group and name it. Returns the group index to collapse later.</summary>
        public static int Begin(string name)
        {
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(name);
            return group;
        }

        /// <summary>Record the given transforms into the current undo group before mutating them.</summary>
        public static void RecordTransforms(Transform[] transforms, string name)
        {
            if (transforms == null || transforms.Length == 0) return;
            // Undo.RecordObjects takes Object[]; Transform derives from Object.
            Undo.RecordObjects(transforms, name);
        }

        /// <summary>Collapse everything since <paramref name="group"/> into a single undo step.</summary>
        public static void Collapse(int group)
        {
            Undo.CollapseUndoOperations(group);
        }

        /// <summary>Snapshot the local TRS of each transform for a possible silent revert.</summary>
        public static Trs[] Cache(Transform[] transforms)
        {
            if (transforms == null) return System.Array.Empty<Trs>();
            var cache = new Trs[transforms.Length];
            for (int i = 0; i < transforms.Length; i++)
                if (transforms[i] != null)
                    cache[i] = Trs.From(transforms[i]);
            return cache;
        }

        /// <summary>Restore cached TRS directly, producing no undo entry (used on modal cancel).</summary>
        public static void Restore(Transform[] transforms, Trs[] cache)
        {
            if (transforms == null || cache == null) return;
            int n = Mathf.Min(transforms.Length, cache.Length);
            for (int i = 0; i < n; i++)
                if (transforms[i] != null)
                    cache[i].ApplyTo(transforms[i]);
        }
    }
}

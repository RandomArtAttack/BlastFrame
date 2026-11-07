using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace RAA.BlenderMode
{
    /// <summary>
    /// Align / distribute / drop tools operating on the current selection. Exposed both from the N-panel
    /// Align tab and the <c>Tools/Blender Mode/Align</c> menu. All operations are undoable. See DESIGN.md §10.
    /// </summary>
    public static class AlignTools
    {
        /// <summary>World axis an align/distribute operation acts on.</summary>
        public enum Axis { X, Y, Z }

        static Transform[] Sel => Selection.transforms;

        static float Comp(Vector3 v, Axis a) => a == Axis.X ? v.x : a == Axis.Y ? v.y : v.z;

        static Vector3 WithComp(Vector3 v, Axis a, float value)
        {
            if (a == Axis.X) v.x = value;
            else if (a == Axis.Y) v.y = value;
            else v.z = value;
            return v;
        }

        /// <summary>Align object positions along <paramref name="axis"/>. mode: -1 min, 0 center, 1 max.</summary>
        public static void Align(Axis axis, int mode)
        {
            Transform[] t = Sel;
            if (t == null || t.Length < 2) return;

            float min = float.PositiveInfinity, max = float.NegativeInfinity;
            for (int i = 0; i < t.Length; i++)
            {
                if (t[i] == null) continue;
                float c = Comp(t[i].position, axis);
                if (c < min) min = c;
                if (c > max) max = c;
            }
            float target = mode < 0 ? min : mode > 0 ? max : (min + max) * 0.5f;

            int group = UndoScope.Begin("Align");
            Undo.RecordObjects(t, "Align");
            for (int i = 0; i < t.Length; i++)
                if (t[i] != null) t[i].position = WithComp(t[i].position, axis, target);
            UndoScope.Collapse(group);
        }

        /// <summary>Distribute the selection evenly between its extreme objects along <paramref name="axis"/>.</summary>
        public static void Distribute(Axis axis)
        {
            var list = new List<Transform>();
            Transform[] t = Sel;
            for (int i = 0; i < (t?.Length ?? 0); i++)
                if (t[i] != null) list.Add(t[i]);
            if (list.Count < 3) return;

            list.Sort((p, q) => Comp(p.position, axis).CompareTo(Comp(q.position, axis)));
            float lo = Comp(list[0].position, axis);
            float hi = Comp(list[list.Count - 1].position, axis);

            int group = UndoScope.Begin("Distribute");
            Undo.RecordObjects(list.ToArray(), "Distribute");
            for (int i = 1; i < list.Count - 1; i++)
            {
                float f = i / (float)(list.Count - 1);
                list[i].position = WithComp(list[i].position, axis, Mathf.Lerp(lo, hi, f));
            }
            UndoScope.Collapse(group);
        }

        /// <summary>Raycast each object straight down and rest it on the first surface below (optional align-to-normal).</summary>
        public static void DropToSurface()
        {
            Transform[] t = Sel;
            if (t == null || t.Length == 0) return;

            int group = UndoScope.Begin("Drop to Surface");
            Undo.RecordObjects(t, "Drop to Surface");
            for (int i = 0; i < t.Length; i++)
            {
                Transform x = t[i];
                if (x == null) continue;
                Vector3 origin = x.position + Vector3.up * 0.01f;
                if (RaycastDown(x, origin, out RaycastHit hit))
                {
                    float bottom = BottomOffset(x);
                    x.position = hit.point + Vector3.up * bottom;
                    if (BlenderModePrefs.DropAlignToNormal)
                        x.rotation = Quaternion.FromToRotation(x.up, hit.normal) * x.rotation;
                }
            }
            UndoScope.Collapse(group);
        }

        /// <summary>Drop each object so its bounds bottom rests on the world floor (y = 0).</summary>
        public static void DropToFloor()
        {
            Transform[] t = Sel;
            if (t == null || t.Length == 0) return;

            int group = UndoScope.Begin("Drop to Floor");
            Undo.RecordObjects(t, "Drop to Floor");
            for (int i = 0; i < t.Length; i++)
            {
                Transform x = t[i];
                if (x == null) continue;
                float off = BottomOffset(x);
                Vector3 p = x.position;
                p.y = off;
                x.position = p;
            }
            UndoScope.Collapse(group);
        }

        static bool RaycastDown(Transform self, Vector3 origin, out RaycastHit hit)
        {
            RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 100000f);
            hit = default;
            float best = float.PositiveInfinity;
            bool found = false;
            for (int i = 0; i < hits.Length; i++)
            {
                Transform ht = hits[i].transform;
                if (ht == null) continue;
                if (ht == self || ht.IsChildOf(self) || self.IsChildOf(ht)) continue;
                if (hits[i].distance < best) { best = hits[i].distance; hit = hits[i]; found = true; }
            }
            return found;
        }

        /// <summary>Vertical distance from an object's pivot to the bottom of its renderer bounds (0 without a renderer).</summary>
        static float BottomOffset(Transform t)
        {
            Renderer r = t.GetComponentInChildren<Renderer>();
            return r != null ? t.position.y - r.bounds.min.y : 0f;
        }

        // ---------------------------------------------------------------- menu

        [MenuItem("Tools/Blender Mode/Align/Align Min X", false, 40)] static void MinX() => Align(Axis.X, -1);
        [MenuItem("Tools/Blender Mode/Align/Align Center X", false, 41)] static void CenterX() => Align(Axis.X, 0);
        [MenuItem("Tools/Blender Mode/Align/Align Max X", false, 42)] static void MaxX() => Align(Axis.X, 1);
        [MenuItem("Tools/Blender Mode/Align/Align Min Y", false, 43)] static void MinY() => Align(Axis.Y, -1);
        [MenuItem("Tools/Blender Mode/Align/Align Center Y", false, 44)] static void CenterY() => Align(Axis.Y, 0);
        [MenuItem("Tools/Blender Mode/Align/Align Max Y", false, 45)] static void MaxY() => Align(Axis.Y, 1);
        [MenuItem("Tools/Blender Mode/Align/Align Min Z", false, 46)] static void MinZ() => Align(Axis.Z, -1);
        [MenuItem("Tools/Blender Mode/Align/Align Center Z", false, 47)] static void CenterZ() => Align(Axis.Z, 0);
        [MenuItem("Tools/Blender Mode/Align/Align Max Z", false, 48)] static void MaxZ() => Align(Axis.Z, 1);

        [MenuItem("Tools/Blender Mode/Align/Distribute X", false, 60)] static void DistX() => Distribute(Axis.X);
        [MenuItem("Tools/Blender Mode/Align/Distribute Y", false, 61)] static void DistY() => Distribute(Axis.Y);
        [MenuItem("Tools/Blender Mode/Align/Distribute Z", false, 62)] static void DistZ() => Distribute(Axis.Z);

        [MenuItem("Tools/Blender Mode/Align/Drop to Surface", false, 80)] static void DropSurf() => DropToSurface();
        [MenuItem("Tools/Blender Mode/Align/Drop to Floor", false, 81)] static void DropFloor() => DropToFloor();
    }
}

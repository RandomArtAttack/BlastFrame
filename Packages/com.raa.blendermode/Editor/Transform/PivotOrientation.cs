using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace RAA.BlenderMode
{
    /// <summary>
    /// Pivot point used by rotate/scale (and cycled via the '.' pie in Phase 3). Mirrors Blender's
    /// transform-pivot options. See DESIGN.md §4.
    /// </summary>
    public enum PivotMode
    {
        /// <summary>Average of the selection's object origins (Blender default).</summary>
        MedianPoint,
        /// <summary>The active (last-clicked) object's origin.</summary>
        ActiveElement,
        /// <summary>Each object rotates/scales around its own origin.</summary>
        IndividualOrigins,
        /// <summary>The 3D cursor position.</summary>
        Cursor3D,
        /// <summary>Center of the selection's combined bounding box.</summary>
        BoundingBoxCenter
    }

    /// <summary>
    /// Transform orientation used to build axis/plane constraints (cycled via the ',' pie in Phase 3).
    /// See DESIGN.md §4.
    /// </summary>
    public enum TransformOrientationMode
    {
        /// <summary>World axes (Blender default).</summary>
        Global,
        /// <summary>The active object's local axes.</summary>
        Local,
        /// <summary>The scene view camera's axes.</summary>
        View,
        /// <summary>Normal orientation (active object's rotation in object mode).</summary>
        Normal,
        /// <summary>A user-saved custom orientation (from <see cref="SceneStateStore"/>).</summary>
        Custom
    }

    /// <summary>
    /// Owns the current pivot-point and transform-orientation settings, cycling APIs, and custom
    /// orientation management. Pivot/orientation selections persist in EditorPrefs; custom orientation
    /// definitions persist per-scene in <see cref="SceneStateStore"/>. See DESIGN.md §4.
    /// </summary>
    public static class PivotOrientation
    {
        const string P = "RAA.BlenderMode.";

        /// <summary>Current pivot point for rotate/scale.</summary>
        public static PivotMode Pivot
        {
            get => (PivotMode)EditorPrefs.GetInt(P + "Pivot", (int)PivotMode.MedianPoint);
            set { EditorPrefs.SetInt(P + "Pivot", (int)value); BlenderModePrefs.RaiseChanged(); }
        }

        /// <summary>Current transform orientation for constraints.</summary>
        public static TransformOrientationMode Orientation
        {
            get => (TransformOrientationMode)EditorPrefs.GetInt(P + "Orientation", (int)TransformOrientationMode.Global);
            set { EditorPrefs.SetInt(P + "Orientation", (int)value); BlenderModePrefs.RaiseChanged(); }
        }

        /// <summary>Name of the selected custom orientation (used when <see cref="Orientation"/> is Custom).</summary>
        public static string CustomOrientationName
        {
            get => EditorPrefs.GetString(P + "CustomOrientation", string.Empty);
            set { EditorPrefs.SetString(P + "CustomOrientation", value ?? string.Empty); SceneView.RepaintAll(); }
        }

        // --------------------------------------------------------------- cycling

        static readonly PivotMode[] PivotOrder =
        {
            PivotMode.MedianPoint, PivotMode.ActiveElement, PivotMode.IndividualOrigins,
            PivotMode.Cursor3D, PivotMode.BoundingBoxCenter
        };

        static readonly TransformOrientationMode[] OrientationOrder =
        {
            TransformOrientationMode.Global, TransformOrientationMode.Local, TransformOrientationMode.View,
            TransformOrientationMode.Normal, TransformOrientationMode.Custom
        };

        /// <summary>Advance the pivot mode by <paramref name="dir"/> (+1/-1) with wraparound.</summary>
        public static void CyclePivot(int dir)
        {
            int i = System.Array.IndexOf(PivotOrder, Pivot);
            i = ((i < 0 ? 0 : i) + dir + PivotOrder.Length) % PivotOrder.Length;
            Pivot = PivotOrder[i];
        }

        /// <summary>Advance the orientation mode by <paramref name="dir"/> (+1/-1) with wraparound.</summary>
        public static void CycleOrientation(int dir)
        {
            int i = System.Array.IndexOf(OrientationOrder, Orientation);
            i = ((i < 0 ? 0 : i) + dir + OrientationOrder.Length) % OrientationOrder.Length;
            Orientation = OrientationOrder[i];
        }

        // --------------------------------------------------------------- custom orientations

        static List<CustomOrientation> Customs => SceneStateStore.instance.ActiveData().customOrientations;

        /// <summary>All custom orientations defined for the active scene.</summary>
        public static IReadOnlyList<CustomOrientation> CustomOrientations => Customs;

        /// <summary>Create a custom orientation from the active object's rotation (or view when none). Selects it.</summary>
        public static CustomOrientation CreateFromActive(string name)
        {
            Transform active = Selection.activeTransform;
            Quaternion rot = active != null ? active.rotation : Quaternion.identity;
            if (active == null)
            {
                SceneView sv = SceneView.lastActiveSceneView;
                if (sv != null) rot = sv.rotation;
            }
            if (string.IsNullOrEmpty(name)) name = "Orientation";

            List<CustomOrientation> list = Customs;
            // Ensure a unique name.
            string unique = name;
            int n = 1;
            while (list.Exists(c => c.name == unique)) unique = name + " " + (++n);

            var co = new CustomOrientation { name = unique, rotationEuler = rot.eulerAngles };
            list.Add(co);
            SceneStateStore.instance.Persist();
            CustomOrientationName = unique;
            Orientation = TransformOrientationMode.Custom;
            return co;
        }

        /// <summary>Delete a custom orientation by name.</summary>
        public static void DeleteCustom(string name)
        {
            List<CustomOrientation> list = Customs;
            int i = list.FindIndex(c => c.name == name);
            if (i >= 0)
            {
                list.RemoveAt(i);
                SceneStateStore.instance.Persist();
                if (CustomOrientationName == name) CustomOrientationName = list.Count > 0 ? list[0].name : string.Empty;
            }
        }

        static CustomOrientation FindCustom(string name)
        {
            List<CustomOrientation> list = Customs;
            for (int i = 0; i < list.Count; i++)
                if (list[i].name == name) return list[i];
            return list.Count > 0 ? list[0] : null;
        }

        // --------------------------------------------------------------- resolution

        /// <summary>Rotation whose axes define the current orientation basis for constraints.</summary>
        public static Quaternion GetOrientationRotation(SceneView sceneView, Transform active)
        {
            switch (Orientation)
            {
                case TransformOrientationMode.Local:
                case TransformOrientationMode.Normal:
                    return active != null ? active.rotation : Quaternion.identity;
                case TransformOrientationMode.View:
                    return sceneView != null && sceneView.camera != null
                        ? sceneView.camera.transform.rotation : Quaternion.identity;
                case TransformOrientationMode.Custom:
                    CustomOrientation co = FindCustom(CustomOrientationName);
                    return co != null ? Quaternion.Euler(co.rotationEuler) : Quaternion.identity;
                default:
                    return Quaternion.identity;
            }
        }

        /// <summary>
        /// World pivot point for the current pivot mode. <see cref="PivotMode.IndividualOrigins"/> resolves
        /// to the median here (callers that support per-object pivots use each object's own origin instead).
        /// </summary>
        public static Vector3 GetPivotPoint(Transform[] targets, Transform active)
        {
            switch (Pivot)
            {
                case PivotMode.ActiveElement:
                    return active != null ? active.position : Median(targets);
                case PivotMode.Cursor3D:
                    return Cursor3D.Position;
                case PivotMode.BoundingBoxCenter:
                    return BoundsCenter(targets);
                case PivotMode.MedianPoint:
                case PivotMode.IndividualOrigins:
                default:
                    return Median(targets);
            }
        }

        static Vector3 Median(Transform[] targets)
        {
            if (targets == null || targets.Length == 0) return Vector3.zero;
            Vector3 sum = Vector3.zero;
            int n = 0;
            for (int i = 0; i < targets.Length; i++)
                if (targets[i] != null) { sum += targets[i].position; n++; }
            return n > 0 ? sum / n : Vector3.zero;
        }

        static Vector3 BoundsCenter(Transform[] targets)
        {
            bool has = false;
            Bounds b = default;
            if (targets != null)
            {
                for (int i = 0; i < targets.Length; i++)
                {
                    Transform t = targets[i];
                    if (t == null) continue;
                    Renderer r = t.GetComponentInChildren<Renderer>();
                    Bounds cur = r != null ? r.bounds : new Bounds(t.position, Vector3.zero);
                    if (!has) { b = cur; has = true; }
                    else b.Encapsulate(cur);
                }
            }
            return has ? b.center : Median(targets);
        }

        /// <summary>Short label for the current pivot mode (HUD/overlay).</summary>
        public static string PivotLabel(PivotMode m)
        {
            switch (m)
            {
                case PivotMode.ActiveElement: return "Active Element";
                case PivotMode.IndividualOrigins: return "Individual Origins";
                case PivotMode.Cursor3D: return "3D Cursor";
                case PivotMode.BoundingBoxCenter: return "Bounding Box Center";
                default: return "Median Point";
            }
        }

        /// <summary>Short label for the current orientation mode (HUD/overlay).</summary>
        public static string OrientationLabel(TransformOrientationMode m)
        {
            switch (m)
            {
                case TransformOrientationMode.Local: return "local";
                case TransformOrientationMode.View: return "view";
                case TransformOrientationMode.Normal: return "normal";
                case TransformOrientationMode.Custom:
                    return string.IsNullOrEmpty(CustomOrientationName) ? "custom" : CustomOrientationName;
                default: return "global";
            }
        }
    }
}

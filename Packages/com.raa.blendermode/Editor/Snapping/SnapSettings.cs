using UnityEditor;
using UnityEngine;

namespace RAA.BlenderMode
{
    /// <summary>Snapping mode (Blender's snap targets minus Edge, which is out of scope). See DESIGN.md §5.</summary>
    public enum SnapMode
    {
        /// <summary>Snap the delta to fixed increments (move/rotate/scale steps).</summary>
        Increment,
        /// <summary>Snap the resulting world position to absolute grid multiples.</summary>
        GridAbsolute,
        /// <summary>Snap the object pivot to the nearest mesh vertex under the cursor.</summary>
        Vertex,
        /// <summary>Snap the object onto the surface (face) under the cursor.</summary>
        Surface
    }

    /// <summary>Axis to align to a hit normal when "Align Rotation to Target" is on.</summary>
    public enum UpAxis
    {
        PlusX, MinusX, PlusY, MinusY, PlusZ, MinusZ
    }

    /// <summary>
    /// Blender's "Snap With" / snap base: which reference point of the moving selection is placed on the
    /// geometry-snap target (Vertex/Surface). See DESIGN.md §5.
    /// </summary>
    public enum SnapBase
    {
        /// <summary>The point of the moving selection closest to the snap target (Blender default).</summary>
        Closest,
        /// <summary>The current transform pivot point (resolved pivot mode).</summary>
        Center,
        /// <summary>The median of the selection's object origins.</summary>
        Median,
        /// <summary>The active object's origin.</summary>
        Active
    }

    /// <summary>
    /// Persisted snapping settings shown in the toolbar overlay / Tool tab. Values are EditorPrefs-backed.
    /// Increments live in <see cref="BlenderModePrefs"/> so they are edited in one place. See DESIGN.md §5.
    /// </summary>
    public static class SnapSettings
    {
        const string P = "RAA.BlenderMode.Snap.";

        static bool GetBool(string k, bool d) => EditorPrefs.GetBool(P + k, d);
        static void SetBool(string k, bool v) { EditorPrefs.SetBool(P + k, v); BlenderModePrefs.RaiseChanged(); }

        /// <summary>Active snap mode.</summary>
        public static SnapMode Mode
        {
            get => (SnapMode)EditorPrefs.GetInt(P + "Mode", (int)SnapMode.Increment);
            set { EditorPrefs.SetInt(P + "Mode", (int)value); BlenderModePrefs.RaiseChanged(); }
        }

        /// <summary>
        /// Snap base ("Snap With"): the reference point of the moving selection that lands on a geometry
        /// (Vertex/Surface) snap target. Default <see cref="SnapBase.Closest"/> like Blender.
        /// </summary>
        public static SnapBase Base
        {
            get => (SnapBase)EditorPrefs.GetInt(P + "Base", (int)SnapBase.Closest);
            set { EditorPrefs.SetInt(P + "Base", (int)value); BlenderModePrefs.RaiseChanged(); }
        }

        /// <summary>Magnet toggle: when on, snapping is always active and Ctrl temporarily disables it (Blender inversion).</summary>
        public static bool Magnet
        {
            get => GetBool("Magnet", false);
            set => SetBool("Magnet", value);
        }

        /// <summary>Whether snapping affects grab/move.</summary>
        public static bool AffectMove { get => GetBool("AffectMove", true); set => SetBool("AffectMove", value); }
        /// <summary>Whether snapping affects rotation.</summary>
        public static bool AffectRotate { get => GetBool("AffectRotate", true); set => SetBool("AffectRotate", value); }
        /// <summary>Whether snapping affects scale.</summary>
        public static bool AffectScale { get => GetBool("AffectScale", true); set => SetBool("AffectScale", value); }

        /// <summary>Surface mode: rotate the object's up-axis to the hit normal.</summary>
        public static bool AlignRotationToTarget
        {
            get => GetBool("AlignRotation", false);
            set => SetBool("AlignRotation", value);
        }

        /// <summary>Which object axis aligns to the surface normal.</summary>
        public static UpAxis AlignAxis
        {
            get => (UpAxis)EditorPrefs.GetInt(P + "AlignAxis", (int)UpAxis.PlusY);
            set { EditorPrefs.SetInt(P + "AlignAxis", (int)value); SceneView.RepaintAll(); }
        }

        /// <summary>Absolute grid size used by <see cref="SnapMode.GridAbsolute"/> and grid snap ops.</summary>
        public static float GridSize
        {
            get => EditorPrefs.GetFloat(P + "GridSize", 1f);
            set { EditorPrefs.SetFloat(P + "GridSize", value); SceneView.RepaintAll(); }
        }

        /// <summary>
        /// Resolve whether snapping is active this event given the Ctrl state, honoring the magnet inversion:
        /// magnet off → active only while Ctrl held; magnet on → active unless Ctrl held.
        /// </summary>
        public static bool IsActive(bool ctrlHeld) => Magnet ? !ctrlHeld : ctrlHeld;

        /// <summary>Vector corresponding to <see cref="AlignAxis"/> in the object's local space.</summary>
        public static Vector3 AlignAxisVector(UpAxis axis)
        {
            switch (axis)
            {
                case UpAxis.PlusX: return Vector3.right;
                case UpAxis.MinusX: return Vector3.left;
                case UpAxis.MinusY: return Vector3.down;
                case UpAxis.PlusZ: return Vector3.forward;
                case UpAxis.MinusZ: return Vector3.back;
                default: return Vector3.up;
            }
        }
    }
}

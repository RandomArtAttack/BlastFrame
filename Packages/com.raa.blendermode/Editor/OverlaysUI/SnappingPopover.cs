using UnityEditor;
using UnityEngine;

namespace RAA.BlenderMode
{
    /// <summary>
    /// Blender-style snapping popover shown from the toolbar's snap button (replaces the plain snap-mode
    /// dropdown). Dark themed like <see cref="BlenderDropdown"/>. Sections: Snap Base (Closest/Center/Median/
    /// Active icon buttons), Snap Target (Increment/Grid Absolute/Vertex/Surface rows), and the toggles
    /// (Align Rotation to Target + align axis, Affect Move/Rotate/Scale). The magnet toggle stays a separate
    /// button on the toolbar. See DESIGN.md §5 / §15.
    /// </summary>
    public sealed class SnappingPopover : PopupWindowContent
    {
        static readonly Color Bg = new Color(0.17f, 0.17f, 0.17f, 1f);
        static readonly Color ActiveBlue = new Color(0x47 / 255f, 0x72 / 255f, 0xB3 / 255f, 1f);
        static readonly Color Label = new Color(0.86f, 0.86f, 0.86f, 1f);

        const float Width = 246f;
        const float Height = 318f;

        GUIStyle _header;
        GUIStyle _rowLabel;

        /// <summary>Open the snapping popover anchored under <paramref name="activatorRect"/>.</summary>
        public static void Show(Rect activatorRect)
        {
            PopupWindow.Show(activatorRect, new SnappingPopover());
        }

        public override Vector2 GetWindowSize() => new Vector2(Width, Height);

        public override void OnGUI(Rect rect)
        {
            EnsureStyles();
            EditorGUI.DrawRect(rect, Bg);

            Event e = Event.current;
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape) { editorWindow.Close(); e.Use(); return; }

            using (new GUILayout.AreaScope(new Rect(8f, 6f, Width - 16f, Height - 12f)))
            {
                GUILayout.Label("Snap Base", _header);
                using (new GUILayout.HorizontalScope())
                {
                    BaseButton(SnapBase.Closest, "snap_base_closest", "Closest — nearest point of the selection");
                    BaseButton(SnapBase.Center, "snap_base_center", "Center — the transform pivot point");
                    BaseButton(SnapBase.Median, "snap_base_median", "Median — selection origins median");
                    BaseButton(SnapBase.Active, "snap_base_active", "Active — the active object's origin");
                }

                GUILayout.Space(6f);
                GUILayout.Label("Snap Target", _header);
                TargetRow(SnapMode.Increment, "snap_increment", "Increment", "Snap the delta to fixed steps");
                TargetRow(SnapMode.GridAbsolute, "snap_grid", "Grid Absolute", "Snap the world position to grid multiples");
                TargetRow(SnapMode.Vertex, "snap_vertex", "Vertex", "Snap to the nearest mesh vertex under the cursor");
                TargetRow(SnapMode.Surface, "snap_face", "Surface", "Snap onto the face under the cursor");

                GUILayout.Space(6f);
                EditorGUI.DrawRect(GUILayoutUtility.GetRect(1f, 1f, GUILayout.ExpandWidth(true)),
                    new Color(1f, 1f, 1f, 0.12f));
                GUILayout.Space(4f);

                bool align = EditorGUILayout.ToggleLeft(
                    new GUIContent(" Align Rotation to Target", "Rotate the align axis to the geometry-snap normal (Surface + Vertex)."),
                    SnapSettings.AlignRotationToTarget);
                if (align != SnapSettings.AlignRotationToTarget) SnapSettings.AlignRotationToTarget = align;

                using (new EditorGUI.DisabledScope(!SnapSettings.AlignRotationToTarget))
                {
                    UpAxis axis = (UpAxis)EditorGUILayout.EnumPopup("Align Axis", SnapSettings.AlignAxis);
                    if (axis != SnapSettings.AlignAxis) SnapSettings.AlignAxis = axis;
                }

                GUILayout.Space(4f);
                GUILayout.Label("Affect", _header);
                using (new GUILayout.HorizontalScope())
                {
                    Affect("Move", SnapSettings.AffectMove, v => SnapSettings.AffectMove = v);
                    Affect("Rotate", SnapSettings.AffectRotate, v => SnapSettings.AffectRotate = v);
                    Affect("Scale", SnapSettings.AffectScale, v => SnapSettings.AffectScale = v);
                }
            }

            if (e.type == EventType.MouseMove) editorWindow.Repaint();
        }

        void BaseButton(SnapBase mode, string icon, string tooltip)
        {
            Color prev = GUI.backgroundColor;
            if (SnapSettings.Base == mode) GUI.backgroundColor = ActiveBlue;
            if (GUILayout.Button(new GUIContent(IconLibrary.Get(icon), tooltip),
                    GUILayout.Width(52f), GUILayout.Height(30f)))
                SnapSettings.Base = mode;
            GUI.backgroundColor = prev;
        }

        void TargetRow(SnapMode mode, string icon, string label, string tooltip)
        {
            Color prev = GUI.backgroundColor;
            if (SnapSettings.Mode == mode) GUI.backgroundColor = ActiveBlue;
            if (GUILayout.Button(new GUIContent(" " + label, IconLibrary.Get(icon), tooltip),
                    _rowLabel, GUILayout.Height(24f)))
                SnapSettings.Mode = mode;
            GUI.backgroundColor = prev;
        }

        static void Affect(string label, bool value, System.Action<bool> set)
        {
            bool nv = GUILayout.Toggle(value, label, EditorStyles.miniButton, GUILayout.Height(20f));
            if (nv != value) set(nv);
        }

        void EnsureStyles()
        {
            if (_header != null) return;
            _header = new GUIStyle(EditorStyles.miniBoldLabel);
            _header.normal.textColor = Label;
            _rowLabel = new GUIStyle(EditorStyles.miniButtonLeft)
            {
                alignment = TextAnchor.MiddleLeft,
                imagePosition = ImagePosition.ImageLeft,
                fixedHeight = 24f
            };
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace RAA.BlenderMode
{
    /// <summary>
    /// A small Blender-style dropdown list shown from a toolbar button (replaces <see cref="GenericMenu"/>,
    /// which can't render icons). Each row is <c>[16px icon] [label]</c> on a dark background; the active row
    /// is highlighted Blender-blue (#4772B3) and the hovered row lightens. Closes on click, Esc, or click-away
    /// (the popup auto-closes when it loses focus). See DESIGN.md §12 / §15.
    /// </summary>
    public sealed class BlenderDropdown : PopupWindowContent
    {
        /// <summary>One selectable row (or a separator when <see cref="separator"/> is set).</summary>
        public struct Row
        {
            public string icon;       // IconLibrary name, or null for no icon
            public string label;
            public bool on;           // draw as the active/checked row (blue)
            public Action action;     // invoked on click
            public bool separator;    // horizontal rule instead of a row

            public static Row Sep() => new Row { separator = true };
        }

        const float RowH = 22f;
        const float IconSz = 16f;
        const float SepH = 7f;
        const float PadY = 4f;

        static readonly Color Bg = new Color(0.17f, 0.17f, 0.17f, 1f);
        static readonly Color ActiveBlue = new Color(0x47 / 255f, 0x72 / 255f, 0xB3 / 255f, 1f);
        static readonly Color HoverTint = new Color(1f, 1f, 1f, 0.09f);
        static readonly Color SepColor = new Color(1f, 1f, 1f, 0.12f);
        static readonly Color LabelColor = new Color(0.86f, 0.86f, 0.86f, 1f);

        readonly List<Row> _rows;
        readonly float _width;
        GUIStyle _labelStyle;
        GUIStyle _labelStyleOn;

        BlenderDropdown(List<Row> rows, float width)
        {
            _rows = rows;
            _width = Mathf.Max(120f, width);
        }

        /// <summary>Open the dropdown anchored under <paramref name="activatorRect"/> (a button's GUI rect).</summary>
        public static void Show(Rect activatorRect, List<Row> rows, float width)
        {
            if (rows == null || rows.Count == 0) return;
            PopupWindow.Show(activatorRect, new BlenderDropdown(rows, width));
        }

        public override Vector2 GetWindowSize()
        {
            float h = PadY * 2f;
            for (int i = 0; i < _rows.Count; i++)
                h += _rows[i].separator ? SepH : RowH;
            return new Vector2(_width, h);
        }

        public override void OnGUI(Rect rect)
        {
            EnsureStyles();
            Event e = Event.current;

            EditorGUI.DrawRect(rect, Bg);

            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
            {
                editorWindow.Close();
                e.Use();
                return;
            }

            float y = PadY;
            for (int i = 0; i < _rows.Count; i++)
            {
                Row r = _rows[i];
                if (r.separator)
                {
                    EditorGUI.DrawRect(new Rect(rect.x + 6f, y + SepH * 0.5f, rect.width - 12f, 1f), SepColor);
                    y += SepH;
                    continue;
                }

                var rowRect = new Rect(0f, y, rect.width, RowH);
                bool hover = rowRect.Contains(e.mousePosition);

                if (r.on) EditorGUI.DrawRect(rowRect, ActiveBlue);
                else if (hover) EditorGUI.DrawRect(rowRect, HoverTint);

                if (!string.IsNullOrEmpty(r.icon))
                {
                    Texture tex = IconLibrary.Get(r.icon);
                    if (tex != null)
                        GUI.DrawTexture(new Rect(rowRect.x + 7f, y + (RowH - IconSz) * 0.5f, IconSz, IconSz),
                            tex, ScaleMode.ScaleToFit);
                }

                var lblRect = new Rect(rowRect.x + 7f + IconSz + 7f, y, rowRect.width - (IconSz + 22f), RowH);
                GUI.Label(lblRect, r.label, r.on ? _labelStyleOn : _labelStyle);

                if (e.type == EventType.MouseDown && e.button == 0 && hover)
                {
                    Action act = r.action;
                    editorWindow.Close();
                    act?.Invoke();
                    e.Use();
                    return;
                }

                y += RowH;
            }

            // Keep the hover highlight live.
            if (e.type == EventType.MouseMove || e.type == EventType.MouseDrag)
                editorWindow.Repaint();
        }

        void EnsureStyles()
        {
            if (_labelStyle != null) return;
            _labelStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 11
            };
            _labelStyle.normal.textColor = LabelColor;
            _labelStyleOn = new GUIStyle(_labelStyle);
            _labelStyleOn.normal.textColor = Color.white;
        }
    }
}

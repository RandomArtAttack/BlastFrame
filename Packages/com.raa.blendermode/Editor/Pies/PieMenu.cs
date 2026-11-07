using UnityEditor;
using UnityEngine;

namespace RAA.BlenderMode
{
    /// <summary>
    /// Generic radial IMGUI pie menu: up to 8 slots at compass directions, a configurable deadzone, and
    /// direction-highlighting. Drawn inside a <see cref="Handles.BeginGUI"/> pass in the scene view. Both
    /// release-confirm (Blender gesture) and click-confirm are supported by the owning operator, which asks
    /// <see cref="HighlightedIndex"/> and calls <see cref="Invoke"/>. See DESIGN.md §8.
    /// </summary>
    public sealed class PieMenu
    {
        /// <summary>One pie slot: an icon, a label and the action to run when picked.</summary>
        public struct Slot
        {
            public string label;
            public Texture icon;
            public System.Action action;
            public bool set;
        }

        const float Radius = 96f;
        const float SlotW = 118f;
        const float SlotH = 40f;

        readonly Slot[] _slots = new Slot[8];
        Vector2 _center;

        static readonly Vector2[] Dirs =
        {
            new Vector2(0f, -1f),                 // 0 N
            new Vector2(0.7071f, -0.7071f),       // 1 NE
            new Vector2(1f, 0f),                  // 2 E
            new Vector2(0.7071f, 0.7071f),        // 3 SE
            new Vector2(0f, 1f),                  // 4 S
            new Vector2(-0.7071f, 0.7071f),       // 5 SW
            new Vector2(-1f, 0f),                 // 6 W
            new Vector2(-0.7071f, -0.7071f),      // 7 NW
        };

        static GUIStyle _slotStyle;
        static GUIStyle _slotStyleHi;

        /// <summary>Set the pie's screen-space center (typically the mouse position at open).</summary>
        public void SetCenter(Vector2 center) => _center = center;

        /// <summary>Assign a slot (0..7, compass order starting at N going clockwise).</summary>
        public void SetSlot(int index, string label, Texture icon, System.Action action)
        {
            if (index < 0 || index >= 8) return;
            _slots[index] = new Slot { label = label, icon = icon, action = action, set = true };
        }

        /// <summary>Index of the slot under the mouse direction, or -1 inside the deadzone / no slot.</summary>
        public int HighlightedIndex(Vector2 mouse)
        {
            Vector2 rel = mouse - _center;
            if (rel.magnitude < Mathf.Max(4f, BlenderModePrefs.PieDeadzone)) return -1;
            rel.Normalize();

            int best = -1;
            float bestDot = 0.5f; // must be within ~45° of a slot direction
            for (int i = 0; i < 8; i++)
            {
                if (!_slots[i].set) continue;
                float d = Vector2.Dot(Dirs[i], rel);
                if (d > bestDot) { bestDot = d; best = i; }
            }
            return best;
        }

        /// <summary>Run the given slot's action.</summary>
        public void Invoke(int index)
        {
            if (index >= 0 && index < 8 && _slots[index].set)
                _slots[index].action?.Invoke();
        }

        /// <summary>Draw the pie (call inside duringSceneGui on Repaint).</summary>
        public void Draw(Vector2 mouse)
        {
            EnsureStyles();
            int hi = HighlightedIndex(mouse);

            Handles.BeginGUI();

            // Center marker for gesture feedback.
            Color prevGui = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.6f);
            GUI.DrawTexture(new Rect(_center.x - 3f, _center.y - 3f, 6f, 6f), EditorGUIUtility.whiteTexture);
            GUI.color = prevGui;

            for (int i = 0; i < 8; i++)
            {
                if (!_slots[i].set) continue;
                Vector2 pos = _center + Dirs[i] * Radius;
                var rect = new Rect(pos.x - SlotW * 0.5f, pos.y - SlotH * 0.5f, SlotW, SlotH);
                var content = new GUIContent(" " + _slots[i].label, _slots[i].icon);
                GUI.Label(rect, content, i == hi ? _slotStyleHi : _slotStyle);
            }

            Handles.EndGUI();
            Handles.color = Color.white;
        }

        static void EnsureStyles()
        {
            if (_slotStyle != null) return;
            _slotStyle = new GUIStyle(EditorStyles.miniButton)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 11,
                imagePosition = ImagePosition.ImageLeft,
                richText = true
            };
            _slotStyleHi = new GUIStyle(_slotStyle) { fontStyle = FontStyle.Bold };
            _slotStyleHi.normal.textColor = new Color(1f, 0.85f, 0.35f);
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

namespace RAA.BlenderMode
{
    /// <summary>
    /// Stable string identifiers for every action Blender Mode can bind, across all phases.
    /// Handlers register against these ids in <see cref="ActionRegistry"/>; unregistered ids no-op.
    /// </summary>
    public static class Actions
    {
        // Navigation (Phase 1)
        public const string ViewFront = "view.front";
        public const string ViewBack = "view.back";
        public const string ViewRight = "view.right";
        public const string ViewLeft = "view.left";
        public const string ViewTop = "view.top";
        public const string ViewBottom = "view.bottom";
        public const string ViewToggleOrtho = "view.toggleOrtho";
        public const string ViewFlip = "view.flip";
        public const string OrbitYawLeft = "orbit.yawLeft";
        public const string OrbitYawRight = "orbit.yawRight";
        public const string OrbitPitchUp = "orbit.pitchUp";
        public const string OrbitPitchDown = "orbit.pitchDown";
        public const string PanLeft = "pan.left";
        public const string PanRight = "pan.right";
        public const string PanUp = "pan.up";
        public const string PanDown = "pan.down";
        public const string ViewCamera = "view.camera";
        public const string FrameSelected = "view.frameSelected";
        public const string FrameAll = "view.frameAll";
        public const string LocalView = "view.localView";
        public const string QuadView = "view.quadView";

        // Transforms (Phase 2)
        public const string Grab = "transform.grab";
        public const string Rotate = "transform.rotate";
        public const string Scale = "transform.scale";
        public const string ClearPosition = "transform.clearPosition";
        public const string ClearRotation = "transform.clearRotation";
        public const string ClearScale = "transform.clearScale";
        public const string Duplicate = "object.duplicate";
        public const string DuplicateLinked = "object.duplicateLinked";
        public const string RepeatLast = "transform.repeatLast";
        public const string AdjustLast = "transform.adjustLast";

        // Viewport header toggles (Phase / §15 header parity)
        public const string XRay = "view.xray";

        // Selection (Phase 3)
        public const string SelectAll = "select.all";
        public const string SelectNone = "select.none";
        public const string SelectInvert = "select.invert";
        public const string BoxSelect = "select.box";
        public const string CircleSelect = "select.circle";
        public const string LassoSelect = "select.lasso";
        public const string HideSelected = "visibility.hideSelected";
        public const string HideUnselected = "visibility.hideUnselected";
        public const string UnhideAll = "visibility.unhideAll";
        public const string Delete = "object.delete";
        public const string DeleteAlt = "object.deleteAlt";
        public const string Parent = "object.parent";
        public const string ClearParent = "object.clearParent";

        // Cursor (Phase 2)
        public const string CursorPlace = "cursor.place";
        public const string CursorReset = "cursor.reset";
        public const string SnapPie = "cursor.snapPie";

        // Pies (Phase 2/3)
        public const string PieShading = "pie.shading";
        public const string PieView = "pie.view";
        public const string PiePivot = "pie.pivot";
        public const string PieOrientation = "pie.orientation";

        // Overlays (Phase 3)
        public const string ToggleNPanel = "panel.toggleN";

        // Bookmarks / selection sets (Phase 3) — indexed 1..9, see Keymap defaults.
        public const string BookmarkStorePrefix = "bookmark.store.";
        public const string BookmarkRecallPrefix = "bookmark.recall.";
        public const string SetStorePrefix = "set.store.";
        public const string SetRecallPrefix = "set.recall.";
    }

    /// <summary>A single input binding: a key (or mouse button) plus modifier state, mapped to an action id.</summary>
    [Serializable]
    public class KeyBinding
    {
        public string actionId;
        public KeyCode key = KeyCode.None;
        public bool ctrl;
        public bool shift;
        public bool alt;
        /// <summary>Mouse button (0 = left, 1 = right, 2 = middle) or -1 when this binding is a keyboard key.</summary>
        public int mouseButton = -1;

        public KeyBinding() { }

        public KeyBinding(string actionId, KeyCode key, bool ctrl = false, bool shift = false, bool alt = false, int mouseButton = -1)
        {
            this.actionId = actionId;
            this.key = key;
            this.ctrl = ctrl;
            this.shift = shift;
            this.alt = alt;
            this.mouseButton = mouseButton;
        }
    }

    /// <summary>
    /// Serializable action-to-binding table with JSON import/export. This is Blender Mode's own keymap;
    /// it deliberately does not use Unity's ShortcutManager so it can be context-sensitive, honor modal
    /// keys, and distinguish numpad keys from the top-row/alpha keys.
    /// </summary>
    [Serializable]
    public class Keymap
    {
        public List<KeyBinding> bindings = new List<KeyBinding>();

        /// <summary>
        /// Resolve the current event to an action id, or null when nothing matches.
        /// A direct match (exact key + modifiers) is always preferred; only if none is found and
        /// <paramref name="emulateNumpad"/> is set are the top-row digit keys folded onto their numpad
        /// equivalents (so Alt+1 bookmarks still win over an emulated numpad view, etc.).
        /// </summary>
        public string Match(Event e, bool emulateNumpad)
        {
            bool ctrl = e.control || e.command;
            bool shift = e.shift;
            bool alt = e.alt;

            if (e.type == EventType.KeyDown)
            {
                string direct = MatchKey(e.keyCode, ctrl, shift, alt);
                if (direct != null) return direct;

                if (emulateNumpad)
                {
                    KeyCode mapped = AlphaToKeypad(e.keyCode);
                    if (mapped != KeyCode.None)
                        return MatchKey(mapped, ctrl, shift, alt);
                }
                return null;
            }

            if (e.type == EventType.MouseDown)
                return MatchMouse(e.button, ctrl, shift, alt);

            return null;
        }

        string MatchKey(KeyCode key, bool ctrl, bool shift, bool alt)
        {
            for (int i = 0; i < bindings.Count; i++)
            {
                KeyBinding b = bindings[i];
                if (b.mouseButton >= 0) continue;
                if (b.key == key && b.ctrl == ctrl && b.shift == shift && b.alt == alt)
                    return b.actionId;
            }
            return null;
        }

        string MatchMouse(int button, bool ctrl, bool shift, bool alt)
        {
            for (int i = 0; i < bindings.Count; i++)
            {
                KeyBinding b = bindings[i];
                if (b.mouseButton != button) continue;
                if (b.ctrl == ctrl && b.shift == shift && b.alt == alt)
                    return b.actionId;
            }
            return null;
        }

        static KeyCode AlphaToKeypad(KeyCode k)
        {
            switch (k)
            {
                case KeyCode.Alpha0: return KeyCode.Keypad0;
                case KeyCode.Alpha1: return KeyCode.Keypad1;
                case KeyCode.Alpha2: return KeyCode.Keypad2;
                case KeyCode.Alpha3: return KeyCode.Keypad3;
                case KeyCode.Alpha4: return KeyCode.Keypad4;
                case KeyCode.Alpha5: return KeyCode.Keypad5;
                case KeyCode.Alpha6: return KeyCode.Keypad6;
                case KeyCode.Alpha7: return KeyCode.Keypad7;
                case KeyCode.Alpha8: return KeyCode.Keypad8;
                case KeyCode.Alpha9: return KeyCode.Keypad9;
                default: return KeyCode.None;
            }
        }

        public string ToJson()
        {
            return JsonUtility.ToJson(this, true);
        }

        public static Keymap FromJson(string json)
        {
            try
            {
                Keymap km = JsonUtility.FromJson<Keymap>(json);
                if (km != null && km.bindings != null && km.bindings.Count > 0)
                    return km;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[BlenderMode] Failed to parse keymap JSON, using defaults. " + ex.Message);
            }
            return null;
        }

        /// <summary>Build the full default keymap (all phases). Later-phase actions are present as data.</summary>
        public static Keymap CreateDefault()
        {
            var km = new Keymap();
            var b = km.bindings;

            void Add(string id, KeyCode key, bool ctrl = false, bool shift = false, bool alt = false)
                => b.Add(new KeyBinding(id, key, ctrl, shift, alt));
            void Mouse(string id, int button, bool ctrl = false, bool shift = false, bool alt = false)
                => b.Add(new KeyBinding(id, KeyCode.None, ctrl, shift, alt, button));

            // --- Navigation: numpad views (distinct from alpha; emulate-numpad pref folds top row on) ---
            Add(Actions.ViewFront, KeyCode.Keypad1);
            Add(Actions.ViewBack, KeyCode.Keypad1, ctrl: true);
            Add(Actions.ViewRight, KeyCode.Keypad3);
            Add(Actions.ViewLeft, KeyCode.Keypad3, ctrl: true);
            Add(Actions.ViewTop, KeyCode.Keypad7);
            Add(Actions.ViewBottom, KeyCode.Keypad7, ctrl: true);
            Add(Actions.ViewToggleOrtho, KeyCode.Keypad5);
            Add(Actions.ViewFlip, KeyCode.Keypad9);
            Add(Actions.OrbitYawLeft, KeyCode.Keypad4);
            Add(Actions.OrbitYawRight, KeyCode.Keypad6);
            Add(Actions.OrbitPitchUp, KeyCode.Keypad8);
            Add(Actions.OrbitPitchDown, KeyCode.Keypad2);
            Add(Actions.PanLeft, KeyCode.Keypad4, ctrl: true);
            Add(Actions.PanRight, KeyCode.Keypad6, ctrl: true);
            Add(Actions.PanUp, KeyCode.Keypad8, ctrl: true);
            Add(Actions.PanDown, KeyCode.Keypad2, ctrl: true);
            Add(Actions.ViewCamera, KeyCode.Keypad0);
            Add(Actions.FrameSelected, KeyCode.KeypadPeriod);
            Add(Actions.FrameAll, KeyCode.Home);
            Add(Actions.LocalView, KeyCode.Slash);
            Add(Actions.LocalView, KeyCode.KeypadDivide);
            Add(Actions.QuadView, KeyCode.Q, ctrl: true, alt: true);

            // --- Transforms ---
            Add(Actions.Grab, KeyCode.G);
            Add(Actions.Rotate, KeyCode.R);
            Add(Actions.Scale, KeyCode.S);
            Add(Actions.ClearPosition, KeyCode.G, alt: true);
            Add(Actions.ClearRotation, KeyCode.R, alt: true);
            Add(Actions.ClearScale, KeyCode.S, alt: true);
            Add(Actions.Duplicate, KeyCode.D, shift: true);
            Add(Actions.DuplicateLinked, KeyCode.D, alt: true);
            Add(Actions.RepeatLast, KeyCode.R, shift: true);
            Add(Actions.AdjustLast, KeyCode.F9);
            Add(Actions.XRay, KeyCode.Z, alt: true);

            // --- Selection ---
            Add(Actions.SelectAll, KeyCode.A);
            Add(Actions.SelectNone, KeyCode.A, alt: true);
            Add(Actions.SelectInvert, KeyCode.I, ctrl: true);
            Add(Actions.BoxSelect, KeyCode.B);
            Add(Actions.CircleSelect, KeyCode.C);
            Mouse(Actions.LassoSelect, 1, ctrl: true);
            Add(Actions.HideSelected, KeyCode.H);
            Add(Actions.HideUnselected, KeyCode.H, shift: true);
            Add(Actions.UnhideAll, KeyCode.H, alt: true);
            Add(Actions.Delete, KeyCode.X);
            Add(Actions.DeleteAlt, KeyCode.Delete);
            Add(Actions.Parent, KeyCode.P, ctrl: true);
            Add(Actions.ClearParent, KeyCode.P, alt: true);

            // --- 3D cursor ---
            Mouse(Actions.CursorPlace, 1, shift: true);
            Add(Actions.CursorReset, KeyCode.C, shift: true);
            Add(Actions.SnapPie, KeyCode.S, shift: true);

            // --- Pies ---
            Add(Actions.PieShading, KeyCode.Z);
            Add(Actions.PieView, KeyCode.BackQuote);
            Add(Actions.PiePivot, KeyCode.Period);
            Add(Actions.PieOrientation, KeyCode.Comma);

            // --- Overlays ---
            Add(Actions.ToggleNPanel, KeyCode.N);

            // --- Bookmarks & selection sets (1..9) ---
            for (int i = 1; i <= 9; i++)
            {
                KeyCode digit = KeyCode.Alpha0 + i;
                Add(Actions.BookmarkStorePrefix + i, digit, ctrl: true, alt: true);
                Add(Actions.BookmarkRecallPrefix + i, digit, alt: true);
                Add(Actions.SetStorePrefix + i, digit, ctrl: true, shift: true);
                Add(Actions.SetRecallPrefix + i, digit, shift: true);
            }

            return km;
        }
    }
}

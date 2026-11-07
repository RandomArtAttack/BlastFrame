using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace RAA.BlenderMode
{
    /// <summary>Turntable vs. trackball orbit behaviour.</summary>
    public enum OrbitStyle
    {
        Turntable,
        Trackball
    }

    /// <summary>How a pie menu confirms a selection.</summary>
    public enum PieConfirmMode
    {
        /// <summary>Release-confirm gesture and click both work.</summary>
        Both,
        /// <summary>Only clicking a slot confirms.</summary>
        ClickOnly,
        /// <summary>Only releasing the pie key confirms.</summary>
        ReleaseOnly
    }

    /// <summary>
    /// All Blender Mode preferences, EditorPrefs-backed so they persist per user/machine.
    /// Groups exposed here mirror the SettingsProvider UI. Later-phase groups (Transforms, Snapping,
    /// Pies, Selection) are represented by their few Phase-1-relevant keys plus stubs.
    /// </summary>
    public static class BlenderModePrefs
    {
        const string P = "RAA.BlenderMode.";

        /// <summary>Raised whenever any preference changes so live views can refresh.</summary>
        public static event Action Changed;

        static void Notify()
        {
            Changed?.Invoke();
            SceneView.RepaintAll();
        }

        /// <summary>Raise <see cref="Changed"/> from external state holders (SnapSettings, PivotOrientation,
        /// ShadingModes) so live overlays repaint when their state changes.</summary>
        public static void RaiseChanged() => Notify();

        // ---- helpers ----
        static bool GetBool(string key, bool def) => EditorPrefs.GetBool(P + key, def);
        static void SetBool(string key, bool value, bool current)
        {
            if (value == current) return;
            EditorPrefs.SetBool(P + key, value);
            Notify();
        }
        static float GetFloat(string key, float def) => EditorPrefs.GetFloat(P + key, def);
        static void SetFloat(string key, float value, float current)
        {
            if (Mathf.Approximately(value, current)) return;
            EditorPrefs.SetFloat(P + key, value);
            Notify();
        }
        static int GetInt(string key, int def) => EditorPrefs.GetInt(P + key, def);
        static void SetInt(string key, int value, int current)
        {
            if (value == current) return;
            EditorPrefs.SetInt(P + key, value);
            Notify();
        }

        // ================= General =================

        /// <summary>Master toggle. When false the addon consumes nothing and Unity is 100% stock.</summary>
        public static bool Enabled
        {
            get => GetBool("Enabled", false);
            set => SetBool("Enabled", value, Enabled);
        }

        /// <summary>Ignore all input while in Play Mode.</summary>
        public static bool SuspendInPlayMode
        {
            get => GetBool("SuspendInPlayMode", true);
            set => SetBool("SuspendInPlayMode", value, SuspendInPlayMode);
        }

        /// <summary>Map the top-row 1..0 keys onto the numpad view actions.</summary>
        public static bool EmulateNumpad
        {
            get => GetBool("EmulateNumpad", false);
            set => SetBool("EmulateNumpad", value, EmulateNumpad);
        }

        // ================= Navigation =================

        public static OrbitStyle OrbitStyle
        {
            get => (OrbitStyle)GetInt("OrbitStyle", (int)OrbitStyle.Turntable);
            set => SetInt("OrbitStyle", (int)value, GetInt("OrbitStyle", (int)OrbitStyle.Turntable));
        }

        public static bool OrbitAroundSelection
        {
            get => GetBool("OrbitAroundSelection", false);
            set => SetBool("OrbitAroundSelection", value, OrbitAroundSelection);
        }

        public static bool ZoomToMouse
        {
            get => GetBool("ZoomToMouse", true);
            set => SetBool("ZoomToMouse", value, ZoomToMouse);
        }

        public static bool InvertZoom
        {
            get => GetBool("InvertZoom", false);
            set => SetBool("InvertZoom", value, InvertZoom);
        }

        /// <summary>Degrees of orbit per pixel of mouse movement.</summary>
        public static float OrbitSensitivity
        {
            get => GetFloat("OrbitSensitivity", 0.4f);
            set => SetFloat("OrbitSensitivity", value, OrbitSensitivity);
        }

        /// <summary>Switch to orthographic when entering an axis view (Blender behaviour).</summary>
        public static bool AutoOrthoOnAxisViews
        {
            get => GetBool("AutoOrthoOnAxisViews", true);
            set => SetBool("AutoOrthoOnAxisViews", value, AutoOrthoOnAxisViews);
        }

        // ================= Modules =================

        public static bool ModuleNavigation
        {
            get => GetBool("Module.Navigation", true);
            set => SetBool("Module.Navigation", value, ModuleNavigation);
        }
        public static bool ModuleTransforms
        {
            get => GetBool("Module.Transforms", true);
            set => SetBool("Module.Transforms", value, ModuleTransforms);
        }
        public static bool ModuleSelection
        {
            get => GetBool("Module.Selection", true);
            set => SetBool("Module.Selection", value, ModuleSelection);
        }
        public static bool ModulePies
        {
            get => GetBool("Module.Pies", true);
            set => SetBool("Module.Pies", value, ModulePies);
        }
        public static bool ModuleCursor
        {
            get => GetBool("Module.Cursor", true);
            set => SetBool("Module.Cursor", value, ModuleCursor);
        }
        public static bool ModuleOverlays
        {
            get => GetBool("Module.Overlays", true);
            set => SetBool("Module.Overlays", value, ModuleOverlays);
        }
        public static bool ModuleBookmarks
        {
            get => GetBool("Module.Bookmarks", true);
            set => SetBool("Module.Bookmarks", value, ModuleBookmarks);
        }

        // ================= Transforms (stubs used later) =================

        public static float IncrementMove
        {
            get => GetFloat("IncrementMove", 0.25f);
            set => SetFloat("IncrementMove", value, IncrementMove);
        }
        public static float IncrementRotate
        {
            get => GetFloat("IncrementRotate", 5f);
            set => SetFloat("IncrementRotate", value, IncrementRotate);
        }
        public static float IncrementScale
        {
            get => GetFloat("IncrementScale", 0.1f);
            set => SetFloat("IncrementScale", value, IncrementScale);
        }
        public static float PrecisionFactor
        {
            get => GetFloat("PrecisionFactor", 0.1f);
            set => SetFloat("PrecisionFactor", value, PrecisionFactor);
        }

        /// <summary>When on, the Grab move-snap increment scales with zoom (Blender adaptive increment);
        /// when off, the fixed <see cref="IncrementMove"/> is used. Default on.</summary>
        public static bool AdaptiveIncrement
        {
            get => GetBool("AdaptiveIncrement", true);
            set => SetBool("AdaptiveIncrement", value, AdaptiveIncrement);
        }

        /// <summary>Whether F9 opens the adjust-last popup after a transform.</summary>
        public static bool AdjustLastEnabled
        {
            get => GetBool("AdjustLastEnabled", true);
            set => SetBool("AdjustLastEnabled", value, AdjustLastEnabled);
        }

        /// <summary>When on, activating Unity's Move/Rotate/Scale tools swaps to Blender Mode's pivot- and
        /// snap-aware gizmo tools. Default on. See <see cref="BlenderTransformToolBridge"/>.</summary>
        public static bool OverrideTransformGizmos
        {
            get => GetBool("OverrideTransformGizmos", true);
            set => SetBool("OverrideTransformGizmos", value, OverrideTransformGizmos);
        }

        // ================= Viewport (header parity, §15) =================

        /// <summary>Grouped overlays visibility (scene grid, gizmos, 3D cursor, modal HUD). Persisted.</summary>
        public static bool OverlaysVisible
        {
            get => GetBool("OverlaysVisible", true);
            set => SetBool("OverlaysVisible", value, OverlaysVisible);
        }

        // ================= Cursor =================

        /// <summary>Shift+RMB placement orients the 3D cursor to the surface normal.</summary>
        public static bool CursorOrientToSurface
        {
            get => GetBool("CursorOrientToSurface", false);
            set => SetBool("CursorOrientToSurface", value, CursorOrientToSurface);
        }

        // ================= Pies =================

        /// <summary>Deadzone radius (pixels) inside which the pie makes no selection.</summary>
        public static float PieDeadzone
        {
            get => GetFloat("PieDeadzone", 20f);
            set => SetFloat("PieDeadzone", value, PieDeadzone);
        }

        /// <summary>How pie menus confirm the pointed-at slot.</summary>
        public static PieConfirmMode PieConfirm
        {
            get => (PieConfirmMode)GetInt("PieConfirm", (int)PieConfirmMode.Both);
            set => SetInt("PieConfirm", (int)value, (int)PieConfirm);
        }

        // ================= Selection =================

        /// <summary>Show a confirmation dialog before X / Delete removes the selection (default off = instant).</summary>
        public static bool DeleteConfirm
        {
            get => GetBool("DeleteConfirm", false);
            set => SetBool("DeleteConfirm", value, DeleteConfirm);
        }

        // ================= Tools =================

        /// <summary>Drop-to-surface also rotates each object's up-axis to the hit normal.</summary>
        public static bool DropAlignToNormal
        {
            get => GetBool("DropAlignToNormal", false);
            set => SetBool("DropAlignToNormal", value, DropAlignToNormal);
        }

        // ================= Keymap =================

        static Keymap _keymap;

        /// <summary>The active keymap, loaded lazily from EditorPrefs JSON or built from defaults.</summary>
        public static Keymap Keymap
        {
            get
            {
                if (_keymap == null)
                {
                    string json = EditorPrefs.GetString(P + "Keymap", string.Empty);
                    _keymap = string.IsNullOrEmpty(json) ? null : Keymap.FromJson(json);
                    if (_keymap == null) _keymap = Keymap.CreateDefault();
                }
                return _keymap;
            }
        }

        public static void SaveKeymap()
        {
            if (_keymap != null)
            {
                EditorPrefs.SetString(P + "Keymap", _keymap.ToJson());
                Notify();
            }
        }

        public static void ResetKeymap()
        {
            _keymap = Keymap.CreateDefault();
            SaveKeymap();
        }

        public static void ImportKeymapJson(string json)
        {
            Keymap km = Keymap.FromJson(json);
            if (km != null)
            {
                _keymap = km;
                SaveKeymap();
            }
        }

        public static string ExportKeymapJson() => Keymap.ToJson();
    }

    /// <summary>Preferences page under Preferences/Blender Mode. General, Navigation and Modules are
    /// functional now; later-phase groups are shown as stubs to be filled in Phases 2-3.</summary>
    static class BlenderModeSettingsProvider
    {
        [SettingsProvider]
        public static SettingsProvider Create()
        {
            var provider = new SettingsProvider("Preferences/Blender Mode", SettingsScope.User)
            {
                label = "Blender Mode",
                guiHandler = _ => DrawGui(),
                keywords = new HashSet<string>(new[]
                {
                    "blender", "navigation", "numpad", "orbit", "zoom", "snap", "pie", "modal", "scene view"
                })
            };
            return provider;
        }

        static void DrawGui()
        {
            EditorGUIUtility.labelWidth = 220f;
            EditorGUILayout.Space();

            // --- General ---
            EditorGUILayout.LabelField("General", EditorStyles.boldLabel);
            BlenderModePrefs.Enabled = EditorGUILayout.Toggle(
                new GUIContent("Enabled (master toggle)", "When off, Blender Mode consumes no input."),
                BlenderModePrefs.Enabled);
            BlenderModePrefs.SuspendInPlayMode = EditorGUILayout.Toggle(
                "Suspend In Play Mode", BlenderModePrefs.SuspendInPlayMode);
            BlenderModePrefs.EmulateNumpad = EditorGUILayout.Toggle(
                new GUIContent("Emulate Numpad", "Map top-row 1..0 to the numpad view actions."),
                BlenderModePrefs.EmulateNumpad);
            if (BlenderModePrefs.EmulateNumpad)
                EditorGUILayout.HelpBox(
                    "With Emulate Numpad on, the Shift+1..9 selection-set shortcuts are unavailable (they collide with emulated views). Use the Sets tab instead.",
                    MessageType.Info);

            EditorGUILayout.Space();

            // --- Navigation ---
            EditorGUILayout.LabelField("Navigation", EditorStyles.boldLabel);
            BlenderModePrefs.OrbitStyle = (OrbitStyle)EditorGUILayout.EnumPopup("Orbit Style", BlenderModePrefs.OrbitStyle);
            if (BlenderModePrefs.OrbitStyle == OrbitStyle.Trackball)
                EditorGUILayout.HelpBox("Trackball orbit is planned; turntable is used for now.", MessageType.None);
            BlenderModePrefs.OrbitAroundSelection = EditorGUILayout.Toggle("Orbit Around Selection", BlenderModePrefs.OrbitAroundSelection);
            BlenderModePrefs.ZoomToMouse = EditorGUILayout.Toggle("Zoom To Mouse Position", BlenderModePrefs.ZoomToMouse);
            BlenderModePrefs.InvertZoom = EditorGUILayout.Toggle("Invert Zoom Direction", BlenderModePrefs.InvertZoom);
            BlenderModePrefs.OrbitSensitivity = EditorGUILayout.Slider("Orbit Sensitivity (deg/px)", BlenderModePrefs.OrbitSensitivity, 0.05f, 1.5f);
            BlenderModePrefs.AutoOrthoOnAxisViews = EditorGUILayout.Toggle("Auto Ortho On Axis Views", BlenderModePrefs.AutoOrthoOnAxisViews);

            EditorGUILayout.Space();

            // --- Modules ---
            EditorGUILayout.LabelField("Modules", EditorStyles.boldLabel);
            BlenderModePrefs.ModuleNavigation = EditorGUILayout.Toggle("Navigation", BlenderModePrefs.ModuleNavigation);
            BlenderModePrefs.ModuleTransforms = EditorGUILayout.Toggle("Transforms", BlenderModePrefs.ModuleTransforms);
            BlenderModePrefs.ModuleSelection = EditorGUILayout.Toggle("Selection", BlenderModePrefs.ModuleSelection);
            BlenderModePrefs.ModulePies = EditorGUILayout.Toggle("Pies", BlenderModePrefs.ModulePies);
            BlenderModePrefs.ModuleCursor = EditorGUILayout.Toggle("Cursor", BlenderModePrefs.ModuleCursor);
            BlenderModePrefs.ModuleOverlays = EditorGUILayout.Toggle("Overlays", BlenderModePrefs.ModuleOverlays);
            BlenderModePrefs.ModuleBookmarks = EditorGUILayout.Toggle("Bookmarks", BlenderModePrefs.ModuleBookmarks);

            EditorGUILayout.Space();

            // --- Transforms ---
            EditorGUILayout.LabelField("Transforms", EditorStyles.boldLabel);
            BlenderModePrefs.IncrementMove = EditorGUILayout.FloatField(
                new GUIContent("Move Increment", "Grab snap step (Ctrl)."), BlenderModePrefs.IncrementMove);
            BlenderModePrefs.IncrementRotate = EditorGUILayout.FloatField(
                new GUIContent("Rotate Increment (°)", "Rotate snap step (Ctrl)."), BlenderModePrefs.IncrementRotate);
            BlenderModePrefs.IncrementScale = EditorGUILayout.FloatField(
                new GUIContent("Scale Increment", "Scale snap step (Ctrl)."), BlenderModePrefs.IncrementScale);
            BlenderModePrefs.AdaptiveIncrement = EditorGUILayout.Toggle(
                new GUIContent("Adaptive Increment", "Move snap step scales with zoom (Blender-style). Off = fixed Move Increment."),
                BlenderModePrefs.AdaptiveIncrement);
            BlenderModePrefs.PrecisionFactor = EditorGUILayout.Slider(
                new GUIContent("Precision Factor", "Movement multiplier while Shift is held."),
                BlenderModePrefs.PrecisionFactor, 0.01f, 1f);
            BlenderModePrefs.AdjustLastEnabled = EditorGUILayout.Toggle(
                new GUIContent("Adjust-Last Popup (F9)", "Show the adjust-last popup after a transform."),
                BlenderModePrefs.AdjustLastEnabled);
            BlenderModePrefs.OverrideTransformGizmos = EditorGUILayout.Toggle(
                new GUIContent("Override Transform Gizmos", "Replace Unity's Move/Rotate/Scale tools with Blender-aware versions honoring pivot mode and snapping."),
                BlenderModePrefs.OverrideTransformGizmos);

            EditorGUILayout.Space();

            // --- Viewport ---
            EditorGUILayout.LabelField("Viewport", EditorStyles.boldLabel);
            BlenderModePrefs.OverlaysVisible = EditorGUILayout.Toggle(
                new GUIContent("Overlays Visible", "Grouped grid / gizmos / 3D cursor / modal HUD visibility."),
                BlenderModePrefs.OverlaysVisible);
            EditorGUILayout.HelpBox(
                "X-Ray (Alt+Z) toggles the scene view into wireframe and back. Gizmos and shading pills live on the toolbar overlay.",
                MessageType.None);

            EditorGUILayout.Space();

            // --- Snapping ---
            EditorGUILayout.LabelField("Snapping", EditorStyles.boldLabel);
            SnapSettings.Mode = (SnapMode)EditorGUILayout.EnumPopup("Default Mode", SnapSettings.Mode);
            SnapSettings.Base = (SnapBase)EditorGUILayout.EnumPopup(
                new GUIContent("Snap Base", "Which point of the moving selection lands on a Vertex/Surface target (Blender 'Snap With')."),
                SnapSettings.Base);
            SnapSettings.Magnet = EditorGUILayout.Toggle(
                new GUIContent("Magnet (always-on)", "Snapping always on; Ctrl temporarily disables it."),
                SnapSettings.Magnet);
            SnapSettings.GridSize = EditorGUILayout.FloatField("Grid Size", SnapSettings.GridSize);
            EditorGUILayout.LabelField("Affect", EditorStyles.miniBoldLabel);
            EditorGUI.indentLevel++;
            SnapSettings.AffectMove = EditorGUILayout.Toggle("Move", SnapSettings.AffectMove);
            SnapSettings.AffectRotate = EditorGUILayout.Toggle("Rotate", SnapSettings.AffectRotate);
            SnapSettings.AffectScale = EditorGUILayout.Toggle("Scale", SnapSettings.AffectScale);
            EditorGUI.indentLevel--;
            SnapSettings.AlignRotationToTarget = EditorGUILayout.Toggle(
                new GUIContent("Align Rotation to Target", "Surface mode: rotate the align axis to the hit normal."),
                SnapSettings.AlignRotationToTarget);
            SnapSettings.AlignAxis = (UpAxis)EditorGUILayout.EnumPopup("Align Axis", SnapSettings.AlignAxis);

            EditorGUILayout.Space();

            // --- Cursor ---
            EditorGUILayout.LabelField("3D Cursor", EditorStyles.boldLabel);
            BlenderModePrefs.CursorOrientToSurface = EditorGUILayout.Toggle(
                new GUIContent("Orient to Surface", "Shift+RMB aligns the cursor to the surface normal."),
                BlenderModePrefs.CursorOrientToSurface);

            EditorGUILayout.Space();

            // --- Pies ---
            EditorGUILayout.LabelField("Pies", EditorStyles.boldLabel);
            BlenderModePrefs.PieDeadzone = EditorGUILayout.Slider("Deadzone Radius (px)", BlenderModePrefs.PieDeadzone, 0f, 80f);
            BlenderModePrefs.PieConfirm = (PieConfirmMode)EditorGUILayout.EnumPopup("Confirm Mode", BlenderModePrefs.PieConfirm);

            EditorGUILayout.Space();

            // --- Selection ---
            EditorGUILayout.LabelField("Selection", EditorStyles.boldLabel);
            BlenderModePrefs.DeleteConfirm = EditorGUILayout.Toggle(
                new GUIContent("Confirm Before Delete", "Show a confirmation dialog before X / Delete."),
                BlenderModePrefs.DeleteConfirm);
            BlenderModePrefs.DropAlignToNormal = EditorGUILayout.Toggle(
                new GUIContent("Drop Aligns to Normal", "Drop-to-Surface also rotates the object to the hit normal."),
                BlenderModePrefs.DropAlignToNormal);

            EditorGUILayout.Space();

            // --- Keymap ---
            DrawKeymapEditor();
        }

        // ------------------------------------------------------------- keymap editor

        static Vector2 _keymapScroll;
        static int _rebindIndex = -1;
        static string _keymapFilter = string.Empty;

        static void DrawKeymapEditor()
        {
            EditorGUILayout.LabelField("Keymap", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Export JSON...", GUILayout.Width(120f))) ExportKeymap();
                if (GUILayout.Button("Import JSON...", GUILayout.Width(120f))) ImportKeymap();
                if (GUILayout.Button("Reset to Defaults", GUILayout.Width(140f)))
                {
                    if (EditorUtility.DisplayDialog("Reset Keymap", "Restore all default bindings?", "Reset", "Cancel"))
                    {
                        BlenderModePrefs.ResetKeymap();
                        _rebindIndex = -1;
                    }
                }
            }

            _keymapFilter = EditorGUILayout.TextField("Filter", _keymapFilter);

            // Capture the next key press while rebinding.
            if (_rebindIndex >= 0)
                CaptureRebind();

            List<KeyBinding> bindings = BlenderModePrefs.Keymap.bindings;
            _keymapScroll = EditorGUILayout.BeginScrollView(_keymapScroll, GUILayout.Height(260f));
            for (int i = 0; i < bindings.Count; i++)
            {
                KeyBinding b = bindings[i];
                if (!string.IsNullOrEmpty(_keymapFilter) &&
                    b.actionId.IndexOf(_keymapFilter, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(b.actionId, GUILayout.Width(200f));
                    EditorGUILayout.LabelField(DescribeBinding(b), EditorStyles.miniLabel);
                    if (_rebindIndex == i)
                    {
                        GUILayout.Label("Press a key… (Esc cancels)", EditorStyles.miniBoldLabel, GUILayout.Width(180f));
                    }
                    else if (GUILayout.Button("Rebind", GUILayout.Width(70f)))
                    {
                        _rebindIndex = i;
                    }
                }
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.HelpBox(
                "Rebind captures a keyboard key plus the modifiers held when you press it. Mouse-button bindings " +
                "(e.g. lasso, cursor place) keep their button; Rebind converts them to a key.",
                MessageType.None);
        }

        static void CaptureRebind()
        {
            Event e = Event.current;
            if (e == null || e.type != EventType.KeyDown) return;

            if (e.keyCode == KeyCode.Escape)
            {
                _rebindIndex = -1;
                e.Use();
                return;
            }

            // Ignore lone modifier presses; wait for a real key.
            if (e.keyCode == KeyCode.LeftControl || e.keyCode == KeyCode.RightControl ||
                e.keyCode == KeyCode.LeftShift || e.keyCode == KeyCode.RightShift ||
                e.keyCode == KeyCode.LeftAlt || e.keyCode == KeyCode.RightAlt ||
                e.keyCode == KeyCode.LeftCommand || e.keyCode == KeyCode.RightCommand ||
                e.keyCode == KeyCode.None)
                return;

            List<KeyBinding> bindings = BlenderModePrefs.Keymap.bindings;
            if (_rebindIndex >= 0 && _rebindIndex < bindings.Count)
            {
                KeyBinding b = bindings[_rebindIndex];
                b.key = e.keyCode;
                b.mouseButton = -1;
                b.ctrl = e.control || e.command;
                b.shift = e.shift;
                b.alt = e.alt;
                BlenderModePrefs.SaveKeymap();
            }
            _rebindIndex = -1;
            e.Use();
        }

        static string DescribeBinding(KeyBinding b)
        {
            var sb = new System.Text.StringBuilder();
            if (b.ctrl) sb.Append("Ctrl+");
            if (b.alt) sb.Append("Alt+");
            if (b.shift) sb.Append("Shift+");
            if (b.mouseButton >= 0)
                sb.Append(b.mouseButton == 0 ? "LMB" : b.mouseButton == 1 ? "RMB" : b.mouseButton == 2 ? "MMB" : "Mouse" + b.mouseButton);
            else
                sb.Append(b.key.ToString());
            return sb.ToString();
        }

        static void ExportKeymap()
        {
            string path = EditorUtility.SaveFilePanel("Export Keymap", "", "blendermode-keymap.json", "json");
            if (string.IsNullOrEmpty(path)) return;
            try { File.WriteAllText(path, BlenderModePrefs.ExportKeymapJson()); }
            catch (Exception ex) { Debug.LogError("[BlenderMode] Keymap export failed: " + ex.Message); }
        }

        static void ImportKeymap()
        {
            string path = EditorUtility.OpenFilePanel("Import Keymap", "", "json");
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                string json = File.ReadAllText(path);
                BlenderModePrefs.ImportKeymapJson(json);
            }
            catch (Exception ex) { Debug.LogError("[BlenderMode] Keymap import failed: " + ex.Message); }
        }
    }
}

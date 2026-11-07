using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEngine;
using UnityEngine.UIElements;

namespace RAA.BlenderMode
{
    /// <summary>
    /// SceneView toolbar overlay replicating Blender's 3D-viewport header (DESIGN.md §15). Left→right:
    /// master toggle | orientation + pivot dropdowns | magnet + snap-mode | spacer |
    /// gizmos + overlays + x-ray toggles | four shading pills + shading menu. Built as a compact IMGUI row;
    /// dropdowns use the icon-capable <see cref="BlenderDropdown"/> (not GenericMenu). All controls read/write
    /// the same state the keymap actions drive and live-sync via <see cref="BlenderModePrefs.Changed"/>.
    /// </summary>
    [Overlay(typeof(SceneView), Id, "Blender Mode", true)]
    public class BlenderToolbarOverlay : Overlay
    {
        public const string Id = "blendermode-toolbar";

        static readonly Color ActiveBlue = new Color(0x47 / 255f, 0x72 / 255f, 0xB3 / 255f); // Blender #4772B3

        IMGUIContainer _imgui;

        public override VisualElement CreatePanelContent()
        {
            _imgui = new IMGUIContainer(DrawToolbar) { style = { minWidth = 560f } };

            BlenderModePrefs.Changed -= RequestRepaint;
            BlenderModePrefs.Changed += RequestRepaint;

            return _imgui;
        }

        public override void OnWillBeDestroyed()
        {
            BlenderModePrefs.Changed -= RequestRepaint;
            base.OnWillBeDestroyed();
        }

        void RequestRepaint() => _imgui?.MarkDirtyRepaint();

        void DrawToolbar()
        {
            SceneView sv = SceneView.lastActiveSceneView;

            using (new GUILayout.HorizontalScope())
            {
                // ---- master toggle ----
                bool enabled = BlenderModePrefs.Enabled;
                bool newEnabled = GUILayout.Toggle(enabled,
                    new GUIContent(enabled ? "BM ●" : "BM ○", "Blender Mode master toggle (Ctrl+Alt+B)"),
                    EditorStyles.toolbarButton, GUILayout.Width(46f));
                if (newEnabled != enabled) BlenderModePrefs.Enabled = newEnabled;

                Sep();

                // ---- orientation + pivot ----
                if (DropButton(OrientIcon(PivotOrientation.Orientation),
                        OrientLabel(PivotOrientation.Orientation), "Transform Orientation (,)", 96f))
                    ShowOrientationMenu(GUILayoutUtility.GetLastRect());

                if (DropButton(PivotIcon(PivotOrientation.Pivot), null,
                        "Pivot Point (.): " + PivotOrientation.PivotLabel(PivotOrientation.Pivot), 30f))
                    ShowPivotMenu(GUILayoutUtility.GetLastRect());

                Sep();

                // ---- snapping: magnet + snapping popover ----
                bool magnet = SnapSettings.Magnet;
                bool newMagnet = GUILayout.Toggle(magnet,
                    new GUIContent(IconLibrary.Get("magnet"), "Snapping (Ctrl) — magnet: always-on; Ctrl inverts"),
                    EditorStyles.toolbarButton, GUILayout.Width(30f));
                if (newMagnet != magnet) SnapSettings.Magnet = newMagnet;

                if (DropButton(SnapModeIcon(SnapSettings.Mode), SnapSettings.Mode.ToString(),
                        "Snapping — target, base, align rotation, affect", 96f))
                    SnappingPopover.Show(GUILayoutUtility.GetLastRect());

                GUILayout.FlexibleSpace();

                // ---- right cluster: gizmos / overlays / x-ray ----
                if (sv != null)
                {
                    bool gizmos = sv.drawGizmos;
                    bool newGizmos = GUILayout.Toggle(gizmos,
                        new GUIContent(IconLibrary.Get("gizmo"), "Gizmos — draw component gizmos"),
                        EditorStyles.toolbarButton, GUILayout.Width(30f));
                    if (newGizmos != gizmos) { sv.drawGizmos = newGizmos; sv.Repaint(); }
                }

                bool overlays = BlenderModePrefs.OverlaysVisible;
                bool newOverlays = GUILayout.Toggle(overlays,
                    new GUIContent(IconLibrary.Get("overlays"), "Overlays — grid, gizmos, 3D cursor, HUD"),
                    EditorStyles.toolbarButton, GUILayout.Width(30f));
                if (newOverlays != overlays) ShadingModes.SetOverlays(sv, newOverlays);

                if (sv != null)
                {
                    bool xray = ShadingModes.IsXRay(sv);
                    bool newXray = GUILayout.Toggle(xray,
                        new GUIContent(IconLibrary.Get("xray"), "X-Ray (wireframe) — Alt+Z"),
                        EditorStyles.toolbarButton, GUILayout.Width(30f));
                    if (newXray != xray) ShadingModes.ToggleXRay(sv);
                }

                Sep();

                // ---- shading pills + menu ----
                if (sv != null)
                {
                    ShadingMode cur = ShadingModes.Current(sv);
                    Pill(sv, ShadingMode.Wireframe, "shade_wire", "Wireframe", cur);
                    Pill(sv, ShadingMode.Solid, "shade_solid", "Solid", cur);
                    Pill(sv, ShadingMode.MaterialPreview, "shade_material", "Material Preview", cur);
                    Pill(sv, ShadingMode.Rendered, "shade_rendered", "Rendered", cur);

                    if (GUILayout.Button(new GUIContent("▾", "Shading options"),
                            EditorStyles.toolbarButton, GUILayout.Width(20f)))
                        ShowShadingMenu(sv, GUILayoutUtility.GetLastRect());
                }
            }
        }

        // ------------------------------------------------------------- widgets

        static void Sep() => GUILayout.Space(4f);

        void Pill(SceneView sv, ShadingMode mode, string icon, string tip, ShadingMode current)
        {
            Color prev = GUI.backgroundColor;
            if (current == mode) GUI.backgroundColor = ActiveBlue;
            if (GUILayout.Button(new GUIContent(IconLibrary.Get(icon), tip + " (Z pie)"),
                    EditorStyles.toolbarButton, GUILayout.Width(28f)))
                ShadingModes.Apply(sv, mode);
            GUI.backgroundColor = prev;
        }

        /// <summary>A toolbar dropdown button: optional icon + optional label. Returns true when clicked.</summary>
        static bool DropButton(string icon, string label, string tooltip, float width)
        {
            Texture tex = icon != null ? IconLibrary.Get(icon) : null;
            GUIContent content = label != null
                ? new GUIContent(" " + label, tex, tooltip)
                : new GUIContent(string.Empty, tex, tooltip);
            return GUILayout.Button(content, EditorStyles.toolbarDropDown, GUILayout.Width(width));
        }

        static string SnapModeIcon(SnapMode mode)
        {
            switch (mode)
            {
                case SnapMode.GridAbsolute: return "snap_grid";
                case SnapMode.Vertex: return "snap_vertex";
                case SnapMode.Surface: return "snap_face";
                default: return "snap_increment";
            }
        }

        static string OrientIcon(TransformOrientationMode m)
        {
            switch (m)
            {
                case TransformOrientationMode.Local: return "orient_local";
                case TransformOrientationMode.View: return "orient_view";
                case TransformOrientationMode.Normal: return "orient_normal";
                case TransformOrientationMode.Custom: return "orient_custom";
                default: return "orient_global";
            }
        }

        static string OrientLabel(TransformOrientationMode m)
        {
            switch (m)
            {
                case TransformOrientationMode.Local: return "Local";
                case TransformOrientationMode.View: return "View";
                case TransformOrientationMode.Normal: return "Normal";
                case TransformOrientationMode.Custom:
                    return string.IsNullOrEmpty(PivotOrientation.CustomOrientationName)
                        ? "Custom" : PivotOrientation.CustomOrientationName;
                default: return "Global";
            }
        }

        static string PivotIcon(PivotMode m)
        {
            switch (m)
            {
                case PivotMode.ActiveElement: return "pivot_active";
                case PivotMode.IndividualOrigins: return "pivot_individual";
                case PivotMode.Cursor3D: return "cursor";
                case PivotMode.BoundingBoxCenter: return "pivot_bounds";
                default: return "pivot_median";
            }
        }

        // ------------------------------------------------------------- Blender-style dropdowns

        static void ShowOrientationMenu(Rect r)
        {
            var rows = new List<BlenderDropdown.Row>();
            foreach (TransformOrientationMode v in (TransformOrientationMode[])Enum.GetValues(typeof(TransformOrientationMode)))
            {
                TransformOrientationMode cap = v;
                rows.Add(new BlenderDropdown.Row
                {
                    icon = OrientIcon(v),
                    label = OrientLabel(v),
                    on = PivotOrientation.Orientation == v,
                    action = () => PivotOrientation.Orientation = cap
                });
            }
            BlenderDropdown.Show(r, rows, 160f);
        }

        static void ShowPivotMenu(Rect r)
        {
            var rows = new List<BlenderDropdown.Row>();
            foreach (PivotMode v in (PivotMode[])Enum.GetValues(typeof(PivotMode)))
            {
                PivotMode cap = v;
                rows.Add(new BlenderDropdown.Row
                {
                    icon = PivotIcon(v),
                    label = PivotOrientation.PivotLabel(v),
                    on = PivotOrientation.Pivot == v,
                    action = () => PivotOrientation.Pivot = cap
                });
            }
            BlenderDropdown.Show(r, rows, 190f);
        }

        static void ShowShadingMenu(SceneView sv, Rect r)
        {
            var rows = new List<BlenderDropdown.Row>();
            rows.Add(new BlenderDropdown.Row
            {
                icon = "eye",
                label = "Scene Lighting",
                on = sv.sceneLighting,
                action = () => { sv.sceneLighting = !sv.sceneLighting; sv.Repaint(); BlenderModePrefs.RaiseChanged(); }
            });
            SceneView.SceneViewState st = sv.sceneViewState;
            if (st != null)
            {
                rows.Add(new BlenderDropdown.Row
                {
                    icon = "shade_rendered", label = "Skybox", on = st.showSkybox,
                    action = () => { st.showSkybox = !st.showSkybox; sv.Repaint(); BlenderModePrefs.RaiseChanged(); }
                });
                rows.Add(new BlenderDropdown.Row
                {
                    icon = "overlays", label = "Fog", on = st.showFog,
                    action = () => { st.showFog = !st.showFog; sv.Repaint(); BlenderModePrefs.RaiseChanged(); }
                });
                rows.Add(new BlenderDropdown.Row
                {
                    icon = "shade_material", label = "Post-processing", on = st.showImageEffects,
                    action = () => { st.showImageEffects = !st.showImageEffects; sv.Repaint(); BlenderModePrefs.RaiseChanged(); }
                });
            }
            rows.Add(BlenderDropdown.Row.Sep());
            ShadingMode cur = ShadingModes.Current(sv);
            rows.Add(ShadeRow(sv, ShadingMode.Wireframe, "shade_wire", "Wireframe", cur));
            rows.Add(ShadeRow(sv, ShadingMode.Solid, "shade_solid", "Solid", cur));
            rows.Add(ShadeRow(sv, ShadingMode.MaterialPreview, "shade_material", "Material Preview", cur));
            rows.Add(ShadeRow(sv, ShadingMode.Rendered, "shade_rendered", "Rendered", cur));
            BlenderDropdown.Show(r, rows, 200f);
        }

        static BlenderDropdown.Row ShadeRow(SceneView sv, ShadingMode mode, string icon, string label, ShadingMode cur)
        {
            return new BlenderDropdown.Row
            {
                icon = icon,
                label = label,
                on = cur == mode,
                action = () => ShadingModes.Apply(sv, mode)
            };
        }
    }
}

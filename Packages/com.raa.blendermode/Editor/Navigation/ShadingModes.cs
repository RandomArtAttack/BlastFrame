using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace RAA.BlenderMode
{
    /// <summary>The four Blender viewport-shading modes mapped onto Unity SceneView state.</summary>
    public enum ShadingMode
    {
        Wireframe,
        Solid,
        MaterialPreview,
        Rendered
    }

    /// <summary>
    /// Shared viewport-shading + right-side-toggle utility (DESIGN.md §15). Both the Z shading pie and the
    /// toolbar shading pills call <see cref="Apply"/>/<see cref="Current"/> so they stay in sync. Also owns the
    /// X-Ray toggle (Alt+Z; wireframe on/off remembering the prior mode) and the grouped Overlays toggle
    /// (scene grid + gizmos + Blender Mode overlays).
    /// </summary>
    [InitializeOnLoad]
    public static class ShadingModes
    {
        static ShadingModes() { Register(); }

        static bool _registered;

        /// <summary>Register the X-Ray action (Alt+Z). Idempotent.</summary>
        public static void Register()
        {
            if (_registered) return;
            _registered = true;
            ActionRegistry.Register(Actions.XRay, (sv, e) => { ToggleXRay(sv); return true; });
        }

        // ---------------------------------------------------------------- shading modes

        /// <summary>Apply a shading mode to the scene view (camera mode + lighting + scene-view state).</summary>
        public static void Apply(SceneView sv, ShadingMode mode)
        {
            if (sv == null) return;
            ClearXRayState(sv); // an explicit shading choice supersedes x-ray
            SceneView.SceneViewState st = sv.sceneViewState;

            switch (mode)
            {
                case ShadingMode.Wireframe:
                    sv.cameraMode = SceneView.GetBuiltinCameraMode(DrawCameraMode.Wireframe);
                    sv.sceneLighting = false;
                    SetState(st, skybox: false, fog: false, flares: false, fx: false);
                    break;
                case ShadingMode.Solid:
                    sv.cameraMode = SceneView.GetBuiltinCameraMode(DrawCameraMode.Textured);
                    sv.sceneLighting = false;
                    SetState(st, skybox: false, fog: false, flares: false, fx: false);
                    break;
                case ShadingMode.MaterialPreview:
                    sv.cameraMode = SceneView.GetBuiltinCameraMode(DrawCameraMode.Textured);
                    sv.sceneLighting = true;
                    SetState(st, skybox: true, fog: false, flares: false, fx: false);
                    break;
                case ShadingMode.Rendered:
                    sv.cameraMode = SceneView.GetBuiltinCameraMode(DrawCameraMode.Textured);
                    sv.sceneLighting = true;
                    SetState(st, skybox: true, fog: true, flares: true, fx: true);
                    break;
            }
            sv.Repaint();
            BlenderModePrefs.RaiseChanged();
        }

        static void SetState(SceneView.SceneViewState st, bool skybox, bool fog, bool flares, bool fx)
        {
            if (st == null) return;
            st.showSkybox = skybox;
            st.showFog = fog;
            st.showFlares = flares;
            st.showImageEffects = fx;
        }

        /// <summary>Best-guess current shading mode for pill highlighting.</summary>
        public static ShadingMode Current(SceneView sv)
        {
            if (sv == null) return ShadingMode.Solid;
            if (sv.cameraMode.drawMode == DrawCameraMode.Wireframe) return ShadingMode.Wireframe;
            if (!sv.sceneLighting) return ShadingMode.Solid;
            SceneView.SceneViewState st = sv.sceneViewState;
            if (st != null && (st.showFog || st.showImageEffects || st.showFlares)) return ShadingMode.Rendered;
            return ShadingMode.MaterialPreview;
        }

        // ---------------------------------------------------------------- x-ray

        static readonly Dictionary<SceneView, SceneView.CameraMode> _xrayPrev =
            new Dictionary<SceneView, SceneView.CameraMode>();

        public static bool IsXRay(SceneView sv) => sv != null && _xrayPrev.ContainsKey(sv);

        /// <summary>Toggle a pseudo x-ray by switching to wireframe and back, remembering the prior mode.</summary>
        public static void ToggleXRay(SceneView sv)
        {
            if (sv == null) return;
            if (_xrayPrev.TryGetValue(sv, out SceneView.CameraMode prev))
            {
                sv.cameraMode = prev;
                _xrayPrev.Remove(sv);
            }
            else
            {
                _xrayPrev[sv] = sv.cameraMode;
                sv.cameraMode = SceneView.GetBuiltinCameraMode(DrawCameraMode.Wireframe);
            }
            sv.Repaint();
            BlenderModePrefs.RaiseChanged();
        }

        static void ClearXRayState(SceneView sv)
        {
            if (sv != null) _xrayPrev.Remove(sv);
        }

        // ---------------------------------------------------------------- overlays group

        static readonly Dictionary<SceneView, (bool grid, bool gizmos)> _overlayPrev =
            new Dictionary<SceneView, (bool, bool)>();

        /// <summary>
        /// Show/hide the grouped overlays (scene grid + gizmos + Blender Mode's own overlays). Remembers the
        /// prior grid/gizmo state so it restores exactly. The Blender Mode overlays (3D cursor, HUD) read
        /// <see cref="BlenderModePrefs.OverlaysVisible"/> directly.
        /// </summary>
        public static void SetOverlays(SceneView sv, bool visible)
        {
            if (sv != null)
            {
                if (!visible)
                {
                    _overlayPrev[sv] = (sv.showGrid, sv.drawGizmos);
                    sv.showGrid = false;
                    sv.drawGizmos = false;
                }
                else if (_overlayPrev.TryGetValue(sv, out (bool grid, bool gizmos) p))
                {
                    sv.showGrid = p.grid;
                    sv.drawGizmos = p.gizmos;
                    _overlayPrev.Remove(sv);
                }
                else
                {
                    sv.showGrid = true;
                    sv.drawGizmos = true;
                }
                sv.Repaint();
            }
            BlenderModePrefs.OverlaysVisible = visible;
        }
    }
}

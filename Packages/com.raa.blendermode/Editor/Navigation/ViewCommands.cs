using UnityEditor;
using UnityEngine;

namespace RAA.BlenderMode.Navigation
{
    /// <summary>
    /// Discrete view commands bound to the numpad (and top row when Emulate Numpad is on): axis views,
    /// ortho toggle, incremental orbit/pan, look-through-camera, frame selected/all, local view (isolate)
    /// and a best-effort quad view. All axis transitions use <see cref="SceneView.LookAt(Vector3,Quaternion,float,bool)"/>
    /// which animates. See DESIGN.md §3.
    /// </summary>
    public static class ViewCommands
    {
        const float OrbitStepDeg = 15f;
        const float PanStepFraction = 0.2f;

        // Axis-view rotations (Unity axes) exactly per the design table.
        static readonly Quaternion Front = Quaternion.Euler(0f, 0f, 0f);
        static readonly Quaternion Back = Quaternion.Euler(0f, 180f, 0f);
        static readonly Quaternion Right = Quaternion.Euler(0f, -90f, 0f);
        static readonly Quaternion Left = Quaternion.Euler(0f, 90f, 0f);
        static readonly Quaternion Top = Quaternion.Euler(90f, 0f, 0f);
        static readonly Quaternion Bottom = Quaternion.Euler(-90f, 0f, 0f);

        static bool _registered;

        /// <summary>Register all navigation action handlers (idempotent).</summary>
        public static void Register()
        {
            if (_registered) return;
            _registered = true;

            ActionRegistry.Register(Actions.ViewFront, (sv, e) => { AxisView(sv, Front); return true; });
            ActionRegistry.Register(Actions.ViewBack, (sv, e) => { AxisView(sv, Back); return true; });
            ActionRegistry.Register(Actions.ViewRight, (sv, e) => { AxisView(sv, Right); return true; });
            ActionRegistry.Register(Actions.ViewLeft, (sv, e) => { AxisView(sv, Left); return true; });
            ActionRegistry.Register(Actions.ViewTop, (sv, e) => { AxisView(sv, Top); return true; });
            ActionRegistry.Register(Actions.ViewBottom, (sv, e) => { AxisView(sv, Bottom); return true; });

            ActionRegistry.Register(Actions.ViewToggleOrtho, (sv, e) => { ToggleOrtho(sv); return true; });
            ActionRegistry.Register(Actions.ViewFlip, (sv, e) => { Flip(sv); return true; });

            ActionRegistry.Register(Actions.OrbitYawLeft, (sv, e) => { OrbitStep(sv, -OrbitStepDeg, 0f); return true; });
            ActionRegistry.Register(Actions.OrbitYawRight, (sv, e) => { OrbitStep(sv, OrbitStepDeg, 0f); return true; });
            ActionRegistry.Register(Actions.OrbitPitchUp, (sv, e) => { OrbitStep(sv, 0f, OrbitStepDeg); return true; });
            ActionRegistry.Register(Actions.OrbitPitchDown, (sv, e) => { OrbitStep(sv, 0f, -OrbitStepDeg); return true; });

            ActionRegistry.Register(Actions.PanLeft, (sv, e) => { PanStep(sv, new Vector2(-1f, 0f)); return true; });
            ActionRegistry.Register(Actions.PanRight, (sv, e) => { PanStep(sv, new Vector2(1f, 0f)); return true; });
            ActionRegistry.Register(Actions.PanUp, (sv, e) => { PanStep(sv, new Vector2(0f, 1f)); return true; });
            ActionRegistry.Register(Actions.PanDown, (sv, e) => { PanStep(sv, new Vector2(0f, -1f)); return true; });

            ActionRegistry.Register(Actions.ViewCamera, (sv, e) => { LookThroughCamera(sv); return true; });
            ActionRegistry.Register(Actions.FrameSelected, (sv, e) => { sv.FrameSelected(); return true; });
            ActionRegistry.Register(Actions.FrameAll, (sv, e) => { FrameAll(sv); return true; });
            ActionRegistry.Register(Actions.LocalView, (sv, e) => { ToggleLocalView(); return true; });
            ActionRegistry.Register(Actions.QuadView, (sv, e) => { ToggleQuadView(sv); return true; });
        }

        // ----------------------------------------------------------------- view ops

        static void AxisView(SceneView sceneView, Quaternion rotation)
        {
            bool ortho = BlenderModePrefs.AutoOrthoOnAxisViews ? true : sceneView.orthographic;
            sceneView.LookAt(sceneView.pivot, rotation, sceneView.size, ortho);
        }

        static void ToggleOrtho(SceneView sceneView)
        {
            sceneView.LookAt(sceneView.pivot, sceneView.rotation, sceneView.size, !sceneView.orthographic);
        }

        static void Flip(SceneView sceneView)
        {
            Vector3 up = sceneView.rotation * Vector3.up;
            Quaternion rot = Quaternion.AngleAxis(180f, up) * sceneView.rotation;
            sceneView.LookAt(sceneView.pivot, rot, sceneView.size, sceneView.orthographic);
        }

        static void OrbitStep(SceneView sceneView, float yaw, float pitch)
        {
            Quaternion rot = sceneView.rotation;
            if (yaw != 0f) rot = Quaternion.AngleAxis(yaw, Vector3.up) * rot;
            if (pitch != 0f) rot = rot * Quaternion.AngleAxis(pitch, Vector3.right);
            sceneView.LookAt(sceneView.pivot, rot, sceneView.size, sceneView.orthographic);
        }

        static void PanStep(SceneView sceneView, Vector2 dir)
        {
            Vector3 right = sceneView.rotation * Vector3.right;
            Vector3 up = sceneView.rotation * Vector3.up;
            float amount = sceneView.size * PanStepFraction;
            Vector3 pivot = sceneView.pivot + (right * dir.x + up * dir.y) * amount;
            sceneView.LookAt(pivot, sceneView.rotation, sceneView.size, sceneView.orthographic);
        }

        static void LookThroughCamera(SceneView sceneView)
        {
            Camera cam = Camera.main;
            if (cam == null) cam = Object.FindAnyObjectByType<Camera>();
            if (cam == null)
            {
                Hud.Notify(sceneView, "No camera to look through");
                return;
            }

            Quaternion rot = cam.transform.rotation;
            Vector3 pivot = cam.transform.position + rot * Vector3.forward * sceneView.size;
            sceneView.LookAt(pivot, rot, sceneView.size, cam.orthographic);
        }

        static void FrameAll(SceneView sceneView)
        {
            if (TryGetSceneBounds(out Bounds bounds))
                sceneView.Frame(bounds, false);
            else
                Hud.Notify(sceneView, "Nothing to frame");
        }

        static void ToggleLocalView()
        {
            SceneVisibilityManager svm = SceneVisibilityManager.instance;
            if (svm.IsCurrentStageIsolated())
            {
                svm.ExitIsolation();
            }
            else
            {
                GameObject[] objs = Selection.gameObjects;
                if (objs != null && objs.Length > 0)
                    svm.Isolate(objs, true);
            }
        }

        static void ToggleQuadView(SceneView sceneView)
        {
            // Unity exposes no public quad-view API; a true 4-viewport layout can't be toggled reliably.
            // Best-effort per the design: notify for now (revisit if an API surface appears).
            Hud.Notify(sceneView, "Quad view unavailable (no public API)");
        }

        // ----------------------------------------------------------------- bounds helpers

        /// <summary>Combined world bounds of the current selection's renderers (or positions).</summary>
        public static bool TryGetSelectionBounds(out Bounds bounds)
        {
            return TryGetBounds(Selection.transforms, out bounds);
        }

        static bool TryGetSceneBounds(out Bounds bounds)
        {
            Renderer[] renderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            bounds = default;
            bool has = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                if (!has) { bounds = renderers[i].bounds; has = true; }
                else bounds.Encapsulate(renderers[i].bounds);
            }
            return has;
        }

        static bool TryGetBounds(Transform[] transforms, out Bounds bounds)
        {
            bounds = default;
            if (transforms == null || transforms.Length == 0) return false;

            bool has = false;
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform t = transforms[i];
                if (t == null) continue;

                Bounds b;
                var r = t.GetComponentInChildren<Renderer>();
                if (r != null) b = r.bounds;
                else b = new Bounds(t.position, Vector3.one * 0.1f);

                if (!has) { bounds = b; has = true; }
                else bounds.Encapsulate(b);
            }
            return has;
        }
    }
}

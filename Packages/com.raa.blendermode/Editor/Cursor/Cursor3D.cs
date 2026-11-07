using UnityEditor;
using UnityEngine;

namespace RAA.BlenderMode
{
    /// <summary>
    /// The Blender-style 3D cursor: a per-scene world position/rotation (persisted in
    /// <see cref="SceneStateStore"/>) drawn always-on-top as a red/white dashed circle with small RGB axes.
    /// Placed with Shift+RMB (surface hit, optional normal orientation) and reset with Shift+C
    /// (origin + frame-all). Usable as a transform pivot. See DESIGN.md §6.
    /// </summary>
    [InitializeOnLoad]
    public static class Cursor3D
    {
        static Cursor3D()
        {
            Register();
        }

        static bool _registered;

        /// <summary>Register action handlers and the always-on drawing pass (idempotent).</summary>
        public static void Register()
        {
            if (_registered) return;
            _registered = true;

            ActionRegistry.Register(Actions.CursorPlace, (sv, e) => { Place(sv, e); return true; });
            ActionRegistry.Register(Actions.CursorReset, (sv, e) => { ResetAndFrame(sv); return true; });

            SceneView.duringSceneGui -= Draw;
            SceneView.duringSceneGui += Draw;
        }

        // ---------------------------------------------------------------- state

        /// <summary>World position of the 3D cursor for the active scene.</summary>
        public static Vector3 Position
        {
            get
            {
                PerSceneData d = SceneStateStore.instance.ActiveData();
                return d.cursorAssigned ? d.cursorPosition : Vector3.zero;
            }
            set
            {
                PerSceneData d = SceneStateStore.instance.ActiveData();
                d.cursorPosition = value;
                d.cursorAssigned = true;
                SceneStateStore.instance.Persist();
                SceneView.RepaintAll();
            }
        }

        /// <summary>World rotation of the 3D cursor for the active scene.</summary>
        public static Quaternion Rotation
        {
            get
            {
                PerSceneData d = SceneStateStore.instance.ActiveData();
                return d.cursorAssigned ? Quaternion.Euler(d.cursorRotationEuler) : Quaternion.identity;
            }
            set
            {
                PerSceneData d = SceneStateStore.instance.ActiveData();
                d.cursorRotationEuler = value.eulerAngles;
                d.cursorAssigned = true;
                SceneStateStore.instance.Persist();
                SceneView.RepaintAll();
            }
        }

        /// <summary>Set both position and rotation in one write.</summary>
        public static void Set(Vector3 position, Quaternion rotation)
        {
            PerSceneData d = SceneStateStore.instance.ActiveData();
            d.cursorPosition = position;
            d.cursorRotationEuler = rotation.eulerAngles;
            d.cursorAssigned = true;
            SceneStateStore.instance.Persist();
            SceneView.RepaintAll();
        }

        // ---------------------------------------------------------------- actions

        /// <summary>Place the cursor at the surface under the mouse (else on the pivot plane).</summary>
        public static void Place(SceneView sceneView, Event e)
        {
            Vector2 gui = e.mousePosition;
            if (SnapEngine.TrySurface(sceneView, gui, null, out Vector3 pos, out Vector3 normal))
            {
                if (BlenderModePrefs.CursorOrientToSurface)
                {
                    Vector3 up = normal.sqrMagnitude < 1e-6f ? Vector3.up : normal.normalized;
                    Vector3 camFwd = sceneView.camera != null ? sceneView.camera.transform.forward : Vector3.forward;
                    Vector3 fwd = Vector3.ProjectOnPlane(camFwd, up);
                    if (fwd.sqrMagnitude < 1e-6f)
                        fwd = Vector3.ProjectOnPlane(Vector3.forward, up);
                    if (fwd.sqrMagnitude < 1e-6f)
                        fwd = Vector3.ProjectOnPlane(Vector3.right, up);
                    Set(pos, Quaternion.LookRotation(fwd.normalized, up));
                }
                else
                {
                    Position = pos;
                }
            }
            else
            {
                // No surface: camera-facing plane through the current pivot.
                Ray ray = HandleUtility.GUIPointToWorldRay(gui);
                var plane = new Plane(-sceneView.camera.transform.forward, sceneView.pivot);
                Position = plane.Raycast(ray, out float enter) ? ray.GetPoint(enter) : sceneView.pivot;
            }
        }

        /// <summary>Reset the cursor to the world origin and frame all.</summary>
        public static void ResetAndFrame(SceneView sceneView)
        {
            Set(Vector3.zero, Quaternion.identity);
            if (sceneView != null)
            {
                Renderer[] rs = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
                bool has = false;
                Bounds b = default;
                for (int i = 0; i < rs.Length; i++)
                {
                    if (rs[i] == null) continue;
                    if (!has) { b = rs[i].bounds; has = true; } else b.Encapsulate(rs[i].bounds);
                }
                if (has) sceneView.Frame(b, false);
            }
        }

        // ---------------------------------------------------------------- drawing

        static void Draw(SceneView sceneView)
        {
            if (!BlenderModePrefs.Enabled || !BlenderModePrefs.ModuleCursor) return;
            if (!BlenderModePrefs.OverlaysVisible) return;
            if (Event.current.type != EventType.Repaint) return;

            PerSceneData d = SceneStateStore.instance.ActiveData();
            Vector3 pos = d.cursorAssigned ? d.cursorPosition : Vector3.zero;
            Quaternion rot = d.cursorAssigned ? Quaternion.Euler(d.cursorRotationEuler) : Quaternion.identity;

            // Draw on top of geometry.
            Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;

            float size = HandleUtility.GetHandleSize(pos) * 0.15f;
            Vector3 camFwd = sceneView.camera != null ? sceneView.camera.transform.forward : Vector3.forward;

            // Red/white dashed circle: alternate short arcs in red and white.
            const int segs = 32;
            Vector3 prev = pos + CircleOffset(sceneView, pos, size, 0f);
            for (int i = 1; i <= segs; i++)
            {
                float t = i / (float)segs;
                Vector3 cur = pos + CircleOffset(sceneView, pos, size, t * Mathf.PI * 2f);
                Handles.color = (i % 2 == 0) ? Color.white : Color.red;
                Handles.DrawLine(prev, cur);
                prev = cur;
            }

            // Small RGB axes oriented by the cursor rotation.
            float ax = size * 1.4f;
            Handles.color = new Color(0.9f, 0.25f, 0.25f);
            Handles.DrawLine(pos, pos + rot * Vector3.right * ax);
            Handles.color = new Color(0.3f, 0.85f, 0.3f);
            Handles.DrawLine(pos, pos + rot * Vector3.up * ax);
            Handles.color = new Color(0.35f, 0.55f, 0.95f);
            Handles.DrawLine(pos, pos + rot * Vector3.forward * ax);

            Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;
            Handles.color = Color.white;
        }

        static Vector3 CircleOffset(SceneView sceneView, Vector3 center, float radius, float angle)
        {
            // Billboard circle facing the camera.
            Camera cam = sceneView.camera;
            Vector3 right = cam != null ? cam.transform.right : Vector3.right;
            Vector3 up = cam != null ? cam.transform.up : Vector3.up;
            return (right * Mathf.Cos(angle) + up * Mathf.Sin(angle)) * radius;
        }
    }
}

using UnityEditor;
using UnityEngine;

namespace RAA.BlenderMode.Navigation
{
    /// <summary>
    /// Blender-style free navigation driven off the middle mouse button and wheel:
    /// MMB = orbit, Shift+MMB = pan, Ctrl+MMB = zoom (vertical drag), Alt+MMB = snap to nearest axis view,
    /// wheel = dolly zoom (optionally toward the cursor). Drives sceneView.pivot/rotation/size directly and
    /// consumes the events so Unity's own MMB pan never fires while Blender Mode is enabled.
    /// </summary>
    public static class ViewNavigation
    {
        enum Mode { None, Orbit, Pan, Zoom }

        static Mode _mode;
        static Vector2 _last;
        static int _hotControl;

        /// <summary>Process the current event for navigation. Called by the router as the lowest priority.</summary>
        public static void Handle(SceneView sceneView)
        {
            Event e = Event.current;
            switch (e.type)
            {
                case EventType.MouseDown:
                    if (e.button == 2)
                    {
                        if (e.alt)
                        {
                            SnapToNearestAxisView(sceneView);
                            e.Use();
                            break;
                        }

                        if (BlenderModePrefs.OrbitAroundSelection)
                            RecenterPivotOnSelection(sceneView);

                        _mode = e.shift ? Mode.Pan : (e.control || e.command) ? Mode.Zoom : Mode.Orbit;
                        _last = e.mousePosition;
                        _hotControl = GUIUtility.GetControlID(FocusType.Passive);
                        GUIUtility.hotControl = _hotControl;
                        e.Use();
                    }
                    break;

                case EventType.MouseDrag:
                    if (_mode != Mode.None && GUIUtility.hotControl == _hotControl)
                    {
                        Vector2 delta = e.mousePosition - _last;
                        _last = e.mousePosition;
                        Apply(sceneView, delta);
                        e.Use();
                        sceneView.Repaint();
                    }
                    break;

                case EventType.MouseUp:
                    if (e.button == 2 && _mode != Mode.None)
                    {
                        _mode = Mode.None;
                        if (GUIUtility.hotControl == _hotControl)
                            GUIUtility.hotControl = 0;
                        _hotControl = 0;
                        e.Use();
                    }
                    break;

                case EventType.ScrollWheel:
                    WheelZoom(sceneView, e);
                    e.Use();
                    sceneView.Repaint();
                    break;
            }
        }

        static void Apply(SceneView sceneView, Vector2 delta)
        {
            switch (_mode)
            {
                case Mode.Orbit: Orbit(sceneView, delta); break;
                case Mode.Pan: Pan(sceneView, delta); break;
                case Mode.Zoom: DragZoom(sceneView, delta); break;
            }
        }

        static void Orbit(SceneView sceneView, Vector2 delta)
        {
            float sens = BlenderModePrefs.OrbitSensitivity;
            Quaternion rot = sceneView.rotation;
            // Turntable: yaw about world up, pitch about the view's local right axis.
            rot = Quaternion.AngleAxis(delta.x * sens, Vector3.up) * rot;
            rot = rot * Quaternion.AngleAxis(delta.y * sens, Vector3.right);
            sceneView.rotation = rot;
        }

        static void Pan(SceneView sceneView, Vector2 delta)
        {
            Vector3 right = sceneView.rotation * Vector3.right;
            Vector3 up = sceneView.rotation * Vector3.up;
            // GUI y grows downward; scale movement with the framed size for consistent feel.
            float scale = sceneView.size * 0.002f;
            sceneView.pivot += (-right * delta.x + up * delta.y) * scale;
        }

        static void DragZoom(SceneView sceneView, Vector2 delta)
        {
            float sign = BlenderModePrefs.InvertZoom ? -1f : 1f;
            // Drag down (positive y) zooms out.
            sceneView.size = Mathf.Max(0.01f, sceneView.size * Mathf.Exp(delta.y * 0.01f * sign));
        }

        static void WheelZoom(SceneView sceneView, Event e)
        {
            float sign = BlenderModePrefs.InvertZoom ? -1f : 1f;
            float oldSize = sceneView.size;
            // e.delta.y > 0 when scrolling down -> zoom out.
            float newSize = Mathf.Max(0.01f, oldSize * Mathf.Exp(e.delta.y * 0.05f * sign));

            if (BlenderModePrefs.ZoomToMouse && sceneView.camera != null)
            {
                Vector3 worldPoint = PointOnPivotPlane(sceneView, e.mousePosition);
                float ratio = newSize / oldSize;
                sceneView.pivot = worldPoint + (sceneView.pivot - worldPoint) * ratio;
            }

            sceneView.size = newSize;
        }

        static Vector3 PointOnPivotPlane(SceneView sceneView, Vector2 guiPoint)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(guiPoint);
            var plane = new Plane(-sceneView.camera.transform.forward, sceneView.pivot);
            return plane.Raycast(ray, out float enter) ? ray.GetPoint(enter) : sceneView.pivot;
        }

        static void RecenterPivotOnSelection(SceneView sceneView)
        {
            if (Selection.activeTransform == null) return;
            if (ViewCommands.TryGetSelectionBounds(out Bounds b))
                sceneView.pivot = b.center;
        }

        static void SnapToNearestAxisView(SceneView sceneView)
        {
            Quaternion current = sceneView.rotation;
            Quaternion best = current;
            float bestDot = -1f;
            for (int i = 0; i < AxisRotations.Length; i++)
            {
                float d = Quaternion.Dot(current, AxisRotations[i]);
                d = Mathf.Abs(d);
                if (d > bestDot)
                {
                    bestDot = d;
                    best = AxisRotations[i];
                }
            }
            sceneView.LookAt(sceneView.pivot, best, sceneView.size, sceneView.orthographic);
        }

        static readonly Quaternion[] AxisRotations =
        {
            Quaternion.Euler(0f, 0f, 0f),    // front
            Quaternion.Euler(0f, 180f, 0f),  // back
            Quaternion.Euler(0f, -90f, 0f),  // right
            Quaternion.Euler(0f, 90f, 0f),   // left
            Quaternion.Euler(90f, 0f, 0f),   // top
            Quaternion.Euler(-90f, 0f, 0f),  // bottom
        };
    }
}

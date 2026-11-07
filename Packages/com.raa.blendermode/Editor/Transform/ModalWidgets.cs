using UnityEditor;
using UnityEngine;

namespace RAA.BlenderMode
{
    /// <summary>
    /// Shared visual-feedback widgets drawn during the Rotate/Scale modals: a Blender-style rotate gizmo
    /// (camera-facing + axis rings plus a swept-angle arc) and a scale gizmo (axis handles with cube caps
    /// that stretch with the applied factor). Draw-only — these never touch modal input. Colors are cached
    /// and nothing is allocated per event. Always drawn on top (zTest Always) to match the pivot marker.
    /// See DESIGN.md §4.
    /// </summary>
    public static class ModalWidgets
    {
        // Line / ring thickness in screen pixels.
        const float DimThickness = 2f;
        const float ActiveThickness = 3f;
        const float PlaneThickness = 2.5f;

        // Alpha levels shared by both widgets (Blender-ish highlight scheme).
        const float ActiveAlpha = 1f;      // the constrained axis / camera-facing ring
        const float PlaneAlpha = 0.9f;     // the two in-plane axes of a plane constraint
        const float LitAlpha = 0.35f;      // unconstrained rotate rings (dimmed but visible)
        const float VeryDimAlpha = 0.12f;  // an inactive axis while constrained

        // Arc fill / border alphas.
        const float ArcFillAlpha = 0.18f;
        const float ArcBorderAlpha = 0.35f;

        static readonly Color Grey = new Color(0.7f, 0.7f, 0.7f);

        static Vector3 Unit(int a) => a == 0 ? Vector3.right : a == 1 ? Vector3.up : Vector3.forward;
        static Color AxisColor(int a) => a == 0 ? Handles.xAxisColor : a == 1 ? Handles.yAxisColor : Handles.zAxisColor;
        static Color A(Color c, float alpha) { c.a = alpha; return c; }

        /// <summary>Per-axis alpha for the current constraint. Unconstrained axes use <paramref name="unconstrained"/>.</summary>
        static float AxisAlpha(int i, int axis, bool plane, float unconstrained)
        {
            if (axis < 0) return unconstrained;
            if (!plane) return i == axis ? ActiveAlpha : VeryDimAlpha;
            return i == axis ? VeryDimAlpha : PlaneAlpha; // plane excludes 'axis'; highlight the other two
        }

        /// <summary>Per-axis line/ring thickness for the current constraint.</summary>
        static float AxisThickness(int i, int axis, bool plane)
        {
            if (axis < 0) return DimThickness;
            if (!plane) return i == axis ? ActiveThickness : DimThickness;
            return i == axis ? DimThickness : PlaneThickness;
        }

        // ------------------------------------------------------------- rotate

        /// <summary>
        /// Draw the rotate gizmo at <paramref name="pivot"/>: dimmed X/Y/Z rings in <paramref name="basis"/>,
        /// a white camera-facing ring when unconstrained, the constrained ring brightened, and a translucent
        /// swept-angle sector of <paramref name="appliedAngle"/> degrees on the active rotation plane, starting
        /// from the drag-start mouse direction (<paramref name="startMouse"/>). Repaint-only, always on top.
        /// </summary>
        public static void DrawRotateWidget(SceneView sceneView, Vector3 pivot, Quaternion basis,
            int axis, bool plane, Vector2 startMouse, float appliedAngle)
        {
            if (Event.current.type != EventType.Repaint) return;

            Camera cam = sceneView != null ? sceneView.camera : null;
            Vector3 camFwd = cam != null ? cam.transform.forward : Vector3.forward;
            float r = HandleUtility.GetHandleSize(pivot) * 1.4f;

            Color prevC = Handles.color;
            var prevZ = Handles.zTest;
            Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;

            // Axis rings in the constraint basis.
            for (int i = 0; i < 3; i++)
            {
                Vector3 n = basis * Unit(i);
                Handles.color = A(AxisColor(i), AxisAlpha(i, axis, plane, LitAlpha));
                Handles.DrawWireDisc(pivot, n, r, AxisThickness(i, axis, plane));
            }

            // View-axis (camera-facing) white ring: the default unconstrained rotation plane.
            if (axis < 0)
            {
                Handles.color = A(Color.white, ActiveAlpha);
                Handles.DrawWireDisc(pivot, camFwd, r, DimThickness);
            }

            // Swept-angle arc on the active rotation plane.
            Vector3 arcNormal = axis < 0 ? -camFwd : (basis * Unit(axis)).normalized;
            Vector3 from = Vector3.ProjectOnPlane(ScreenSeedDir(cam, pivot, startMouse), arcNormal);
            if (from.sqrMagnitude < 1e-6f)
                from = Vector3.ProjectOnPlane(cam != null ? cam.transform.up : Vector3.up, arcNormal);
            from.Normalize();

            float arc = appliedAngle % 360f; // draw modulo a full turn; sign preserved
            Color arcCol = axis < 0 ? Color.white : AxisColor(axis);
            Handles.color = A(arcCol, ArcFillAlpha);
            Handles.DrawSolidArc(pivot, arcNormal, from, arc, r);
            Handles.color = A(arcCol, ArcBorderAlpha);
            Handles.DrawWireArc(pivot, arcNormal, from, arc, r, DimThickness);

            Handles.zTest = prevZ;
            Handles.color = prevC;
        }

        /// <summary>World direction of the drag-start mouse relative to the pivot's screen position (GUI y is down).</summary>
        static Vector3 ScreenSeedDir(Camera cam, Vector3 pivot, Vector2 startMouse)
        {
            if (cam == null) return Vector3.right;
            Vector2 pg = HandleUtility.WorldToGUIPoint(pivot);
            Vector2 v = startMouse - pg;
            if (v.sqrMagnitude < 1e-4f) return cam.transform.right;
            return cam.transform.right * v.x - cam.transform.up * v.y;
        }

        // ------------------------------------------------------------- scale

        /// <summary>
        /// Draw the scale gizmo at <paramref name="pivot"/>: three axis handles in <paramref name="basis"/> with
        /// cube caps that offset outward/inward by the current per-axis factor in <paramref name="scaleFactors"/>
        /// (a dim ghost cap marks the 1.0 reference), the constrained axis brightened, a grey centre cube, and —
        /// when unconstrained — a faint camera-facing ring scaling with the uniform factor. Always on top.
        /// </summary>
        public static void DrawScaleWidget(SceneView sceneView, Vector3 pivot, Quaternion basis,
            int axis, bool plane, Vector3 scaleFactors)
        {
            Camera cam = sceneView != null ? sceneView.camera : null;
            float size = HandleUtility.GetHandleSize(pivot) * 1.4f;
            float capSize = size * 0.06f;
            bool repaint = Event.current.type == EventType.Repaint;

            Color prevC = Handles.color;
            var prevZ = Handles.zTest;
            Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;

            for (int i = 0; i < 3; i++)
            {
                Vector3 dir = (basis * Unit(i)).normalized;
                float alpha = AxisAlpha(i, axis, plane, ActiveAlpha);
                float th = AxisThickness(i, axis, plane);

                // Dim ghost cap at the 1.0 reference position.
                Vector3 refPos = pivot + dir * size;
                if (repaint)
                {
                    Handles.color = A(AxisColor(i), alpha * 0.3f);
                    Handles.CubeHandleCap(0, refPos, Quaternion.identity, capSize * 0.7f, EventType.Repaint);
                }

                // Live handle: cap offset by the current factor along the axis.
                Vector3 capPos = pivot + dir * size * scaleFactors[i];
                Handles.color = A(AxisColor(i), alpha);
                Handles.DrawLine(pivot, capPos, th);
                if (repaint)
                    Handles.CubeHandleCap(0, capPos, Quaternion.identity, capSize, EventType.Repaint);
            }

            // Centre cube (grey).
            if (repaint)
            {
                Handles.color = A(Grey, 0.9f);
                Handles.CubeHandleCap(0, pivot, Quaternion.identity, capSize * 0.9f, EventType.Repaint);
            }

            // Unconstrained: faint camera-facing ring that scales with the uniform factor.
            if (axis < 0 && cam != null)
            {
                Handles.color = A(Color.white, 0.15f);
                Handles.DrawWireDisc(pivot, cam.transform.forward, size * scaleFactors.x, DimThickness);
            }

            Handles.zTest = prevZ;
            Handles.color = prevC;
        }
    }
}

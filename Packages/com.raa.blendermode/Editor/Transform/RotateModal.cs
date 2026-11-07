using System.Text;
using UnityEditor;
using UnityEngine;

namespace RAA.BlenderMode
{
    /// <summary>
    /// Rotate (R): rotate the selection about the pivot. Default axis is the view axis; X/Y/Z constrain to a
    /// world/local axis. Angle is the mouse sweep around the pivot's screen position, with numeric entry,
    /// Ctrl increment snapping and Shift precision. Individual-origins pivot rotates each object in place.
    /// See DESIGN.md §4.
    /// </summary>
    public sealed class RotateModal : TransformModal
    {
        protected override string OpName => "Rotate";
        protected override bool AffectsSnap => SnapSettings.AffectRotate;

        float _angle;         // degrees, accumulated mouse sweep
        float _appliedAngle;  // degrees actually applied this event (snapped/numeric); drives the widget arc
        float _prevMouseAng;
        bool _angInit;
        Vector3 _axisWorld = Vector3.forward;

        /// <summary>The rotation angle currently applied (swept, snapped, or numeric), in degrees. Drives the widget arc.</summary>
        protected float AppliedAngle => _appliedAngle;

        protected override void ComputeAndApply()
        {
            Vector3 axis = RotationAxis();
            float angle;

            if (Numeric.HasInput)
            {
                angle = Numeric.GetComponentOr(0, 0f);
            }
            else
            {
                Vector2 pivotGui = HandleUtility.WorldToGUIPoint(Pivot);
                Vector2 v = EffectiveMouse() - pivotGui;
                float cur = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
                if (!_angInit) { _prevMouseAng = cur; _angInit = true; }
                _angle += Mathf.DeltaAngle(_prevMouseAng, cur);
                _prevMouseAng = cur;

                angle = _angle;
                if (SnapActive)
                    angle = SnapEngine.Snap(angle, RotateIncrement());
            }

            _axisWorld = axis;
            _appliedAngle = angle;
            ApplyRotation(axis, angle);
        }

        Vector3 RotationAxis()
        {
            if (Axis >= 0)
            {
                Quaternion b = ConstraintBasis();
                return (b * (Axis == 0 ? Vector3.right : Axis == 1 ? Vector3.up : Vector3.forward)).normalized;
            }
            // View axis: negate camera forward so a CCW screen sweep reads as a positive angle.
            Camera cam = SceneView != null ? SceneView.camera : null;
            return cam != null ? -cam.transform.forward : Vector3.forward;
        }

        void ApplyRotation(Vector3 axis, float angleDeg)
        {
            Quaternion dr = Quaternion.AngleAxis(angleDeg, axis);
            for (int i = 0; i < Targets.Length; i++)
            {
                Transform t = Targets[i];
                if (t == null) continue;
                Vector3 pivot = PerObjectPivot ? StartPos[i] : Pivot;
                Quaternion rot = dr * StartRot[i];
                Vector3 pos = pivot + dr * (StartPos[i] - pivot);
                t.SetPositionAndRotation(pos, rot);
            }
        }

        protected override TransformRecord BuildRecord()
        {
            return new TransformRecord
            {
                kind = TransformKind.Rotate,
                rotationAxis = _axisWorld,
                rotationAngle = _angle,
                pivotMode = PivotOrientation.Pivot,
                perObjectPivot = PerObjectPivot,
                pivotPoint = Pivot
            };
        }

        public override string HudText()
        {
            var sb = new StringBuilder();
            float shown = Numeric.HasInput ? Numeric.GetComponentOr(0, 0f) : _angle;
            sb.Append("Rotate  ").Append(shown.ToString("0.##")).Append('°');
            string c = ConstraintLabel();
            if (c.Length > 0) sb.Append("  about ").Append(c);
            AppendPivot(sb);
            AppendSnapState(sb, RotateIncrement(), "°");
            AppendNumeric(sb);
            return sb.ToString();
        }

        public override void DrawHandles(SceneView sceneView)
        {
            DrawPivotMarker(sceneView);

            // Guide line from the pivot toward the mouse (screen space).
            Handles.BeginGUI();
            Vector2 pivotGui = HandleUtility.WorldToGUIPoint(Pivot);
            Color prev = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.6f);
            DrawGuiLine(pivotGui, CurMouse);
            GUI.color = prev;
            Handles.EndGUI();

            // Rotation gizmo: rings + swept-angle arc at the (median) pivot. Respect the overlay toggle.
            if (BlenderModePrefs.OverlaysVisible)
                ModalWidgets.DrawRotateWidget(sceneView, Pivot, WidgetBasis, Axis, Plane, StartMouse, _appliedAngle);
        }

        static void DrawGuiLine(Vector2 a, Vector2 b)
        {
            // Thin quad along a→b using the built-in white texture.
            Vector2 d = b - a;
            float len = d.magnitude;
            if (len < 1f) return;
            float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            Matrix4x4 m = GUI.matrix;
            GUIUtility.RotateAroundPivot(ang, a);
            GUI.DrawTexture(new Rect(a.x, a.y - 1f, len, 2f), EditorGUIUtility.whiteTexture);
            GUI.matrix = m;
        }
    }
}

using System.Text;
using UnityEditor;
using UnityEngine;

namespace RAA.BlenderMode
{
    /// <summary>
    /// Grab (G): translate the selection with the mouse. Supports axis/plane constraints, numeric entry,
    /// Ctrl snapping (increment / grid-absolute / vertex / surface) and Shift precision. See DESIGN.md §4.
    /// </summary>
    public sealed class GrabModal : TransformModal
    {
        protected override string OpName => "Grab";
        protected override bool AffectsSnap => SnapSettings.AffectMove;

        Vector3 _delta;
        bool _alignToNormal;
        Vector3 _surfaceNormal = Vector3.up;

        protected override void ComputeAndApply()
        {
            Vector3 delta;

            if (Numeric.HasInput)
            {
                delta = NumericVector();
            }
            else
            {
                bool snap = SnapActive;
                if (snap && (SnapSettings.Mode == SnapMode.Vertex || SnapSettings.Mode == SnapMode.Surface))
                {
                    delta = SurfaceOrVertexDelta();
                }
                else
                {
                    Vector3 w0 = PointOnPivotPlane(StartMouse);
                    Vector3 wN = PointOnPivotPlane(EffectiveMouse());
                    delta = ConstrainVector(wN - w0);
                    if (snap) delta = SnapDelta(delta);
                }
            }

            _delta = delta;
            ApplyTranslation(delta);
        }

        Vector3 SurfaceOrVertexDelta()
        {
            _alignToNormal = false;
            Vector2 gui = EffectiveMouse();
            if (SnapSettings.Mode == SnapMode.Vertex)
            {
                if (SnapEngine.TryVertex(SceneView, gui, Targets, SnapCache, out Vector3 v, out Vector3 vn))
                {
                    _surfaceNormal = vn;
                    _alignToNormal = SnapSettings.AlignRotationToTarget;
                    return ConstrainVector(v - GeoBasePoint(v));
                }
            }
            else // Surface
            {
                if (SnapEngine.TrySurface(SceneView, gui, Targets, out Vector3 p, out Vector3 normal))
                {
                    _surfaceNormal = normal;
                    _alignToNormal = SnapSettings.AlignRotationToTarget;
                    return ConstrainVector(p - GeoBasePoint(p));
                }
            }
            // No hit: fall back to plane drag.
            Vector3 w0 = PointOnPivotPlane(StartMouse);
            Vector3 wN = PointOnPivotPlane(gui);
            return ConstrainVector(wN - w0);
        }

        Vector3 SnapDelta(Vector3 delta)
        {
            if (SnapSettings.Mode == SnapMode.GridAbsolute)
                return delta; // absolute grid handled per-object in ApplyTranslation
            // Increment: snap the delta expressed in the constraint basis.
            Quaternion b = Axis >= 0 ? ConstraintBasis() : OrientationBasis;
            Vector3 local = Quaternion.Inverse(b) * delta;
            local = SnapEngine.SnapVector(local, MoveIncrement());
            return b * local;
        }

        void ApplyTranslation(Vector3 delta)
        {
            bool gridAbs = SnapActive && SnapSettings.Mode == SnapMode.GridAbsolute;
            float grid = SnapSettings.GridSize;

            for (int i = 0; i < Targets.Length; i++)
            {
                Transform t = Targets[i];
                if (t == null) continue;
                Vector3 p = StartPos[i] + delta;
                if (gridAbs)
                    p = new Vector3(SnapEngine.Snap(p.x, grid), SnapEngine.Snap(p.y, grid), SnapEngine.Snap(p.z, grid));
                t.position = p;

                if (_alignToNormal)
                {
                    Vector3 axisWorld = (StartRot[i] * SnapSettings.AlignAxisVector(SnapSettings.AlignAxis)).normalized;
                    t.rotation = Quaternion.FromToRotation(axisWorld, _surfaceNormal.normalized) * StartRot[i];
                }
                else
                {
                    t.rotation = StartRot[i];
                }
            }
        }

        protected override TransformRecord BuildRecord()
        {
            return new TransformRecord
            {
                kind = TransformKind.Grab,
                translation = _delta,
                pivotMode = PivotOrientation.Pivot,
                perObjectPivot = PerObjectPivot
            };
        }

        public override string HudText()
        {
            var sb = new StringBuilder();
            sb.Append("Grab  D: ")
              .Append(_delta.x.ToString("0.###")).Append(", ")
              .Append(_delta.y.ToString("0.###")).Append(", ")
              .Append(_delta.z.ToString("0.###"));
            string c = ConstraintLabel();
            if (c.Length > 0) sb.Append("  along ").Append(c);
            if (SnapActive)
            {
                if (SnapSettings.Mode == SnapMode.Increment)
                    AppendSnapState(sb, MoveIncrement(), string.Empty);
                else if (SnapSettings.Mode == SnapMode.Vertex || SnapSettings.Mode == SnapMode.Surface)
                    sb.Append("  (snap ").Append(SnapSettings.Mode).Append(", ").Append(SnapSettings.Base).Append(" base)");
                else
                    sb.Append("  (snap ").Append(SnapSettings.Mode).Append(')');
            }
            else if (Precise) sb.Append("  (precision)");
            AppendNumeric(sb);
            return sb.ToString();
        }

        public override void DrawHandles(SceneView sceneView)
        {
            if (Axis < 0) return;
            Quaternion b = ConstraintBasis();
            Vector3 dir = b * (Axis == 0 ? Vector3.right : Axis == 1 ? Vector3.up : Vector3.forward);
            Handles.color = Axis == 0 ? Color.red : Axis == 1 ? Color.green : Color.blue;
            float len = HandleUtility.GetHandleSize(Pivot) * 1000f;
            Handles.DrawLine(Pivot - dir * len, Pivot + dir * len);
            Handles.color = Color.white;
        }
    }
}

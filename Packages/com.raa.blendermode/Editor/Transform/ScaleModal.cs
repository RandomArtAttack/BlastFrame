using System.Text;
using UnityEditor;
using UnityEngine;

namespace RAA.BlenderMode
{
    /// <summary>
    /// Scale (S): scale the selection about the pivot. Ratio = current mouse distance to the pivot's screen
    /// position ÷ the distance at start. Axis/plane constraints scale only those components (in the current
    /// orientation basis). Numeric entry, Ctrl increment snapping and Shift precision are supported.
    /// Individual-origins pivot scales each object in place. See DESIGN.md §4.
    /// </summary>
    public sealed class ScaleModal : TransformModal
    {
        protected override string OpName => "Scale";
        protected override bool AffectsSnap => SnapSettings.AffectScale;

        Vector3 _scaleVec = Vector3.one;

        /// <summary>Per-axis scale factors currently applied, expressed in the active constraint/orientation basis. Drives the widget.</summary>
        protected Vector3 ScaleFactors => _scaleVec;

        protected override void ComputeAndApply()
        {
            Vector3 s;
            if (Numeric.HasInput)
            {
                s = NumericScale();
            }
            else
            {
                Vector2 pivotGui = HandleUtility.WorldToGUIPoint(Pivot);
                float startDist = Vector2.Distance(StartMouse, pivotGui);
                float nowDist = Vector2.Distance(EffectiveMouse(), pivotGui);
                float ratio = startDist > 1e-3f ? nowDist / startDist : 1f;

                if (SnapActive)
                    ratio = SnapEngine.Snap(ratio, ScaleIncrement());

                s = ScaleVecFromRatio(ratio);
            }

            _scaleVec = s;
            ApplyScale(s);
        }

        Vector3 ScaleVecFromRatio(float ratio)
        {
            if (Axis < 0) return new Vector3(ratio, ratio, ratio);
            if (!Plane)
            {
                Vector3 v = Vector3.one;
                v[Axis] = ratio;
                return v;
            }
            Vector3 pv = new Vector3(ratio, ratio, ratio);
            pv[Axis] = 1f; // plane excludes Axis
            return pv;
        }

        Vector3 NumericScale()
        {
            if (Axis < 0)
            {
                if (Numeric.ComponentCount <= 1)
                {
                    float u = Numeric.GetComponentOr(0, 1f);
                    return new Vector3(u, u, u);
                }
                return new Vector3(
                    Numeric.GetComponentOr(0, 1f),
                    Numeric.GetComponentOr(1, 1f),
                    Numeric.GetComponentOr(2, 1f));
            }
            if (!Plane)
            {
                Vector3 v = Vector3.one;
                v[Axis] = Numeric.GetComponentOr(0, 1f);
                return v;
            }
            Vector3 pv = Vector3.one;
            switch (Axis)
            {
                case 0: pv.y = Numeric.GetComponentOr(0, 1f); pv.z = Numeric.GetComponentOr(1, 1f); break;
                case 1: pv.x = Numeric.GetComponentOr(0, 1f); pv.z = Numeric.GetComponentOr(1, 1f); break;
                default: pv.x = Numeric.GetComponentOr(0, 1f); pv.y = Numeric.GetComponentOr(1, 1f); break;
            }
            return pv;
        }

        void ApplyScale(Vector3 s)
        {
            // Basis in which the scale components are expressed (orientation, or local when so constrained).
            Quaternion basis = Axis >= 0 ? ConstraintBasis() : OrientationBasis;
            Quaternion invBasis = Quaternion.Inverse(basis);

            for (int i = 0; i < Targets.Length; i++)
            {
                Transform t = Targets[i];
                if (t == null) continue;

                // Object's own scale (approximation for arbitrary bases; exact for uniform/local/global-aligned).
                t.localScale = Vector3.Scale(StartScale[i], s);

                if (!PerObjectPivot)
                {
                    Vector3 offset = StartPos[i] - Pivot;
                    Vector3 local = invBasis * offset;
                    local = Vector3.Scale(local, s);
                    t.position = Pivot + basis * local;
                }
            }
        }

        protected override TransformRecord BuildRecord()
        {
            return new TransformRecord
            {
                kind = TransformKind.Scale,
                scaleVec = _scaleVec,
                basis = Axis >= 0 ? ConstraintBasis() : OrientationBasis,
                pivotMode = PivotOrientation.Pivot,
                perObjectPivot = PerObjectPivot,
                pivotPoint = Pivot
            };
        }

        public override string HudText()
        {
            var sb = new StringBuilder();
            sb.Append("Scale  ")
              .Append(_scaleVec.x.ToString("0.###")).Append(", ")
              .Append(_scaleVec.y.ToString("0.###")).Append(", ")
              .Append(_scaleVec.z.ToString("0.###"));
            string c = ConstraintLabel();
            if (c.Length > 0) sb.Append("  along ").Append(c);
            AppendPivot(sb);
            AppendSnapState(sb, ScaleIncrement(), string.Empty);
            AppendNumeric(sb);
            return sb.ToString();
        }

        public override void DrawHandles(SceneView sceneView)
        {
            DrawPivotMarker(sceneView);

            // Scale gizmo: axis handles with cube caps that stretch by the applied factor. Respect the overlay toggle.
            if (BlenderModePrefs.OverlaysVisible)
                ModalWidgets.DrawScaleWidget(sceneView, Pivot, WidgetBasis, Axis, Plane, ScaleFactors);
        }
    }
}

using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace RAA.BlenderMode
{
    /// <summary>
    /// Shared base for the Grab/Rotate/Scale modal operators. Owns the common modal state: target gathering,
    /// pivot resolution, orientation, axis/plane constraints with global↔local double-tap cycling, MMB
    /// nearest-axis picking, Ctrl-snap / Shift-precision (with rebasing so there is no jump), numeric input,
    /// undo handling and HUD. Concrete ops implement the actual math in <see cref="ComputeAndApply"/>.
    /// See DESIGN.md §4.
    /// </summary>
    public abstract class TransformModal : ModalOperator
    {
        // ---- targets / start state ----
        protected Transform[] Targets;
        protected Transform Active;
        Trs[] _preTrs;
        protected Vector3[] StartPos;      // world
        protected Quaternion[] StartRot;   // world
        protected Vector3[] StartScale;    // local
        protected Vector3 ActiveStartPos;

        // ---- pivot / orientation ----
        protected Vector3 Pivot;           // world (resolved pivot-point mode: median/active/cursor/bounds)
        protected Vector3 MotionRef;       // world; stable drag-plane depth = selection median, pivot-independent
        protected bool PerObjectPivot;     // Individual Origins
        protected Quaternion OrientationBasis;

        // ---- constraints ----
        protected int Axis = -1;           // 0=X,1=Y,2=Z, -1 none
        protected bool Plane;              // Shift+axis => plane (exclude Axis)
        protected bool ConstraintLocal;    // second tap => local orientation

        // ---- modifiers / mouse ----
        protected bool Ctrl;
        protected bool Shift;
        protected Vector2 StartMouse;
        protected Vector2 CurMouse;
        PrecisionAxis _px, _py;

        // ---- MMB nearest-axis pick ----
        bool _mmbPick;

        // ---- numeric ----
        protected readonly NumericInput Numeric = new NumericInput();

        // ---- snapping ----
        protected DragSnapCache SnapCache = new DragSnapCache();
        /// <summary>Closest-snap candidate points captured at drag start (bounds corners + centers).</summary>
        protected Vector3[] ClosestCandidates;
        Vector3 _snapBaseDelta;

        int _undoGroup;
        bool _prevWantsMouseMove;

        /// <summary>Undo-group / HUD name for this operation.</summary>
        protected abstract string OpName { get; }

        /// <summary>The relevant SnapSettings.Affect* flag for this operation (move/rotate/scale).</summary>
        protected abstract bool AffectsSnap { get; }

        /// <summary>Recompute the preview transform from the cached start state (called every event).</summary>
        protected abstract void ComputeAndApply();

        /// <summary>Capture the confirmed operation so it can be repeated / adjusted.</summary>
        protected abstract TransformRecord BuildRecord();

        /// <summary>True when a valid selection exists to transform (assets/empty selection are ignored).</summary>
        public static bool HasValidSelection()
        {
            Transform[] t = Selection.transforms;
            return t != null && t.Length > 0;
        }

        public override void OnStart(SceneView sceneView)
        {
            base.OnStart(sceneView);

            Targets = Selection.transforms;
            Active = Selection.activeTransform != null ? Selection.activeTransform
                   : (Targets.Length > 0 ? Targets[0] : null);

            _preTrs = UndoScope.Cache(Targets);
            int n = Targets.Length;
            StartPos = new Vector3[n];
            StartRot = new Quaternion[n];
            StartScale = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                Transform t = Targets[i];
                if (t == null) continue;
                StartPos[i] = t.position;
                StartRot[i] = t.rotation;
                StartScale[i] = t.localScale;
            }
            ActiveStartPos = Active != null ? Active.position : Pivot;

            PerObjectPivot = PivotOrientation.Pivot == PivotMode.IndividualOrigins;
            Pivot = PivotOrientation.GetPivotPoint(Targets, Active);
            OrientationBasis = PivotOrientation.GetOrientationRotation(sceneView, Active);

            // Drag-plane depth for Grab: always the selection median, independent of the pivot-point
            // setting, so a distant 3D-cursor / active pivot doesn't distort the mouse→world scale.
            MotionRef = MedianOf(StartPos);

            // Snap-base ("Snap With") reference points, captured once (the selection translates rigidly).
            ClosestCandidates = SnapEngine.BuildClosestCandidates(Targets);
            _snapBaseDelta = Vector3.zero;

            StartMouse = Event.current != null ? Event.current.mousePosition : Vector2.zero;
            CurMouse = StartMouse;

            _prevWantsMouseMove = sceneView.wantsMouseMove;
            sceneView.wantsMouseMove = true;
        }

        public override void HandleEvent(Event e)
        {
            Ctrl = e.control || e.command;
            Shift = e.shift;

            switch (e.type)
            {
                case EventType.KeyDown:
                    if (IsCancel(e)) { Cancel(); e.Use(); return; }
                    if (IsConfirm(e)) { Confirm(); e.Use(); return; }
                    if (HandleAxisKey(e)) { ComputeAndApply(); e.Use(); return; }
                    if (Numeric.HandleKey(e)) { ComputeAndApply(); e.Use(); return; }
                    // Consume any other key so the modal stays exclusive.
                    ComputeAndApply();
                    e.Use();
                    return;

                case EventType.KeyUp:
                    ComputeAndApply();
                    e.Use();
                    return;

                case EventType.MouseDown:
                    if (e.button == 0) { Confirm(); e.Use(); return; }
                    if (e.button == 1) { Cancel(); e.Use(); return; }
                    if (e.button == 2) { _mmbPick = true; e.Use(); return; }
                    return;

                case EventType.MouseUp:
                    if (e.button == 2) { _mmbPick = false; e.Use(); return; }
                    return;

                case EventType.MouseMove:
                case EventType.MouseDrag:
                    CurMouse = e.mousePosition;
                    if (_mmbPick) PickNearestAxis();
                    ComputeAndApply();
                    e.Use();
                    return;

                case EventType.Repaint:
                case EventType.Layout:
                    ComputeAndApply();
                    return;
            }
        }

        public override void Confirm()
        {
            // Reset to the true original, then record and re-apply the final state as one undo group.
            UndoScope.Restore(Targets, _preTrs);
            _undoGroup = UndoScope.Begin(OpName);
            Undo.RecordObjects(RecordSet(), OpName);
            ComputeAndApply();
            UndoScope.Collapse(_undoGroup);
            TransformOps.SetLast(BuildRecord(), Targets, _preTrs);
            Finish();
        }

        public override void Cancel()
        {
            UndoScope.Restore(Targets, _preTrs);
            Finish();
        }

        /// <summary>The target transforms for a single Undo.RecordObjects call.</summary>
        Object[] RecordSet()
        {
            var list = new List<Object>();
            for (int i = 0; i < Targets.Length; i++)
                if (Targets[i] != null) list.Add(Targets[i]);
            return list.ToArray();
        }

        void Finish()
        {
            if (SceneView != null) SceneView.wantsMouseMove = _prevWantsMouseMove;
            End();
        }

        // ------------------------------------------------------------- constraints

        bool HandleAxisKey(Event e)
        {
            int axis;
            if (e.keyCode == KeyCode.X) axis = 0;
            else if (e.keyCode == KeyCode.Y) axis = 1;
            else if (e.keyCode == KeyCode.Z) axis = 2;
            else return false;

            bool plane = e.shift;
            if (Axis == axis && Plane == plane)
            {
                if (!ConstraintLocal) ConstraintLocal = true;     // 2nd tap => local
                else { Axis = -1; Plane = false; ConstraintLocal = false; }  // 3rd tap => clear
            }
            else
            {
                Axis = axis; Plane = plane; ConstraintLocal = false; // 1st tap => current orientation
            }
            return true;
        }

        /// <summary>Basis used to interpret the axis constraint (local override vs the current orientation).</summary>
        protected Quaternion ConstraintBasis()
        {
            return ConstraintLocal
                ? (Active != null ? Active.rotation : Quaternion.identity)
                : OrientationBasis;
        }

        /// <summary>
        /// Basis the transform widgets/axis lines are drawn in: the constraint basis (which follows the
        /// local override when so constrained) while an axis/plane constraint is active, else the orientation
        /// basis. Matches the basis <see cref="ComputeAndApply"/> uses, so the widget tracks the real motion.
        /// </summary>
        protected Quaternion WidgetBasis => Axis >= 0 ? ConstraintBasis() : OrientationBasis;

        static Vector3 Unit(int axis) => axis == 0 ? Vector3.right : axis == 1 ? Vector3.up : Vector3.forward;

        static void PlaneAxes(int excluded, out int a1, out int a2)
        {
            switch (excluded)
            {
                case 0: a1 = 1; a2 = 2; break;
                case 1: a1 = 0; a2 = 2; break;
                default: a1 = 0; a2 = 1; break;
            }
        }

        /// <summary>Project a world-space delta onto the active axis/plane constraint.</summary>
        protected Vector3 ConstrainVector(Vector3 delta)
        {
            if (Axis < 0) return delta;
            Quaternion b = ConstraintBasis();
            if (!Plane)
            {
                Vector3 dir = b * Unit(Axis);
                return dir * Vector3.Dot(delta, dir);
            }
            Vector3 n = b * Unit(Axis);
            return delta - n * Vector3.Dot(delta, n);
        }

        void PickNearestAxis()
        {
            Vector2 motion = CurMouse - StartMouse;
            if (motion.sqrMagnitude < 4f) return;
            motion.Normalize();

            Quaternion b = OrientationBasis;
            Vector2 pivotGui = HandleUtility.WorldToGUIPoint(Pivot);
            int best = -1;
            float bestDot = 0.2f;
            for (int i = 0; i < 3; i++)
            {
                Vector3 world = Pivot + b * Unit(i);
                Vector2 dir = (HandleUtility.WorldToGUIPoint(world) - pivotGui);
                if (dir.sqrMagnitude < 1e-4f) continue;
                dir.Normalize();
                float d = Mathf.Abs(Vector2.Dot(dir, motion));
                if (d > bestDot) { bestDot = d; best = i; }
            }
            if (best >= 0) { Axis = best; Plane = false; ConstraintLocal = false; }
        }

        // ------------------------------------------------------------- numeric helpers

        /// <summary>Map numeric components to a world delta given the current constraint (for grab/scale offsets).</summary>
        protected Vector3 NumericVector()
        {
            Quaternion b = Axis >= 0 ? ConstraintBasis() : OrientationBasis;
            if (Axis >= 0 && !Plane)
                return b * Unit(Axis) * Numeric.GetComponentOr(0, 0f);
            if (Axis >= 0 && Plane)
            {
                PlaneAxes(Axis, out int a1, out int a2);
                return b * Unit(a1) * Numeric.GetComponentOr(0, 0f) + b * Unit(a2) * Numeric.GetComponentOr(1, 0f);
            }
            return b * new Vector3(
                Numeric.GetComponentOr(0, 0f),
                Numeric.GetComponentOr(1, 0f),
                Numeric.GetComponentOr(2, 0f));
        }

        // ------------------------------------------------------------- snap / precision state (Blender semantics)

        /// <summary>
        /// True when increment/grid/vertex/surface snapping is active this event, honoring the magnet
        /// inversion (magnet off → only while Ctrl; magnet on → unless Ctrl) and the per-op Affect flag.
        /// Never active while numeric input is engaged (numeric wins). See DESIGN.md §5.
        /// </summary>
        protected bool SnapActive => !Numeric.HasInput && AffectsSnap && SnapSettings.IsActive(Ctrl);

        /// <summary>Ctrl+Shift-style fine snap: snapping active AND Shift held → snap at 1/10 the increment.</summary>
        protected bool FineSnap => SnapActive && Shift;

        /// <summary>Precision (Shift, 10× slower) mode: Shift held while snapping is NOT active. Snap wins over precision.</summary>
        protected bool Precise => Shift && !SnapActive;

        /// <summary>
        /// Precision-filtered mouse position. Motion is accumulated per mouse-move scaled by the precision
        /// factor while <see cref="Precise"/>, so pressing/releasing Shift never jumps and — because precision
        /// only applies when snapping is inactive — snapped positions are never dampened. See DESIGN.md §5.
        /// </summary>
        protected Vector2 EffectiveMouse()
        {
            float factor = Mathf.Clamp(BlenderModePrefs.PrecisionFactor, 0.001f, 1f);
            bool p = Precise;
            float x = _px.Apply(CurMouse.x, p, factor);
            float y = _py.Apply(CurMouse.y, p, factor);
            return new Vector2(x, y);
        }

        /// <summary>
        /// Resolve the snap-base reference point (Closest/Center/Median/Active) that a geometry snap places on
        /// <paramref name="target"/>. Center = resolved pivot, Median = selection median, Active = active
        /// origin, Closest = nearest cached candidate. The returned delta from this point drives every object.
        /// </summary>
        protected Vector3 GeoBasePoint(Vector3 target)
        {
            return SnapEngine.ResolveGeometryBasePoint(SnapSettings.Base, target,
                Pivot, MotionRef, ActiveStartPos, ClosestCandidates, ref _snapBaseDelta);
        }

        /// <summary>Effective move increment: adaptive (zoom-derived) or fixed, 1/10 for fine on Ctrl+Shift.</summary>
        protected float MoveIncrement()
        {
            float b = (BlenderModePrefs.AdaptiveIncrement && SceneView != null)
                ? SnapEngine.AdaptiveStep(SceneView.size)
                : BlenderModePrefs.IncrementMove;
            return FineSnap ? b * 0.1f : b;
        }

        /// <summary>Effective rotate snap increment: 5° coarse, 1° fine (Blender). Pref-editable coarse step.</summary>
        protected float RotateIncrement()
        {
            float b = Mathf.Max(0.0001f, BlenderModePrefs.IncrementRotate);
            return FineSnap ? Mathf.Min(1f, b) : b;
        }

        /// <summary>Effective scale snap increment: 0.1 coarse, 0.01 fine (Blender). Pref-editable coarse step.</summary>
        protected float ScaleIncrement()
        {
            float b = BlenderModePrefs.IncrementScale;
            return FineSnap ? b * 0.1f : b;
        }

        // ------------------------------------------------------------- scene helpers

        /// <summary>Intersect a GUI point with the camera-facing plane through the selection median (drag depth).</summary>
        protected Vector3 PointOnPivotPlane(Vector2 guiPoint)
        {
            Camera cam = SceneView != null ? SceneView.camera : null;
            Ray ray = HandleUtility.GUIPointToWorldRay(guiPoint);
            Vector3 normal = cam != null ? -cam.transform.forward : Vector3.forward;
            var plane = new Plane(normal, MotionRef);
            return plane.Raycast(ray, out float enter) ? ray.GetPoint(enter) : MotionRef;
        }

        static Vector3 MedianOf(Vector3[] pts)
        {
            if (pts == null || pts.Length == 0) return Vector3.zero;
            Vector3 sum = Vector3.zero;
            for (int i = 0; i < pts.Length; i++) sum += pts[i];
            return sum / pts.Length;
        }

        // ------------------------------------------------------------- HUD

        /// <summary>Short label for the active constraint (e.g. "global X", "local XZ plane", or "").</summary>
        protected string ConstraintLabel()
        {
            if (Axis < 0) return string.Empty;
            string orient = ConstraintLocal ? "local" : PivotOrientation.OrientationLabel(PivotOrientation.Orientation);
            string letter = Axis == 0 ? "X" : Axis == 1 ? "Y" : "Z";
            if (Plane)
            {
                PlaneAxes(Axis, out int a1, out int a2);
                string p = "" + "XYZ"[a1] + "XYZ"[a2];
                return orient + " " + p + " plane";
            }
            return orient + " " + letter;
        }

        /// <summary>Append the numeric-entry display to a HUD builder when typing is active.</summary>
        protected void AppendNumeric(StringBuilder sb)
        {
            if (Numeric.HasInput)
                sb.Append("  [numeric: ").Append(Numeric.Display()).Append(']');
        }

        /// <summary>Append the effective snap / precision state, e.g. "(snap 0.25)", "(snap 0.025 fine)", "(precision)".</summary>
        protected void AppendSnapState(StringBuilder sb, float increment, string unit)
        {
            if (SnapActive)
            {
                sb.Append("  (snap ").Append(increment.ToString("0.####")).Append(unit);
                if (FineSnap) sb.Append(" fine");
                sb.Append(')');
            }
            else if (Precise)
            {
                sb.Append("  (precision)");
            }
        }

        /// <summary>Human-readable active pivot mode for the HUD ("Median Point", "3D Cursor", "individual origins").</summary>
        protected string PivotStateLabel()
        {
            return PerObjectPivot ? "individual origins" : PivotOrientation.PivotLabel(PivotOrientation.Pivot);
        }

        /// <summary>Append the active pivot mode to a HUD builder (rotate/scale feedback).</summary>
        protected void AppendPivot(StringBuilder sb)
        {
            sb.Append("  [pivot: ").Append(PivotStateLabel()).Append(']');
        }

        /// <summary>
        /// Draw the point(s) the operation orbits/scales about, so the active pivot mode is visible. Median /
        /// Active / Cursor / Bounding-Box draw one marker at <see cref="Pivot"/>; Individual Origins draws one
        /// at each object's origin. Always-on-top, Blender orange.
        /// </summary>
        protected void DrawPivotMarker(SceneView sceneView)
        {
            if (!BlenderModePrefs.OverlaysVisible) return;
            Camera cam = sceneView != null ? sceneView.camera : null;
            Vector3 fwd = cam != null ? cam.transform.forward : Vector3.forward;
            Vector3 right = cam != null ? cam.transform.right : Vector3.right;
            Vector3 up = cam != null ? cam.transform.up : Vector3.up;

            Color prev = Handles.color;
            var prevZ = Handles.zTest;
            Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;

            if (PerObjectPivot)
            {
                for (int i = 0; i < Targets.Length; i++)
                    if (Targets[i] != null) DrawOnePivot(StartPos[i], fwd, right, up);
            }
            else
            {
                DrawOnePivot(Pivot, fwd, right, up);
            }

            Handles.zTest = prevZ;
            Handles.color = prev;
        }

        static void DrawOnePivot(Vector3 p, Vector3 fwd, Vector3 right, Vector3 up)
        {
            float s = HandleUtility.GetHandleSize(p) * 0.09f;
            Handles.color = new Color(1f, 0.6f, 0.1f, 1f);   // Blender pivot orange
            Handles.DrawSolidDisc(p, fwd, s * 0.5f);
            Handles.color = new Color(1f, 1f, 1f, 0.9f);
            Handles.DrawWireDisc(p, fwd, s);
            Handles.DrawLine(p - right * s * 1.6f, p + right * s * 1.6f);
            Handles.DrawLine(p - up * s * 1.6f, p + up * s * 1.6f);
        }
    }

    /// <summary>
    /// Per-axis precision accumulator for Shift-precision. Integrates the mouse motion, scaling each
    /// incremental delta by the precision factor while precise and by 1 otherwise — so pressing/releasing
    /// Shift mid-drag rebases with zero jump (only the *incremental* motion is ever scaled, never the total).
    /// See DESIGN.md §5.
    /// </summary>
    public struct PrecisionAxis
    {
        bool _init;
        float _rawPrev;
        float _value;

        public float Apply(float raw, bool precise, float factor)
        {
            if (!_init) { _init = true; _rawPrev = raw; _value = raw; }
            _value += (raw - _rawPrev) * (precise ? factor : 1f);
            _rawPrev = raw;
            return _value;
        }
    }
}

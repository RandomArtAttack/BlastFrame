using System;
using System.Text;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;

namespace RAA.BlenderMode
{
    /// <summary>
    /// Watches the active editor tool and, while Blender Mode is enabled and "Override Transform Gizmos" is
    /// on, swaps Unity's built-in Move/Rotate/Scale tools for Blender Mode's pivot- and snap-aware versions
    /// (<see cref="BlenderMoveTool"/> / <see cref="BlenderRotateTool"/> / <see cref="BlenderScaleTool"/>).
    /// Because W/E/R and the toolbar select the built-in tool first, we simply react to the tool change and
    /// swap. When the master toggle (or the override pref) turns off, any active Blender tool is restored to
    /// the matching built-in. While one of our tools is active, Unity's own handle Ctrl-snap is neutralized
    /// (its increments are saved and set near-zero) so it can't fight our snapping; the user's values are
    /// restored when a non-Blender tool becomes active. See DESIGN.md §4/§5.
    /// </summary>
    [InitializeOnLoad]
    internal static class BlenderTransformToolBridge
    {
        static bool _swapping;
        static bool _snapNeutralized;
        static Vector3 _savedMove;
        static float _savedRotate;
        static float _savedScale;

        static BlenderTransformToolBridge()
        {
            ToolManager.activeToolChanged -= OnActiveToolChanged;
            ToolManager.activeToolChanged += OnActiveToolChanged;
            BlenderModePrefs.Changed -= OnPrefsChanged;
            BlenderModePrefs.Changed += OnPrefsChanged;
            // ToolManager isn't fully ready inside the static ctor on load; sync on the next tick.
            EditorApplication.delayCall += InitialSync;
        }

        /// <summary>True when the override behavior should be engaged.</summary>
        static bool Active => BlenderModePrefs.Enabled && BlenderModePrefs.OverrideTransformGizmos;

        static void InitialSync()
        {
            if (Active) SwapForCurrentBuiltin();
            UpdateSnapNeutralization();
        }

        static void OnActiveToolChanged()
        {
            // Swap the built-in transform tools to ours (guarded against the re-entrant change our own
            // SetActiveTool call raises). Snap neutralization is refreshed on every change, including the
            // guarded one, so entering/leaving our tools always toggles it correctly.
            if (!_swapping && Active) SwapForCurrentBuiltin();
            UpdateSnapNeutralization();
        }

        static void OnPrefsChanged()
        {
            Type at = ToolManager.activeToolType;
            bool ours = at != null && IsOurs(at);

            if (!Active && ours)
            {
                // Master toggle / override turned off while one of our tools was active: restore the built-in.
                if (at == typeof(BlenderMoveTool)) Tools.current = Tool.Move;
                else if (at == typeof(BlenderRotateTool)) Tools.current = Tool.Rotate;
                else Tools.current = Tool.Scale;
            }
            else if (Active && !ours)
            {
                // Turned on while a built-in transform tool is active (toggling a pref doesn't itself fire
                // activeToolChanged): swap now.
                SwapForCurrentBuiltin();
            }

            UpdateSnapNeutralization();
        }

        static void SwapForCurrentBuiltin()
        {
            switch (Tools.current)
            {
                case Tool.Move: SwapTo(typeof(BlenderMoveTool)); break;
                case Tool.Rotate: SwapTo(typeof(BlenderRotateTool)); break;
                case Tool.Scale: SwapTo(typeof(BlenderScaleTool)); break;
            }
        }

        static void SwapTo(Type type)
        {
            if (ToolManager.activeToolType == type) return; // already ours; avoid a re-entrant loop
            _swapping = true;
            try { ToolManager.SetActiveTool(type); }
            finally { _swapping = false; }
        }

        static bool IsOurs(Type t) =>
            t == typeof(BlenderMoveTool) || t == typeof(BlenderRotateTool) || t == typeof(BlenderScaleTool);

        /// <summary>Save + neutralize Unity's handle snap increments while a Blender tool is active; restore
        /// the user's values once a non-Blender tool becomes active.</summary>
        static void UpdateSnapNeutralization()
        {
            Type at = ToolManager.activeToolType;
            bool ours = at != null && IsOurs(at);

            if (ours && !_snapNeutralized)
            {
                _savedMove = EditorSnapSettings.move;
                _savedRotate = EditorSnapSettings.rotate;
                _savedScale = EditorSnapSettings.scale;
                EditorSnapSettings.move = new Vector3(1e-5f, 1e-5f, 1e-5f);
                EditorSnapSettings.rotate = 1e-4f;
                EditorSnapSettings.scale = 1e-5f;
                _snapNeutralized = true;
            }
            else if (!ours && _snapNeutralized)
            {
                EditorSnapSettings.move = _savedMove;
                EditorSnapSettings.rotate = _savedRotate;
                EditorSnapSettings.scale = _savedScale;
                _snapNeutralized = false;
            }
        }
    }

    /// <summary>
    /// Shared base for the Blender-aware Move/Rotate/Scale editor tools. Owns the drag lifecycle (cache the
    /// selection's start TRS and the resolved pivot on drag begin, recompute the preview from that cached
    /// state every GUI pass so snapping never accumulates error), the pivot/orientation resolution, undo
    /// grouping and the snap modifier helpers (mirroring <see cref="TransformModal"/>). Concrete tools draw
    /// the handle and apply the delta in <see cref="OnToolGUI"/>. See DESIGN.md §4/§5.
    /// </summary>
    internal abstract class BlenderTransformTool : EditorTool
    {
        protected bool Dragging;
        protected Transform[] Targets;
        protected Transform Active;
        protected Vector3[] StartPos;      // world
        protected Quaternion[] StartRot;   // world
        protected Vector3[] StartScale;    // local
        protected Vector3 ActiveStartPos;  // world

        protected Vector3 Pivot;           // resolved pivot (median for the handle when Individual Origins)
        protected bool PerObjectPivot;     // Individual Origins
        protected Quaternion OrientationBasis;

        protected readonly DragSnapCache SnapCache = new DragSnapCache();
        int _undoGroup;

        /// <summary>Undo-group / HUD name for this operation.</summary>
        protected abstract string OpName { get; }

        // ---- modifiers ----
        protected static bool Ctrl => Event.current != null && (Event.current.control || Event.current.command);
        protected static bool Shift => Event.current != null && Event.current.shift;

        public override void OnWillBeDeactivated()
        {
            if (Dragging) EndDrag();
        }

        /// <summary>Resolve the pivot/orientation from the live selection (called each idle frame).</summary>
        protected void ResolvePivot(SceneView sv)
        {
            PerObjectPivot = PivotOrientation.Pivot == PivotMode.IndividualOrigins;
            Pivot = PivotOrientation.GetPivotPoint(Targets, Active);
            OrientationBasis = PivotOrientation.GetOrientationRotation(sv, Active);
        }

        /// <summary>Cache the selection's start state and open the undo group at the start of a drag.</summary>
        protected void BeginDrag()
        {
            Dragging = true;
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

            SnapCache.Filters = null;
            SnapCache.Verts.Clear();

            _undoGroup = UndoScope.Begin(OpName);
            UndoScope.RecordTransforms(Targets, OpName);
        }

        /// <summary>Collapse the drag's undo group into a single step.</summary>
        protected void EndDrag()
        {
            Dragging = false;
            UndoScope.Collapse(_undoGroup);
        }

        /// <summary>Median of a set of world points (selection origins), used as a snap-base reference.</summary>
        protected static Vector3 Median(Vector3[] pts)
        {
            if (pts == null || pts.Length == 0) return Vector3.zero;
            Vector3 sum = Vector3.zero;
            int n = 0;
            for (int i = 0; i < pts.Length; i++) { sum += pts[i]; n++; }
            return n > 0 ? sum / n : Vector3.zero;
        }

        /// <summary>Fetch the current selection; returns false (and clears drag state) when nothing is selected.</summary>
        protected bool AcquireTargets()
        {
            Targets = Selection.transforms;
            if (Targets == null || Targets.Length == 0)
            {
                Dragging = false;
                return false;
            }
            Active = Selection.activeTransform != null ? Selection.activeTransform : Targets[0];
            return true;
        }

        // ---- effective snap increments (mirror TransformModal) ----

        protected float MoveIncrement(SceneView sv, bool fine)
        {
            float b = (BlenderModePrefs.AdaptiveIncrement && sv != null)
                ? SnapEngine.AdaptiveStep(sv.size)
                : BlenderModePrefs.IncrementMove;
            return fine ? b * 0.1f : b;
        }

        protected float RotateIncrement(bool fine)
        {
            float b = Mathf.Max(0.0001f, BlenderModePrefs.IncrementRotate);
            return fine ? Mathf.Min(1f, b) : b;
        }

        protected float ScaleIncrement(bool fine)
        {
            float b = BlenderModePrefs.IncrementScale;
            return fine ? b * 0.1f : b;
        }

        /// <summary>Draw the modal-style HUD footer with the op name and effective snap state.</summary>
        protected void DrawFooter(SceneView sv, string body)
        {
            if (!Dragging) return;
            var sb = new StringBuilder();
            sb.Append(OpName).Append("  ").Append(body);
            Hud.DrawFooter(sv, sb.ToString());
        }
    }

    /// <summary>
    /// Blender-aware Move gizmo. Draws a <see cref="Handles.PositionHandle"/> at the resolved pivot in the
    /// current transform orientation and applies the drag delta to every selected transform. Snapping honors
    /// Blender Mode semantics: Increment snaps the delta (Ctrl / magnet inversion, Shift = fine), Grid
    /// Absolute snaps each resulting world position, and Vertex/Surface place the active object's origin on
    /// the target under the cursor (others keep their offset; Surface can align to the hit normal).
    /// </summary>
    [EditorTool("Blender Move")]
    internal sealed class BlenderMoveTool : BlenderTransformTool
    {
        protected override string OpName => "Move";

        GUIContent _icon;
        public override GUIContent toolbarIcon =>
            _icon ??= new GUIContent(IconLibrary.Get("tool_move"),
                "Blender Move — G (gizmo: W)");

        Vector3 _liveHandlePos;

        // Snap-base ("Snap With") state, captured at drag start.
        Vector3[] _closestCandidates;
        Vector3 _snapBaseDelta;
        Vector3 _medianStart;

        public override void OnToolGUI(EditorWindow window)
        {
            if (!(window is SceneView sv)) return;
            if (!AcquireTargets()) return;

            if (!Dragging) { ResolvePivot(sv); _liveHandlePos = Pivot; }

            Quaternion hRot = OrientationBasis;
            EditorGUI.BeginChangeCheck();
            Vector3 result = Handles.PositionHandle(_liveHandlePos, hRot);
            if (EditorGUI.EndChangeCheck())
            {
                if (!Dragging)
                {
                    BeginDrag();                // Pivot was resolved this frame → freeze it as the drag start
                    _closestCandidates = SnapEngine.BuildClosestCandidates(Targets);
                    _snapBaseDelta = Vector3.zero;
                    _medianStart = Median(StartPos);
                }
                _liveHandlePos = result;
                ApplyMove(sv, result - Pivot, hRot);
            }

            if (Dragging && GUIUtility.hotControl == 0) EndDrag();

            DrawFooter(sv, SnapLabel(sv));
        }

        void ApplyMove(SceneView sv, Vector3 rawDelta, Quaternion basis)
        {
            bool snap = SnapSettings.AffectMove && SnapSettings.IsActive(Ctrl);
            bool fine = snap && Shift;
            SnapMode mode = SnapSettings.Mode;

            if (snap && (mode == SnapMode.Vertex || mode == SnapMode.Surface))
            {
                Vector2 gui = Event.current.mousePosition;
                bool aligned = false;
                Vector3 normal = Vector3.up;
                Vector3 delta;
                if (mode == SnapMode.Vertex && SnapEngine.TryVertex(sv, gui, Targets, SnapCache, out Vector3 v, out Vector3 vn))
                {
                    Vector3 basePt = SnapEngine.ResolveGeometryBasePoint(SnapSettings.Base, v,
                        Pivot, _medianStart, ActiveStartPos, _closestCandidates, ref _snapBaseDelta);
                    delta = v - basePt;
                    normal = vn;
                    aligned = SnapSettings.AlignRotationToTarget;
                }
                else if (mode == SnapMode.Surface && SnapEngine.TrySurface(sv, gui, Targets, out Vector3 p, out normal))
                {
                    Vector3 basePt = SnapEngine.ResolveGeometryBasePoint(SnapSettings.Base, p,
                        Pivot, _medianStart, ActiveStartPos, _closestCandidates, ref _snapBaseDelta);
                    delta = p - basePt;
                    aligned = SnapSettings.AlignRotationToTarget;
                }
                else
                {
                    delta = rawDelta; // no hit under the cursor: fall back to the free drag
                }

                for (int i = 0; i < Targets.Length; i++)
                {
                    Transform t = Targets[i];
                    if (t == null) continue;
                    t.position = StartPos[i] + delta;
                    t.rotation = aligned
                        ? Quaternion.FromToRotation(
                              (StartRot[i] * SnapSettings.AlignAxisVector(SnapSettings.AlignAxis)).normalized,
                              normal.normalized) * StartRot[i]
                        : StartRot[i];
                }
                return;
            }

            Vector3 d = rawDelta;
            if (snap && mode == SnapMode.Increment)
            {
                Vector3 local = Quaternion.Inverse(basis) * d;
                local = SnapEngine.SnapVector(local, MoveIncrement(sv, fine));
                d = basis * local;
            }

            bool gridAbs = snap && mode == SnapMode.GridAbsolute;
            float grid = SnapSettings.GridSize;
            for (int i = 0; i < Targets.Length; i++)
            {
                Transform t = Targets[i];
                if (t == null) continue;
                Vector3 pos = StartPos[i] + d;
                if (gridAbs)
                    pos = new Vector3(SnapEngine.Snap(pos.x, grid), SnapEngine.Snap(pos.y, grid), SnapEngine.Snap(pos.z, grid));
                t.position = pos;
            }
        }

        string SnapLabel(SceneView sv)
        {
            if (!(SnapSettings.AffectMove && SnapSettings.IsActive(Ctrl))) return string.Empty;
            bool fine = Shift;
            switch (SnapSettings.Mode)
            {
                case SnapMode.Increment:
                    return "(snap " + MoveIncrement(sv, fine).ToString("0.####") + (fine ? " fine)" : ")");
                default:
                    return "(snap " + SnapSettings.Mode + ")";
            }
        }
    }

    /// <summary>
    /// Blender-aware Rotate gizmo. Draws a <see cref="Handles.RotationHandle"/> at the resolved pivot; the
    /// rotation delta orbits every selected object about that pivot (pos' = pivot + R·(pos−pivot), rot' =
    /// R·rot). Individual Origins rotates each object about its own origin in place. Snaps to 5° (1° fine).
    /// </summary>
    [EditorTool("Blender Rotate")]
    internal sealed class BlenderRotateTool : BlenderTransformTool
    {
        protected override string OpName => "Rotate";

        GUIContent _icon;
        public override GUIContent toolbarIcon =>
            _icon ??= new GUIContent(IconLibrary.Get("tool_rotate"),
                "Blender Rotate — R (gizmo: E)");

        Quaternion _liveRot = Quaternion.identity;
        Quaternion _refRot = Quaternion.identity;

        public override void OnToolGUI(EditorWindow window)
        {
            if (!(window is SceneView sv)) return;
            if (!AcquireTargets()) return;

            if (!Dragging) { ResolvePivot(sv); _liveRot = OrientationBasis; }

            EditorGUI.BeginChangeCheck();
            Quaternion result = Handles.RotationHandle(_liveRot, Pivot);
            if (EditorGUI.EndChangeCheck())
            {
                if (!Dragging) { BeginDrag(); _refRot = OrientationBasis; }
                _liveRot = result;
                ApplyRotate(result * Quaternion.Inverse(_refRot));
            }

            if (Dragging && GUIUtility.hotControl == 0) EndDrag();

            DrawFooter(sv, SnapLabel());
        }

        void ApplyRotate(Quaternion deltaRaw)
        {
            bool snap = SnapSettings.AffectRotate && SnapSettings.IsActive(Ctrl);
            Quaternion delta = deltaRaw;
            if (snap)
            {
                deltaRaw.ToAngleAxis(out float ang, out Vector3 axis);
                if (float.IsNaN(axis.x) || axis.sqrMagnitude < 1e-8f) axis = Vector3.up;
                if (ang > 180f) ang -= 360f; // symmetric snapping
                float snapped = SnapEngine.Snap(ang, RotateIncrement(snap && Shift));
                delta = Quaternion.AngleAxis(snapped, axis);
            }

            for (int i = 0; i < Targets.Length; i++)
            {
                Transform t = Targets[i];
                if (t == null) continue;
                Vector3 pivot = PerObjectPivot ? StartPos[i] : Pivot;
                t.SetPositionAndRotation(pivot + delta * (StartPos[i] - pivot), delta * StartRot[i]);
            }
        }

        string SnapLabel()
        {
            if (!(SnapSettings.AffectRotate && SnapSettings.IsActive(Ctrl))) return string.Empty;
            bool fine = Shift;
            return "(snap " + RotateIncrement(fine).ToString("0.##") + "°" + (fine ? " fine)" : ")");
        }
    }

    /// <summary>
    /// Blender-aware Scale gizmo. Draws a <see cref="Handles.ScaleHandle"/> at the resolved pivot; the scale
    /// factor multiplies each object's localScale and spans its position from the pivot (pos' = pivot +
    /// s∘(pos−pivot)). Individual Origins scales each object in place. Snaps to 0.1 (0.01 fine).
    /// </summary>
    [EditorTool("Blender Scale")]
    internal sealed class BlenderScaleTool : BlenderTransformTool
    {
        protected override string OpName => "Scale";

        GUIContent _icon;
        public override GUIContent toolbarIcon =>
            _icon ??= new GUIContent(IconLibrary.Get("tool_scale"),
                "Blender Scale — S (gizmo: R)");

        Vector3 _liveScale = Vector3.one;

        public override void OnToolGUI(EditorWindow window)
        {
            if (!(window is SceneView sv)) return;
            if (!AcquireTargets()) return;

            if (!Dragging) { ResolvePivot(sv); _liveScale = Vector3.one; }

            Quaternion basis = OrientationBasis;
            float size = HandleUtility.GetHandleSize(Pivot);
            EditorGUI.BeginChangeCheck();
            Vector3 result = Handles.ScaleHandle(_liveScale, Pivot, basis, size);
            if (EditorGUI.EndChangeCheck())
            {
                if (!Dragging) BeginDrag();
                _liveScale = result;
                ApplyScale(result, basis);
            }

            if (Dragging && GUIUtility.hotControl == 0) EndDrag();

            DrawFooter(sv, SnapLabel());
        }

        void ApplyScale(Vector3 rawScale, Quaternion basis)
        {
            bool snap = SnapSettings.AffectScale && SnapSettings.IsActive(Ctrl);
            Vector3 s = snap ? SnapEngine.SnapVector(rawScale, ScaleIncrement(snap && Shift)) : rawScale;

            Quaternion invBasis = Quaternion.Inverse(basis);
            for (int i = 0; i < Targets.Length; i++)
            {
                Transform t = Targets[i];
                if (t == null) continue;
                t.localScale = Vector3.Scale(StartScale[i], s);
                if (!PerObjectPivot)
                {
                    Vector3 local = invBasis * (StartPos[i] - Pivot);
                    local = Vector3.Scale(local, s);
                    t.position = Pivot + basis * local;
                }
            }
        }

        string SnapLabel()
        {
            if (!(SnapSettings.AffectScale && SnapSettings.IsActive(Ctrl))) return string.Empty;
            bool fine = Shift;
            return "(snap " + ScaleIncrement(fine).ToString("0.####") + (fine ? " fine)" : ")");
        }
    }
}

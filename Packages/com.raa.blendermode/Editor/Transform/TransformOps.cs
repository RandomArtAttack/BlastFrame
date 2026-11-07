using UnityEditor;
using UnityEngine;

namespace RAA.BlenderMode
{
    /// <summary>Kind of a confirmed transform, used for repeat-last / adjust-last.</summary>
    public enum TransformKind
    {
        Grab, Rotate, Scale, ClearPosition, ClearRotation, ClearScale
    }

    /// <summary>
    /// A confirmed transform operation captured for repeat-last (Shift+R) and adjust-last (F9). Holds enough
    /// parameters to re-derive the result from a set of start transforms. See DESIGN.md §4.
    /// </summary>
    public sealed class TransformRecord
    {
        public TransformKind kind;
        public Vector3 translation;              // Grab: world delta
        public Vector3 rotationAxis = Vector3.forward; // Rotate: world axis
        public float rotationAngle;              // Rotate: degrees
        public Vector3 scaleVec = Vector3.one;   // Scale: per-basis factors
        public Quaternion basis = Quaternion.identity; // Scale: basis for scaleVec
        public PivotMode pivotMode;
        public bool perObjectPivot;
        public Vector3 pivotPoint;               // captured pivot (rotate/scale)

        public TransformRecord Clone()
        {
            return (TransformRecord)MemberwiseClone();
        }
    }

    /// <summary>
    /// Transform-related instant operations and post-op tools: the G/R/S starters, Alt+G/R/S clears,
    /// Shift+D duplicate (+grab), Alt+D linked duplicate, Shift+R repeat-last and F9 adjust-last.
    /// See DESIGN.md §4.
    /// </summary>
    [InitializeOnLoad]
    public static class TransformOps
    {
        static TransformOps() { Register(); }

        static bool _registered;

        static TransformRecord _last;
        static Transform[] _lastTargets;
        static Trs[] _lastPreTrs;

        /// <summary>Register all transform action handlers (idempotent).</summary>
        public static void Register()
        {
            if (_registered) return;
            _registered = true;

            ActionRegistry.Register(Actions.Grab, (sv, e) => Start(new GrabModal(), sv));
            ActionRegistry.Register(Actions.Rotate, (sv, e) => Start(new RotateModal(), sv));
            ActionRegistry.Register(Actions.Scale, (sv, e) => Start(new ScaleModal(), sv));

            ActionRegistry.Register(Actions.ClearPosition, (sv, e) => Clear(TransformKind.ClearPosition));
            ActionRegistry.Register(Actions.ClearRotation, (sv, e) => Clear(TransformKind.ClearRotation));
            ActionRegistry.Register(Actions.ClearScale, (sv, e) => Clear(TransformKind.ClearScale));

            ActionRegistry.Register(Actions.Duplicate, (sv, e) => Duplicate(sv, false));
            ActionRegistry.Register(Actions.DuplicateLinked, (sv, e) => Duplicate(sv, true));

            ActionRegistry.Register(Actions.RepeatLast, (sv, e) => RepeatLast());
            ActionRegistry.Register(Actions.AdjustLast, (sv, e) => AdjustLast(sv));
        }

        static bool Start(TransformModal modal, SceneView sv)
        {
            if (!TransformModal.HasValidSelection()) return false;
            BlenderModeManager.StartOperator(modal, sv);
            return true;
        }

        /// <summary>Store the last confirmed operation (called by the modals on confirm).</summary>
        public static void SetLast(TransformRecord record, Transform[] targets, Trs[] preTrs)
        {
            _last = record;
            _lastTargets = targets;
            _lastPreTrs = preTrs;
        }

        // ------------------------------------------------------------- clears

        static bool Clear(TransformKind kind)
        {
            Transform[] targets = Selection.transforms;
            if (targets == null || targets.Length == 0) return false;

            string name = kind == TransformKind.ClearPosition ? "Clear Location"
                        : kind == TransformKind.ClearRotation ? "Clear Rotation" : "Clear Scale";

            int group = UndoScope.Begin(name);
            Undo.RecordObjects(targets, name);
            for (int i = 0; i < targets.Length; i++)
            {
                Transform t = targets[i];
                if (t == null) continue;
                switch (kind)
                {
                    case TransformKind.ClearPosition: t.localPosition = Vector3.zero; break;
                    case TransformKind.ClearRotation: t.localRotation = Quaternion.identity; break;
                    case TransformKind.ClearScale: t.localScale = Vector3.one; break;
                }
            }
            UndoScope.Collapse(group);
            return true;
        }

        // ------------------------------------------------------------- duplicate

        static bool Duplicate(SceneView sv, bool linked)
        {
            GameObject[] sel = Selection.gameObjects;
            if (sel == null || sel.Length == 0) return false;

            if (!linked)
            {
                // Unity's Duplicate keeps prefab instances as instances.
                EditorApplication.ExecuteMenuItem("Edit/Duplicate");
            }
            else
            {
                DuplicateLinked(sel);
            }

            // Immediately enter Grab on the new selection.
            if (TransformModal.HasValidSelection())
                BlenderModeManager.StartOperator(new GrabModal(), sv);
            return true;
        }

        static void DuplicateLinked(GameObject[] sel)
        {
            var created = new System.Collections.Generic.List<Object>();
            int group = UndoScope.Begin("Linked Duplicate");

            foreach (GameObject go in sel)
            {
                if (go == null) continue;
                GameObject dup = null;

                if (PrefabUtility.IsPartOfPrefabInstance(go))
                {
                    string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
                    Object asset = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (asset != null)
                    {
                        dup = (GameObject)PrefabUtility.InstantiatePrefab(asset, go.transform.parent);
                        dup.transform.SetPositionAndRotation(go.transform.position, go.transform.rotation);
                        dup.transform.localScale = go.transform.localScale;
                    }
                }

                if (dup == null)
                {
                    dup = Object.Instantiate(go, go.transform.parent);
                    dup.name = go.name;
                }

                Undo.RegisterCreatedObjectUndo(dup, "Linked Duplicate");
                created.Add(dup);
            }

            UndoScope.Collapse(group);
            if (created.Count > 0)
                Selection.objects = created.ToArray();
        }

        // ------------------------------------------------------------- repeat-last

        static bool RepeatLast()
        {
            if (_last == null) return false;
            Transform[] targets = Selection.transforms;
            if (targets == null || targets.Length == 0) return false;

            int group = UndoScope.Begin("Repeat " + _last.kind);
            Undo.RecordObjects(targets, "Repeat");

            if (_last.kind == TransformKind.ClearPosition || _last.kind == TransformKind.ClearRotation
                || _last.kind == TransformKind.ClearScale)
            {
                ApplyClear(targets, _last.kind);
            }
            else
            {
                Transform active = Selection.activeTransform;
                Vector3 pivot = PivotOrientation.GetPivotPoint(targets, active);
                ApplyCore(_last, targets, pivot);
            }

            UndoScope.Collapse(group);
            return true;
        }

        static void ApplyClear(Transform[] targets, TransformKind kind)
        {
            for (int i = 0; i < targets.Length; i++)
            {
                Transform t = targets[i];
                if (t == null) continue;
                if (kind == TransformKind.ClearPosition) t.localPosition = Vector3.zero;
                else if (kind == TransformKind.ClearRotation) t.localRotation = Quaternion.identity;
                else t.localScale = Vector3.one;
            }
        }

        /// <summary>Apply a grab/rotate/scale record to targets using their CURRENT transforms as the start.</summary>
        static void ApplyCore(TransformRecord r, Transform[] targets, Vector3 pivot)
        {
            int n = targets.Length;
            var startPos = new Vector3[n];
            var startRot = new Quaternion[n];
            var startScale = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                if (targets[i] == null) continue;
                startPos[i] = targets[i].position;
                startRot[i] = targets[i].rotation;
                startScale[i] = targets[i].localScale;
            }
            ApplyCore(r, targets, startPos, startRot, startScale, pivot);
        }

        static void ApplyCore(TransformRecord r, Transform[] targets, Vector3[] startPos,
            Quaternion[] startRot, Vector3[] startScale, Vector3 pivot)
        {
            switch (r.kind)
            {
                case TransformKind.Grab:
                    for (int i = 0; i < targets.Length; i++)
                        if (targets[i] != null) targets[i].position = startPos[i] + r.translation;
                    break;

                case TransformKind.Rotate:
                {
                    Quaternion dr = Quaternion.AngleAxis(r.rotationAngle, r.rotationAxis);
                    for (int i = 0; i < targets.Length; i++)
                    {
                        if (targets[i] == null) continue;
                        Vector3 pv = r.perObjectPivot ? startPos[i] : pivot;
                        targets[i].SetPositionAndRotation(pv + dr * (startPos[i] - pv), dr * startRot[i]);
                    }
                    break;
                }

                case TransformKind.Scale:
                {
                    Quaternion invBasis = Quaternion.Inverse(r.basis);
                    for (int i = 0; i < targets.Length; i++)
                    {
                        if (targets[i] == null) continue;
                        targets[i].localScale = Vector3.Scale(startScale[i], r.scaleVec);
                        if (!r.perObjectPivot)
                        {
                            Vector3 local = invBasis * (startPos[i] - pivot);
                            local = Vector3.Scale(local, r.scaleVec);
                            targets[i].position = pivot + r.basis * local;
                        }
                    }
                    break;
                }
            }
        }

        // ------------------------------------------------------------- adjust-last

        static bool AdjustLast(SceneView sv)
        {
            if (!BlenderModePrefs.AdjustLastEnabled) return false;
            if (_last == null || _lastTargets == null) return false;
            AdjustLastWindow.Open(sv);
            return true;
        }

        /// <summary>Re-apply the last operation (with possibly-edited values) to the ORIGINAL targets from their pre-op state.</summary>
        public static void ReapplyAdjust(TransformRecord edited)
        {
            if (_lastTargets == null || _lastPreTrs == null) return;

            UndoScope.Restore(_lastTargets, _lastPreTrs);
            int group = UndoScope.Begin("Adjust " + edited.kind);
            Undo.RecordObjects(_lastTargets, "Adjust");

            if (edited.kind == TransformKind.ClearPosition || edited.kind == TransformKind.ClearRotation
                || edited.kind == TransformKind.ClearScale)
            {
                ApplyClear(_lastTargets, edited.kind);
            }
            else
            {
                ApplyCore(edited, _lastTargets, edited.pivotPoint);
            }

            UndoScope.Collapse(group);
            _last = edited; // subsequent edits build on the same pre-op base
        }

        /// <summary>The last confirmed operation (for the adjust-last window), or null.</summary>
        public static TransformRecord LastRecord => _last;
    }

    /// <summary>
    /// Small floating popup (F9 / adjust-last) that shows the last operation's values and re-applies them
    /// from the pre-op state as they are edited. Closes when it loses focus. See DESIGN.md §4.
    /// </summary>
    public sealed class AdjustLastWindow : EditorWindow
    {
        TransformRecord _working;

        public static void Open(SceneView sv)
        {
            TransformRecord src = TransformOps.LastRecord;
            if (src == null) return;

            var w = CreateInstance<AdjustLastWindow>();
            w._working = src.Clone();

            Vector2 size = new Vector2(260f, 96f);
            Rect anchor = sv != null ? sv.position : new Rect(100, 100, 400, 400);
            w.position = new Rect(anchor.x + 12f, anchor.y + anchor.height - size.y - 40f, size.x, size.y);
            w.ShowPopup();
            w.Focus();
        }

        void OnLostFocus() { Close(); }

        void OnGUI()
        {
            if (_working == null) { Close(); return; }

            EditorGUILayout.LabelField(_working.kind.ToString(), EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();

            switch (_working.kind)
            {
                case TransformKind.Grab:
                    _working.translation = EditorGUILayout.Vector3Field("Move", _working.translation);
                    break;
                case TransformKind.Rotate:
                    _working.rotationAngle = EditorGUILayout.FloatField("Angle", _working.rotationAngle);
                    break;
                case TransformKind.Scale:
                    _working.scaleVec = EditorGUILayout.Vector3Field("Scale", _working.scaleVec);
                    break;
                default:
                    EditorGUILayout.LabelField("No adjustable values.");
                    break;
            }

            if (EditorGUI.EndChangeCheck())
                TransformOps.ReapplyAdjust(_working.Clone());

            if (Event.current.type == EventType.KeyDown &&
                (Event.current.keyCode == KeyCode.Escape || Event.current.keyCode == KeyCode.Return))
                Close();
        }
    }
}

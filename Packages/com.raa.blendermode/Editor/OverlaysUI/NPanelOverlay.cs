using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEngine;
using UnityEngine.UIElements;

namespace RAA.BlenderMode
{
    /// <summary>
    /// The N-panel: a SceneView overlay (hidden by default, toggled by the N key) with Item / View / Tool /
    /// Sets / Align tabs. Drawn with IMGUI inside the overlay for robustness. See DESIGN.md §9.
    /// </summary>
    [Overlay(typeof(SceneView), Id, "Blender Mode N-Panel", false)]
    public class NPanelOverlay : Overlay
    {
        public const string Id = "blendermode-npanel";

        /// <summary>Live overlay instances, one per SceneView, so the N-key handler can find its panel
        /// without relying on non-public OverlayCanvas lookup APIs.</summary>
        internal static readonly List<NPanelOverlay> Instances = new List<NPanelOverlay>();

        static readonly string[] Tabs = { "Item", "View", "Tool", "Sets", "Align" };

        IMGUIContainer _imgui;
        int _tab;
        string _newSetName = string.Empty;
        AlignTools.Axis _alignAxis = AlignTools.Axis.X;
        Vector2 _scroll;

        public override VisualElement CreatePanelContent()
        {
            _imgui = new IMGUIContainer(DrawPanel) { style = { minWidth = 300f, width = 320f } };

            Selection.selectionChanged -= RequestRepaint;
            Selection.selectionChanged += RequestRepaint;
            BlenderModePrefs.Changed -= RequestRepaint;
            BlenderModePrefs.Changed += RequestRepaint;

            return _imgui;
        }

        public override void OnCreated()
        {
            base.OnCreated();
            if (!Instances.Contains(this))
                Instances.Add(this);
        }

        public override void OnWillBeDestroyed()
        {
            Instances.Remove(this);
            Selection.selectionChanged -= RequestRepaint;
            BlenderModePrefs.Changed -= RequestRepaint;
            base.OnWillBeDestroyed();
        }

        void RequestRepaint() => _imgui?.MarkDirtyRepaint();

        void DrawPanel()
        {
            _tab = GUILayout.Toolbar(_tab, Tabs);
            EditorGUILayout.Space(4f);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            switch (_tab)
            {
                case 0: DrawItem(); break;
                case 1: DrawView(); break;
                case 2: DrawTool(); break;
                case 3: DrawSets(); break;
                case 4: DrawAlign(); break;
            }
            EditorGUILayout.EndScrollView();
        }

        // ---------------------------------------------------------------- Item

        void DrawItem()
        {
            Transform active = Selection.activeTransform;
            if (active == null)
            {
                EditorGUILayout.HelpBox("No active object.", MessageType.Info);
                return;
            }

            Transform[] sel = Selection.transforms;
            EditorGUILayout.LabelField("Transform (" + sel.Length + " selected)", EditorStyles.boldLabel);

            if (Vec3Expr("Position", active.position, out Vector3 p))
                Apply(sel, "Set Position", t => t.position = p);
            if (Vec3Expr("Rotation", active.localEulerAngles, out Vector3 r))
                Apply(sel, "Set Rotation", t => t.localEulerAngles = r);
            if (Vec3Expr("Scale", active.localScale, out Vector3 s))
                Apply(sel, "Set Scale", t => t.localScale = s);

            EditorGUILayout.Space();
            if (TryGetSelectionBounds(sel, out Bounds b))
                EditorGUILayout.LabelField("Dimensions", b.size.ToString("0.###"));
        }

        static void Apply(Transform[] sel, string name, System.Action<Transform> op)
        {
            int group = UndoScope.Begin(name);
            Undo.RecordObjects(sel, name);
            for (int i = 0; i < sel.Length; i++)
                if (sel[i] != null) op(sel[i]);
            UndoScope.Collapse(group);
        }

        static bool Vec3Expr(string label, Vector3 cur, out Vector3 result)
        {
            result = cur;
            bool changed = false;
            EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);
            using (new GUILayout.HorizontalScope())
            {
                result.x = Component("X", cur.x, ref changed);
                result.y = Component("Y", cur.y, ref changed);
                result.z = Component("Z", cur.z, ref changed);
            }
            return changed;
        }

        static float Component(string label, float value, ref bool changed)
        {
            GUILayout.Label(label, GUILayout.Width(12f));
            EditorGUI.BeginChangeCheck();
            string s = EditorGUILayout.DelayedTextField(value.ToString("0.#####"));
            if (EditorGUI.EndChangeCheck() && ExpressionEvaluator.TryEvaluate(s, out float r))
            {
                changed = true;
                return r;
            }
            return value;
        }

        static bool TryGetSelectionBounds(Transform[] sel, out Bounds bounds)
        {
            bounds = default;
            bool has = false;
            for (int i = 0; i < sel.Length; i++)
            {
                if (sel[i] == null) continue;
                Renderer rd = sel[i].GetComponentInChildren<Renderer>();
                Bounds b = rd != null ? rd.bounds : new Bounds(sel[i].position, Vector3.zero);
                if (!has) { bounds = b; has = true; }
                else bounds.Encapsulate(b);
            }
            return has;
        }

        // ---------------------------------------------------------------- View

        void DrawView()
        {
            EditorGUILayout.LabelField("3D Cursor", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            Vector3 cp = EditorGUILayout.Vector3Field("Position", Cursor3D.Position);
            Vector3 ce = EditorGUILayout.Vector3Field("Rotation", Cursor3D.Rotation.eulerAngles);
            if (EditorGUI.EndChangeCheck())
                Cursor3D.Set(cp, Quaternion.Euler(ce));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Camera Bookmarks", EditorStyles.boldLabel);
            SceneView sv = SceneView.lastActiveSceneView;
            IReadOnlyList<CameraBookmark> bms = CameraBookmarks.Bookmarks;
            for (int i = 0; i < CameraBookmarks.SlotCount; i++)
            {
                CameraBookmark b = bms[i];
                int slot = i + 1;
                using (new GUILayout.HorizontalScope())
                {
                    string newName = EditorGUILayout.DelayedTextField(b.name);
                    if (newName != b.name) CameraBookmarks.Rename(slot, newName);
                    GUILayout.Label(b.assigned ? "●" : "○", GUILayout.Width(16f));
                    if (GUILayout.Button("Store", GUILayout.Width(50f)) && sv != null) CameraBookmarks.Store(sv, slot);
                    using (new EditorGUI.DisabledScope(!b.assigned))
                        if (GUILayout.Button("Recall", GUILayout.Width(54f)) && sv != null) CameraBookmarks.Recall(sv, slot);
                }
            }
        }

        // ---------------------------------------------------------------- Tool

        void DrawTool()
        {
            EditorGUILayout.LabelField("Snapping", EditorStyles.boldLabel);
            SnapSettings.Mode = (SnapMode)EditorGUILayout.EnumPopup("Mode", SnapSettings.Mode);
            SnapSettings.Base = (SnapBase)EditorGUILayout.EnumPopup(
                new GUIContent("Snap Base", "Which point of the selection lands on a Vertex/Surface target."),
                SnapSettings.Base);
            SnapSettings.Magnet = EditorGUILayout.Toggle("Magnet (always-on)", SnapSettings.Magnet);
            SnapSettings.GridSize = EditorGUILayout.FloatField("Grid Size", SnapSettings.GridSize);

            EditorGUILayout.LabelField("Affect", EditorStyles.miniBoldLabel);
            SnapSettings.AffectMove = EditorGUILayout.ToggleLeft("Move", SnapSettings.AffectMove);
            SnapSettings.AffectRotate = EditorGUILayout.ToggleLeft("Rotate", SnapSettings.AffectRotate);
            SnapSettings.AffectScale = EditorGUILayout.ToggleLeft("Scale", SnapSettings.AffectScale);

            SnapSettings.AlignRotationToTarget = EditorGUILayout.Toggle("Align Rotation to Target", SnapSettings.AlignRotationToTarget);
            SnapSettings.AlignAxis = (UpAxis)EditorGUILayout.EnumPopup("Align Axis", SnapSettings.AlignAxis);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Increments", EditorStyles.miniBoldLabel);
            BlenderModePrefs.IncrementMove = EditorGUILayout.FloatField("Move", BlenderModePrefs.IncrementMove);
            BlenderModePrefs.IncrementRotate = EditorGUILayout.FloatField("Rotate (°)", BlenderModePrefs.IncrementRotate);
            BlenderModePrefs.IncrementScale = EditorGUILayout.FloatField("Scale", BlenderModePrefs.IncrementScale);
        }

        // ---------------------------------------------------------------- Sets

        void DrawSets()
        {
            EditorGUILayout.LabelField("Selection Sets", EditorStyles.boldLabel);
            using (new GUILayout.HorizontalScope())
            {
                _newSetName = EditorGUILayout.TextField(_newSetName);
                if (GUILayout.Button("Store New", GUILayout.Width(80f)))
                {
                    string n = string.IsNullOrEmpty(_newSetName) ? "Set " + (SelectionSets.All.Count + 1) : _newSetName;
                    SelectionSets.Store(n);
                    _newSetName = string.Empty;
                }
            }

            // Iterate a snapshot so mutations during draw don't disturb the loop.
            var snapshot = new List<SelectionSet>(SelectionSets.All);
            if (snapshot.Count == 0)
                EditorGUILayout.HelpBox("No sets yet. Select objects and Store New, or use Ctrl+Shift+1..9.", MessageType.Info);

            for (int i = 0; i < snapshot.Count; i++)
            {
                SelectionSet s = snapshot[i];
                using (new GUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new GUILayout.HorizontalScope())
                    {
                        string nn = EditorGUILayout.DelayedTextField(s.name);
                        if (nn != s.name) SelectionSets.Rename(s.name, nn);
                        GUILayout.Label(s.objectIds.Count + " obj", GUILayout.Width(52f));
                    }
                    using (new GUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("Recall")) SelectionSets.Recall(s.name);
                        if (GUILayout.Button("Add")) SelectionSets.AddToSet(s.name);
                        if (GUILayout.Button("Remove")) SelectionSets.RemoveFromSet(s.name);
                        if (GUILayout.Button("Delete")) SelectionSets.Delete(s.name);
                    }
                }
            }
        }

        // ---------------------------------------------------------------- Align

        void DrawAlign()
        {
            EditorGUILayout.LabelField("Align", EditorStyles.boldLabel);
            _alignAxis = (AlignTools.Axis)EditorGUILayout.EnumPopup("Axis", _alignAxis);
            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Min")) AlignTools.Align(_alignAxis, -1);
                if (GUILayout.Button("Center")) AlignTools.Align(_alignAxis, 0);
                if (GUILayout.Button("Max")) AlignTools.Align(_alignAxis, 1);
            }
            if (GUILayout.Button("Distribute Along Axis")) AlignTools.Distribute(_alignAxis);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Drop", EditorStyles.boldLabel);
            BlenderModePrefs.DropAlignToNormal = EditorGUILayout.Toggle("Align to Surface Normal", BlenderModePrefs.DropAlignToNormal);
            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Drop to Surface")) AlignTools.DropToSurface();
                if (GUILayout.Button("Drop to Floor")) AlignTools.DropToFloor();
            }
        }
    }

    /// <summary>
    /// Registers the N key handler that toggles the <see cref="NPanelOverlay"/>'s visibility on the focused
    /// SceneView. Overlay instances self-register in <see cref="NPanelOverlay.Instances"/> because
    /// OverlayCanvas lookup APIs are not public in this Unity version. See DESIGN.md §9.
    /// </summary>
    [InitializeOnLoad]
    static class NPanelController
    {
        static NPanelController()
        {
            ActionRegistry.Register(Actions.ToggleNPanel, (sv, e) => Toggle(sv));
        }

        static bool Toggle(SceneView sv)
        {
            if (sv == null) return true;
            for (int i = 0; i < NPanelOverlay.Instances.Count; i++)
            {
                NPanelOverlay overlay = NPanelOverlay.Instances[i];
                if (overlay != null && overlay.containerWindow == sv)
                {
                    overlay.displayed = !overlay.displayed;
                    break;
                }
            }
            return true;
        }
    }
}

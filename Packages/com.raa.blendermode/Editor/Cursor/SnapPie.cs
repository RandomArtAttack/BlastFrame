using UnityEditor;
using UnityEngine;

namespace RAA.BlenderMode
{
    /// <summary>
    /// The Shift+S snap pie: Selection↔Cursor/Grid/Active snapping actions built on the generic
    /// <see cref="PieMenu"/>. Supports both release-confirm (release Shift+S while pointing at a slot) and
    /// click-confirm. Esc/RMB cancels. See DESIGN.md §6 and §8.
    /// </summary>
    [InitializeOnLoad]
    public sealed class SnapPie : ModalOperator
    {
        static SnapPie()
        {
            ActionRegistry.Register(Actions.SnapPie, (sv, e) =>
            {
                if (!BlenderModePrefs.ModulePies) return false;
                BlenderModeManager.StartOperator(new SnapPie(), sv);
                return true;
            });
        }

        readonly PieMenu _pie = new PieMenu();
        Vector2 _mouse;
        bool _prevWantsMouseMove;

        public override void OnStart(SceneView sceneView)
        {
            base.OnStart(sceneView);
            _mouse = Event.current != null ? Event.current.mousePosition : Vector2.zero;
            _pie.SetCenter(_mouse);

            Texture cur = IconLibrary.Get("cursor");
            Texture grid = IconLibrary.Get("grid");
            Texture piv = IconLibrary.Get("pivot");

            _pie.SetSlot(0, "Sel → Cursor", cur, SelectionToCursor);
            _pie.SetSlot(1, "Sel → Cursor (Offset)", cur, SelectionToCursorKeepOffset);
            _pie.SetSlot(2, "Sel → Grid", grid, SelectionToGrid);
            _pie.SetSlot(3, "Cursor → Active", piv, CursorToActive);
            _pie.SetSlot(4, "Cursor → Selected", piv, CursorToSelected);
            _pie.SetSlot(6, "Cursor → Grid", grid, CursorToGrid);
            _pie.SetSlot(7, "Cursor → World Origin", cur, CursorToWorldOrigin);

            _prevWantsMouseMove = sceneView.wantsMouseMove;
            sceneView.wantsMouseMove = true;
        }

        public override void HandleEvent(Event e)
        {
            switch (e.type)
            {
                case EventType.MouseMove:
                case EventType.MouseDrag:
                    _mouse = e.mousePosition;
                    e.Use();
                    return;

                case EventType.MouseDown:
                    if (e.button == 1) { Cancel(); e.Use(); return; }        // RMB cancel
                    if (e.button == 0 && BlenderModePrefs.PieConfirm != PieConfirmMode.ReleaseOnly)
                    { Confirm(); e.Use(); return; }                          // click-confirm
                    return;

                case EventType.KeyDown:
                    if (e.keyCode == KeyCode.Escape) { Cancel(); e.Use(); return; }
                    e.Use();
                    return;

                case EventType.KeyUp:
                    // Release-confirm gesture: releasing S (or Shift) picks the highlighted slot.
                    if (BlenderModePrefs.PieConfirm != PieConfirmMode.ClickOnly &&
                        (e.keyCode == KeyCode.S || e.keyCode == KeyCode.LeftShift || e.keyCode == KeyCode.RightShift))
                    {
                        if (_pie.HighlightedIndex(_mouse) >= 0) { Confirm(); e.Use(); return; }
                    }
                    e.Use();
                    return;
            }
        }

        public override void Confirm()
        {
            int i = _pie.HighlightedIndex(_mouse);
            _pie.Invoke(i);
            Finish();
        }

        public override void Cancel() => Finish();

        void Finish()
        {
            if (SceneView != null) SceneView.wantsMouseMove = _prevWantsMouseMove;
            End();
        }

        public override void DrawHandles(SceneView sceneView)
        {
            if (Event.current.type == EventType.Repaint)
                _pie.Draw(_mouse);
        }

        public override string HudText() => "Snap  (release to confirm, RMB/Esc to cancel)";

        // ---------------------------------------------------------------- snap actions

        static Transform[] Sel => Selection.transforms;

        static Vector3 Median(Transform[] t)
        {
            if (t == null || t.Length == 0) return Vector3.zero;
            Vector3 sum = Vector3.zero;
            int n = 0;
            for (int i = 0; i < t.Length; i++)
                if (t[i] != null) { sum += t[i].position; n++; }
            return n > 0 ? sum / n : Vector3.zero;
        }

        static void SelectionToCursor()
        {
            Transform[] t = Sel;
            if (t.Length == 0) return;
            int g = UndoScope.Begin("Selection to Cursor");
            Undo.RecordObjects(t, "Selection to Cursor");
            Vector3 c = Cursor3D.Position;
            for (int i = 0; i < t.Length; i++) if (t[i] != null) t[i].position = c;
            UndoScope.Collapse(g);
        }

        static void SelectionToCursorKeepOffset()
        {
            Transform[] t = Sel;
            if (t.Length == 0) return;
            int g = UndoScope.Begin("Selection to Cursor (Keep Offset)");
            Undo.RecordObjects(t, "Selection to Cursor");
            Vector3 delta = Cursor3D.Position - Median(t);
            for (int i = 0; i < t.Length; i++) if (t[i] != null) t[i].position += delta;
            UndoScope.Collapse(g);
        }

        static void SelectionToGrid()
        {
            Transform[] t = Sel;
            if (t.Length == 0) return;
            float grid = SnapSettings.GridSize;
            int g = UndoScope.Begin("Selection to Grid");
            Undo.RecordObjects(t, "Selection to Grid");
            for (int i = 0; i < t.Length; i++)
            {
                if (t[i] == null) continue;
                Vector3 p = t[i].position;
                t[i].position = new Vector3(SnapEngine.Snap(p.x, grid), SnapEngine.Snap(p.y, grid), SnapEngine.Snap(p.z, grid));
            }
            UndoScope.Collapse(g);
        }

        static void CursorToSelected() => Cursor3D.Position = Median(Sel);

        static void CursorToActive()
        {
            Transform a = Selection.activeTransform;
            if (a != null) Cursor3D.Set(a.position, a.rotation);
        }

        static void CursorToWorldOrigin() => Cursor3D.Set(Vector3.zero, Quaternion.identity);

        static void CursorToGrid()
        {
            float grid = SnapSettings.GridSize;
            Vector3 p = Cursor3D.Position;
            Cursor3D.Position = new Vector3(SnapEngine.Snap(p.x, grid), SnapEngine.Snap(p.y, grid), SnapEngine.Snap(p.z, grid));
        }
    }
}

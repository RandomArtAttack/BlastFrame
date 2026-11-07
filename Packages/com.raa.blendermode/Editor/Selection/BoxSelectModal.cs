using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace RAA.BlenderMode
{
    /// <summary>
    /// Box select (B): shows crosshair lines at the cursor, then LMB-drag a rectangle. Objects inside the
    /// rectangle are picked with <see cref="HandleUtility.PickRectObjects(Rect)"/>. Shift adds to the current
    /// selection, Ctrl subtracts. RMB / Esc cancels. See DESIGN.md §7.
    /// </summary>
    [InitializeOnLoad]
    public sealed class BoxSelectModal : ModalOperator
    {
        static BoxSelectModal()
        {
            ActionRegistry.Register(Actions.BoxSelect, (sv, e) =>
            {
                if (!BlenderModePrefs.ModuleSelection) return false;
                BlenderModeManager.StartOperator(new BoxSelectModal(), sv);
                return true;
            });
        }

        Vector2 _mouse;
        Vector2 _dragStart;
        bool _dragging;
        bool _prevWantsMouseMove;

        public override void OnStart(SceneView sceneView)
        {
            base.OnStart(sceneView);
            _mouse = Event.current != null ? Event.current.mousePosition : Vector2.zero;
            _prevWantsMouseMove = sceneView.wantsMouseMove;
            sceneView.wantsMouseMove = true;
        }

        public override void HandleEvent(Event e)
        {
            switch (e.type)
            {
                case EventType.MouseMove:
                    _mouse = e.mousePosition;
                    e.Use();
                    return;

                case EventType.MouseDown:
                    if (e.button == 1) { Cancel(); e.Use(); return; }
                    if (e.button == 0)
                    {
                        _dragging = true;
                        _dragStart = e.mousePosition;
                        _mouse = _dragStart;
                        e.Use();
                    }
                    return;

                case EventType.MouseDrag:
                    _mouse = e.mousePosition;
                    e.Use();
                    return;

                case EventType.MouseUp:
                    if (e.button == 0 && _dragging)
                    {
                        Commit(e);
                        Finish();
                        e.Use();
                    }
                    return;

                case EventType.KeyDown:
                    if (e.keyCode == KeyCode.Escape) { Cancel(); e.Use(); return; }
                    e.Use();
                    return;
            }
        }

        Rect CurrentRect()
        {
            float x = Mathf.Min(_dragStart.x, _mouse.x);
            float y = Mathf.Min(_dragStart.y, _mouse.y);
            return new Rect(x, y, Mathf.Abs(_mouse.x - _dragStart.x), Mathf.Abs(_mouse.y - _dragStart.y));
        }

        void Commit(Event e)
        {
            Rect r = CurrentRect();
            if (r.width < 2f || r.height < 2f) return;

            GameObject[] picked = HandleUtility.PickRectObjects(r);
            bool add = e.shift;
            bool sub = e.control || e.command;

            var set = new HashSet<Object>();
            if (add || sub)
                foreach (Object o in Selection.objects) set.Add(o);

            if (sub)
                for (int i = 0; i < picked.Length; i++) set.Remove(picked[i]);
            else
                for (int i = 0; i < picked.Length; i++) set.Add(picked[i]);

            var result = new List<Object>(set);
            Selection.objects = result.ToArray();
        }

        public override void Confirm() => Finish();
        public override void Cancel() => Finish();

        void Finish()
        {
            if (SceneView != null) SceneView.wantsMouseMove = _prevWantsMouseMove;
            End();
        }

        public override void DrawHandles(SceneView sceneView)
        {
            if (Event.current.type != EventType.Repaint) return;

            Handles.BeginGUI();
            if (_dragging)
            {
                Rect r = CurrentRect();
                FillRect(r, new Color(0.35f, 0.55f, 1f, 0.15f));
                Color edge = new Color(0.45f, 0.65f, 1f, 0.9f);
                FillRect(new Rect(r.x, r.y, r.width, 1f), edge);
                FillRect(new Rect(r.x, r.yMax - 1f, r.width, 1f), edge);
                FillRect(new Rect(r.x, r.y, 1f, r.height), edge);
                FillRect(new Rect(r.xMax - 1f, r.y, 1f, r.height), edge);
            }
            else
            {
                float w = sceneView.position.width;
                float h = sceneView.position.height;
                Color c = new Color(1f, 1f, 1f, 0.55f);
                FillRect(new Rect(0f, _mouse.y, w, 1f), c);
                FillRect(new Rect(_mouse.x, 0f, 1f, h), c);
            }
            Handles.EndGUI();
        }

        static void FillRect(Rect r, Color c)
        {
            Color prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, EditorGUIUtility.whiteTexture);
            GUI.color = prev;
        }

        public override string HudText() =>
            "Box Select  —  drag a rectangle  (Shift add, Ctrl remove, RMB/Esc cancel)";
    }
}

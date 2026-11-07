using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace RAA.BlenderMode
{
    /// <summary>
    /// Circle select (C): a resizable circle follows the cursor (mouse wheel changes the radius). LMB-drag
    /// paints objects into the selection, MMB-drag removes them; RMB / Esc exits. Membership is approximate:
    /// an object is in the circle when its screen-space bounds center is inside. See DESIGN.md §7.
    /// </summary>
    [InitializeOnLoad]
    public sealed class CircleSelectModal : ModalOperator
    {
        static CircleSelectModal()
        {
            ActionRegistry.Register(Actions.CircleSelect, (sv, e) =>
            {
                if (!BlenderModePrefs.ModuleSelection) return false;
                BlenderModeManager.StartOperator(new CircleSelectModal(), sv);
                return true;
            });
        }

        Vector2 _mouse;
        float _radius = 60f;
        bool _paint;
        bool _erase;
        bool _prevWantsMouseMove;
        List<GameObject> _objects;

        public override void OnStart(SceneView sceneView)
        {
            base.OnStart(sceneView);
            _mouse = Event.current != null ? Event.current.mousePosition : Vector2.zero;
            _prevWantsMouseMove = sceneView.wantsMouseMove;
            sceneView.wantsMouseMove = true;
            _objects = SelectTools.EnumerateSelectable(true);
        }

        public override void HandleEvent(Event e)
        {
            switch (e.type)
            {
                case EventType.ScrollWheel:
                    _radius = Mathf.Clamp(_radius - e.delta.y * 2f, 8f, 600f);
                    e.Use();
                    return;

                case EventType.MouseMove:
                    _mouse = e.mousePosition;
                    e.Use();
                    return;

                case EventType.MouseDown:
                    if (e.button == 1) { Cancel(); e.Use(); return; }
                    if (e.button == 0) { _paint = true; Apply(true); e.Use(); return; }
                    if (e.button == 2) { _erase = true; Apply(false); e.Use(); return; }
                    return;

                case EventType.MouseDrag:
                    _mouse = e.mousePosition;
                    if (_paint) Apply(true);
                    else if (_erase) Apply(false);
                    e.Use();
                    return;

                case EventType.MouseUp:
                    if (e.button == 0) _paint = false;
                    if (e.button == 2) _erase = false;
                    e.Use();
                    return;

                case EventType.KeyDown:
                    if (e.keyCode == KeyCode.Escape) { Cancel(); e.Use(); return; }
                    if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) { Confirm(); e.Use(); return; }
                    e.Use();
                    return;
            }
        }

        void Apply(bool select)
        {
            var set = new HashSet<Object>(Selection.objects);
            float r2 = _radius * _radius;
            for (int i = 0; i < _objects.Count; i++)
            {
                GameObject go = _objects[i];
                if (go == null) continue;
                Vector2 g = HandleUtility.WorldToGUIPoint(ScreenCenter(go));
                if ((g - _mouse).sqrMagnitude <= r2)
                {
                    if (select) set.Add(go);
                    else set.Remove(go);
                }
            }
            var result = new List<Object>(set);
            Selection.objects = result.ToArray();
        }

        static Vector3 ScreenCenter(GameObject go)
        {
            Renderer r = go.GetComponent<Renderer>();
            return r != null ? r.bounds.center : go.transform.position;
        }

        // Circle select applies live; both exit gestures simply keep what was painted.
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
            // 2D screen-space circle drawn as dots (Handles.DrawLine would be world-space here).
            Color prev = GUI.color;
            GUI.color = new Color(0.9f, 0.9f, 0.95f, 0.9f);
            const int segs = 48;
            for (int i = 0; i < segs; i++)
            {
                float a = i / (float)segs * Mathf.PI * 2f;
                float x = _mouse.x + Mathf.Cos(a) * _radius;
                float y = _mouse.y + Mathf.Sin(a) * _radius;
                GUI.DrawTexture(new Rect(x - 1f, y - 1f, 2f, 2f), EditorGUIUtility.whiteTexture);
            }
            GUI.color = prev;
            Handles.EndGUI();
        }

        public override string HudText() =>
            "Circle Select  —  LMB paint, MMB erase, wheel resize  (RMB/Esc to exit)";
    }
}

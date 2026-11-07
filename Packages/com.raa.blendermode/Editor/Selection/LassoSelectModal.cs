using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace RAA.BlenderMode
{
    /// <summary>
    /// Lasso select (Ctrl+RMB drag): the dragged path forms a polygon; objects whose screen-space pivot is
    /// inside the polygon are selected (Shift adds to the current selection). Releasing RMB commits; Esc
    /// cancels. See DESIGN.md §7.
    /// </summary>
    [InitializeOnLoad]
    public sealed class LassoSelectModal : ModalOperator
    {
        static LassoSelectModal()
        {
            ActionRegistry.Register(Actions.LassoSelect, (sv, e) =>
            {
                if (!BlenderModePrefs.ModuleSelection) return false;
                var op = new LassoSelectModal();
                op._additive = e.shift;
                BlenderModeManager.StartOperator(op, sv);
                op._points.Add(e.mousePosition);
                return true;
            });
        }

        readonly List<Vector2> _points = new List<Vector2>();
        bool _additive;
        bool _prevWantsMouseMove;

        public override void OnStart(SceneView sceneView)
        {
            base.OnStart(sceneView);
            _prevWantsMouseMove = sceneView.wantsMouseMove;
            sceneView.wantsMouseMove = true;
        }

        public override void HandleEvent(Event e)
        {
            switch (e.type)
            {
                case EventType.MouseDrag:
                    _points.Add(e.mousePosition);
                    e.Use();
                    return;

                case EventType.MouseUp:
                    if (e.button == 1) { Confirm(); e.Use(); return; }
                    return;

                case EventType.MouseDown:
                    e.Use();
                    return;

                case EventType.KeyDown:
                    if (e.keyCode == KeyCode.Escape) { Cancel(); e.Use(); return; }
                    e.Use();
                    return;
            }
        }

        public override void Confirm()
        {
            if (_points.Count >= 3)
            {
                Vector2[] poly = _points.ToArray();
                List<GameObject> all = SelectTools.EnumerateSelectable(true);
                var set = new HashSet<Object>();
                if (_additive)
                    foreach (Object o in Selection.objects) set.Add(o);

                for (int i = 0; i < all.Count; i++)
                {
                    GameObject go = all[i];
                    if (go == null) continue;
                    Vector2 g = HandleUtility.WorldToGUIPoint(go.transform.position);
                    if (PointInPolygon(g, poly)) set.Add(go);
                }

                var result = new List<Object>(set);
                Selection.objects = result.ToArray();
            }
            Finish();
        }

        public override void Cancel() => Finish();

        void Finish()
        {
            if (SceneView != null) SceneView.wantsMouseMove = _prevWantsMouseMove;
            End();
        }

        static bool PointInPolygon(Vector2 p, Vector2[] poly)
        {
            bool inside = false;
            int n = poly.Length;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                if (((poly[i].y > p.y) != (poly[j].y > p.y)) &&
                    (p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x))
                    inside = !inside;
            }
            return inside;
        }

        public override void DrawHandles(SceneView sceneView)
        {
            if (Event.current.type != EventType.Repaint || _points.Count < 2) return;

            Handles.BeginGUI();
            Color prev = GUI.color;
            GUI.color = new Color(0.45f, 0.65f, 1f, 0.9f);
            for (int i = 1; i < _points.Count; i++)
                DrawSegment(_points[i - 1], _points[i]);
            // Close the loop back to the start for a lasso feel.
            DrawSegment(_points[_points.Count - 1], _points[0]);
            GUI.color = prev;
            Handles.EndGUI();
        }

        static void DrawSegment(Vector2 a, Vector2 b)
        {
            // Approximate a 2D line with a series of dots (GUI has no primitive line).
            float dist = Vector2.Distance(a, b);
            int steps = Mathf.Max(1, Mathf.CeilToInt(dist / 3f));
            for (int i = 0; i <= steps; i++)
            {
                Vector2 p = Vector2.Lerp(a, b, i / (float)steps);
                GUI.DrawTexture(new Rect(p.x - 1f, p.y - 1f, 2f, 2f), EditorGUIUtility.whiteTexture);
            }
        }

        public override string HudText() =>
            "Lasso Select  —  release RMB to commit  (Esc to cancel)";
    }
}

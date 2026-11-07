using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BlastFrame.EditorTools.LevelDesign
{
    /// <summary>
    /// Scene-view jump planner. Ctrl+Shift+Click a surface to put a takeoff marker there (facing the camera's
    /// view direction), or drag its handles. Draws the REAL motor's measured arcs (from the cached reach profile)
    /// out over your geometry, marks where each arc lands, and labels the lip-to-lip gap budget.
    /// </summary>
    public sealed class JumpReachWindow : EditorWindow
    {
        private static readonly Color[] KindColors =
        {
            new Color(0.35f, 0.9f, 0.45f),  // StandingJump
            new Color(0.3f, 0.75f, 1f),     // RunJump
            new Color(1f, 0.55f, 0.15f),    // DashJump
        };

        private ReachProfile _profile;
        private bool _stale;
        private bool _active = true;
        private bool[] _showKind = { true, true, true };
        private bool _showRings = true;
        private Vector3 _takeoff;
        private float _yaw;
        private bool _hasTakeoff;
        private int _mask = ~0;
        private readonly RaycastHit[] _hits = new RaycastHit[16];

        [MenuItem("Tools/Blast Frame/Level Design/Jump Reach Gizmo")]
        private static void Open() => GetWindow<JumpReachWindow>("Jump Reach");

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            Reload();
        }

        private void OnDisable() => SceneView.duringSceneGui -= OnSceneGUI;

        private void Reload()
        {
            _profile = ReachMeasureMenu.LoadFresh(out _stale);
            _mask = _profile != null ? _profile.collisionMask : ~0;
        }

        private void OnGUI()
        {
            EditorGUILayout.Space();
            if (_profile == null)
            {
                EditorGUILayout.HelpBox("No reach profile yet. Measure once (takes a minute or two); it is cached until " +
                                        "the Player's movement tuning in Core.unity changes.", MessageType.Info);
            }
            else
            {
                EditorGUILayout.LabelField("Profile", $"{_profile.measuredAt}  ({_profile.templateSource})");
                if (_stale)
                    EditorGUILayout.HelpBox("Player tuning changed since this profile was measured — re-measure.", MessageType.Warning);
            }
            if (GUILayout.Button(_profile == null ? "Measure Player Reach" : "Re-measure"))
            {
                ReachMeasureMenu.MeasureAndCache(interactive: true);
                Reload();
            }

            EditorGUILayout.Space();
            _active = EditorGUILayout.ToggleLeft("Draw in Scene view", _active);
            for (int i = 0; i < _showKind.Length; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    var prev = GUI.color;
                    GUI.color = KindColors[i];
                    GUILayout.Label("■", GUILayout.Width(14));
                    GUI.color = prev;
                    _showKind[i] = EditorGUILayout.ToggleLeft(((MoveKind)i).ToString(), _showKind[i]);
                }
            }
            _showRings = EditorGUILayout.ToggleLeft("Flat-ground gap rings (lip-to-lip)", _showRings);

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("Ctrl+Shift+Click a surface in the Scene view to place the takeoff point " +
                                    "(faces where the camera looks). Drag the arrow to aim.", MessageType.None);
            using (new EditorGUI.DisabledScope(Selection.activeTransform == null))
            {
                if (GUILayout.Button("Takeoff = selected object"))
                {
                    var t = Selection.activeTransform;
                    _takeoff = t.position;
                    _yaw = t.eulerAngles.y;
                    _hasTakeoff = true;
                    SceneView.RepaintAll();
                }
            }

            if (_profile != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Budgets (comfortable / perfect)", EditorStyles.boldLabel);
                foreach (var m in _profile.moves)
                    EditorGUILayout.LabelField(m.kind.ToString(),
                        $"flat {m.GapAt(0, true):0.0} / {m.GapAt(0, false):0.0} m   up 2 m {m.GapAt(2, true):0.0} / {m.GapAt(2, false):0.0} m");
                EditorGUILayout.LabelField("Tallest ledge", $"{_profile.maxLedgeHeight:0.00} m (jump + vault)");
                EditorGUILayout.LabelField("Auto step-up", $"{_profile.maxStepHeight:0.00} m");
            }
        }

        private void OnSceneGUI(SceneView view)
        {
            if (!_active) return;
            var e = Event.current;

            if (e.type == EventType.MouseDown && e.button == 0 && e.control && e.shift)
            {
                var ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
                Physics.SyncTransforms();
                if (Physics.Raycast(ray, out var hit, 5000f, _mask, QueryTriggerInteraction.Ignore))
                {
                    _takeoff = hit.point;
                    var f = view.camera.transform.forward;
                    _yaw = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
                    _hasTakeoff = true;
                    e.Use();
                    Repaint();
                }
            }

            if (!_hasTakeoff || _profile == null) return;

            EditorGUI.BeginChangeCheck();
            var rot = Quaternion.Euler(0f, _yaw, 0f);
            var newPos = Handles.PositionHandle(_takeoff, rot);
            float size = HandleUtility.GetHandleSize(_takeoff);
            var newRot = Handles.Disc(rot, _takeoff, Vector3.up, size * 1.2f, false, 5f);
            if (EditorGUI.EndChangeCheck())
            {
                _takeoff = newPos;
                _yaw = newRot.eulerAngles.y;
                Repaint();
            }

            Vector3 fwd = rot * Vector3.forward;
            Handles.color = Color.white;
            Handles.ArrowHandleCap(0, _takeoff, Quaternion.LookRotation(fwd), size, EventType.Repaint);

            foreach (var m in _profile.moves)
            {
                int k = (int)m.kind;
                if (k >= _showKind.Length || !_showKind[k]) continue;
                DrawArc(m, fwd, KindColors[k]);
                if (_showRings) DrawRing(m, KindColors[k]);
            }
        }

        private void DrawArc(MoveReach m, Vector3 fwd, Color color)
        {
            if (m.arc.Count < 2) return;
            var pts = new List<Vector3>(m.arc.Count);
            Vector3 prev = _takeoff;
            pts.Add(prev);
            bool landed = false;
            RaycastHit landHit = default;
            for (int i = 1; i < m.arc.Count; i++)
            {
                Vector3 p = _takeoff + fwd * m.arc[i].x + Vector3.up * m.arc[i].y;
                if (i > 2 && Physics.Linecast(prev, p, out landHit, _mask, QueryTriggerInteraction.Ignore))
                {
                    pts.Add(landHit.point);
                    landed = true;
                    break;
                }
                pts.Add(p);
                prev = p;
            }

            Handles.color = color;
            Handles.DrawAAPolyLine(4f, pts.ToArray());

            if (!landed) return;
            bool walkable = Vector3.Angle(landHit.normal, Vector3.up) <= 50f;
            Handles.color = walkable ? color : Color.red;
            Handles.DrawWireDisc(landHit.point, landHit.normal, _profile.capsuleRadius);
            float horiz = Vector3.ProjectOnPlane(landHit.point - _takeoff, Vector3.up).magnitude;
            float dh = landHit.point.y - _takeoff.y;
            Handles.Label(landHit.point + Vector3.up * 0.4f,
                $"{m.kind}\n{horiz:0.0} m  Δh {dh:+0.0;-0.0}{(walkable ? "" : "\nWALL HIT")}", EditorStyles.whiteMiniLabel);
        }

        private void DrawRing(MoveReach m, Color color)
        {
            float r = m.GapAt(0f, true);
            if (r <= 0f) return;
            Handles.color = new Color(color.r, color.g, color.b, 0.5f);
            Handles.DrawWireDisc(_takeoff, Vector3.up, r);
        }
    }
}

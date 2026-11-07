using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace BlastFrame.EditorTools.LevelDesign
{
    /// <summary>
    /// "Can the player actually get there?" for the open scene. Colours every standable surface by the hardest
    /// move needed to reach it from the start (Entry marker, selection, or the largest island), lists islands
    /// that can't be reached at all, and checks every RoomMarker (Exit / Secret / Checkpoint).
    /// </summary>
    public sealed class ReachabilityWindow : EditorWindow
    {
        public static readonly Color[] ClassColors =
        {
            new Color(0.25f, 0.85f, 0.35f, 0.55f), // Walk
            new Color(0.55f, 0.95f, 0.35f, 0.55f), // Jump
            new Color(1f, 0.9f, 0.2f, 0.6f),       // PerfectJump
            new Color(0.25f, 0.7f, 1f, 0.6f),      // DashJump
            new Color(0.75f, 0.4f, 1f, 0.65f),     // PerfectDashJump
            new Color(1f, 0.15f, 0.15f, 0.7f),     // Unreachable
        };
        private static readonly Color HazardColor = new Color(1f, 0.45f, 0f, 0.7f);

        private ReachProfile _profile;
        private bool _stale;
        private ReachResult _result;
        private float _cellSize = 0.5f;
        private bool _useSelection;
        private bool _limitToSelection;
        private bool _draw = true;
        private Mesh _mesh;
        private Material _mat;
        private Vector2 _scroll;

        [MenuItem("Tools/Blast Frame/Level Design/Reachability Lint (open scene)")]
        private static void Open() => GetWindow<ReachabilityWindow>("Reachability");

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            _profile = ReachMeasureMenu.LoadFresh(out _stale);
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            if (_mesh != null) DestroyImmediate(_mesh);
            if (_mat != null) DestroyImmediate(_mat);
        }

        private void OnGUI()
        {
            if (_profile == null)
            {
                EditorGUILayout.HelpBox("Needs a reach profile first.", MessageType.Info);
                if (GUILayout.Button("Measure Player Reach")) { ReachMeasureMenu.MeasureAndCache(true); _profile = ReachMeasureMenu.LoadFresh(out _stale); }
                return;
            }
            if (_stale) EditorGUILayout.HelpBox("Player tuning changed since the profile was measured — re-measure for accurate results.", MessageType.Warning);

            _cellSize = EditorGUILayout.Slider(new GUIContent("Cell size (m)", "Grid spacing for surface sampling. 0.5 is a good default; 1.0 for huge scenes."), _cellSize, 0.25f, 2f);
            _useSelection = EditorGUILayout.ToggleLeft("Start from selected object (else Entry marker, else largest island)", _useSelection);
            _limitToSelection = EditorGUILayout.ToggleLeft(new GUIContent("Only analyse inside the selected object's bounds", "Select a parent or a big trigger box covering the playable space to skip skyboxes, backdrops and distant scenery (much faster)."), _limitToSelection);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Analyse open scene", GUILayout.Height(28))) Run();
                if (GUILayout.Button("Clear", GUILayout.Width(60), GUILayout.Height(28))) { _result = null; RebuildMesh(); SceneView.RepaintAll(); }
            }
            bool d = EditorGUILayout.ToggleLeft("Draw overlay", _draw);
            if (d != _draw) { _draw = d; SceneView.RepaintAll(); }

            DrawLegend();
            if (_result == null) return;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"Start: {_result.StartDescription}", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField($"{_result.NodePos.Count} surface samples · {_result.Islands.Count} islands · " +
                                       $"{_result.HazardNodes} hazard · {_result.Seconds:0.0}s [{_result.PhaseTimes}]", EditorStyles.miniLabel);
            if (_result.DynamicColliders > 0)
                EditorGUILayout.HelpBox($"{_result.DynamicColliders} moving/riding-platform colliders analysed at their CURRENT pose only.", MessageType.None);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            if (_result.Markers.Count > 0)
            {
                EditorGUILayout.LabelField("Markers", EditorStyles.boldLabel);
                foreach (var (marker, island, cls) in _result.Markers)
                {
                    if (marker == null) continue;
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        Swatch(cls);
                        EditorGUILayout.LabelField($"{marker.Kind}: {marker.Label}", island < 0 ? "no surface nearby" : cls.ToString());
                        if (GUILayout.Button("Select", GUILayout.Width(52))) { Selection.activeObject = marker; SceneView.lastActiveSceneView?.FrameSelected(); }
                    }
                }
            }

            var bad = _result.Islands.Where(i => i.Class == ReachClass.Unreachable).OrderByDescending(i => i.Nodes.Count).ToList();
            EditorGUILayout.LabelField($"Unreachable islands ({bad.Count})", EditorStyles.boldLabel);
            foreach (var isl in bad.Take(60))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    Swatch(ReachClass.Unreachable);
                    float area = isl.Nodes.Count * _result.CellSize * _result.CellSize;
                    EditorGUILayout.LabelField($"#{isl.Id}  ~{area:0.#} m²  at {isl.Bounds.center:F0}{(isl.Dynamic ? "  (moving)" : "")}");
                    if (GUILayout.Button("Frame", GUILayout.Width(52))) FrameBounds(isl.Bounds);
                }
            }
            var counts = new int[ClassColors.Length];
            foreach (var isl in _result.Islands) counts[(int)isl.Class] += isl.Nodes.Count;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Surface by hardest move needed", EditorStyles.boldLabel);
            for (int c = 0; c < counts.Length; c++)
                using (new EditorGUILayout.HorizontalScope())
                {
                    Swatch((ReachClass)c);
                    EditorGUILayout.LabelField(((ReachClass)c).ToString(), $"{counts[c] * _result.CellSize * _result.CellSize:0} m²");
                }
            EditorGUILayout.EndScrollView();
        }

        private static void Swatch(ReachClass c)
        {
            var r = GUILayoutUtility.GetRect(12, 12, GUILayout.Width(12));
            r.y += 3;
            EditorGUI.DrawRect(r, ClassColors[(int)c]);
        }

        private static void DrawLegend()
        {
            EditorGUILayout.LabelField("Walk · Jump (comfy timing) · Perfect jump · Dash jump · Perfect dash · Unreachable · orange = hazard",
                EditorStyles.wordWrappedMiniLabel);
        }

        private void Run()
        {
            Vector3? hint = null;
            string label = "";
            if (_useSelection && Selection.activeTransform != null)
            {
                hint = Selection.activeTransform.position;
                label = "selection '" + Selection.activeTransform.name + "'";
            }
            EditorUtility.DisplayProgressBar("Reachability", "Sampling surfaces…", 0.3f);
            Bounds? limit = null;
            if (_limitToSelection && Selection.activeGameObject != null)
            {
                bool has = false;
                var b = new Bounds(Selection.activeGameObject.transform.position, Vector3.zero);
                foreach (var c in Selection.activeGameObject.GetComponentsInChildren<Collider>()) { if (!has) { b = c.bounds; has = true; } else b.Encapsulate(c.bounds); }
                foreach (var rr in Selection.activeGameObject.GetComponentsInChildren<Renderer>()) { if (!has) { b = rr.bounds; has = true; } else b.Encapsulate(rr.bounds); }
                if (has) { b.Expand(new Vector3(0f, 4f, 0f)); limit = b; }
            }
            try { _result = ReachabilityAnalyzer.Analyze(_profile, _cellSize, hint, label, null, limit); }
            finally { EditorUtility.ClearProgressBar(); }
            RebuildMesh();
            SceneView.RepaintAll();
        }

        private static void FrameBounds(Bounds b)
        {
            b.Expand(4f);
            SceneView.lastActiveSceneView?.Frame(b, false);
        }

        private void RebuildMesh()
        {
            if (_mesh != null) DestroyImmediate(_mesh);
            _mesh = null;
            if (_result == null || _result.NodePos.Count == 0) return;

            int n = _result.NodePos.Count;
            var verts = new List<Vector3>(n * 4);
            var cols = new List<Color>(n * 4);
            var tris = new List<int>(n * 6);
            float h = _result.CellSize * 0.45f;
            for (int i = 0; i < n; i++)
            {
                var p = _result.NodePos[i] + Vector3.up * 0.03f;
                Color c = _result.NodeHazard[i] ? HazardColor
                    : _result.NodeIsland[i] >= 0 ? ClassColors[(int)_result.Islands[_result.NodeIsland[i]].Class]
                    : ClassColors[(int)ReachClass.Unreachable];
                int v = verts.Count;
                verts.Add(p + new Vector3(-h, 0, -h)); verts.Add(p + new Vector3(-h, 0, h));
                verts.Add(p + new Vector3(h, 0, h)); verts.Add(p + new Vector3(h, 0, -h));
                cols.Add(c); cols.Add(c); cols.Add(c); cols.Add(c);
                tris.Add(v); tris.Add(v + 1); tris.Add(v + 2); tris.Add(v); tris.Add(v + 2); tris.Add(v + 3);
            }
            _mesh = new Mesh { indexFormat = IndexFormat.UInt32, hideFlags = HideFlags.HideAndDontSave };
            _mesh.SetVertices(verts);
            _mesh.SetColors(cols);
            _mesh.SetTriangles(tris, 0);
        }

        private void OnSceneGUI(SceneView view)
        {
            if (!_draw || _mesh == null || Event.current.type != EventType.Repaint) return;
            if (_mat == null)
            {
                _mat = new Material(Shader.Find("Hidden/Internal-Colored")) { hideFlags = HideFlags.HideAndDontSave };
                _mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                _mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                _mat.SetInt("_Cull", (int)CullMode.Off);
                _mat.SetInt("_ZWrite", 0);
            }
            _mat.SetPass(0);
            Graphics.DrawMeshNow(_mesh, Matrix4x4.identity);
        }
    }
}

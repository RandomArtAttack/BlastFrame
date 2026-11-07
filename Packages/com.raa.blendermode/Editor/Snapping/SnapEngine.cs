using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace RAA.BlenderMode
{
    /// <summary>
    /// Per-drag cache for vertex snapping: the candidate mesh filters and their (shared) vertex arrays,
    /// captured once when a drag begins so hot-path events don't re-read meshes. See DESIGN.md §5.
    /// </summary>
    public sealed class DragSnapCache
    {
        public MeshFilter[] Filters;
        public readonly Dictionary<Mesh, Vector3[]> Verts = new Dictionary<Mesh, Vector3[]>();
        public readonly Dictionary<Mesh, Vector3[]> Normals = new Dictionary<Mesh, Vector3[]>();

        public Vector3[] GetVertices(Mesh mesh)
        {
            if (mesh == null) return null;
            if (!Verts.TryGetValue(mesh, out Vector3[] v))
            {
                v = mesh.isReadable ? mesh.vertices : null;
                Verts[mesh] = v;
            }
            return v;
        }

        /// <summary>Cached mesh normals (shared array) for vertex-normal align-rotation. Null when unreadable/absent.</summary>
        public Vector3[] GetNormals(Mesh mesh)
        {
            if (mesh == null) return null;
            if (!Normals.TryGetValue(mesh, out Vector3[] n))
            {
                n = mesh.isReadable ? mesh.normals : null;
                if (n != null && n.Length == 0) n = null;
                Normals[mesh] = n;
            }
            return n;
        }
    }

    /// <summary>
    /// Snapping queries: increment/grid rounding, surface placement (<see cref="HandleUtility.PlaceObject"/>
    /// with a <see cref="HandleUtility.RaySnap(Ray)"/> fallback, excluding the transformed objects via
    /// <see cref="HandleUtility.ignoreRaySnapObjects"/>), and nearest-vertex snapping within a screen radius.
    /// See DESIGN.md §5.
    /// </summary>
    public static class SnapEngine
    {
        const float VertexScreenRadius = 50f;

        // HandleUtility.ignoreRaySnapObjects is internal; access it reflectively so PlaceObject/RaySnap can
        // exclude the objects being transformed. Falls back to no-op if the field is unavailable.
        static readonly FieldInfo IgnoreRaySnapField = typeof(HandleUtility)
            .GetField("ignoreRaySnapObjects", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);

        static Transform[] GetIgnore()
        {
            return IgnoreRaySnapField != null ? IgnoreRaySnapField.GetValue(null) as Transform[] : null;
        }

        static void SetIgnore(Transform[] value)
        {
            if (IgnoreRaySnapField != null) IgnoreRaySnapField.SetValue(null, value);
        }

        /// <summary>Round a scalar to the nearest multiple of <paramref name="increment"/>.</summary>
        public static float Snap(float value, float increment)
        {
            if (increment <= 0f) return value;
            return Mathf.Round(value / increment) * increment;
        }

        /// <summary>Round each component of a vector to <paramref name="increment"/>.</summary>
        public static Vector3 SnapVector(Vector3 v, float increment)
        {
            if (increment <= 0f) return v;
            return new Vector3(Snap(v.x, increment), Snap(v.y, increment), Snap(v.z, increment));
        }

        /// <summary>
        /// Zoom-derived move-snap step approximating Blender's adaptive increment: a power of ten that tracks
        /// the visible scene-grid subdivision as you zoom. <paramref name="viewSize"/> is
        /// <see cref="SceneView.size"/> (world half-extent-ish); the constant K is tuned so the step follows
        /// the grid roughly one order below the view, clamped to a sensible range.
        /// </summary>
        public static float AdaptiveStep(float viewSize)
        {
            const float K = 0.1f; // tuned so the step matches the visible Unity grid subdivision
            float s = Mathf.Max(1e-4f, viewSize * K);
            float step = Mathf.Pow(10f, Mathf.Floor(Mathf.Log10(s)));
            return Mathf.Clamp(step, 0.001f, 1000f);
        }

        // ---------------------------------------------------------------- surface

        /// <summary>
        /// Place a point on the surface under <paramref name="guiPoint"/>, excluding <paramref name="ignore"/>.
        /// Uses <see cref="HandleUtility.PlaceObject"/>; falls back to <see cref="HandleUtility.RaySnap(Ray)"/>.
        /// </summary>
        public static bool TrySurface(SceneView sceneView, Vector2 guiPoint, Transform[] ignore,
            out Vector3 position, out Vector3 normal)
        {
            position = Vector3.zero;
            normal = Vector3.up;

            Transform[] prevIgnore = GetIgnore();
            SetIgnore(ignore);
            try
            {
                if (HandleUtility.PlaceObject(guiPoint, out position, out normal))
                {
                    if (normal.sqrMagnitude < 1e-6f) normal = Vector3.up;
                    return true;
                }

                Ray ray = HandleUtility.GUIPointToWorldRay(guiPoint);
                object hit = HandleUtility.RaySnap(ray);
                if (hit is RaycastHit rh)
                {
                    position = rh.point;
                    normal = rh.normal.sqrMagnitude < 1e-6f ? Vector3.up : rh.normal;
                    return true;
                }
            }
            finally
            {
                SetIgnore(prevIgnore);
            }
            return false;
        }

        // ---------------------------------------------------------------- vertex

        /// <summary>
        /// Find the nearest shared-mesh vertex to <paramref name="guiPoint"/> within a 50px screen radius,
        /// excluding <paramref name="ignore"/>. Vertex arrays are cached in <paramref name="cache"/> per drag.
        /// Also returns the world-space vertex normal (for align-rotation); defaults to up when unavailable.
        /// </summary>
        public static bool TryVertex(SceneView sceneView, Vector2 guiPoint, Transform[] ignore,
            DragSnapCache cache, out Vector3 worldVertex, out Vector3 worldNormal)
        {
            worldVertex = Vector3.zero;
            worldNormal = Vector3.up;
            if (cache == null) return false;

            if (cache.Filters == null)
                cache.Filters = Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None);

            float best = VertexScreenRadius;
            bool found = false;

            for (int f = 0; f < cache.Filters.Length; f++)
            {
                MeshFilter mf = cache.Filters[f];
                if (mf == null) continue;
                if (ignore != null && System.Array.IndexOf(ignore, mf.transform) >= 0) continue;

                Mesh mesh = mf.sharedMesh;
                if (mesh == null) continue;

                // Cheap screen-space reject using the bounds center.
                Vector3 centerWorld = mf.transform.TransformPoint(mesh.bounds.center);
                Vector2 centerGui = HandleUtility.WorldToGUIPoint(centerWorld);
                if (Vector2.Distance(centerGui, guiPoint) > 400f) continue;

                Vector3[] verts = cache.GetVertices(mesh);
                if (verts == null) continue;
                Vector3[] normals = cache.GetNormals(mesh);

                Matrix4x4 l2w = mf.transform.localToWorldMatrix;
                for (int i = 0; i < verts.Length; i++)
                {
                    Vector3 w = l2w.MultiplyPoint3x4(verts[i]);
                    Vector2 g = HandleUtility.WorldToGUIPoint(w);
                    float d = Vector2.Distance(g, guiPoint);
                    if (d < best)
                    {
                        best = d;
                        worldVertex = w;
                        found = true;
                        worldNormal = (normals != null && i < normals.Length)
                            ? l2w.MultiplyVector(normals[i]).normalized
                            : Vector3.up;
                    }
                }
            }
            if (worldNormal.sqrMagnitude < 1e-6f) worldNormal = Vector3.up;
            return found;
        }

        // ---------------------------------------------------------------- snap base ("Snap With")

        /// <summary>
        /// Build the candidate points used by <see cref="SnapBase.Closest"/> for each target, in world space
        /// at drag start: each object's <see cref="Renderer.bounds"/> corners (8) + center, or its transform
        /// position when it has no renderer. Cache this once per drag (the whole selection translates rigidly
        /// during a geometry snap, so a candidate's current position is simply start + delta). See DESIGN.md §5.
        /// </summary>
        public static Vector3[] BuildClosestCandidates(Transform[] targets)
        {
            var list = new List<Vector3>(targets != null ? targets.Length * 9 : 0);
            if (targets == null) return list.ToArray();
            for (int i = 0; i < targets.Length; i++)
            {
                Transform t = targets[i];
                if (t == null) continue;
                Renderer r = t.GetComponentInChildren<Renderer>();
                if (r != null)
                {
                    Bounds bnd = r.bounds;
                    Vector3 c = bnd.center, e = bnd.extents;
                    for (int sx = -1; sx <= 1; sx += 2)
                        for (int sy = -1; sy <= 1; sy += 2)
                            for (int sz = -1; sz <= 1; sz += 2)
                                list.Add(c + new Vector3(sx * e.x, sy * e.y, sz * e.z));
                    list.Add(c);
                }
                else
                {
                    list.Add(t.position);
                }
            }
            return list.ToArray();
        }

        /// <summary>
        /// Resolve the base point (in drag-start world space) that a geometry snap should land on the target.
        /// The move delta is then <c>target − basePoint</c>; every selected object moves by it, preserving
        /// relative offsets. For <see cref="SnapBase.Closest"/> the closest candidate is chosen once this call
        /// using its start position shifted by <paramref name="lastDelta"/> (the previous snapped delta), which
        /// is updated to the new delta so subsequent events track the moving selection. See DESIGN.md §5.
        /// </summary>
        public static Vector3 ResolveGeometryBasePoint(SnapBase baseMode, Vector3 target,
            Vector3 pivotStart, Vector3 medianStart, Vector3 activeStart,
            Vector3[] closestCandidates, ref Vector3 lastDelta)
        {
            switch (baseMode)
            {
                case SnapBase.Center: return pivotStart;
                case SnapBase.Median: return medianStart;
                case SnapBase.Active: return activeStart;
                default: // Closest
                    if (closestCandidates == null || closestCandidates.Length == 0)
                        return activeStart;
                    int best = -1;
                    float bestSqr = float.MaxValue;
                    for (int i = 0; i < closestCandidates.Length; i++)
                    {
                        float d = (closestCandidates[i] + lastDelta - target).sqrMagnitude;
                        if (d < bestSqr) { bestSqr = d; best = i; }
                    }
                    Vector3 basePt = closestCandidates[best];
                    lastDelta = target - basePt;
                    return basePt;
            }
        }
    }
}

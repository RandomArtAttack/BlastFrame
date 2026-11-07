using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using BlastFrame.Gameplay.Environment;
using BlastFrame.Gameplay.Platforms;
using BlastFrame.Gameplay.Player;
using BlastFrame.Gameplay.Rooms;

namespace BlastFrame.EditorTools.LevelDesign
{
    /// <summary>Hardest move needed to get somewhere, easiest first. Unreachable sorts last.</summary>
    public enum ReachClass { Walk, Jump, PerfectJump, DashJump, PerfectDashJump, Unreachable }

    public sealed class ReachIsland
    {
        public int Id;
        public readonly List<int> Nodes = new List<int>();
        public ReachClass Class = ReachClass.Unreachable;
        public bool Dynamic;           // sits on a moving / riding platform (analysed at its current pose)
        public Bounds Bounds;
        public int ReachedFrom = -1;   // island id we jumped from on the best path
    }

    public sealed class ReachResult
    {
        public float CellSize;
        public readonly List<Vector3> NodePos = new List<Vector3>();
        public readonly List<int> NodeIsland = new List<int>();
        public readonly List<bool> NodeHazard = new List<bool>();
        public readonly List<ReachIsland> Islands = new List<ReachIsland>();
        public int StartIsland = -1;
        public string StartDescription = "";
        public readonly List<(RoomMarker marker, int island, ReachClass cls)> Markers = new List<(RoomMarker, int, ReachClass)>();
        public int DynamicColliders;
        public int HazardNodes;
        public float Seconds;
        public string PhaseTimes = "";
    }

    /// <summary>
    /// Samples every standable surface in the loaded scenes on a grid, groups them into walk-connected islands,
    /// links islands with jumps the measured <see cref="ReachProfile"/> says are possible, and finds the easiest
    /// route from the start island to every other. Gaps use lip-to-lip distance estimated from cell centres
    /// (optimistic by up to one cell), so a "Jump" result near the limit still deserves a hand test.
    /// Edit-mode physics queries only — nothing moves.
    /// </summary>
    public static class ReachabilityAnalyzer
    {
        private const float MaxGroundAngle = 50f;
        private const float HeadroomMargin = 0.05f;
        /// <summary>Blocked flight checks tolerated per island pair before the pair counts as walled off.</summary>
        private const int MaxBlockedChecksPerPair = 4096;

        public static ReachResult Analyze(ReachProfile profile, float cellSize, Vector3? startHint, string startHintLabel,
            Func<GameObject, bool> include = null, Bounds? limit = null)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            Physics.SyncTransforms();
            int mask = profile.collisionMask;
            var r = new ReachResult { CellSize = cellSize };

            // ---- gather solid colliders + world bounds
            var dynamicColliders = new HashSet<Collider>();
            Bounds world = default;
            bool any = false;
            foreach (var col in AllColliders(include))
            {
                if (col.isTrigger || ((1 << col.gameObject.layer) & mask) == 0) continue;
                if (col.GetComponentInParent<PlayerController>() != null) continue;
                if (col.GetComponentInParent<IRidePlatform>() != null || col.GetComponentInParent<ILaunchPlatform>() != null)
                    dynamicColliders.Add(col);
                if (!any) { world = col.bounds; any = true; } else world.Encapsulate(col.bounds);
            }
            r.DynamicColliders = dynamicColliders.Count;
            if (!any) return r;
            if (limit.HasValue)
            {
                var l = limit.Value;
                world.SetMinMax(Vector3.Max(world.min, l.min), Vector3.Min(world.max, l.max));
                if (world.size.x <= 0f || world.size.z <= 0f || world.size.y < 0f) return r;
            }

            // ---- sample standable nodes (several per column for multi-level geometry)
            float radius = profile.capsuleRadius > 0f ? profile.capsuleRadius : 0.4f;
            const float height = 2f;
            var hits = new RaycastHit[32];
            var colliderHits = new Collider[16];
            float top = world.max.y + 1f, depth = world.size.y + 2f;
            int nx = Mathf.CeilToInt(world.size.x / cellSize) + 1;
            int nz = Mathf.CeilToInt(world.size.z / cellSize) + 1;
            var columns = new Dictionary<long, List<int>>(nx * nz / 2);
            var nodeDynamic = new List<bool>();

            for (int ix = 0; ix < nx; ix++)
            for (int iz = 0; iz < nz; iz++)
            {
                var origin = new Vector3(world.min.x + ix * cellSize, top, world.min.z + iz * cellSize);
                int n = Physics.RaycastNonAlloc(origin, Vector3.down, hits, depth, mask, QueryTriggerInteraction.Ignore);
                for (int h = 0; h < n; h++)
                {
                    var hit = hits[h];
                    if (Vector3.Angle(hit.normal, Vector3.up) > MaxGroundAngle) continue;
                    if (include != null && !include(hit.collider.gameObject)) continue;
                    if (hit.collider.GetComponentInParent<PlayerController>() != null) continue;
                    var p = hit.point;
                    if (limit.HasValue && (p.y < world.min.y - 0.01f || p.y > world.max.y + 0.01f)) continue;
                    // Headroom: the capsule must fit standing here.
                    var p1 = p + Vector3.up * (radius + HeadroomMargin + 0.02f);
                    var p2 = p + Vector3.up * (height - radius);
                    if (Physics.CheckCapsule(p1, p2, radius * 0.9f, mask, QueryTriggerInteraction.Ignore)) continue;

                    bool hazard = false;
                    int hc = Physics.OverlapSphereNonAlloc(p + Vector3.up * 0.3f, 0.25f, colliderHits, ~0, QueryTriggerInteraction.Collide);
                    for (int c = 0; c < hc && !hazard; c++)
                        if (colliderHits[c].GetComponentInParent<LavaHazardVolume>() != null) hazard = true;

                    int id = r.NodePos.Count;
                    r.NodePos.Add(p);
                    r.NodeIsland.Add(-1);
                    r.NodeHazard.Add(hazard);
                    nodeDynamic.Add(dynamicColliders.Contains(hit.collider));
                    if (hazard) r.HazardNodes++;
                    long key = Key(ix, iz);
                    if (!columns.TryGetValue(key, out var list)) columns[key] = list = new List<int>(2);
                    list.Add(id);
                }
            }

            r.PhaseTimes += $"sample {sw.Elapsed.TotalSeconds:0.0}s "; 
            // ---- walk-connected islands (flood fill, 8-neighbour, step height, no wall between)
            float step = Mathf.Max(0.05f, profile.maxStepHeight);
            var queue = new Queue<(int node, int ix, int iz)>();
            var nodeCell = new (int ix, int iz)[r.NodePos.Count];
            foreach (var kv in columns)
            {
                int ix = (int)(kv.Key >> 32), iz = (int)(kv.Key & 0xffffffff);
                foreach (int id in kv.Value) nodeCell[id] = (ix, iz);
            }

            for (int start = 0; start < r.NodePos.Count; start++)
            {
                if (r.NodeIsland[start] >= 0 || r.NodeHazard[start]) continue;
                var island = new ReachIsland { Id = r.Islands.Count, Bounds = new Bounds(r.NodePos[start], Vector3.zero) };
                r.Islands.Add(island);
                r.NodeIsland[start] = island.Id;
                queue.Enqueue((start, nodeCell[start].ix, nodeCell[start].iz));
                while (queue.Count > 0)
                {
                    var (node, cx, cz) = queue.Dequeue();
                    island.Nodes.Add(node);
                    island.Bounds.Encapsulate(r.NodePos[node]);
                    island.Dynamic |= nodeDynamic[node];
                    for (int dx = -1; dx <= 1; dx++)
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        if (dx == 0 && dz == 0) continue;
                        if (!columns.TryGetValue(Key(cx + dx, cz + dz), out var neigh)) continue;
                        foreach (int other in neigh)
                        {
                            if (r.NodeIsland[other] >= 0 || r.NodeHazard[other]) continue;
                            var a = r.NodePos[node];
                            var b = r.NodePos[other];
                            if (Mathf.Abs(a.y - b.y) > step) continue;
                            float probe = Mathf.Max(a.y, b.y) + step + 0.1f;
                            if (Physics.Linecast(new Vector3(a.x, probe, a.z), new Vector3(b.x, probe, b.z), mask,
                                    QueryTriggerInteraction.Ignore)) continue;
                            r.NodeIsland[other] = island.Id;
                            queue.Enqueue((other, cx + dx, cz + dz));
                        }
                    }
                }
            }

            r.PhaseTimes += $"islands {sw.Elapsed.TotalSeconds:0.0}s ";
            // ---- jump edges between islands: best class per directed pair, using boundary nodes only
            float maxReach = 0f;
            foreach (var m in profile.moves)
                foreach (var g in m.gaps) maxReach = Mathf.Max(maxReach, g.perfectGap);
            maxReach += cellSize * 2f;

            var boundary = new List<int>();
            for (int id = 0; id < r.NodePos.Count; id++)
            {
                if (r.NodeIsland[id] < 0) continue;
                var (cx, cz) = nodeCell[id];
                bool edge = false;
                for (int dx = -1; dx <= 1 && !edge; dx++)
                for (int dz = -1; dz <= 1 && !edge; dz++)
                {
                    if (dx == 0 && dz == 0) continue;
                    if (!columns.TryGetValue(Key(cx + dx, cz + dz), out var neigh)) { edge = true; break; }
                    bool same = false;
                    foreach (int o in neigh) if (r.NodeIsland[o] == r.NodeIsland[id]) { same = true; break; }
                    if (!same) edge = true;
                }
                if (edge) boundary.Add(id);
            }

            // Cluster each island's boundary into 2 m voxels. A cluster's AABB gives an optimistic bound
            // (closest horizontal distance, lowest landing height): the class can only get harder for any member,
            // so a cluster is opened member-by-member only when that bound could beat the best link found so far.
            const float voxel = 2f;
            var clusters = new List<Cluster>();
            var clusterIndex = new Dictionary<(int island, int x, int y, int z), int>();
            foreach (int id in boundary)
            {
                var p = r.NodePos[id];
                var key = (r.NodeIsland[id], Mathf.FloorToInt(p.x / voxel), Mathf.FloorToInt(p.y / voxel), Mathf.FloorToInt(p.z / voxel));
                if (!clusterIndex.TryGetValue(key, out int ci))
                {
                    ci = clusters.Count;
                    clusterIndex[key] = ci;
                    clusters.Add(new Cluster { Island = key.Item1, Min = p, Max = p });
                }
                var c = clusters[ci];
                c.Members.Add(id);
                c.Min = Vector3.Min(c.Min, p);
                c.Max = Vector3.Max(c.Max, p);
            }

            const float bucket = 4f;
            var grid = new Dictionary<long, List<int>>();
            for (int ci = 0; ci < clusters.Count; ci++)
            {
                var c = clusters[ci];
                long k = Key(Mathf.FloorToInt((c.Min.x + c.Max.x) * 0.5f / bucket), Mathf.FloorToInt((c.Min.z + c.Max.z) * 0.5f / bucket));
                if (!grid.TryGetValue(k, out var l)) grid[k] = l = new List<int>();
                l.Add(ci);
            }
            int ring = Mathf.CeilToInt((maxReach + voxel) / bucket);

            var table = new ClassTable(profile);
            var islandBoundary = new List<int>[r.Islands.Count];
            for (int i = 0; i < islandBoundary.Length; i++) islandBoundary[i] = new List<int>();
            foreach (int id in boundary) islandBoundary[r.NodeIsland[id]].Add(id);
            long nCluster = 0, nMember = 0, nHead = 0, nHeadFail = 0;
            int expanded = 0;

            // ---- start island
            r.StartIsland = PickStart(r, startHint, startHintLabel, include, out r.StartDescription);
            if (r.StartIsland < 0) { r.Seconds = (float)sw.Elapsed.TotalSeconds; return r; }

            // ---- bottleneck search (minimise the hardest move along the route). Out-links are computed lazily,
            // only for islands the player can actually reach — scenery the player never touches costs nothing.
            var outBest = new Dictionary<int, ReachClass>();
            var outFails = new Dictionary<int, int>();
            r.Islands[r.StartIsland].Class = ReachClass.Walk;
            var open = new SortedSet<(int cls, int id)> { ((int)ReachClass.Walk, r.StartIsland) };
            var done = new bool[r.Islands.Count];
            while (open.Count > 0)
            {
                var cur = open.Min;
                open.Remove(cur);
                if (done[cur.id]) continue;
                done[cur.id] = true;
                expanded++;

                // Easiest link from this island to every neighbour island.
                outBest.Clear();
                outFails.Clear();
                int ia = cur.id;
                foreach (int a in islandBoundary[ia])
                {
                    var pa = r.NodePos[a];
                    int bx = Mathf.FloorToInt(pa.x / bucket), bz = Mathf.FloorToInt(pa.z / bucket);
                    for (int dx = -ring; dx <= ring; dx++)
                    for (int dz = -ring; dz <= ring; dz++)
                    {
                        if (!grid.TryGetValue(Key(bx + dx, bz + dz), out var cand)) continue;
                        foreach (int ci in cand)
                        {
                            var c = clusters[ci];
                            int ib = c.Island;
                            if (ib == ia || done[ib]) continue;
                            float ox = Mathf.Max(0f, Mathf.Max(c.Min.x - pa.x, pa.x - c.Max.x));
                            float oz = Mathf.Max(0f, Mathf.Max(c.Min.z - pa.z, pa.z - c.Max.z));
                            float horizMin = Mathf.Sqrt(ox * ox + oz * oz);
                            if (horizMin > maxReach) continue;
                            bool known = outBest.TryGetValue(ib, out var existing);
                            if (known && existing == ReachClass.Walk) continue;
                            outFails.TryGetValue(ib, out int fails);
                            if (fails >= MaxBlockedChecksPerPair) continue;
                            var bound = table.Classify(Mathf.Max(0f, horizMin - cellSize), c.Min.y - pa.y, horizMin);
                            if (bound == ReachClass.Unreachable || (known && existing <= bound)) continue;
                            nCluster++;

                            foreach (int b in c.Members)
                            {
                                nMember++;
                                var pb = r.NodePos[b];
                                float horiz = new Vector2(pb.x - pa.x, pb.z - pa.z).magnitude;
                                if (horiz > maxReach) continue;
                                var cls = table.Classify(Mathf.Max(0f, horiz - cellSize), pb.y - pa.y, horiz);
                                if (cls == ReachClass.Unreachable || (known && existing <= cls)) continue;
                                nHead++;
                                if (!HeadClear(pa, pb, mask))
                                {
                                    nHeadFail++;
                                    outFails[ib] = ++fails;
                                    if (fails >= MaxBlockedChecksPerPair) break;
                                    continue;
                                }
                                outBest[ib] = existing = cls;
                                known = true;
                                if (cls <= bound) break; // nothing in this cluster can beat its own bound
                            }
                        }
                    }
                }

                foreach (var kv in outBest)
                {
                    int to = kv.Key;
                    var c = (ReachClass)Mathf.Max(cur.cls, (int)kv.Value);
                    if (c >= r.Islands[to].Class) continue;
                    open.Remove(((int)r.Islands[to].Class, to));
                    r.Islands[to].Class = c;
                    r.Islands[to].ReachedFrom = cur.id;
                    open.Add(((int)c, to));
                }
            }
            r.PhaseTimes += $"search {sw.Elapsed.TotalSeconds:0.0}s (expanded {expanded}/{r.Islands.Count} islands, boundary {boundary.Count}, " +
                            $"clusters {clusters.Count}, opened {nCluster}, members {nMember}, flight checks {nHead}/{nHeadFail} blocked) ";

            // ---- markers
            foreach (var marker in AllMarkers(include))
            {
                int island = NearestIsland(r, marker.transform.position, 3f);
                r.Markers.Add((marker, island, island >= 0 ? r.Islands[island].Class : ReachClass.Unreachable));
            }

            r.Seconds = (float)sw.Elapsed.TotalSeconds;
            return r;
        }

        /// <summary>Easiest move class that covers this lip-to-lip gap at this height change.</summary>
        public static ReachClass Classify(ReachProfile p, float gap, float dh, float horiz)
        {
            // Short climbs onto an adjacent ledge: jump + vault reach.
            if (horiz <= 1.5f && dh > 0f)
            {
                if (dh <= p.maxStepHeight) return ReachClass.Walk;
                return dh <= p.maxLedgeHeight ? ReachClass.Jump : ReachClass.Unreachable;
            }
            if (gap <= 0.05f && dh <= p.maxStepHeight) return ReachClass.Walk;
            if (dh < 0f && gap <= 0.5f) return ReachClass.Walk; // just walk off the edge

            float dhClamped = Mathf.Max(dh, ReachMeasurer.HeightRows[0]);
            var stand = p.Get(MoveKind.StandingJump);
            var run = p.Get(MoveKind.RunJump);
            var dash = p.Get(MoveKind.DashJump);
            float easy = Mathf.Max(stand?.GapAt(dhClamped, true) ?? 0f, run?.GapAt(dhClamped, true) ?? 0f);
            float easyPerfect = Mathf.Max(stand?.GapAt(dhClamped, false) ?? 0f, run?.GapAt(dhClamped, false) ?? 0f);
            if (gap <= easy) return ReachClass.Jump;
            if (gap <= easyPerfect) return ReachClass.PerfectJump;
            if (dash != null && gap <= dash.GapAt(dhClamped, true)) return ReachClass.DashJump;
            if (dash != null && gap <= dash.GapAt(dhClamped, false)) return ReachClass.PerfectDashJump;
            return ReachClass.Unreachable;
        }

        /// <summary>Cheap flight-path check: nothing solid between the two lips at head height and mid-arc.</summary>
        private static bool HeadClear(Vector3 a, Vector3 b, int mask)
        {
            float lift = Mathf.Max(a.y, b.y) + 1.6f;
            var mid = (a + b) * 0.5f;
            mid.y = lift + 0.8f;
            var a1 = new Vector3(a.x, a.y + 1.6f, a.z);
            var b1 = new Vector3(b.x, b.y + 1.6f, b.z);
            return !Physics.Linecast(a1, mid, mask, QueryTriggerInteraction.Ignore)
                && !Physics.Linecast(mid, b1, mask, QueryTriggerInteraction.Ignore);
        }

        private static int PickStart(ReachResult r, Vector3? hint, string hintLabel, Func<GameObject, bool> include, out string desc)
        {
            if (hint.HasValue)
            {
                int i = NearestIsland(r, hint.Value, 5f);
                desc = i >= 0 ? hintLabel : hintLabel + " (no standable surface within 5 m)";
                if (i >= 0) return i;
            }
            foreach (var m in AllMarkers(include))
            {
                if (m.Kind != RoomMarkerKind.Entry) continue;
                int i = NearestIsland(r, m.transform.position, 5f);
                if (i >= 0) { desc = "Entry marker '" + m.Label + "'"; return i; }
            }
            int best = -1;
            for (int i = 0; i < r.Islands.Count; i++)
                if (best < 0 || r.Islands[i].Nodes.Count > r.Islands[best].Nodes.Count) best = i;
            desc = best >= 0 ? "largest island (no Entry marker or selection)" : "none";
            return best;
        }

        public static int NearestIsland(ReachResult r, Vector3 p, float maxDist)
        {
            int best = -1;
            float bestD = maxDist * maxDist;
            for (int i = 0; i < r.NodePos.Count; i++)
            {
                if (r.NodeIsland[i] < 0) continue;
                float d = (r.NodePos[i] - p).sqrMagnitude;
                if (d < bestD) { bestD = d; best = r.NodeIsland[i]; }
            }
            return best;
        }

        private static IEnumerable<Collider> AllColliders(Func<GameObject, bool> include)
        {
            for (int s = 0; s < SceneManager.sceneCount; s++)
            {
                var scene = SceneManager.GetSceneAt(s);
                if (!scene.isLoaded) continue;
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var c in root.GetComponentsInChildren<Collider>(false))
                        if (include == null || include(c.gameObject)) yield return c;
            }
        }

        private static IEnumerable<RoomMarker> AllMarkers(Func<GameObject, bool> include)
        {
            for (int s = 0; s < SceneManager.sceneCount; s++)
            {
                var scene = SceneManager.GetSceneAt(s);
                if (!scene.isLoaded) continue;
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var m in root.GetComponentsInChildren<RoomMarker>(false))
                        if (include == null || include(m.gameObject)) yield return m;
            }
        }

        private static long Key(int a, int b) => ((long)a << 32) | (uint)b;
    }
}


namespace BlastFrame.EditorTools.LevelDesign
{
    /// <summary>Profile thresholds pre-sampled every 0.1 m of height change so the edge pass is a few compares.</summary>
    internal sealed class Cluster
    {
        public int Island;
        public Vector3 Min, Max;
        public readonly List<int> Members = new List<int>(8);
    }

    internal sealed class ClassTable
    {
        private const float Step = 0.1f;
        private readonly ReachProfile _p;
        private readonly float _minDh, _maxDh;
        private readonly float[] _easy, _easyPerfect, _dash, _dashPerfect;

        public ClassTable(ReachProfile p)
        {
            _p = p;
            _minDh = ReachMeasurer.HeightRows[0];
            _maxDh = Mathf.Max(p.maxLedgeHeight, ReachMeasurer.HeightRows[ReachMeasurer.HeightRows.Length - 1]);
            int n = Mathf.CeilToInt((_maxDh - _minDh) / Step) + 1;
            _easy = new float[n]; _easyPerfect = new float[n]; _dash = new float[n]; _dashPerfect = new float[n];
            var stand = p.Get(MoveKind.StandingJump);
            var run = p.Get(MoveKind.RunJump);
            var dash = p.Get(MoveKind.DashJump);
            for (int i = 0; i < n; i++)
            {
                float dh = _minDh + i * Step;
                _easy[i] = Mathf.Max(stand?.GapAt(dh, true) ?? 0f, run?.GapAt(dh, true) ?? 0f);
                _easyPerfect[i] = Mathf.Max(stand?.GapAt(dh, false) ?? 0f, run?.GapAt(dh, false) ?? 0f);
                _dash[i] = dash?.GapAt(dh, true) ?? 0f;
                _dashPerfect[i] = dash?.GapAt(dh, false) ?? 0f;
            }
        }

        /// <summary>Same rules as <see cref="ReachabilityAnalyzer.Classify"/>, table-driven.</summary>
        public ReachClass Classify(float gap, float dh, float horiz)
        {
            if (horiz <= 1.5f && dh > 0f)
            {
                if (dh <= _p.maxStepHeight) return ReachClass.Walk;
                return dh <= _p.maxLedgeHeight ? ReachClass.Jump : ReachClass.Unreachable;
            }
            if (gap <= 0.05f && dh <= _p.maxStepHeight) return ReachClass.Walk;
            if (dh < 0f && gap <= 0.5f) return ReachClass.Walk;
            if (dh > _maxDh) return ReachClass.Unreachable;
            int i = Mathf.Clamp(Mathf.RoundToInt((Mathf.Max(dh, _minDh) - _minDh) / Step), 0, _easy.Length - 1);
            if (gap <= _easy[i]) return ReachClass.Jump;
            if (gap <= _easyPerfect[i]) return ReachClass.PerfectJump;
            if (gap <= _dash[i]) return ReachClass.DashJump;
            if (gap <= _dashPerfect[i]) return ReachClass.PerfectDashJump;
            return ReachClass.Unreachable;
        }
    }
}

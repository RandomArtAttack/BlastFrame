using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace BlastFrame.EditorTools.LevelDesign
{
    public enum MoveKind { StandingJump, RunJump, DashJump }

    [Serializable]
    public sealed class GapRow
    {
        [Tooltip("Landing height minus takeoff height, metres.")]
        public float heightDelta;
        [Tooltip("Widest edge-to-edge gap cleared with frame-perfect timing, metres.")]
        public float perfectGap;
        [Tooltip("Widest gap that still has a comfortable timing window (ReachMeasurer.ComfortWindowTicks), metres.")]
        public float comfortableGap;
    }

    [Serializable]
    public sealed class MoveReach
    {
        public MoveKind kind;
        [Tooltip("Free-flight arc of the real motor from the optimal takeoff tick: x = forward distance, y = height, " +
                 "relative to the takeoff point (player feet).")]
        public List<Vector2> arc = new List<Vector2>();
        public List<GapRow> gaps = new List<GapRow>();

        /// <summary>Linear interpolation over heightDelta; beyond the table, clamps (conservative for climbs:
        /// above the highest measured row the gap is reported as 0).</summary>
        public float GapAt(float heightDelta, bool comfortable)
        {
            if (gaps.Count == 0) return 0f;
            if (heightDelta > gaps[gaps.Count - 1].heightDelta + 0.01f) return 0f;
            if (heightDelta <= gaps[0].heightDelta) return comfortable ? gaps[0].comfortableGap : gaps[0].perfectGap;
            for (int i = 1; i < gaps.Count; i++)
            {
                if (heightDelta > gaps[i].heightDelta) continue;
                var a = gaps[i - 1];
                var b = gaps[i];
                float t = Mathf.InverseLerp(a.heightDelta, b.heightDelta, heightDelta);
                return comfortable ? Mathf.Lerp(a.comfortableGap, b.comfortableGap, t)
                                   : Mathf.Lerp(a.perfectGap, b.perfectGap, t);
            }
            return 0f;
        }
    }

    [Serializable]
    public sealed class ChimneyRow
    {
        [Tooltip("Wall-to-wall width of the shaft, metres.")]
        public float width;
        [Tooltip("Net height gained after the measured number of wall jumps, metres (<= 0 = not climbable).")]
        public float heightGain;
        public int wallJumps;
    }

    /// <summary>Everything the level-design tools know about player reach, measured from the real motor.</summary>
    [Serializable]
    public sealed class ReachProfile
    {
        public string templateHash;
        public string templateSource;
        public string measuredAt;
        public float capsuleRadius;
        [Tooltip("Player motor collision mask at measure time — the layers the tools treat as solid.")]
        public int collisionMask = ~0;
        public float maxStepHeight;
        [Tooltip("Tallest ledge the player can get on top of from standing against it (jump + ledge vault), metres.")]
        public float maxLedgeHeight;
        [Tooltip("Apex of a full held jump on flat ground, metres.")]
        public float jumpApex;
        public List<MoveReach> moves = new List<MoveReach>();
        public List<ChimneyRow> chimney = new List<ChimneyRow>();

        public MoveReach Get(MoveKind k)
        {
            foreach (var m in moves) if (m.kind == k) return m;
            return null;
        }

        public static string CachePath => Path.Combine("Library", "BlastFrame", "ReachProfile.json");

        public void Save()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CachePath));
            File.WriteAllText(CachePath, JsonUtility.ToJson(this, true));
        }

        public static ReachProfile LoadCached()
        {
            if (!File.Exists(CachePath)) return null;
            try { return JsonUtility.FromJson<ReachProfile>(File.ReadAllText(CachePath)); }
            catch (Exception) { return null; }
        }

        public string ToMarkdown()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Measured from the real motor ({templateSource}) on {measuredAt}.");
            sb.AppendLine($"Jump apex {jumpApex:0.00} m · tallest ledge from standing {maxLedgeHeight:0.00} m · auto step-up {maxStepHeight:0.00} m");
            sb.AppendLine();
            sb.Append("| Δheight |");
            foreach (var m in moves) sb.Append($" {m.kind} perfect / comfy |");
            sb.AppendLine();
            sb.Append("|---|");
            foreach (var _ in moves) sb.Append("---|");
            sb.AppendLine();
            var first = moves.Count > 0 ? moves[0] : null;
            if (first != null)
            {
                for (int r = 0; r < first.gaps.Count; r++)
                {
                    sb.Append($"| {first.gaps[r].heightDelta:+0.0;-0.0;0.0} |");
                    foreach (var m in moves)
                    {
                        var g = r < m.gaps.Count ? m.gaps[r] : null;
                        sb.Append(g == null ? " - |" : $" {g.perfectGap:0.00} / {g.comfortableGap:0.00} |");
                    }
                    sb.AppendLine();
                }
            }
            if (chimney.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("| Chimney width | Net height after wall jumps |");
                sb.AppendLine("|---|---|");
                foreach (var c in chimney)
                    sb.AppendLine($"| {c.width:0.0} | {(c.heightGain > 0.05f ? $"+{c.heightGain:0.00} m ({c.wallJumps} jumps)" : "not climbable")} |");
            }
            return sb.ToString();
        }
    }

    /// <summary>
    /// Runs the real player motor through scripted gap / ledge / chimney courses and binary-searches the limits.
    /// Gaps are EDGE-TO-EDGE (takeoff lip to landing lip), which is what a level designer measures.
    /// Every sim rebuilds the player, so module timers never leak between runs.
    /// </summary>
    public static class ReachMeasurer
    {
        /// <summary>Jump-press timing window that counts as "comfortable": 5 ticks = 100 ms at 50 Hz.</summary>
        public const int ComfortWindowTicks = 5;

        public static readonly float[] HeightRows = { -6f, -4f, -2f, -1f, 0f, 1f, 2f, 3f };
        public static readonly float[] ChimneyWidths = { 1.5f, 2f, 2.5f, 3f, 4f, 5f, 6f };

        private const float RunUp = 12f;         // metres of runway before the takeoff lip
        private const float YawPlusX = 90f;      // player faces +X
        private const float MaxSimSeconds = 5f;
        private const float GapSearchMax = 30f;
        private const float GapResolution = 0.02f;

        public static ReachProfile MeasureAll(MotorSimRig rig, Action<string, float> progress = null)
        {
            var p = new ReachProfile
            {
                templateHash = rig.Template.Hash,
                templateSource = rig.Template.Source,
                measuredAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                capsuleRadius = rig.Template.Radius,
                collisionMask = rig.Template.CollisionMask,
            };
            p.maxStepHeight = ReadMotorFloat(rig, "maxStepHeight");

            var kinds = (MoveKind[])Enum.GetValues(typeof(MoveKind));
            int total = kinds.Length * HeightRows.Length + ChimneyWidths.Length + 2;
            int done = 0;
            void Report(string what) => progress?.Invoke(what, (float)done++ / total);

            Report("Jump apex");
            p.jumpApex = MeasureApex(rig);
            Report("Ledge height");
            p.maxLedgeHeight = MeasureMaxLedge(rig);

            foreach (var k in kinds)
            {
                var mr = new MoveReach { kind = k, arc = RecordArc(rig, k) };
                foreach (float h in HeightRows)
                {
                    Report($"{k} Δh {h:+0;-0;0}");
                    mr.gaps.Add(new GapRow
                    {
                        heightDelta = h,
                        perfectGap = SearchGap(rig, k, h, 1),
                        comfortableGap = SearchGap(rig, k, h, ComfortWindowTicks),
                    });
                }
                p.moves.Add(mr);
            }

            foreach (float w in ChimneyWidths)
            {
                Report($"Chimney {w:0.0}");
                p.chimney.Add(MeasureChimney(rig, w));
            }
            return p;
        }

        private static float ReadMotorFloat(MotorSimRig rig, string field)
        {
            if (!rig.Template.Json.TryGetValue(typeof(Gameplay.Player.PlayerMotor), out var json)) return 0f;
            var m = System.Text.RegularExpressions.Regex.Match(json, "\"" + field + "\":(-?[0-9.eE+-]+)");
            return m.Success ? float.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) : 0f;
        }

        // ------------------------------------------------------------------ gap search

        /// <summary>Widest gap where at least <paramref name="minWindow"/> distinct jump-press ticks land.</summary>
        public static float SearchGap(MotorSimRig rig, MoveKind kind, float heightDelta, int minWindow)
        {
            float lo = 0f, hi = GapSearchMax;
            while (hi - lo > GapResolution)
            {
                float mid = (lo + hi) * 0.5f;
                if (CountLandingTicks(rig, kind, mid, heightDelta, minWindow) >= minWindow) lo = mid;
                else hi = mid;
            }
            return lo;
        }

        /// <summary>Tries every plausible jump-press tick for this course and counts how many land on the far side.
        /// Stops early once <paramref name="enough"/> successes are found.</summary>
        public static int CountLandingTicks(MotorSimRig rig, MoveKind kind, float gap, float heightDelta, int enough)
        {
            if (kind == MoveKind.StandingJump)
            {
                // Timing is free when standing; "comfortable" instead means toes 0.25 m back from the lip.
                BuildGapCourse(rig, gap, heightDelta);
                return RunGapSim(rig, kind, gap, heightDelta, 1, enough > 1 ? 0.25f : 0f) ? enough : 0;
            }

            int leaveTick = FindLeaveTick(rig, kind);
            BuildGapCourse(rig, gap, heightDelta);
            int coyoteTicks = Mathf.CeilToInt(0.5f / rig.Dt);
            int first = Mathf.Max(2, leaveTick - 25);
            int last = leaveTick + coyoteTicks;
            int successes = 0;
            for (int t = last; t >= first; t--) // latest presses first: those are the long ones
            {
                if (RunGapSim(rig, kind, gap, heightDelta, t)) { if (++successes >= enough) break; }
                else if (successes > 0 && t < leaveTick - 3) break; // left the landing window going earlier
            }
            return successes;
        }

        private static void BuildGapCourse(MotorSimRig rig, float gap, float h)
        {
            rig.ClearArena();
            const float thick = 4f, halfWidth = 6f;
            rig.Box(new Vector3(-RunUp - 4f, -thick, -halfWidth), new Vector3(0f, 0f, halfWidth), "Takeoff");
            float floor = Mathf.Min(0f, h);
            rig.Box(new Vector3(gap, floor - thick, -halfWidth), new Vector3(gap + 30f, h, halfWidth), "Landing");
        }

        /// <summary>Spawn, hold forward, settle one grounded tick, then (DashJump) press and hold dash.</summary>
        private static void Begin(MotorSimRig rig, MoveKind kind, float toesBack = 0f)
        {
            rig.Spawn(StartFor(rig, kind) - new Vector3(toesBack, 0f, 0f), YawPlusX);
            rig.Input.Move = Vector2.up;
            rig.Step();
            if (kind == MoveKind.DashJump) rig.Input.PressDash();
        }

        private static Vector3 StartFor(MotorSimRig rig, MoveKind kind) =>
            kind == MoveKind.StandingJump
                ? new Vector3(-rig.Template.Radius, 0.02f, 0f)   // toes on the lip
                : new Vector3(-RunUp, 0.02f, 0f);

        private static MotorSimRig _leaveRig;
        private static readonly Dictionary<MoveKind, int> LeaveTicks = new Dictionary<MoveKind, int>();

        /// <summary>Tick at which the player stops being grounded when running straight off the lip with no jump.</summary>
        private static int FindLeaveTick(MotorSimRig rig, MoveKind kind)
        {
            if (_leaveRig != rig) { _leaveRig = rig; LeaveTicks.Clear(); }
            if (LeaveTicks.TryGetValue(kind, out int cached)) return cached;
            rig.ClearArena();
            rig.Box(new Vector3(-RunUp - 4f, -4f, -6f), new Vector3(0f, 0f, 6f), "Takeoff");
            Begin(rig, kind);
            bool wasGrounded = false;
            for (int i = 0; i < MaxSimSeconds / rig.Dt; i++)
            {
                rig.Step();
                if (rig.Motor.IsGrounded) wasGrounded = true;
                else if (wasGrounded && rig.Position.x > -1f) return LeaveTicks[kind] = rig.Tick;
            }
            return LeaveTicks[kind] = rig.Tick;
        }

        /// <summary>One run: hold forward (and dash for DashJump), press jump at pressTick and hold it.
        /// Success = standing on the landing block's top surface.</summary>
        private static bool RunGapSim(MotorSimRig rig, MoveKind kind, float gap, float h, int pressTick, float toesBack = 0f)
        {
            Begin(rig, kind, toesBack);

            bool airborne = false;
            float kill = Mathf.Min(0f, h) - 3f;
            int maxTicks = Mathf.CeilToInt(MaxSimSeconds / rig.Dt);
            for (int i = 0; i < maxTicks; i++)
            {
                if (rig.Tick == pressTick) rig.Input.PressJump();
                rig.Step();
                var pos = rig.Position;
                if (pos.y < kill) return false;
                if (!rig.Motor.IsGrounded) { airborne = true; continue; }
                if (!airborne)
                {
                    if (pos.x > gap + 0.5f && pos.y > h - 0.1f) return true; // walked or stepped across
                    continue;
                }
                // Landed somewhere after being airborne.
                if (pos.x > gap - rig.Template.Radius && pos.y > h - 0.1f) return true;
                if (pos.x < gap) return false; // came back down on the takeoff side
            }
            return false;
        }

        // ------------------------------------------------------------------ arcs / vertical

        /// <summary>Records the free-flight arc (no landing block) from the optimal press tick.</summary>
        public static List<Vector2> RecordArc(MotorSimRig rig, MoveKind kind)
        {
            rig.ClearArena();
            rig.Box(new Vector3(-RunUp - 4f, -4f, -6f), new Vector3(0f, 0f, 6f), "Takeoff");
            int press = kind == MoveKind.StandingJump ? 1 : FindLeaveTick(rig, kind) - 1;

            Begin(rig, kind);
            var arc = new List<Vector2>();
            Vector3 origin = Vector3.zero;
            bool started = false;
            for (int i = 0; i < MaxSimSeconds / rig.Dt; i++)
            {
                if (rig.Tick == press) rig.Input.PressJump();
                rig.Step();
                var pos = rig.Position;
                if (!started && rig.Tick > press) { started = true; origin = pos; arc.Add(Vector2.zero); continue; }
                if (!started) continue;
                arc.Add(new Vector2(pos.x - origin.x, pos.y - origin.y));
                if (pos.y < -12f) break;
            }
            return arc;
        }

        public static float MeasureApex(MotorSimRig rig)
        {
            rig.ClearArena();
            rig.Box(new Vector3(-5f, -4f, -5f), new Vector3(5f, 0f, 5f), "Floor");
            rig.Spawn(new Vector3(0f, 0.02f, 0f), YawPlusX);
            rig.Step();
            rig.Input.PressJump();
            float apex = 0f;
            for (int i = 0; i < 200; i++)
            {
                rig.Step();
                apex = Mathf.Max(apex, rig.Position.y);
            }
            return apex;
        }

        /// <summary>Tallest wall the player can end up on top of, from standing 1 m away, holding forward.</summary>
        public static float MeasureMaxLedge(MotorSimRig rig)
        {
            float lo = 0f, hi = 8f;
            while (hi - lo > GapResolution)
            {
                float mid = (lo + hi) * 0.5f;
                if (TryLedge(rig, mid)) lo = mid; else hi = mid;
            }
            return lo;
        }

        private static bool TryLedge(MotorSimRig rig, float height)
        {
            for (int pressTick = 1; pressTick <= 12; pressTick += 2)
            {
                rig.ClearArena();
                rig.Box(new Vector3(-6f, -4f, -6f), new Vector3(60f, 0f, 6f), "Floor");
                rig.Box(new Vector3(1f, 0f, -6f), new Vector3(60f, height, 6f), "Ledge");
                rig.Spawn(new Vector3(-0.5f, 0.02f, 0f), YawPlusX);
                rig.Input.Move = Vector2.up;
                for (int i = 0; i < 150; i++)
                {
                    if (rig.Tick == pressTick) rig.Input.PressJump();
                    rig.Step();
                }
                var pos = rig.Position;
                if (rig.Motor.IsGrounded && pos.y > height - 0.1f && pos.x > 1f) return true;
            }
            return false;
        }

        /// <summary>Shaft between two walls along X; input always steers toward the wall the player is flying at,
        /// and jump is pressed each time the player touches a wall. Reports net height after 8 wall jumps.</summary>
        public static ChimneyRow MeasureChimney(MotorSimRig rig, float width)
        {
            const int jumps = 8;
            rig.ClearArena();
            float half = width * 0.5f;
            rig.Box(new Vector3(-half - 2f, -4f, -6f), new Vector3(half + 2f, 0f, 6f), "Floor");
            rig.Box(new Vector3(-half - 2f, 0f, -6f), new Vector3(-half, 60f, 6f), "WallL");
            rig.Box(new Vector3(half, 0f, -6f), new Vector3(half + 2f, 60f, 6f), "WallR");

            rig.Spawn(new Vector3(-half + rig.Template.Radius + 0.05f, 0.02f, 0f), YawPlusX);
            rig.Input.Move = Vector2.up; // toward +X wall first
            rig.Step();
            rig.Input.PressJump();

            int done = 0;
            float lastJumpY = 0f, firstWallY = -1f;
            bool released = false;
            int ticksSinceJump = 0, wallTicks = 0;
            for (int i = 0; i < 1500 && done < jumps; i++)
            {
                rig.Step();
                ticksSinceJump++;
                if (!released && ticksSinceJump > 2) { rig.Input.ReleaseJump(); released = true; }
                wallTicks = rig.Motor.IsOnWall && !rig.Motor.IsGrounded ? wallTicks + 1 : 0;
                if (wallTicks >= 2 && ticksSinceJump > 4)
                {
                    var n = rig.Motor.WallNormal;
                    if (firstWallY < 0f) firstWallY = rig.Position.y;
                    rig.Input.Move = new Vector2(0f, n.x > 0f ? 1f : -1f); // steer toward the opposite wall (yaw faces +X)
                    rig.Input.PressJump();
                    released = false;
                    ticksSinceJump = 0;
                    lastJumpY = rig.Position.y;
                    done++;
                }
                if (rig.Motor.IsGrounded && done > 0) break;
            }
            return new ChimneyRow
            {
                width = width,
                wallJumps = done,
                heightGain = done >= jumps && firstWallY >= 0f ? lastJumpY - firstWallY : 0f,
            };
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using BlastFrame.Gameplay.Enemies;
using BlastFrame.Gameplay.Rooms;

namespace BlastFrame.Tests.Stress
{
    /// <summary>
    /// Large-sample checks: seeded variant selection fairness/independence, and the arc turret's aim math
    /// flown through ArcProjectile's exact integrator against static and moving targets.
    /// </summary>
    [Category("Slow")]
    public class StressTests
    {
        // ------------------------------------------------------------------ seeds

        [TestCase(2), TestCase(3), TestCase(4), TestCase(5)]
        public void VariantPick_IsUniformPerRoom(int variantCount)
        {
            var vs = Enumerable.Range(0, variantCount).Select(_ => ScriptableObject.CreateInstance<RoomVariantSO>()).ToList();
            try
            {
                const int seeds = 300_000;
                var rng = new System.Random(7);
                for (int room = 0; room < 12; room++)
                {
                    var counts = new int[variantCount];
                    for (int i = 0; i < seeds; i++)
                    {
                        int seed = rng.Next(int.MinValue, int.MaxValue);
                        counts[vs.IndexOf(RoomVariantSelector.Pick(vs, seed, room))]++;
                    }
                    double chi = ChiSquare(counts);
                    TestContext.WriteLine($"variants {variantCount} room {room}: [{string.Join(", ", counts)}] chi²={chi:0.0}");
                    Assert.Less(chi, ChiCritical(variantCount - 1), $"room {room} pick is biased: [{string.Join(", ", counts)}]");
                }
            }
            finally { foreach (var v in vs) UnityEngine.Object.DestroyImmediate(v); }
        }

        [Test]
        public void VariantPick_RoomsAreIndependent()
        {
            var vs = Enumerable.Range(0, 3).Select(_ => ScriptableObject.CreateInstance<RoomVariantSO>()).ToList();
            try
            {
                var rng = new System.Random(11);
                var worst = 0.0;
                for (int a = 0; a < 9; a++)
                for (int b = a + 1; b < 9; b++)
                {
                    var joint = new int[9];
                    for (int i = 0; i < 200_000; i++)
                    {
                        int s = rng.Next(int.MinValue, int.MaxValue);
                        joint[vs.IndexOf(RoomVariantSelector.Pick(vs, s, a)) * 3 + vs.IndexOf(RoomVariantSelector.Pick(vs, s, b))]++;
                    }
                    double chi = ChiSquare(joint);
                    worst = Math.Max(worst, chi);
                    Assert.Less(chi, ChiCritical(8), $"rooms {a} and {b} picks are correlated: [{string.Join(", ", joint)}]");
                }
                TestContext.WriteLine($"worst pairwise chi² (df 8) = {worst:0.0}");
            }
            finally { foreach (var v in vs) UnityEngine.Object.DestroyImmediate(v); }
        }

        [Test]
        public void VariantPick_SequentialSeedsStillVary()
        {
            // Runs seeded from a clock or counter produce sequential seeds; the same room must not repeat
            // the same variant across consecutive runs much more than chance (1/3).
            var vs = Enumerable.Range(0, 3).Select(_ => ScriptableObject.CreateInstance<RoomVariantSO>()).ToList();
            try
            {
                for (int room = 0; room < 9; room++)
                {
                    int same = 0, n = 100_000;
                    var prev = RoomVariantSelector.Pick(vs, 0, room);
                    for (int s = 1; s <= n; s++)
                    {
                        var cur = RoomVariantSelector.Pick(vs, s, room);
                        if (cur == prev) same++;
                        prev = cur;
                    }
                    double rate = (double)same / n;
                    TestContext.WriteLine($"room {room}: consecutive-seed repeat rate {rate:P1}");
                    Assert.That(rate, Is.InRange(0.30, 0.37), $"room {room}: sequential seeds repeat {rate:P1} of the time (chance = 33%)");
                }
            }
            finally { foreach (var v in vs) UnityEngine.Object.DestroyImmediate(v); }
        }

        private static double ChiSquare(int[] counts)
        {
            double expected = counts.Sum() / (double)counts.Length;
            return counts.Sum(c => (c - expected) * (c - expected) / expected);
        }

        /// <summary>Critical chi² at p = 0.001.</summary>
        private static double ChiCritical(int df) => df switch
        {
            1 => 10.83, 2 => 13.82, 3 => 16.27, 4 => 18.47, 8 => 26.12, _ => 30.0,
        };

        // ------------------------------------------------------------------ arc turret aim

        private const BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        private sealed class Turret : IDisposable
        {
            public GameObject Go;
            public EnemyBehaviorArcPredict Arc;
            public Transform Barrel, Muzzle;
            private static readonly MethodInfo Angle = typeof(EnemyBehaviorArcPredict).GetMethod("ComputeLaunchAngle", Any);
            private static readonly MethodInfo Predict = typeof(EnemyBehaviorArcPredict).GetMethod("PredictTarget", Any);
            private static readonly FieldInfo Vel = typeof(EnemyBehaviorArcPredict).GetField("_playerVelocity", Any);

            public Turret(float speed, float gravityScale, float muzzleLength)
            {
                Go = new GameObject("StressTurret");
                Go.AddComponent<EnemyCore>();
                var stats = Go.GetComponent<EnemyStats>(); // auto-added by EnemyCore [RequireComponent]
                Arc = Go.AddComponent<EnemyBehaviorArcPredict>();
                Barrel = new GameObject("Barrel").transform;
                Barrel.SetParent(Go.transform, false);
                Barrel.localPosition = new Vector3(0, 1.5f, 0);
                Muzzle = new GameObject("Muzzle").transform;
                Muzzle.SetParent(Barrel, false);
                Muzzle.localPosition = new Vector3(0, 0, muzzleLength);
                var so = new SerializedObject(Arc);
                so.FindProperty("muzzle").objectReferenceValue = Muzzle;
                so.FindProperty("barrelPivot").objectReferenceValue = Barrel;
                so.FindProperty("projectileGravityScale").FindPropertyRelative("ConstantValue").floatValue = gravityScale;
                so.ApplyModifiedPropertiesWithoutUndo();
                var sso = new SerializedObject(stats);
                sso.FindProperty("projectileSpeed").FindPropertyRelative("ConstantValue").floatValue = speed;
                sso.ApplyModifiedPropertiesWithoutUndo();
                typeof(EnemyBehaviorBase).GetMethod("Awake", Any).Invoke(Arc, null);
            }

            /// <summary>Settles yaw + pitch the way FixedUpdate does once the barrel has caught up: re-solve from
            /// the (moving) muzzle tip until the pitch stops changing.</summary>
            public void AimAt(Vector3 target)
            {
                var flat = new Vector3(target.x, 0, target.z) - new Vector3(Go.transform.position.x, 0, Go.transform.position.z);
                if (flat.sqrMagnitude > 1e-4f) Go.transform.rotation = Quaternion.LookRotation(flat, Vector3.up);
                for (int i = 0; i < 8; i++)
                {
                    float rad = (float)Angle.Invoke(Arc, new object[] { target });
                    Barrel.localEulerAngles = new Vector3(-rad * Mathf.Rad2Deg, 0, 0);
                }
            }

            public Vector3 Lead(Vector3 playerPos, Vector3 playerVel)
            {
                Vel.SetValue(Arc, playerVel);
                return (Vector3)Predict.Invoke(Arc, new object[] { playerPos });
            }

            public void Dispose() => UnityEngine.Object.DestroyImmediate(Go);
        }

        /// <summary>ArcProjectile's exact integration (v += g·scale·dt; p += v·dt) until it falls through targetY.
        /// Returns impact point and flight time; null if it never comes down within 20 s.</summary>
        private static (Vector3 p, float t)? Fly(Vector3 from, Vector3 vel, float gravityScale, float targetY)
        {
            const float dt = 0.02f;
            var p = from;
            var g = Physics.gravity * gravityScale;
            for (int i = 1; i < 1000; i++)
            {
                vel += g * dt;
                var next = p + vel * dt;
                if (vel.y < 0f && next.y <= targetY)
                {
                    float f = Mathf.Approximately(p.y, next.y) ? 1f : (p.y - targetY) / (p.y - next.y);
                    return (Vector3.Lerp(p, next, f), (i - 1 + f) * dt);
                }
                p = next;
            }
            return null;
        }

        private static float Percentile(List<float> xs, float q)
        {
            if (xs.Count == 0) return float.NaN;
            var s = xs.OrderBy(x => x).ToList();
            return s[Mathf.Clamp(Mathf.RoundToInt(q * (s.Count - 1)), 0, s.Count - 1)];
        }

        [TestCase(12f, 1f, 0.8f)]
        [TestCase(12f, 0.2f, 0.8f)]
        [TestCase(6f, 0.2f, 1.2f)]   // glossary heavy mortar: ~half speed, 20% gravity
        [TestCase(20f, 1f, 1.5f)]
        public void ArcShot_LandsOnStaticTargets(float speed, float gravityScale, float muzzleLength)
        {
            using var t = new Turret(speed, gravityScale, muzzleLength);
            var rng = new System.Random(99);
            var misses = new List<float>();
            int outOfRange = 0;
            float g = Mathf.Abs(Physics.gravity.y) * gravityScale;
            for (int i = 0; i < 4000; i++)
            {
                float ang = (float)(rng.NextDouble() * Math.PI * 2);
                float r = 3f + (float)rng.NextDouble() * 37f;
                float y = (float)(rng.NextDouble() * 16 - 8);
                var target = new Vector3(Mathf.Cos(ang) * r, y, Mathf.Sin(ang) * r);
                t.AimAt(target);
                var muzzle = t.Muzzle.position;
                // Skip targets the solver itself flags unreachable (it falls back to 45° = max range).
                float R = new Vector2(target.x - muzzle.x, target.z - muzzle.z).magnitude, dy = target.y - muzzle.y;
                float v2 = speed * speed;
                if (v2 * v2 - g * (g * R * R + 2 * dy * v2) < 0f) { outOfRange++; continue; }
                var hit = Fly(muzzle, t.Muzzle.forward * speed, gravityScale, target.y);
                if (hit == null) { outOfRange++; continue; }
                misses.Add(new Vector2(hit.Value.p.x - target.x, hit.Value.p.z - target.z).magnitude);
            }
            float p50 = Percentile(misses, 0.5f), p95 = Percentile(misses, 0.95f), max = misses.Count > 0 ? misses.Max() : 0f;
            TestContext.WriteLine($"speed {speed} gScale {gravityScale} muzzle {muzzleLength}: {misses.Count} in range, {outOfRange} out of range; miss p50 {p50:0.000} p95 {p95:0.000} max {max:0.000} m");
            Debug.Log($"[ArcStress] static speed {speed} g×{gravityScale} muzzle {muzzleLength}: in-range {misses.Count}, out {outOfRange}, miss p50 {p50:0.000} p95 {p95:0.000} max {max:0.000} m");
            Assert.Less(p95, 0.75f, "95% of in-range shots should land within half the 1.5 m blast radius");
        }

        [TestCase(12f, 1f)]
        [TestCase(6f, 0.2f)]
        public void ArcShot_LeadBeatsNoLead_OnStraightRunners(float speed, float gravityScale)
        {
            using var t = new Turret(speed, gravityScale, 0.8f);
            var rng = new System.Random(5);
            // Buckets by running direction relative to the turret: away / toward / sideways.
            string[] names = { "away", "toward", "sideways" };
            var lead = names.Select(_ => new List<float>()).ToArray();
            var none = names.Select(_ => new List<float>()).ToArray();
            var flight = new List<float>();
            for (int i = 0; i < 3000; i++)
            {
                float ang = (float)(rng.NextDouble() * Math.PI * 2);
                float r = 6f + (float)rng.NextDouble() * 22f;
                var player = new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r);
                float runAng = (float)(rng.NextDouble() * Math.PI * 2);
                var dir = new Vector3(Mathf.Cos(runAng), 0, Mathf.Sin(runAng));
                var vel = dir * (3f + (float)rng.NextDouble() * 4f);
                float radial = Vector3.Dot(dir, player.normalized);
                int bucket = radial > 0.7f ? 0 : radial < -0.7f ? 1 : 2;

                foreach (bool useLead in new[] { true, false })
                {
                    var aim = useLead ? t.Lead(player, vel) : player;
                    t.AimAt(aim);
                    var hit = Fly(t.Muzzle.position, t.Muzzle.forward * speed, gravityScale, 0f);
                    if (hit == null) continue;
                    if (useLead) flight.Add(hit.Value.t);
                    var playerAtImpact = player + vel * hit.Value.t;
                    float miss = new Vector2(hit.Value.p.x - playerAtImpact.x, hit.Value.p.z - playerAtImpact.z).magnitude;
                    (useLead ? lead : none)[bucket].Add(miss);
                }
            }
            var sb = new System.Text.StringBuilder($"speed {speed} g×{gravityScale}: flight p50 {Percentile(flight, 0.5f):0.00}s p95 {Percentile(flight, 0.95f):0.00}s |");
            for (int b = 0; b < names.Length; b++)
            {
                float hitL = lead[b].Count(m => m <= 1.5f) / (float)Mathf.Max(1, lead[b].Count);
                float hitN = none[b].Count(m => m <= 1.5f) / (float)Mathf.Max(1, none[b].Count);
                sb.Append($" {names[b]}: miss p50 lead {Percentile(lead[b], 0.5f):0.0} / none {Percentile(none[b], 0.5f):0.0} m, in-blast lead {hitL:P0} / none {hitN:P0} |");
            }
            TestContext.WriteLine(sb.ToString());
            Debug.Log("[ArcStress] " + sb);
            var allLead = lead.SelectMany(x => x).ToList();
            var allNone = none.SelectMany(x => x).ToList();
            Assert.Less(Percentile(allLead, 0.5f), Percentile(allNone, 0.5f), "leading should beat aiming at the current position");
        }
    }
}

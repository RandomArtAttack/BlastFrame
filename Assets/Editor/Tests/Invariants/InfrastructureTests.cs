using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using BlastFrame.Core.Save;
using BlastFrame.Core.Seeding;
using BlastFrame.Gameplay.Areas;
using BlastFrame.Gameplay.Rooms;

namespace BlastFrame.Tests.Invariants
{
    /// <summary>SeedService, VariantSetSO, AreaGraph and the save codec (DESIGN.md §6–8, §11).</summary>
    public class InfrastructureTests
    {
        private readonly List<Object> _made = new List<Object>();
        private T Make<T>() where T : ScriptableObject { var o = ScriptableObject.CreateInstance<T>(); _made.Add(o); return o; }

        [TearDown]
        public void TearDown() { foreach (var o in _made) Object.DestroyImmediate(o); _made.Clear(); }

        // ------------------------------------------------------------------ seeds

        [Test]
        public void Seed_GoldenValues_NeverChange()
        {
            // If this fails, someone changed SeedService.Hash: every saved run / shared seed would roll different
            // content. Only update these numbers on purpose (and bump a save version).
            Assert.AreEqual(Golden(12345, "lava_works", 3, "layout"), SeedService.Hash(12345, "lava_works", 3, "layout"));
            var expected = new uint[] { SeedService.Hash(0, "", 0, ""), SeedService.Hash(-1, "a", 1, "b"), SeedService.Hash(int.MaxValue, "area", 8, "slot") };
            TestContext.WriteLine("golden: " + string.Join(", ", expected));
            CollectionAssert.AreEqual(GoldenTable, expected, "SeedService.Hash output changed — see comment");
        }

        // Pinned on 2026-11-07 when SeedService was introduced.
        private static readonly uint[] GoldenTable = { GoldenPin0, GoldenPin1, GoldenPin2 };
        private const uint GoldenPin0 = 1634255244u, GoldenPin1 = 1373967994u, GoldenPin2 = 3536981047u;
        private static uint Golden(int s, string a, int r, string b) => SeedService.Hash(s, a, r, b);

        [Test]
        public void Seed_DifferentInputs_DifferentRolls()
        {
            var seen = new HashSet<uint>();
            int collisions = 0;
            for (int seed = 0; seed < 50; seed++)
            for (int room = 0; room < 20; room++)
            foreach (var slot in new[] { "layout", "spawnA", "spawnB", "variant" })
                if (!seen.Add(SeedService.Hash(seed, "area", room, slot))) collisions++;
            Assert.LessOrEqual(collisions, 1, "4000 distinct inputs should essentially never collide in 32 bits");
            Assert.AreNotEqual(SeedService.Hash(1, "ab", 0, "c"), SeedService.Hash(1, "a", 0, "bc"), "id boundary must matter");
        }

        [Test]
        public void Seed_RangeIsUniform()
        {
            var counts = new int[7];
            for (int i = 0; i < 70_000; i++) counts[SeedService.RangeFrom(SeedService.Hash(i, "a", 0, "s"), 7)]++;
            foreach (int c in counts) Assert.That(c, Is.InRange(9_500, 10_500), string.Join(",", counts));
            Assert.AreEqual(0, SeedService.RangeFrom(uint.MaxValue, 1));
            Assert.AreEqual(6, SeedService.RangeFrom(uint.MaxValue, 7));
        }

        [Test]
        public void SeedService_SameRunSeed_SameContent()
        {
            var a = new SeedService(777);
            var b = new SeedService(777);
            for (int r = 0; r < 30; r++) Assert.AreEqual(a.Range("x", r, "layout", 3), b.Range("x", r, "layout", 3));
            float v = a.Value01("x", 0, "y");
            Assert.That(v, Is.GreaterThanOrEqualTo(0f).And.LessThan(1f));
        }

        // ------------------------------------------------------------------ weighted sets

        private RoomVariantSetSO Set(params (RoomVariantSO v, float w)[] e)
        {
            var s = Make<RoomVariantSetSO>();
            s.EditorSetEntries(e.Select(x => new WeightedEntry<RoomVariantSO> { item = x.v, weight = x.w }).ToList());
            return s;
        }

        [Test]
        public void VariantSet_RespectsWeights_SkipsZeroAndNull()
        {
            var a = Make<RoomVariantSO>(); var b = Make<RoomVariantSO>(); var c = Make<RoomVariantSO>();
            var set = Set((a, 1f), (b, 3f), (c, 0f), (null, 5f));
            int na = 0, nb = 0;
            for (int i = 0; i < 40_000; i++)
            {
                var p = set.Pick(SeedService.Hash(i, "w", 0, "s"));
                Assert.AreNotSame(c, p, "weight 0 must never be picked");
                if (p == a) na++; else if (p == b) nb++;
            }
            Assert.AreEqual(40_000, na + nb, "null items must never be picked");
            Assert.That(nb / (double)na, Is.InRange(2.8, 3.2), $"a {na} b {nb}");
        }

        [Test]
        public void VariantSet_EmptyReturnsNull_SameRollSamePick()
        {
            Assert.IsNull(Set().Pick(123u));
            Assert.IsNull(Set((Make<RoomVariantSO>(), 0f)).Pick(123u));
            var a = Make<RoomVariantSO>(); var b = Make<RoomVariantSO>();
            var set = Set((a, 1f), (b, 1f));
            var seeds = new SeedService(42);
            Assert.AreSame(set.Pick(seeds, "area", 2, "layout"), set.Pick(seeds, "area", 2, "layout"));
            Assert.IsNotNull(set.Pick(uint.MaxValue));
            Assert.IsNotNull(set.Pick(0u));
        }

        // ------------------------------------------------------------------ area graph

        private AreaDefinitionSO Area(string id, bool start, params AreaEffect[] fx)
        {
            var a = Make<AreaDefinitionSO>();
            a.EditorInit(id, start, fx.ToList());
            var so = new UnityEditor.SerializedObject(a);
            so.FindProperty("sceneName").stringValue = "Scene_" + id;
            so.ApplyModifiedPropertiesWithoutUndo();
            return a;
        }

        private static AreaEffect Fx(AreaEffectType t, string target, string key = null, float amount = 0f) =>
            new AreaEffect { type = t, targetAreaId = target, key = key, amount = amount };

        [Test]
        public void AreaGraph_DerivesUnlocksAndAlterations()
        {
            var lava = Area("lava", true, Fx(AreaEffectType.UnlockArea, "ice"), Fx(AreaEffectType.AlterSpawnTable, "ice", "melting"),
                                         Fx(AreaEffectType.ApplyBossWeakness, "ice", "flame_gun", 2.5f), Fx(AreaEffectType.PriceCut, null, null, 10f));
            var ice = Area("ice", false, Fx(AreaEffectType.OpenShortcut, "lava", "frozen_bridge"));
            var sky = Area("sky", true);
            var areas = new List<AreaDefinitionSO> { lava, ice, sky };

            var fresh = AreaGraph.Derive(areas, new HashSet<string>());
            Assert.IsTrue(fresh.Get("lava").Unlocked);
            Assert.IsFalse(fresh.Get("ice").Unlocked);
            Assert.AreEqual(0f, fresh.PriceCutPercent);

            var afterLava = AreaGraph.Derive(areas, new HashSet<string> { "lava" });
            var iceState = afterLava.Get("ice");
            Assert.IsTrue(iceState.Unlocked);
            CollectionAssert.Contains(iceState.UnlockedBy, "lava");
            CollectionAssert.Contains(iceState.Alterations, "melting");
            Assert.AreEqual(2.5f, iceState.BossWeaknesses["flame_gun"]);
            Assert.AreEqual(10f, afterLava.PriceCutPercent);
            Assert.IsEmpty(afterLava.Get("lava").Shortcuts, "ice not completed yet");

            var both = AreaGraph.Derive(areas, new HashSet<string> { "lava", "ice" });
            CollectionAssert.Contains(both.Get("lava").Shortcuts, "frozen_bridge");
        }

        [Test]
        public void AreaGraph_UnknownCompletedIdsAreIgnored_PriceCutClamped()
        {
            var greedy = Area("a", true, Fx(AreaEffectType.PriceCut, null, null, 70f));
            var greedy2 = Area("b", true, Fx(AreaEffectType.PriceCut, null, null, 70f));
            var w = AreaGraph.Derive(new List<AreaDefinitionSO> { greedy, greedy2 }, new HashSet<string> { "a", "b", "deleted_area" });
            Assert.AreEqual(AreaGraph.MaxPriceCutPercent, w.PriceCutPercent);
            Assert.IsNull(w.Get("deleted_area"));
        }

        [Test]
        public void AreaGraph_Validate_FindsAuthoringMistakes()
        {
            var a = Area("a", true, Fx(AreaEffectType.UnlockArea, "missing"), Fx(AreaEffectType.AlterSpawnTable, "a", "x"),
                                    Fx(AreaEffectType.OpenShortcut, "b"));
            var b = Area("b", false);                 // nothing unlocks b
            var dup = Area("a", false);
            var problems = AreaGraph.Validate(new List<AreaDefinitionSO> { a, b, dup });
            foreach (var p in problems) TestContext.WriteLine(p);
            Assert.That(problems, Has.Some.Contains("unknown target 'missing'"));
            Assert.That(problems, Has.Some.Contains("targets itself"));
            Assert.That(problems, Has.Some.Contains("needs a key"));
            Assert.That(problems, Has.Some.Contains("duplicate id"));
            Assert.That(problems, Has.Some.Contains("b: can never be unlocked"));
        }

        [Test]
        public void AreaGraph_Validate_CleanGraphHasNoProblems()
        {
            var a = Area("a", true, Fx(AreaEffectType.UnlockArea, "b"));
            var b = Area("b", false, Fx(AreaEffectType.UnlockArea, "c"), Fx(AreaEffectType.ApplyBossWeakness, "c", "spark", 2f));
            var c = Area("c", false);
            Assert.IsEmpty(AreaGraph.Validate(new List<AreaDefinitionSO> { a, b, c }));
        }

        // ------------------------------------------------------------------ save

        [Test]
        public void Save_RoundTripsThroughEncryption()
        {
            var d = new SaveData { metaCurrency = 321, invertY = true };
            d.completedAreaIds.Add("lava");
            d.unlockedAbilityIds.Add("grapple");
            d.runState = new RunSaveData { runSeed = -99, currentAreaId = "ice", currentRoomIndex = 4, currentHealth = 2 };
            var back = SaveManager.Decode(SaveManager.Encode(d));
            Assert.AreEqual(321, back.metaCurrency);
            Assert.IsTrue(back.invertY);
            CollectionAssert.AreEquivalent(new[] { "lava" }, back.completedAreaIds);
            CollectionAssert.AreEquivalent(new[] { "grapple" }, back.unlockedAbilityIds);
            Assert.AreEqual(-99, back.runState.runSeed);
            Assert.AreEqual("ice", back.runState.currentAreaId);
        }

        [Test]
        public void Save_OldFormatStillLoads()
        {
            // A pre-§11 save: has the retired linear fields, lacks the new ones.
            const string oldJson = "{\"metaCurrency\":50,\"purchasedPermanentIds\":[\"hp_up\"],\"unlockedLevelIndex\":3," +
                                   "\"unlockedWeaponIds\":[],\"runState\":{\"currentLevelIndex\":2,\"currentRoomIndex\":1,\"difficulty\":1," +
                                   "\"currentHealth\":4,\"activeRunPowerupIds\":[]},\"totalRuns\":7}";
            var d = Newtonsoft.Json.JsonConvert.DeserializeObject<SaveData>(oldJson);
            Assert.AreEqual(50, d.metaCurrency);
            Assert.AreEqual(7, d.totalRuns);
            Assert.IsNotNull(d.completedAreaIds, "new collections must not be null after loading an old save");
            Assert.IsNotNull(d.unlockedAbilityIds);
            Assert.AreEqual(1, d.runState.currentRoomIndex);
        }

        [Test]
        public void Save_CorruptBytesThrow_NotSilentlyEmpty()
        {
            Assert.Catch(() => SaveManager.Decode(new byte[] { 1, 2, 3, 4, 5 }));
        }
    }
}

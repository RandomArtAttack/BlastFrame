using NUnit.Framework;
using UnityEngine;
using BlastFrame.EditorTools.LevelDesign;
using BlastFrame.Gameplay.Rooms;

namespace BlastFrame.Tests.Movement
{
    /// <summary>
    /// Builds a synthetic course whose answers follow from the measured profile and checks the analyzer
    /// classifies each platform correctly. Uses the cached profile if it matches current tuning, else measures.
    /// </summary>
    [TestFixture]
    public class ReachabilityTests
    {
        private ReachProfile _p;
        private IsolatedSimScene _scene;
        private GameObject _root;

        [OneTimeSetUp]
        public void Setup()
        {
            _p = ReachMeasureMenu.LoadFresh(out bool stale);
            if (_p == null || stale || PlayerTemplate.Capture().Hash != _p.templateHash)
                _p = ReachMeasureMenu.MeasureAndCache(interactive: false);
            _scene = IsolatedSimScene.TryEnter(interactive: false);
        }

        [OneTimeTearDown]
        public void Teardown() => _scene?.Dispose();

        [SetUp]
        public void Clear()
        {
            if (_root != null) Object.DestroyImmediate(_root);
            _root = new GameObject("Course");
        }

        private GameObject Plat(string name, float x0, float x1, float top, float z = 3f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root.transform, false);
            go.transform.position = new Vector3((x0 + x1) * 0.5f, top - 1f, 0f);
            go.AddComponent<BoxCollider>().size = new Vector3(x1 - x0, 2f, z * 2f);
            return go;
        }

        private ReachClass ClassAt(ReachResult r, float x, float y)
        {
            int i = ReachabilityAnalyzer.NearestIsland(r, new Vector3(x, y, 0f), 1.5f);
            Assert.GreaterOrEqual(i, 0, $"no island near ({x},{y})");
            return r.Islands[i].Class;
        }

        [Test]
        public void ClassifiesGapsByMeasuredBudget()
        {
            float jump = _p.Get(MoveKind.RunJump).GapAt(0f, true);
            float dash = _p.Get(MoveKind.DashJump).GapAt(0f, true);
            Assume.That(dash > jump + 3f, "profile should separate jump and dash reach");

            Plat("Start", -20f, 0f, 0f);
            float bStart = jump * 0.6f;                 // easy jump
            Plat("B", bStart, bStart + 10f, 0f);
            float cStart = bStart + 10f + (jump + dash) * 0.5f; // needs dash
            Plat("C", cStart, cStart + 10f, 0f);
            float dStart = cStart + 10f + dash + 4f;    // beyond everything
            Plat("D", dStart, dStart + 10f, 0f);

            var r = ReachabilityAnalyzer.Analyze(_p, 0.5f, new Vector3(-10f, 0f, 0f), "test start");
            Assert.AreEqual(ReachClass.Walk, ClassAt(r, -10f, 0f));
            Assert.AreEqual(ReachClass.Jump, ClassAt(r, bStart + 5f, 0f));
            Assert.That(ClassAt(r, cStart + 5f, 0f), Is.EqualTo(ReachClass.DashJump).Or.EqualTo(ReachClass.PerfectDashJump));
            Assert.AreEqual(ReachClass.Unreachable, ClassAt(r, dStart + 5f, 0f));
        }

        [Test]
        public void LedgeClimbRespectsMaxLedgeHeight()
        {
            Plat("Floor", -10f, 10f, 0f);
            Plat("Low", 10f, 20f, _p.maxLedgeHeight - 0.5f);
            Plat("TooHigh", -20f, -10f, _p.maxLedgeHeight + 1.5f);
            var r = ReachabilityAnalyzer.Analyze(_p, 0.5f, new Vector3(0f, 0f, 0f), "floor");
            Assert.AreEqual(ReachClass.Jump, ClassAt(r, 15f, _p.maxLedgeHeight - 0.5f));
            Assert.AreEqual(ReachClass.Unreachable, ClassAt(r, -15f, _p.maxLedgeHeight + 1.5f));
        }

        [Test]
        public void StepUpIsWalkable()
        {
            Plat("Floor", -10f, 0f, 0f);
            Plat("Step", 0f, 10f, Mathf.Min(0.3f, _p.maxStepHeight * 0.5f));
            var r = ReachabilityAnalyzer.Analyze(_p, 0.5f, new Vector3(-5f, 0f, 0f), "floor");
            Assert.AreEqual(ReachClass.Walk, ClassAt(r, 5f, 0.3f));
        }

        [Test]
        public void MarkerReportsItsClass()
        {
            Plat("Start", -20f, 0f, 0f);
            float far = _p.Get(MoveKind.DashJump).GapAt(0f, false) + 6f;
            Plat("Island", far, far + 6f, 0f);
            var entry = new GameObject("Entry").AddComponent<RoomMarker>();
            entry.transform.SetParent(_root.transform);
            entry.transform.position = new Vector3(-10f, 0f, 0f);
            SetKind(entry, RoomMarkerKind.Entry);
            var secret = new GameObject("Secret").AddComponent<RoomMarker>();
            secret.transform.SetParent(_root.transform);
            secret.transform.position = new Vector3(far + 3f, 0f, 0f);

            var r = ReachabilityAnalyzer.Analyze(_p, 0.5f, null, "");
            StringAssert.Contains("Entry", r.StartDescription);
            bool found = false;
            foreach (var (m, _, cls) in r.Markers)
                if (m == secret) { found = true; Assert.AreEqual(ReachClass.Unreachable, cls); }
            Assert.IsTrue(found);
        }

        [Test]
        public void HazardSurfaceIsNotAnIsland()
        {
            Plat("Floor", -10f, 10f, 0f);
            var lava = new GameObject("Lava");
            lava.transform.SetParent(_root.transform);
            lava.transform.position = new Vector3(5f, 0.5f, 0f);
            var trig = lava.AddComponent<BoxCollider>();
            trig.isTrigger = true;
            trig.size = new Vector3(4f, 1f, 6f);
            lava.AddComponent<BlastFrame.Gameplay.Environment.LavaHazardVolume>();
            var r = ReachabilityAnalyzer.Analyze(_p, 0.5f, new Vector3(-5f, 0f, 0f), "floor");
            Assert.Greater(r.HazardNodes, 0);
        }

        private static void SetKind(RoomMarker m, RoomMarkerKind k)
        {
            var so = new UnityEditor.SerializedObject(m);
            so.FindProperty("kind").enumValueIndex = (int)k;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}

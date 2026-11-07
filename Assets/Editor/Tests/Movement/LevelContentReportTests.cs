using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using BlastFrame.EditorTools.LevelDesign;

namespace BlastFrame.Tests.Movement
{
    /// <summary>
    /// Runs the level-design lints over the project's real content. Room data errors fail; scene reachability is
    /// reported (logged) rather than asserted, because decorative unreachable surfaces are legitimate.
    /// </summary>
    [TestFixture, Category("Report")]
    public class LevelContentReportTests
    {
        [Test]
        public void RoomVariants_HaveNoDataErrors()
        {
            var issues = RoomVariantLint.Run(interactive: false);
            foreach (var i in issues) TestContext.WriteLine($"{i.Severity}: {i.Message}");
            var errors = issues.Where(i => i.Severity == LintSeverity.Error).Select(i => i.Message).ToList();
            Assert.IsEmpty(errors, string.Join("\n", errors));
        }

        [TestCase("Assets/Scenes/Lava World/Area1LavaBridge.unity")]
        [TestCase("Assets/Scenes/Sandbox.unity")]
        [TestCase("Assets/Scenes/TestLevel.unity")]
        [Timeout(900000)]
        public void SceneReachability_Report(string path)
        {
            var profile = ReachMeasureMenu.LoadFresh(out bool stale);
            if (profile == null || stale) profile = ReachMeasureMenu.MeasureAndCache(interactive: false);

            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var r = ReachabilityAnalyzer.Analyze(profile, 0.5f, null, "");
                var area = new float[6];
                foreach (var isl in r.Islands) area[(int)isl.Class] += isl.Nodes.Count * 0.25f;
                var bad = r.Islands.Where(i => i.Class == ReachClass.Unreachable && i.Nodes.Count >= 8)
                    .OrderByDescending(i => i.Nodes.Count).Take(12)
                    .Select(i => $"#{i.Id} {i.Nodes.Count * 0.25f:0}m² @({i.Bounds.center.x:0},{i.Bounds.center.y:0},{i.Bounds.center.z:0}){(i.Dynamic ? " moving" : "")}");
                string report = $"[SceneReach] {path}\n start: {r.StartDescription}\n islands {r.Islands.Count}, samples {r.NodePos.Count}, hazard {r.HazardNodes}, dynamic colliders {r.DynamicColliders}, {r.Seconds:0.0}s [{r.PhaseTimes}]\n" +
                                $" m² by class: Walk {area[0]:0} | Jump {area[1]:0} | PerfectJump {area[2]:0} | Dash {area[3]:0} | PerfectDash {area[4]:0} | Unreachable {area[5]:0}\n" +
                                $" markers: {r.Markers.Count}\n largest unreachable (>=2m²): {string.Join("; ", bad)}";
                Debug.Log(report);
                TestContext.WriteLine(report);
                Assert.Greater(r.NodePos.Count, 0, "scene has no standable surfaces?");
            }
            finally
            {
                if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }
    }
}

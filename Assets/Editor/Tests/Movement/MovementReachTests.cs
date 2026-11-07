using NUnit.Framework;
using UnityEngine;
using BlastFrame.EditorTools.LevelDesign;

namespace BlastFrame.Tests.Movement
{
    /// <summary>
    /// Drives the REAL player motor + modules (values copied from Core.unity's Player) through gap/ledge courses.
    /// Budgets come from the CLAUDE.md glossary: standing long jump clears 7 units, dash jump clears 10.
    /// </summary>
    [TestFixture]
    public class MovementReachTests
    {
        private IsolatedSimScene _scene;
        private MotorSimRig _rig;

        [OneTimeSetUp]
        public void Setup()
        {
            _scene = IsolatedSimScene.TryEnter(interactive: false);
            _rig = new MotorSimRig(PlayerTemplate.Capture());
        }

        [OneTimeTearDown]
        public void Teardown()
        {
            _rig?.Dispose();
            _scene?.Dispose();
        }

        [Test]
        public void Rig_PlayerLandsOnFloor()
        {
            _rig.ClearArena();
            _rig.Box(new Vector3(-5, -4, -5), new Vector3(5, 0, 5));
            _rig.Spawn(new Vector3(0, 1.5f, 0), 90f);
            for (int i = 0; i < 60; i++) _rig.Step();
            Assert.IsTrue(_rig.Motor.IsGrounded, "player never grounded");
            Assert.That(_rig.Position.y, Is.InRange(-0.01f, 0.05f), "feet should rest on the floor top (y=0)");
        }

        [Test]
        public void Rig_WalksForward()
        {
            _rig.ClearArena();
            _rig.Box(new Vector3(-5, -4, -5), new Vector3(20, 0, 5));
            _rig.Spawn(new Vector3(0, 0.02f, 0), 90f);
            _rig.Input.Move = Vector2.up;
            for (int i = 0; i < 50; i++) _rig.Step(); // 1 s
            Assert.Greater(_rig.Position.x, 3f, "forward input should move the player along +X");
        }

        [Test]
        public void Budget_StandingLongJump_Clears7()
        {
            float gap = ReachMeasurer.SearchGap(_rig, MoveKind.StandingJump, 0f, 1);
            TestContext.WriteLine($"Standing jump flat gap: {gap:0.00} m");
            Assert.GreaterOrEqual(gap, 7f, "Glossary budget: standing long jump clears 7 units");
        }

        [Test]
        public void Budget_DashJump_Clears10()
        {
            float gap = ReachMeasurer.SearchGap(_rig, MoveKind.DashJump, 0f, ReachMeasurer.ComfortWindowTicks);
            TestContext.WriteLine($"Dash jump flat gap (comfortable): {gap:0.00} m");
            Assert.GreaterOrEqual(gap, 10f, "Glossary budget: dash jump clears 10 units");
        }

        [Test]
        public void DashJump_BeatsRunJump()
        {
            float run = ReachMeasurer.SearchGap(_rig, MoveKind.RunJump, 0f, 1);
            float dash = ReachMeasurer.SearchGap(_rig, MoveKind.DashJump, 0f, 1);
            TestContext.WriteLine($"run {run:0.00} dash {dash:0.00}");
            Assert.Greater(dash, run);
        }

        [Test]
        public void WallSlide_FallsSlowerThanFreeFall()
        {
            _rig.ClearArena();
            _rig.Box(new Vector3(1f, -20, -5), new Vector3(3f, 40, 5), "Wall");
            _rig.Spawn(new Vector3(1f - 0.4f - 0.05f, 20f, 0), 90f); // airborne, adjacent (not pushing)
            for (int i = 0; i < 50; i++) _rig.Step();
            float vy = _rig.Motor.Velocity.y;
            TestContext.WriteLine($"vy after 1 s beside wall: {vy:0.00}");
            Assert.IsTrue(_rig.Motor.IsOnWall, "adjacent wall should register without pushing into it (glossary)");
            Assert.Greater(vy, -5f, "wall slide should clamp fall speed");
        }

        [Test, Category("Slow")]
        public void Report_FullReachProfile()
        {
            var p = ReachMeasurer.MeasureAll(_rig);
            p.Save();
            TestContext.WriteLine(p.ToMarkdown());
            Debug.Log("[ReachReport]\n" + p.ToMarkdown());
            Assert.Greater(p.jumpApex, 0.5f);
        }
    }
}

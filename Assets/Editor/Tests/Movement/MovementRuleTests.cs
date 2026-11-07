using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using BlastFrame.EditorTools.LevelDesign;
using BlastFrame.Gameplay.Player;
using BlastFrame.Gameplay.Player.Movement;

namespace BlastFrame.Tests.Movement
{
    /// <summary>The CLAUDE.md glossary movement rulings, each driven through the real motor.</summary>
    [TestFixture]
    public class MovementRuleTests
    {
        private IsolatedSimScene _scene;
        private MotorSimRig _rig;
        private PlayerStats Stats => _rig.Player.GetComponent<PlayerStats>();
        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        [OneTimeSetUp]
        public void Setup()
        {
            _scene = IsolatedSimScene.TryEnter(false);
            _rig = new MotorSimRig(PlayerTemplate.Capture());
        }

        [OneTimeTearDown]
        public void Teardown() { _rig?.Dispose(); _scene?.Dispose(); }

        private void Floor(float x1 = 60f)
        {
            _rig.ClearArena();
            _rig.Box(new Vector3(-20, -4, -10), new Vector3(x1, 0, 10), "Floor");
        }

        private void Steps(int n) { for (int i = 0; i < n; i++) _rig.Step(); }

        [Test]
        public void Dash_IsHeld_ReleaseEndsItImmediately()
        {
            Floor();
            _rig.Spawn(new Vector3(0, 0.02f, 0), 90f);
            _rig.Input.Move = Vector2.zero;
            _rig.Step();
            _rig.Input.PressDash();
            Steps(5);
            var dash = _rig.Player.GetComponent<DashModule>();
            Assert.IsTrue(dash.IsDashing, "dash should be active while held");
            Assert.Greater(Flat(_rig.Motor.Velocity).magnitude, Stats.MoveSpeed + 1f);
            _rig.Input.ReleaseDash();
            _rig.Step();
            Assert.IsFalse(dash.IsDashing, "releasing dash must end it at once (glossary: held-button dash)");
        }

        [Test]
        public void CoyoteJump_FiresJustAfterLeavingLedge_NotLater()
        {
            Floor();
            _rig.Spawn(new Vector3(0, 0.02f, 0), 90f);
            float coyote = Stats.CoyoteTime;
            int coyoteTicks = Mathf.FloorToInt(coyote / _rig.Dt);
            Assume.That(coyoteTicks, Is.GreaterThanOrEqualTo(2), "coyote window shorter than 2 ticks");

            bool JumpsAfter(int ticksAfterLeaving)
            {
                _rig.ClearArena();
                _rig.Box(new Vector3(-20, -4, -10), new Vector3(0, 0, 10), "Ledge");
                _rig.Spawn(new Vector3(-6, 0.02f, 0), 90f);
                _rig.Input.Move = Vector2.up;
                int left = -1;
                for (int i = 0; i < 200; i++)
                {
                    _rig.Step();
                    if (left < 0 && !_rig.Motor.IsGrounded && _rig.Position.x > -1f) left = _rig.Tick;
                    if (left >= 0 && _rig.Tick == left + ticksAfterLeaving) { _rig.Input.PressJump(); _rig.Step(); _rig.Step(); return _rig.Motor.Velocity.y > 1f; }
                }
                return false;
            }

            Assert.IsTrue(JumpsAfter(1), "jump 1 tick after leaving the ledge should fire (coyote)");
            Assert.IsTrue(JumpsAfter(coyoteTicks - 1), $"jump inside the {coyote:0.00}s coyote window should fire");
            Assert.IsFalse(JumpsAfter(coyoteTicks + 3), "jump well after the coyote window must not fire");
        }

        [Test]
        public void WallJump_LaunchesAwayWithoutInput()
        {
            _rig.ClearArena();
            _rig.Box(new Vector3(1f, -20, -10), new Vector3(3f, 40, 10), "Wall");
            _rig.Spawn(new Vector3(1f - 0.45f, 10f, 0), 90f);
            _rig.Input.Move = Vector2.zero;
            Steps(10);
            Assert.IsTrue(_rig.Motor.IsOnWall, "should be wall-adjacent");
            _rig.Input.PressJump();
            Steps(2);
            Assert.Less(_rig.Motor.Velocity.x, -2f, "wall jump must push away from the wall (-X) with no stick input");
            Assert.Greater(_rig.Motor.Velocity.y, 2f, "wall jump must go up");
        }

        [Test]
        public void DashWallJump_LaunchesAtDashSpeed()
        {
            _rig.ClearArena();
            _rig.Box(new Vector3(1f, -20, -10), new Vector3(3f, 40, 10), "Wall");
            _rig.Spawn(new Vector3(1f - 0.45f, 10f, 0), 90f);
            Steps(10);
            _rig.Input.PressDash();
            Steps(2);
            Assert.AreEqual(Stats.DashSpeed, Mathf.Abs(_rig.Motor.Velocity.x), 0.6f, "dash wall-jump launches at dash speed (glossary)");
        }

        [Test]
        public void DashJump_HoldingForwardDoesNotDecelerate_BackBrakes()
        {
            float SpeedAfter(Vector2 airInput)
            {
                Floor(200f);
                _rig.Spawn(new Vector3(0, 0.02f, 0), 90f);
                _rig.Input.Move = Vector2.up;
                _rig.Step();
                _rig.Input.PressDash();
                Steps(10);
                _rig.Input.PressJump();
                Steps(3);
                Assert.IsFalse(_rig.Motor.IsGrounded, "should be airborne after the dash jump");
                _rig.Input.ReleaseDash();
                _rig.Input.Move = airInput;
                Steps(15);
                return _rig.Motor.Velocity.x;
            }

            float fwd = SpeedAfter(Vector2.up);
            float back = SpeedAfter(Vector2.down);
            TestContext.WriteLine($"after 0.3s air: forward {fwd:0.00}, back {back:0.00}, dash {Stats.DashSpeed:0.00}");
            Assert.GreaterOrEqual(fwd, Stats.DashSpeed - 0.05f, "holding forward after a dash jump must not bleed speed (glossary)");
            Assert.Less(back, fwd - 1f, "pressing back must brake");
        }

        [Test]
        public void StepUp_ClimbsLowLedgeWithoutJumping()
        {
            float step = Mathf.Min(0.3f, new SerializedObject(_rig.Player.GetComponent<PlayerMotor>()).FindProperty("maxStepHeight").floatValue * 0.8f);
            Assume.That(step, Is.GreaterThan(0.05f));
            Floor();
            _rig.Box(new Vector3(3, 0, -10), new Vector3(60, step, 10), "Step");
            _rig.Spawn(new Vector3(0, 0.02f, 0), 90f);
            _rig.Input.Move = Vector2.up;
            Steps(60);
            Assert.Greater(_rig.Position.x, 5f, "should walk onto the step, not stop at it");
            Assert.AreEqual(step, _rig.Position.y, 0.06f);
        }

        [Test]
        public void VariableJump_EarlyReleaseIsLower()
        {
            float Apex(int releaseTick)
            {
                Floor();
                _rig.Spawn(new Vector3(0, 0.02f, 0), 90f);
                _rig.Step();
                _rig.Input.PressJump();
                float apex = 0f;
                for (int i = 0; i < 120; i++)
                {
                    if (i == releaseTick) _rig.Input.ReleaseJump();
                    _rig.Step();
                    apex = Mathf.Max(apex, _rig.Position.y);
                }
                return apex;
            }
            float full = Apex(1000), tap = Apex(3);
            TestContext.WriteLine($"full {full:0.00} m, tap {tap:0.00} m");
            Assert.Less(tap, full * 0.75f, "releasing jump early must cut the jump short");
        }

        [Test]
        public void Gravity_ExactlyMatchesStatsOnFlatJump()
        {
            Floor();
            _rig.Spawn(new Vector3(0, 0.02f, 0), 90f);
            _rig.Step();
            _rig.Input.PressJump();
            float apex = 0f;
            for (int i = 0; i < 150; i++) { _rig.Step(); apex = Mathf.Max(apex, _rig.Position.y); }
            float expected = Stats.JumpForce * Stats.JumpForce / (2f * Stats.Gravity);
            Assert.AreEqual(expected, apex, expected * 0.06f, "apex should match v²/2g within discretisation");
        }
    }
}

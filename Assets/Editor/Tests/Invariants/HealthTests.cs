using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using BlastFrame.Gameplay;
using BlastFrame.Gameplay.Player;

namespace BlastFrame.Tests.Invariants
{
    /// <summary>PlayerHealth contract: start 5, clamp at 0, death fires once, i-frames, heal clamps.</summary>
    public class HealthTests
    {
        private GameObject _go;
        private PlayerHealth _h;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("Health");
            _h = _go.AddComponent<PlayerHealth>();
            SetInvuln(0f);
            typeof(PlayerHealth).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_h, null);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_go);

        private void SetInvuln(float s)
        {
            var so = new SerializedObject(_h);
            var p = so.FindProperty("invulnerabilitySeconds");
            p.FindPropertyRelative("UseConstant").boolValue = true;
            p.FindPropertyRelative("ConstantValue").floatValue = s;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        [Test] public void StartsAtFive() { Assert.AreEqual(5, _h.Current); Assert.AreEqual(5, _h.Max); }

        [Test]
        public void DamageClampsAtZero_DeathFiresOnce()
        {
            int deaths = 0;
            _h.OnDeath += () => deaths++;
            _h.TakeDamage(3);
            _h.TakeDamage(10);
            _h.TakeDamage(1);
            Assert.AreEqual(0, _h.Current);
            Assert.IsTrue(_h.IsDead);
            Assert.AreEqual(1, deaths);
        }

        [Test]
        public void NonPositiveDamageIgnored()
        {
            _h.TakeDamage(0);
            _h.TakeDamage(-4);
            Assert.AreEqual(5, _h.Current);
        }

        [Test]
        public void InvulnerabilityBlocksImmediateSecondHit()
        {
            SetInvuln(5f);
            _h.TakeDamage(1);
            _h.TakeDamage(1);
            Assert.AreEqual(4, _h.Current);
        }

        [Test]
        public void HealClampsToMax_DeadCannotHeal()
        {
            _h.TakeDamage(2);
            _h.Heal(10);
            Assert.AreEqual(5, _h.Current);
            _h.TakeDamage(5);
            _h.Heal(3);
            Assert.AreEqual(0, _h.Current);
        }

        [Test]
        public void DamageInfoReachesListeners()
        {
            DamageInfo got = default;
            _h.OnDamagedInfo += i => got = i;
            _h.TakeDamage(new DamageInfo(2, new Vector3(1, 2, 3), DamageKind.Explosion));
            Assert.AreEqual(DamageKind.Explosion, got.Kind);
            Assert.IsTrue(got.HasSource);
            Assert.AreEqual(new Vector3(1, 2, 3), got.SourcePosition);
        }
    }
}

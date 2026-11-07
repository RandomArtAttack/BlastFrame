using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using BlastFrame.Core;
using BlastFrame.Core.Services;
using BlastFrame.Gameplay.Enemies;
using BlastFrame.Gameplay.Projectiles;

namespace BlastFrame.Tests.Invariants
{
    /// <summary>
    /// Glossary "Turret Shooter" / "Turret projectile (tuned)": barrel always pitched ≥45°, projectile leaves
    /// along the barrel at a constant speed and never inherits turret/player velocity.
    /// </summary>
    public class ArcTurretTests
    {
        private const BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        internal sealed class CapturePool : IPoolManager
        {
            public GameObject Last;
            public GameObject Spawn(string poolId, Vector3 position, Quaternion rotation)
            {
                Last = new GameObject("Spawned_" + poolId);
                Last.transform.SetPositionAndRotation(position, rotation);
                Last.AddComponent<Rigidbody>().isKinematic = true;
                if (poolId == PoolIds.ArcProjectile) Last.AddComponent<ArcProjectile>();
                return Last;
            }
            public void Despawn(GameObject instance) => UnityEngine.Object.DestroyImmediate(instance);
        }

        private GameObject _turret;
        private EnemyBehaviorArcPredict _arc;
        private Transform _muzzle;

        internal static void Call(object target, string name, params object[] args)
        {
            for (var t = target.GetType(); t != null; t = t.BaseType)
            {
                var m = t.GetMethod(name, Any | BindingFlags.DeclaredOnly);
                if (m == null) continue;
                m.Invoke(target, args);
                return;
            }
            throw new MissingMethodException(target.GetType().Name, name);
        }

        internal static T Get<T>(object target, string field)
        {
            for (var t = target.GetType(); t != null; t = t.BaseType)
            {
                var f = t.GetField(field, Any | BindingFlags.DeclaredOnly);
                if (f != null) return (T)f.GetValue(target);
            }
            throw new MissingFieldException(target.GetType().Name, field);
        }

        [SetUp]
        public void SetUp()
        {
            _turret = new GameObject("ArcTurret");
            _turret.AddComponent<EnemyCore>();
            var stats = _turret.AddComponent<EnemyStats>();
            _arc = _turret.AddComponent<EnemyBehaviorArcPredict>();
            var barrel = new GameObject("Barrel").transform;
            barrel.SetParent(_turret.transform, false);
            barrel.localPosition = new Vector3(0, 1.5f, 0);
            _muzzle = new GameObject("Muzzle").transform;
            _muzzle.SetParent(barrel, false);
            _muzzle.localPosition = new Vector3(0, 0, 0.8f);

            var so = new SerializedObject(_arc);
            so.FindProperty("muzzle").objectReferenceValue = _muzzle;
            so.FindProperty("barrelPivot").objectReferenceValue = barrel;
            so.ApplyModifiedPropertiesWithoutUndo();
            var sso = new SerializedObject(stats);
            sso.FindProperty("projectileSpeed").FindPropertyRelative("ConstantValue").floatValue = 12f;
            sso.ApplyModifiedPropertiesWithoutUndo();

            Call(_turret.GetComponent<EnemyCore>(), "Awake");
            Call(_arc, "Awake");
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(_turret);

        private float AngleDeg(Vector3 target)
        {
            var m = typeof(EnemyBehaviorArcPredict).GetMethod("ComputeLaunchAngle", Any);
            return (float)m.Invoke(_arc, new object[] { target }) * Mathf.Rad2Deg;
        }

        [Test]
        public void LaunchAngle_NeverBelow45_AcrossRandomTargets()
        {
            var rng = new System.Random(1234);
            for (int i = 0; i < 5000; i++)
            {
                var t = new Vector3((float)(rng.NextDouble() * 80 - 40), (float)(rng.NextDouble() * 30 - 15), (float)(rng.NextDouble() * 80 - 40));
                float a = AngleDeg(t);
                Assert.That(a, Is.GreaterThanOrEqualTo(45f - 1e-3f), $"target {t}");
                Assert.That(a, Is.LessThanOrEqualTo(90f + 1e-3f), $"target {t}");
            }
        }

        [Test]
        public void Fire_UsesBarrelDirection_ConstantSpeed_NoInheritedVelocity()
        {
            var pool = new CapturePool();
            typeof(EnemyBehaviorArcPredict).GetField("_pool", Any).SetValue(_arc, pool);
            // A fast-moving player sample must not leak into the shot.
            typeof(EnemyBehaviorArcPredict).GetField("_playerVelocity", Any).SetValue(_arc, new Vector3(30, 5, -20));
            _muzzle.parent.localEulerAngles = new Vector3(-60f, 0f, 0f);
            _turret.transform.rotation = Quaternion.Euler(0, 33, 0);

            Call(_arc, "Fire", new Vector3(20, 0, 10));
            Assert.IsNotNull(pool.Last, "turret did not fire");
            var proj = pool.Last.GetComponent<ArcProjectile>();
            var v = Get<Vector3>(proj, "_velocity");
            Assert.AreEqual(12f, v.magnitude, 1e-3f, "launch speed must equal EnemyStats.ProjectileSpeed");
            Assert.Less(Vector3.Angle(v, _muzzle.forward), 0.01f, "projectile must leave along the barrel");
            Assert.AreEqual(_muzzle.position, pool.Last.transform.position, "spawns at the muzzle");
            UnityEngine.Object.DestroyImmediate(pool.Last);
        }

        [Test]
        public void Fire_TooClose_DoesNotFire()
        {
            var pool = new CapturePool();
            typeof(EnemyBehaviorArcPredict).GetField("_pool", Any).SetValue(_arc, pool);
            Call(_arc, "Fire", _muzzle.position + new Vector3(0.5f, -1f, 0f));
            Assert.IsNull(pool.Last);
        }
    }
}

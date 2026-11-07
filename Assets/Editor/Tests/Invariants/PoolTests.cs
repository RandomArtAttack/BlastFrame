using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using BlastFrame.Core.Pooling;

namespace BlastFrame.Tests.Invariants
{
    /// <summary>Generic Pool&lt;T&gt; contract (CLAUDE.md "Object Pooling").</summary>
    public class PoolTests
    {
        private sealed class Probe : MonoBehaviour, IPoolable
        {
            public int Spawns, Despawns;
            public void OnSpawn() => Spawns++;
            public void OnDespawn() => Despawns++;
        }

        private GameObject _prefab;
        private Transform _holder;

        [SetUp]
        public void SetUp()
        {
            _prefab = new GameObject("PoolPrefab");
            _prefab.AddComponent<Probe>();
            _holder = new GameObject("PoolHolder").transform;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_prefab);
            Object.DestroyImmediate(_holder.gameObject);
        }

        [Test]
        public void Prewarm_CreatesInactiveInstances()
        {
            new Pool<Transform>(_prefab, _holder, "t", 4, 2);
            Assert.AreEqual(4, _holder.childCount);
            foreach (Transform c in _holder) Assert.IsFalse(c.gameObject.activeSelf);
        }

        [Test]
        public void Prewarm_StampsPooledMarkerWithId()
        {
            new Pool<Transform>(_prefab, _holder, "MyPool", 1, 1);
            var marker = _holder.GetChild(0).GetComponent<PooledMarker>();
            Assert.IsTrue(marker != null, "pooled instance has no PooledMarker — Despawn can't route it home");
            Assert.AreEqual("MyPool", marker.PoolId);
        }

        [Test]
        public void GetAndReturn_FireHooksAndRecycle()
        {
            var pool = new Pool<Transform>(_prefab, _holder, "t", 1, 1);
            var a = pool.Get(Vector3.one, Quaternion.identity);
            Assert.IsTrue(a.activeSelf);
            Assert.AreEqual(Vector3.one, a.transform.position);
            Assert.AreEqual(1, a.GetComponent<Probe>().Spawns);
            pool.Return(a);
            Assert.IsFalse(a.activeSelf);
            Assert.AreEqual(1, a.GetComponent<Probe>().Despawns);
            var b = pool.Get(Vector3.zero, Quaternion.identity);
            Assert.AreSame(a, b, "returned instance should be reused");
        }

        [Test]
        public void Exhausted_ExpandsAndWarns_NeverReturnsNull()
        {
            var pool = new Pool<Transform>(_prefab, _holder, "t", 1, 3);
            pool.Get(Vector3.zero, Quaternion.identity);
            LogAssert.Expect(LogType.Warning, new Regex("exhausted"));
            var second = pool.Get(Vector3.zero, Quaternion.identity);
            Assert.IsNotNull(second);
            Assert.AreEqual(4, _holder.childCount, "1 prewarm + expand increment 3");
        }

        [Test]
        public void DoubleReturn_DoesNotHandOutSameInstanceTwice()
        {
            var pool = new Pool<Transform>(_prefab, _holder, "t", 2, 1);
            var a = pool.Get(Vector3.zero, Quaternion.identity);
            pool.Return(a);
            pool.Return(a); // e.g. two hits in one tick both despawning
            var x = pool.Get(Vector3.zero, Quaternion.identity);
            var y = pool.Get(Vector3.zero, Quaternion.identity);
            Assert.AreNotSame(x, y, "a double Return put one instance in the pool twice — two live 'projectiles' share one object");
            Assert.AreEqual(1, a.GetComponent<Probe>().Despawns, "OnDespawn should fire once per real despawn");
        }
    }
}

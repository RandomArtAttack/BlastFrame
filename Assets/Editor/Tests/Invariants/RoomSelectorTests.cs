using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using BlastFrame.Gameplay.Rooms;

namespace BlastFrame.Tests.Invariants
{
    /// <summary>Seeded variant pick: same seed + room = same variant; always in range; null-safe.</summary>
    public class RoomSelectorTests
    {
        private List<RoomVariantSO> _three;

        [SetUp]
        public void SetUp()
        {
            _three = new List<RoomVariantSO>();
            for (int i = 0; i < 3; i++) _three.Add(ScriptableObject.CreateInstance<RoomVariantSO>());
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var v in _three) Object.DestroyImmediate(v);
        }

        [Test]
        public void SameSeedAndRoom_SameVariant()
        {
            for (int seed = -500; seed < 500; seed += 7)
            for (int room = 0; room < 12; room++)
                Assert.AreSame(RoomVariantSelector.Pick(_three, seed, room), RoomVariantSelector.Pick(_three, seed, room));
        }

        [Test]
        public void ExtremeSeeds_StayInRange()
        {
            int[] seeds = { int.MinValue, int.MinValue + 1, -1, 0, 1, int.MaxValue - 1, int.MaxValue };
            foreach (int s in seeds)
            for (int room = -2; room < 40; room++)
                Assert.IsNotNull(RoomVariantSelector.Pick(_three, s, room), $"seed {s} room {room}");
        }

        [Test]
        public void EmptyOrNull_LogsAndReturnsNull()
        {
            LogAssert.Expect(LogType.Error, new Regex("null or empty"));
            Assert.IsNull(RoomVariantSelector.Pick(new List<RoomVariantSO>(), 1, 0));
            LogAssert.Expect(LogType.Error, new Regex("null or empty"));
            Assert.IsNull(RoomVariantSelector.Pick(null, 1, 0));
        }
    }
}

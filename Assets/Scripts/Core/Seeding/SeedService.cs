using System;

namespace BlastFrame.Core.Seeding
{
    /// <summary>Per-run deterministic roll source (DESIGN.md §7). Registered by the run owner when a run starts.</summary>
    public interface ISeedService
    {
        int RunSeed { get; }
        uint Roll(string areaId, int roomIndex, string slotId);
        int Range(string areaId, int roomIndex, string slotId, int count);
        float Value01(string areaId, int roomIndex, string slotId);
    }

    /// <summary>
    /// Derives every content roll from a single run seed: roll(areaId, roomIndex, slotId) = Hash(runSeed, ...).
    /// No UnityEngine.Random, no shared mutable state, so a resumed run (same runSeed) re-derives identical
    /// content and seeds can be shared. The hash is FNV-1a over the UTF-16 code units of the ids, mixed with the
    /// seed and room index, then finalised with SplitMix64 — stable across platforms, runtimes and versions
    /// (string.GetHashCode is NOT: it may be randomised per process). Changing this function changes every
    /// existing seed's content: SeedServiceTests pins golden values to catch that.
    /// </summary>
    public sealed class SeedService : ISeedService
    {
        public int RunSeed { get; }

        public SeedService(int runSeed) => RunSeed = runSeed;

        /// <summary>Fresh seed for a new run. Uses the clock (fine: only the CHOICE of seed is random; all content
        /// derived from it is deterministic).</summary>
        public static int NewRunSeed() => unchecked((int)Mix((ulong)DateTime.UtcNow.Ticks));

        public uint Roll(string areaId, int roomIndex, string slotId) => Hash(RunSeed, areaId, roomIndex, slotId);

        public int Range(string areaId, int roomIndex, string slotId, int count) =>
            count <= 1 ? 0 : RangeFrom(Roll(areaId, roomIndex, slotId), count);

        public float Value01(string areaId, int roomIndex, string slotId) => Value01From(Roll(areaId, roomIndex, slotId));

        // ------------------------------------------------------------------ pure helpers

        public static uint Hash(int seed, string areaId, int roomIndex, string slotId)
        {
            ulong h = 14695981039346656037UL;                 // FNV-1a 64 offset basis
            h = Fnv(h, areaId);
            h = (h ^ 0x1F) * 1099511628211UL;                  // separator so ("ab","c") != ("a","bc")
            h = Fnv(h, slotId);
            h ^= (ulong)(uint)seed * 0x9E3779B97F4A7C15UL;
            h ^= (ulong)(uint)roomIndex * 0xC2B2AE3D27D4EB4FUL;
            return (uint)(Mix(h) >> 32);
        }

        /// <summary>Unbiased-enough map of a 32-bit roll to [0, count): multiply-shift, no modulo bias.</summary>
        public static int RangeFrom(uint roll, int count) => count <= 1 ? 0 : (int)(((ulong)roll * (uint)count) >> 32);

        public static float Value01From(uint roll) => (roll >> 8) * (1f / 16777216f); // 24-bit mantissa, [0,1)

        private static ulong Fnv(ulong h, string s)
        {
            if (s == null) return (h ^ 0xFF) * 1099511628211UL;
            for (int i = 0; i < s.Length; i++)
            {
                h ^= s[i];
                h *= 1099511628211UL;
            }
            return h;
        }

        /// <summary>SplitMix64 finaliser.</summary>
        private static ulong Mix(ulong z)
        {
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }
}

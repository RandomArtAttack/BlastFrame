using System;
using System.Collections.Generic;

namespace BlastFrame.Core.Save
{
    /// <summary>
    /// Plain serializable C# class — no MonoBehaviour, no SO. Stores ids only; SO references are
    /// resolved at runtime via registries. Shape follows DESIGN.md §11 (which supersedes CLAUDE.md).
    /// Old saves still load: Newtonsoft ignores keys that no longer exist (e.g. the retired linear
    /// unlockedLevelIndex) and new fields keep their initialisers.
    /// </summary>
    [Serializable]
    public class SaveData
    {
        public int metaCurrency;
        public List<string> purchasedPermanentIds = new List<string>();

        /// <summary>Drives the area effect graph (AreaGraph.Derive). Area unlock/alter state is DERIVED from
        /// this set, never stored.</summary>
        public HashSet<string> completedAreaIds = new HashSet<string>();

        public List<string> unlockedWeaponIds = new List<string>();
        public List<string> unlockedAbilityIds = new List<string>();

        public RunSaveData runState; // null unless a run is mid-progress and resumable

        // Stats totals
        public int bestLevelReached;
        public int totalRuns;
        public int totalDeaths;
        public int totalKills;

        // Settings
        public float audioVolumeMaster = 1f;
        public float audioVolumeMusic = 1f;
        public float audioVolumeSfx = 1f;
        public float mouseSensitivity = 1f;
        public bool invertY;
    }

    /// <summary>Only present when a run is mid-progress and explicitly designed as resumable.</summary>
    [Serializable]
    public class RunSaveData
    {
        /// <summary>Single seed every content roll of this run derives from (SeedService, DESIGN.md §7).</summary>
        public int runSeed;
        public string currentAreaId;
        public int currentRoomIndex;
        public int difficulty; // cast of Difficulty enum
        public int currentHealth;
        public List<string> activeRunPowerupIds = new List<string>();
    }
}

using System;
using System.Collections.Generic;

namespace BlastFrame.Core.Save
{
    /// <summary>
    /// Plain serializable C# class — no MonoBehaviour, no SO. Stores ids only; SO references are
    /// resolved at runtime via registries. Shape mirrors the Save Data section of CLAUDE.md.
    /// </summary>
    [Serializable]
    public class SaveData
    {
        public int metaCurrency;
        public List<string> purchasedPermanentIds = new List<string>();
        public int unlockedLevelIndex;
        public List<string> unlockedWeaponIds = new List<string>();

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
        public int currentLevelIndex;
        public int currentRoomIndex;
        public int difficulty; // cast of Difficulty enum
        public int currentHealth;
        public List<string> activeRunPowerupIds = new List<string>();
    }
}

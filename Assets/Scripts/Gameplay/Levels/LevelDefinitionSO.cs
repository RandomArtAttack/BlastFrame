using UnityEngine;
using BlastFrame.Core.Variables;

namespace BlastFrame.Gameplay.Levels
{
    /// <summary>
    /// Designer-authored definition for one of the 9 levels. Stores structural data (index,
    /// display name, room count) and difficulty-scaling FloatReferences the RunManager reads
    /// when setting up a run. One asset per level — does not store run-state.
    ///
    /// Room variant content (which RoomVariantSO sets are in each slot) is authored on
    /// RoomController GameObjects in the level scene itself; this SO only records the room
    /// count so the RunManager and LevelController know how many rooms to step through.
    /// </summary>
    [CreateAssetMenu(fileName = "Level00_Definition", menuName = "Blast Frame/Levels/Level Definition")]
    public sealed class LevelDefinitionSO : ScriptableObject
    {
        [Tooltip("Zero-based index of this level (0–8). Level 0 = Level01, Level 8 = Level09. " +
                 "Must match the index used by RunManager and LevelController.")]
        [SerializeField] private int _levelIndex;

        [Tooltip("Human-readable display name shown in the HQ run-start panel. Example: 'Sector 1 — Assembly Bay'.")]
        [SerializeField] private string _displayName = "Level Name";

        [Tooltip("Number of room slots in this level scene (not counting mini-boss or boss rooms). " +
                 "LevelController uses this to know when to advance to the boss fight.")]
        [SerializeField] private int _roomCount = 4;

        [Tooltip("Scales the number of enemies that spawn in each room. 1.0 = baseline enemy count. " +
                 "Applied per-difficulty in combination with RunManager difficulty choice.")]
        [SerializeField] private FloatReference _enemyCountScale = new FloatReference(1f);

        [Tooltip("Scales enemy stat values (health, damage, speed) for this level. 1.0 = baseline stats. " +
                 "Designers tune this per level to produce a difficulty ramp across the 9 levels.")]
        [SerializeField] private FloatReference _enemyStatScale = new FloatReference(1f);

        [Tooltip("Multiplier applied to metaCurrency rewards for completing rooms and killing enemies in this level. " +
                 "Higher levels yield more currency to compensate for increased difficulty.")]
        [SerializeField] private FloatReference _rewardScale = new FloatReference(1f);

        // -----------------------------------------------------------------------------------------
        // Public getters

        /// <summary>Zero-based level index (0–8).</summary>
        public int LevelIndex => _levelIndex;

        /// <summary>Display name shown in HQ UI.</summary>
        public string DisplayName => _displayName;

        /// <summary>Number of room slots in this level's scene.</summary>
        public int RoomCount => _roomCount;

        /// <summary>Multiplier for enemy spawn count. Use with a per-difficulty multiplier.</summary>
        public float EnemyCountScale => _enemyCountScale.Value;

        /// <summary>Multiplier for enemy stat values (health, damage, speed).</summary>
        public float EnemyStatScale => _enemyStatScale.Value;

        /// <summary>Multiplier for metaCurrency rewards earned in this level.</summary>
        public float RewardScale => _rewardScale.Value;
    }
}

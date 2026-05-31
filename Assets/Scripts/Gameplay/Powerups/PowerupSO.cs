using UnityEngine;
using BlastFrame.Core.Variables;

namespace BlastFrame.Gameplay.Powerups
{
    /// <summary>
    /// Designer-authored definition for a single powerup. Stats are FloatReferences so
    /// constants or shared Variable assets are both usable. All powerups are run-scoped
    /// unless explicitly noted — they are lost on death and must NOT be persisted to save data.
    /// </summary>
    [CreateAssetMenu(fileName = "NewPowerup", menuName = "Blast Frame/Powerups/Powerup")]
    public class PowerupSO : ScriptableObject
    {
        /// <summary>
        /// The effect applied when this powerup is collected.
        /// Add new values here as the roster grows — pickup logic switches on this.
        /// </summary>
        public enum PowerupEffect
        {
            /// <summary>Instantly restore HP equal to magnitude (clamped to max).</summary>
            Heal,
            /// <summary>Add magnitude to move speed for the run (run-scoped).</summary>
            MoveSpeedBuff,
            /// <summary>Raise max health by magnitude for the run (run-scoped).</summary>
            MaxHealthUp,
        }

        [Tooltip("Unique string id used by save data and registries. Must not change once authored.")]
        [SerializeField] private string _id;

        [Tooltip("Human-readable name shown in UI.")]
        [SerializeField] private string _displayName;

        [Tooltip("Effect applied when this powerup is collected.")]
        [SerializeField] private PowerupEffect _effect;

        [Tooltip("Numeric magnitude of the effect (e.g. HP restored, speed added). Use a constant or Variable asset.")]
        [SerializeField] private FloatReference _magnitude = new FloatReference(1f);

        [Tooltip("Duration in seconds. 0 means instant or lasts the full run.")]
        [SerializeField] private FloatReference _duration = new FloatReference(0f);

        public string Id => _id;
        public string DisplayName => _displayName;
        public PowerupEffect Effect => _effect;
        public float Magnitude => _magnitude;
        public float Duration => _duration;
    }
}

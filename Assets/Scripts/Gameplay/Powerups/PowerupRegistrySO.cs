using System.Collections.Generic;
using UnityEngine;

namespace BlastFrame.Gameplay.Powerups
{
    /// <summary>
    /// Master list of all PowerupSOs in the project. Systems look up powerups by id rather
    /// than holding direct SO references. Populate this in the Inspector by dragging every
    /// PowerupSO asset into the list.
    /// </summary>
    [CreateAssetMenu(fileName = "PowerupRegistry", menuName = "Blast Frame/Powerups/Powerup Registry")]
    public class PowerupRegistrySO : ScriptableObject
    {
        [Tooltip("All PowerupSO assets in the project. Drag every authored powerup here so systems can look them up by id.")]
        [SerializeField] private List<PowerupSO> _powerups = new List<PowerupSO>();

        public IReadOnlyList<PowerupSO> All => _powerups;

        /// <summary>Returns the PowerupSO with the given id, or null if not found.</summary>
        public PowerupSO GetById(string id)
        {
            for (int i = 0; i < _powerups.Count; i++)
            {
                if (_powerups[i] != null && _powerups[i].Id == id)
                    return _powerups[i];
            }
            return null;
        }
    }
}

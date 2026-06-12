using System.Collections.Generic;
using UnityEngine;

namespace BlastFrame.Gameplay.HQ
{
    /// <summary>
    /// Registry of all PermanentUpgradeSO assets. ShopManager and ShopUI reference this SO
    /// to enumerate available upgrades and look them up by id without Find or hard-coded paths.
    /// Populated via the Inspector or Fix019. Never modified at runtime.
    /// </summary>
    [CreateAssetMenu(fileName = "PermanentRegistry", menuName = "Blast Frame/HQ/Permanent Upgrade Registry")]
    public class PermanentUpgradeRegistrySO : ScriptableObject
    {
        [Tooltip("All permanent upgrades available in the HQ shop. " +
                 "Add each PermanentUpgradeSO asset here. Order determines display order in ShopUI.")]
        [SerializeField] private List<PermanentUpgradeSO> upgrades = new List<PermanentUpgradeSO>();

        /// <summary>All upgrades in display order.</summary>
        public IReadOnlyList<PermanentUpgradeSO> Upgrades => upgrades;

        /// <summary>
        /// Returns the upgrade with the given id, or null if not found.
        /// O(n) — acceptable for the small sizes expected here (< 50 upgrades).
        /// </summary>
        public PermanentUpgradeSO GetById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < upgrades.Count; i++)
            {
                if (upgrades[i] != null && upgrades[i].Id == id)
                    return upgrades[i];
            }
            return null;
        }
    }
}

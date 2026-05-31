using UnityEngine;
using BlastFrame.Core.Variables;

namespace BlastFrame.Gameplay.HQ
{
    /// <summary>
    /// Defines a single permanent upgrade purchasable at HQ with meta-currency.
    /// Stats (cost, magnitude) use IntReference / FloatReference so designers can drive them
    /// from Variable SO assets or type a constant directly in the Inspector.
    /// </summary>
    [CreateAssetMenu(fileName = "NewPermanentUpgrade", menuName = "Blast Frame/HQ/Permanent Upgrade")]
    public class PermanentUpgradeSO : ScriptableObject
    {
        // ------------------------------------------------------------------
        // Identity
        // ------------------------------------------------------------------

        [Tooltip("Unique string key for this upgrade. Must match the id used in SaveData.purchasedPermanentIds " +
                 "and in ShopManager lookups. Example: \"ExtraHealth\", \"FasterDash\".")]
        [SerializeField] private string id;

        [Tooltip("Human-readable name shown in the Shop UI. Example: \"Extra Health\".")]
        [SerializeField] private string displayName;

        [Tooltip("Short description shown in the Shop UI row.")]
        [TextArea(2, 4)]
        [SerializeField] private string description;

        // ------------------------------------------------------------------
        // Economy
        // ------------------------------------------------------------------

        [Tooltip("Meta-currency cost to purchase this upgrade. Use a constant or plug in an IntVariable asset.")]
        [SerializeField] private IntReference cost;

        // ------------------------------------------------------------------
        // Effect
        // ------------------------------------------------------------------

        [Tooltip("Which gameplay stat this upgrade affects. Add new values to the enum as the roster grows.")]
        [SerializeField] private PermanentUpgradeEffect effect;

        [Tooltip("Magnitude of the effect — how much to add/multiply the target stat. " +
                 "Interpretation depends on PermanentUpgradeEffect. Use a constant or a FloatVariable asset.")]
        [SerializeField] private FloatReference magnitude;

        // ------------------------------------------------------------------
        // Public accessors (read-only — gameplay code never writes back to SOs)
        // ------------------------------------------------------------------

        public string Id          => id;
        public string DisplayName => displayName;
        public string Description => description;
        public int    Cost        => cost.Value;
        public PermanentUpgradeEffect Effect => effect;
        public float  Magnitude   => magnitude.Value;
    }

    /// <summary>
    /// Which stat a PermanentUpgradeSO affects. Extend as the upgrade roster grows.
    /// Systems that apply the effect switch on this enum.
    /// </summary>
    public enum PermanentUpgradeEffect
    {
        None,
        MaxHealthBonus,     // adds flat HP to max health
        DashCooldownReduce, // reduces dash cooldown by magnitude seconds
        MoveSpeedBonus,     // adds magnitude to move speed
        DamageBonus,        // multiplies player damage by magnitude
        StartingCurrency,   // adds magnitude as bonus meta-currency at run start
    }
}

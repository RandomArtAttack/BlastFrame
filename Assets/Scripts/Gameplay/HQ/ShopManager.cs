using System.Collections.Generic;
using UnityEngine;
using BlastFrame.Core;
using BlastFrame.Core.Events;
using BlastFrame.Core.Services;

namespace BlastFrame.Gameplay.HQ
{
    /// <summary>
    /// Service managing HQ permanent-upgrade purchases. Registers as IShopManager in Awake.
    /// Ownership is backed by ISaveManager.Data.purchasedPermanentIds when a save system is
    /// present; falls back to an in-memory HashSet when it is not (i.e. the save system is
    /// not yet wired or the game is running without persistence). All purchases are
    /// idempotent — buying an already-owned upgrade is a no-op that returns false.
    /// </summary>
    public class ShopManager : MonoBehaviour, IShopManager
    {
        [Tooltip("All available permanent upgrades. Assign the PermanentUpgradeRegistrySO asset here.")]
        [SerializeField] private PermanentUpgradeRegistrySO registry;

        [Tooltip("Optional: parameterless GameEventSO raised after a successful purchase. " +
                 "Assign to notify any listener (e.g. ShopUI, AudioManager) without coupling.")]
        [SerializeField] private GameEventSO onPurchasedEvent;

        // ------------------------------------------------------------------
        // IShopManager
        // ------------------------------------------------------------------

        public bool IsOwned(string upgradeId)
        {
            if (string.IsNullOrEmpty(upgradeId)) return false;

            if (ServiceLocator.TryGet<ISaveManager>(out var save) && save.Data != null)
                return save.Data.purchasedPermanentIds.Contains(upgradeId);

            return _ownedFallback.Contains(upgradeId);
        }

        public bool TryPurchase(string upgradeId)
        {
            if (registry == null)
            {
                Debug.LogWarning("[ShopManager] No registry assigned — cannot purchase.");
                return false;
            }

            if (IsOwned(upgradeId))
            {
                Debug.LogWarning($"[ShopManager] '{upgradeId}' is already owned.");
                return false;
            }

            var upgrade = registry.GetById(upgradeId);
            if (upgrade == null)
            {
                Debug.LogWarning($"[ShopManager] Upgrade id '{upgradeId}' not found in registry.");
                return false;
            }

            if (!ServiceLocator.TryGet<ICurrencyManager>(out var currency))
            {
                Debug.LogError("[ShopManager] ICurrencyManager not registered — cannot spend currency.");
                return false;
            }

            if (!currency.TrySpend(upgrade.Cost))
            {
                Debug.Log($"[ShopManager] Not enough currency to purchase '{upgradeId}' (cost {upgrade.Cost}).");
                return false;
            }

            // Mark owned.
            if (ServiceLocator.TryGet<ISaveManager>(out var save) && save.Data != null)
            {
                save.Data.purchasedPermanentIds.Add(upgradeId);
                save.Data.metaCurrency = currency.MetaCurrency;
                save.Save();
            }
            else
            {
                _ownedFallback.Add(upgradeId);
            }

            onPurchasedEvent?.Raise();

            Debug.Log($"[ShopManager] Purchased '{upgradeId}'. Remaining currency: {currency.MetaCurrency}.");
            return true;
        }

        // ------------------------------------------------------------------
        // Unity lifecycle
        // ------------------------------------------------------------------

        // Fallback ownership set used when ISaveManager is not registered.
        private readonly HashSet<string> _ownedFallback = new HashSet<string>();

        private void Awake()
        {
            ServiceLocator.Register<IShopManager>(this);
        }

        private void OnDestroy()
        {
            ServiceLocator.Unregister<IShopManager>(this);
        }
    }
}

using System;
using UnityEngine;
using BlastFrame.Core;
using BlastFrame.Core.Events;
using BlastFrame.Core.Services;

namespace BlastFrame.Gameplay.Economy
{
    /// <summary>
    /// Service that manages the player's meta-currency wallet. Registers as ICurrencyManager
    /// in Awake. Loads initial balance from ISaveManager in Start (TryGet — save system is
    /// optional). Raises OnCurrencyChanged (C# event) and optionally a typed GameEventSO
    /// after every Add/TrySpend so any system can react without coupling to this class.
    /// </summary>
    public class CurrencyManager : MonoBehaviour, ICurrencyManager
    {
        [Tooltip("Optional: GameEventSO<int> raised after every balance change. " +
                 "Assign an IntGameEventSO asset to broadcast the new balance to any listener. " +
                 "Leave null to skip the SO broadcast (C# event still fires).")]
        [SerializeField] private IntGameEventSO onCurrencyChangedEvent;

        // ------------------------------------------------------------------
        // ICurrencyManager
        // ------------------------------------------------------------------

        public int MetaCurrency => _wallet.Balance;

        public event Action<int> OnCurrencyChanged;

        public void Add(int amount)
        {
            if (amount <= 0) return;
            _wallet.Add(amount);
            NotifyChanged();
        }

        public bool TrySpend(int amount)
        {
            if (!_wallet.TrySpend(amount)) return false;
            NotifyChanged();
            return true;
        }

        // ------------------------------------------------------------------
        // Unity lifecycle
        // ------------------------------------------------------------------

        private CurrencyWallet _wallet;

        private void Awake()
        {
            ServiceLocator.Register<ICurrencyManager>(this);
        }

        private void Start()
        {
            // ISaveManager is optional — use TryGet so this works without a save system.
            if (ServiceLocator.TryGet<ISaveManager>(out var saveManager) && saveManager.Data != null)
            {
                _wallet = new CurrencyWallet(saveManager.Data.metaCurrency);
            }
            else
            {
                _wallet = new CurrencyWallet(0);
            }

            // Notify so any listening UI initialises with the correct value.
            NotifyChanged();
        }

        private void OnDestroy()
        {
            ServiceLocator.Unregister<ICurrencyManager>(this);
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private void NotifyChanged()
        {
            OnCurrencyChanged?.Invoke(_wallet.Balance);
            onCurrencyChangedEvent?.Raise(_wallet.Balance);
        }
    }
}

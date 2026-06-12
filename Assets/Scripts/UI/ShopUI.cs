using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using BlastFrame.Core;
using BlastFrame.Core.Events;
using BlastFrame.Core.Services;
using BlastFrame.Gameplay.HQ;

namespace BlastFrame.UI
{
    /// <summary>
    /// Builds and maintains the HQ shop rows at runtime from a PermanentUpgradeRegistrySO.
    /// Each row shows the upgrade name, cost, and owned/buy state via TextMeshProUGUI + Button.
    /// Reacts to ICurrencyManager.OnCurrencyChanged and onPurchasedEvent to refresh without
    /// querying managers in Update. Never holds direct references to other scene objects —
    /// managers are accessed via ServiceLocator in Start.
    /// </summary>
    public class ShopUI : MonoBehaviour
    {
        // ------------------------------------------------------------------
        // Inspector fields
        // ------------------------------------------------------------------

        [Tooltip("Registry listing every permanent upgrade to display. Assign the PermanentUpgradeRegistrySO asset.")]
        [SerializeField] private PermanentUpgradeRegistrySO registry;

        [Tooltip("Parent RectTransform that row GameObjects are spawned under. Assign a VerticalLayoutGroup-backed panel.")]
        [SerializeField] private RectTransform rowContainer;

        [Tooltip("TextMeshProUGUI element showing the player's current meta-currency balance.")]
        [SerializeField] private TextMeshProUGUI currencyLabel;

        [Tooltip("Optional: GameEventSO that ShopManager raises after any purchase. " +
                 "Subscribe here so ShopUI refreshes rows when a purchase happens via code (e.g. TestStat). " +
                 "Leave null — OnCurrencyChanged alone is sufficient if you do not assign this.")]
        [SerializeField] private GameEventSO onPurchasedEvent;

        // ------------------------------------------------------------------
        // Private state
        // ------------------------------------------------------------------

        private ICurrencyManager _currencyManager;
        private IShopManager     _shopManager;

        // One entry per upgrade row.
        private readonly List<RowData> _rows = new List<RowData>();

        private struct RowData
        {
            public string          UpgradeId;
            public TextMeshProUGUI NameLabel;
            public TextMeshProUGUI CostLabel;
            public TextMeshProUGUI StatusLabel;
            public Button          BuyButton;
        }

        // ------------------------------------------------------------------
        // Unity lifecycle
        // ------------------------------------------------------------------

        private void Start()
        {
            _currencyManager = ServiceLocator.Get<ICurrencyManager>();
            _shopManager     = ServiceLocator.Get<IShopManager>();

            _currencyManager.OnCurrencyChanged += OnCurrencyChanged;

            if (onPurchasedEvent != null)
                onPurchasedEvent.Register(OnPurchased);

            BuildRows();
            RefreshAll();
        }

        private void OnDestroy()
        {
            if (_currencyManager != null)
                _currencyManager.OnCurrencyChanged -= OnCurrencyChanged;

            if (onPurchasedEvent != null)
                onPurchasedEvent.Unregister(OnPurchased);
        }

        // ------------------------------------------------------------------
        // Row construction
        // ------------------------------------------------------------------

        private void BuildRows()
        {
            if (registry == null || rowContainer == null) return;

            foreach (var upgrade in registry.Upgrades)
            {
                if (upgrade == null) continue;
                BuildRow(upgrade);
            }
        }

        private void BuildRow(PermanentUpgradeSO upgrade)
        {
            // Row root — horizontal layout.
            var rowGo = new GameObject($"Row_{upgrade.Id}");
            rowGo.transform.SetParent(rowContainer, false);

            var rowRT = rowGo.AddComponent<RectTransform>();
            rowRT.sizeDelta = new Vector2(0f, 60f);

            var hlg = rowGo.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing          = 12f;
            hlg.childAlignment   = TextAnchor.MiddleLeft;
            hlg.childForceExpandHeight = true;
            hlg.childForceExpandWidth  = false;
            hlg.padding = new RectOffset(8, 8, 4, 4);

            // Name label.
            var nameLbl = MakeLabel(rowGo.transform, $"Name_{upgrade.Id}", upgrade.DisplayName, 22f, TextAlignmentOptions.Left);
            SetFlexWidth(nameLbl.GetComponent<RectTransform>(), 280f, true);

            // Description label.
            var descLbl = MakeLabel(rowGo.transform, $"Desc_{upgrade.Id}", upgrade.Description, 16f, TextAlignmentOptions.Left);
            descLbl.color = new Color(0.75f, 0.75f, 0.75f, 1f);
            SetFlexWidth(descLbl.GetComponent<RectTransform>(), 340f, true);

            // Cost label.
            var costLbl = MakeLabel(rowGo.transform, $"Cost_{upgrade.Id}", upgrade.Cost.ToString(), 22f, TextAlignmentOptions.Right);
            SetFlexWidth(costLbl.GetComponent<RectTransform>(), 80f, false);

            // Status label (OWNED / empty).
            var statusLbl = MakeLabel(rowGo.transform, $"Status_{upgrade.Id}", string.Empty, 20f, TextAlignmentOptions.Center);
            statusLbl.color = new Color(0.3f, 1f, 0.4f, 1f);
            SetFlexWidth(statusLbl.GetComponent<RectTransform>(), 80f, false);

            // Buy button.
            var btnGo   = new GameObject($"Btn_{upgrade.Id}");
            btnGo.transform.SetParent(rowGo.transform, false);

            var btnRT = btnGo.AddComponent<RectTransform>();
            btnRT.sizeDelta = new Vector2(100f, 44f);

            var btnImg = btnGo.AddComponent<Image>();
            btnImg.color = new Color(0.2f, 0.55f, 1f, 1f);

            var btn = btnGo.AddComponent<Button>();
            btn.targetGraphic = btnImg;

            var btnLblGo = new GameObject("Label");
            btnLblGo.transform.SetParent(btnGo.transform, false);
            var btnLblRT = btnLblGo.AddComponent<RectTransform>();
            btnLblRT.anchorMin = Vector2.zero;
            btnLblRT.anchorMax = Vector2.one;
            btnLblRT.offsetMin = Vector2.zero;
            btnLblRT.offsetMax = Vector2.zero;
            var btnTMP = btnLblGo.AddComponent<TextMeshProUGUI>();
            btnTMP.text        = "Buy";
            btnTMP.fontSize    = 18f;
            btnTMP.alignment   = TextAlignmentOptions.Center;
            btnTMP.color       = Color.white;

            // Capture id for the closure — loop variable would be captured by reference.
            string capturedId = upgrade.Id;
            btn.onClick.AddListener(() => OnBuyClicked(capturedId));

            var le = btnGo.AddComponent<LayoutElement>();
            le.preferredWidth  = 100f;
            le.preferredHeight = 44f;
            le.flexibleWidth   = 0f;

            _rows.Add(new RowData
            {
                UpgradeId   = upgrade.Id,
                NameLabel   = nameLbl,
                CostLabel   = costLbl,
                StatusLabel = statusLbl,
                BuyButton   = btn
            });
        }

        // ------------------------------------------------------------------
        // Row refresh helpers
        // ------------------------------------------------------------------

        private void RefreshAll()
        {
            int balance = _currencyManager?.MetaCurrency ?? 0;
            UpdateCurrencyLabel(balance);

            for (int i = 0; i < _rows.Count; i++)
                RefreshRow(_rows[i], balance);
        }

        private void RefreshRow(in RowData row, int balance)
        {
            if (_shopManager == null) return;

            bool owned = _shopManager.IsOwned(row.UpgradeId);

            row.StatusLabel.text   = owned ? "OWNED" : string.Empty;
            row.BuyButton.gameObject.SetActive(!owned);

            if (!owned)
            {
                var upgrade = registry.GetById(row.UpgradeId);
                bool canAfford = upgrade != null && balance >= upgrade.Cost;
                row.BuyButton.interactable = canAfford;

                // Dim cost label when cannot afford.
                row.CostLabel.color = canAfford
                    ? new Color(1f, 0.85f, 0.2f, 1f)   // gold
                    : new Color(0.6f, 0.6f, 0.6f, 1f); // grey
            }
        }

        private void UpdateCurrencyLabel(int balance)
        {
            if (currencyLabel != null)
                currencyLabel.text = balance.ToString();
        }

        // ------------------------------------------------------------------
        // Event handlers
        // ------------------------------------------------------------------

        private void OnCurrencyChanged(int newBalance)
        {
            UpdateCurrencyLabel(newBalance);
            for (int i = 0; i < _rows.Count; i++)
                RefreshRow(_rows[i], newBalance);
        }

        private void OnPurchased()
        {
            int balance = _currencyManager?.MetaCurrency ?? 0;
            for (int i = 0; i < _rows.Count; i++)
                RefreshRow(_rows[i], balance);
        }

        private void OnBuyClicked(string upgradeId)
        {
            if (_shopManager == null) return;
            _shopManager.TryPurchase(upgradeId);
            // RefreshAll is driven by OnCurrencyChanged + OnPurchased events, not here.
        }

        // ------------------------------------------------------------------
        // UI helpers
        // ------------------------------------------------------------------

        private static TextMeshProUGUI MakeLabel(Transform parent, string name, string text,
                                                  float fontSize, TextAlignmentOptions alignment)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text      = text;
            tmp.fontSize  = fontSize;
            tmp.alignment = alignment;
            tmp.color     = Color.white;
            return tmp;
        }

        private static void SetFlexWidth(RectTransform rt, float preferred, bool flexible)
        {
            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = preferred;
            le.flexibleWidth  = flexible ? 1f : 0f;
        }
    }
}

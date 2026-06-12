using UnityEngine;
using UnityEngine.UI;
using BlastFrame.Core.Entities;
using BlastFrame.Gameplay;

namespace BlastFrame.UI
{
    /// <summary>
    /// Horizontal filled Image whose fillAmount tracks weapon charge (0..1). Binds to
    /// IChargeReadout.OnChargeChanged found anywhere in the player's hierarchy (e.g. ChargeShot on
    /// the camera child). The bar's CanvasGroup/alpha is hidden when charge == 0 so it does not
    /// clutter the screen during normal movement.
    /// </summary>
    public class ChargeBarUI : MonoBehaviour
    {
        [Tooltip("The shared EntityRegistry SO. Assigned by HUDController.Awake or directly in the Inspector.")]
        [SerializeField] private EntityRegistrySO entityRegistry;

        [Tooltip("Horizontal filled Image used as the charge bar. Set Image Type to Filled/Horizontal in the Inspector.")]
        [SerializeField] private Image chargeBarImage;

        private bool _bound;
        private IChargeReadout _chargeSource;

        /// <summary>Called by HUDController to inject the registry when it is set there.</summary>
        public void SetRegistry(EntityRegistrySO registry) => entityRegistry = registry;

        private void Start()
        {
            SetVisible(false);
            TryBind();
        }

        private void Update()
        {
            if (!_bound) TryBind();
        }

        private void TryBind()
        {
            if (entityRegistry == null || !entityRegistry.HasPlayer) return;

            _chargeSource = entityRegistry.PlayerTransform.GetComponentInChildren<IChargeReadout>();
            if (_chargeSource == null)
            {
                // No charge weapon yet — stay hidden.
                _bound = true;
                return;
            }

            _chargeSource.OnChargeChanged += OnChargeChanged;
            OnChargeChanged(_chargeSource.Charge01);
            _bound = true;
        }

        private void OnChargeChanged(float charge)
        {
            if (chargeBarImage != null)
                chargeBarImage.fillAmount = charge;

            SetVisible(charge > 0f);
        }

        private void SetVisible(bool visible)
        {
            if (chargeBarImage != null)
                chargeBarImage.gameObject.SetActive(visible);
        }

        private void OnDestroy()
        {
            if (_chargeSource != null)
                _chargeSource.OnChargeChanged -= OnChargeChanged;
        }
    }
}

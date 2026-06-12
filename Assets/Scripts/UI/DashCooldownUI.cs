using UnityEngine;
using UnityEngine.UI;
using BlastFrame.Core.Entities;
using BlastFrame.Gameplay.Player.Movement;

namespace BlastFrame.UI
{
    /// <summary>
    /// Radial filled Image whose fillAmount tracks dash readiness (0 = on cooldown, 1 = ready).
    /// Binds to DashModule.OnCooldownChanged via the EntityRegistry. If the player has no
    /// DashModule the ring stays at fillAmount 1 (always ready / invisible ring).
    /// </summary>
    public class DashCooldownUI : MonoBehaviour
    {
        [Tooltip("The shared EntityRegistry SO. Assigned by HUDController.Awake or directly in the Inspector.")]
        [SerializeField] private EntityRegistrySO entityRegistry;

        [Tooltip("Radial filled Image used as the cooldown ring. Set Image Type to Filled/Radial360 in the Inspector.")]
        [SerializeField] private Image dashRingImage;

        private bool _bound;

        /// <summary>Called by HUDController to inject the registry when it is set there.</summary>
        public void SetRegistry(EntityRegistrySO registry) => entityRegistry = registry;

        private void Start()
        {
            if (dashRingImage != null) dashRingImage.fillAmount = 1f;
            TryBind();
        }

        private void Update()
        {
            if (!_bound) TryBind();
        }

        private void TryBind()
        {
            if (entityRegistry == null || !entityRegistry.HasPlayer) return;

            var dash = entityRegistry.PlayerTransform.GetComponent<DashModule>();
            if (dash == null)
            {
                // No DashModule — stay full (graceful degradation).
                _bound = true;
                return;
            }

            dash.OnCooldownChanged += OnCooldownChanged;
            OnCooldownChanged(dash.IsReady ? 1f : 0f);
            _bound = true;
        }

        private void OnCooldownChanged(float readiness)
        {
            if (dashRingImage != null)
                dashRingImage.fillAmount = readiness;
        }

        private void OnDestroy()
        {
            if (!_bound || entityRegistry == null || !entityRegistry.HasPlayer) return;
            var dash = entityRegistry.PlayerTransform.GetComponent<DashModule>();
            if (dash != null) dash.OnCooldownChanged -= OnCooldownChanged;
        }
    }
}

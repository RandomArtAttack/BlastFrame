using TMPro;
using UnityEngine;
using BlastFrame.Core.Entities;
using BlastFrame.Gameplay.Player;

namespace BlastFrame.UI
{
    /// <summary>
    /// Shows the player's current health as a TextMeshProUGUI number. Binds to
    /// PlayerHealth.OnHealthChanged via the EntityRegistry. Retries each frame until the player
    /// is registered, so it is safe even when the player spawns after the HUD.
    /// </summary>
    public class HealthDisplay : MonoBehaviour
    {
        [Tooltip("The shared EntityRegistry SO. Assigned by HUDController.Awake or directly in the Inspector.")]
        [SerializeField] private EntityRegistrySO entityRegistry;

        [Tooltip("TextMeshProUGUI element that displays the current health number.")]
        [SerializeField] private TextMeshProUGUI healthText;

        private bool _bound;

        /// <summary>Called by HUDController to inject the registry when it is set there.</summary>
        public void SetRegistry(EntityRegistrySO registry) => entityRegistry = registry;

        private void Start()
        {
            TryBind();
        }

        private void Update()
        {
            if (!_bound) TryBind();
        }

        private void TryBind()
        {
            if (entityRegistry == null || !entityRegistry.HasPlayer) return;

            var health = entityRegistry.PlayerTransform.GetComponent<PlayerHealth>();
            if (health == null) return;

            health.OnHealthChanged += OnHealthChanged;
            OnHealthChanged(health.Current, health.Max);
            _bound = true;
        }

        private void OnHealthChanged(int current, int max)
        {
            if (healthText != null)
                healthText.text = current.ToString();
        }

        private void OnDestroy()
        {
            if (!_bound || entityRegistry == null || !entityRegistry.HasPlayer) return;
            var health = entityRegistry.PlayerTransform.GetComponent<PlayerHealth>();
            if (health != null) health.OnHealthChanged -= OnHealthChanged;
        }
    }
}

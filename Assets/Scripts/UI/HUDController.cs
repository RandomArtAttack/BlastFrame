using UnityEngine;
using BlastFrame.Core.Entities;

namespace BlastFrame.UI
{
    /// <summary>
    /// Lightweight orchestrator that owns the EntityRegistrySO reference and ensures each HUD
    /// widget has it. Widgets bind themselves defensively (retry until player is registered), so
    /// no ordering contract is needed between the player spawn and the HUD initialisation.
    /// </summary>
    public class HUDController : MonoBehaviour
    {
        [Tooltip("The shared EntityRegistry SO. Assign the EntityRegistry.asset here.")]
        [SerializeField] private EntityRegistrySO entityRegistry;

        [Tooltip("Health number widget child component on this HUD prefab.")]
        [SerializeField] private HealthDisplay healthDisplay;

        [Tooltip("Dash cooldown ring widget child component on this HUD prefab.")]
        [SerializeField] private DashCooldownUI dashCooldownUI;

        [Tooltip("Charge bar widget child component on this HUD prefab.")]
        [SerializeField] private ChargeBarUI chargeBarUI;

        private void Awake()
        {
            // Propagate registry to widgets that may have been added before the registry was
            // wired (e.g. when the Fix creates them and assigns later via SerializedObject).
            if (entityRegistry == null)
            {
                Debug.LogWarning("[HUDController] entityRegistry not assigned — widgets will not bind.");
                return;
            }

            if (healthDisplay  != null) healthDisplay.SetRegistry(entityRegistry);
            if (dashCooldownUI != null) dashCooldownUI.SetRegistry(entityRegistry);
            if (chargeBarUI    != null) chargeBarUI.SetRegistry(entityRegistry);
        }
    }
}

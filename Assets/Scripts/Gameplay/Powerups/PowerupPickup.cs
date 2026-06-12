using UnityEngine;
using BlastFrame.Core.Entities;
using BlastFrame.Core.Events;
using BlastFrame.Gameplay.Player;

namespace BlastFrame.Gameplay.Powerups
{
    /// <summary>
    /// Placed on a trigger-collider GameObject. When the player enters the trigger the
    /// configured powerup effect is applied, the optional event is raised, and this pickup
    /// is destroyed.
    ///
    /// IMPORTANT — run-scope: all effects are temporary and must not be persisted to save
    /// data. They are cleared on death (the player returns to HQ and the run resets).
    ///
    /// Requires a Collider with isTrigger = true on this or a child GameObject.
    /// </summary>
    public class PowerupPickup : MonoBehaviour
    {
        [Tooltip("The powerup definition to apply when the player collects this pickup.")]
        [SerializeField] private PowerupSO _powerup;

        [Tooltip("EntityRegistrySO used to identify the player without Find(). Assign the project's EntityRegistry asset.")]
        [SerializeField] private EntityRegistrySO _entityRegistry;

        [Tooltip("Optional event raised after the effect is applied (for audio, VFX, HUD flash). Leave empty if not needed.")]
        [SerializeField] private GameEventSO _onPickedEvent;

        private void OnTriggerEnter(Collider other)
        {
            if (_powerup == null || _entityRegistry == null) return;
            if (!_entityRegistry.HasPlayer) return;

            // Only react to the player.
            if (other.transform != _entityRegistry.PlayerTransform &&
                other.transform != _entityRegistry.PlayerTransform.root) return;

            ApplyEffect();
            _onPickedEvent?.Raise();
            Destroy(gameObject);
        }

        private void ApplyEffect()
        {
            Transform playerRoot = _entityRegistry.PlayerTransform;

            switch (_powerup.Effect)
            {
                case PowerupSO.PowerupEffect.Heal:
                {
                    if (playerRoot.TryGetComponent(out PlayerHealth health))
                    {
                        health.Heal(Mathf.RoundToInt(_powerup.Magnitude));
                        Debug.Log($"[PowerupPickup] Heal applied: +{Mathf.RoundToInt(_powerup.Magnitude)} HP.");
                    }
                    else
                    {
                        Debug.LogWarning("[PowerupPickup] Heal powerup: no PlayerHealth found on player root.");
                    }
                    break;
                }

                case PowerupSO.PowerupEffect.MoveSpeedBuff:
                {
                    // TODO: run-scoped buff — wire into a PlayerStats modifier system when implemented.
                    // Effect: add _powerup.Magnitude to move speed for the duration of the current run.
                    // This is intentionally stubbed; adding the buff here without a modifier system would
                    // permanently mutate the shared FloatVariable, which is incorrect.
                    Debug.Log($"[PowerupPickup] MoveSpeedBuff (RUN-SCOPED, STUBBED): +{_powerup.Magnitude} m/s for the run. Implement a stat modifier layer to apply.");
                    break;
                }

                case PowerupSO.PowerupEffect.MaxHealthUp:
                {
                    // TODO: run-scoped buff — wire into a PlayerHealth max-health modifier when implemented.
                    // Effect: raise player's max health by _powerup.Magnitude for the current run.
                    // Stubbed for the same reason as MoveSpeedBuff: requires a modifier/overlay system.
                    Debug.Log($"[PowerupPickup] MaxHealthUp (RUN-SCOPED, STUBBED): +{_powerup.Magnitude} max HP for the run. Implement a max-health modifier layer to apply.");
                    break;
                }

                default:
                    Debug.LogWarning($"[PowerupPickup] Unhandled PowerupEffect: {_powerup.Effect}");
                    break;
            }
        }
    }
}

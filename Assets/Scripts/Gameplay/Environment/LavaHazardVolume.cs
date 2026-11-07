using UnityEngine;
using BlastFrame.Core.Variables;
using BlastFrame.Gameplay.Player;

namespace BlastFrame.Gameplay.Environment
{
    using BlastFrame.Gameplay; // DamageInfo / DamageKind

    /// <summary>
    /// Trigger-volume damage-over-time for lava waterfalls and splash zones. Designer-placed on the
    /// Default layer (the player motor ignores triggers, so these do NOT block movement — you can dash
    /// through them and take a tick). The player takes an immediate first tick on entry, then one tick
    /// per interval while inside. Only the player is tracked — a single cached target is sufficient.
    /// Self-contained: no cross-object Inspector references; the player is found via the collider that
    /// enters the trigger.
    /// </summary>
    public class LavaHazardVolume : MonoBehaviour
    {
        [Tooltip("Damage dealt per tick while the player is inside this volume. Design default 1.")]
        [SerializeField] private IntReference damagePerTick = new IntReference(1);

        [Tooltip("Seconds between damage ticks while the player stays inside. Keep above the player's " +
                 "i-frames (0.5 s) so every tick lands. Design default 0.75.")]
        [SerializeField] private FloatReference tickInterval = new FloatReference(0.75f);

        private Collider _collider;         // our own trigger, for ClosestPoint direction
        private Collider _enterCollider;    // the player collider currently inside (cheap per-tick match)
        private PlayerHealth _target;       // cached player health; only the player takes hazard damage
        private Transform _targetTf;        // player root transform, for ClosestPoint
        private float _nextTick;

        private void Awake()
        {
            if (!TryGetComponent(out _collider))
                Debug.LogWarning("[LavaHazardVolume] No Collider on this object — it can't deal touch damage. " +
                    "Add a trigger Collider.", this);
            else if (!_collider.isTrigger)
                _collider.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_target != null) return; // already tracking the player

            var health = other.GetComponentInParent<PlayerHealth>();
            if (health == null) return;

            _target = health;
            _targetTf = health.transform;
            _enterCollider = other;
            ApplyTick(); // immediate first tick on entry
        }

        private void OnTriggerStay(Collider other)
        {
            if (_target == null || other != _enterCollider) return;
            if (Time.time >= _nextTick) ApplyTick();
        }

        private void OnTriggerExit(Collider other)
        {
            if (other != _enterCollider) return;
            _target = null;
            _targetTf = null;
            _enterCollider = null;
        }

        private void ApplyTick()
        {
            Vector3 playerPos = _targetTf.position;
            Vector3 closestPoint = _collider != null ? _collider.ClosestPoint(playerPos) : playerPos;
            _target.TakeDamage(new DamageInfo(damagePerTick.Value, closestPoint, DamageKind.Hazard));
            _nextTick = Time.time + tickInterval.Value;
        }
    }
}

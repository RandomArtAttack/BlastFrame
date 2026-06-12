using UnityEngine;
using BlastFrame.Core.Variables;

namespace BlastFrame.Gameplay.Enemies
{
    /// <summary>
    /// Tunable stats for an enemy, as Float/Int References. Behavior components read these via
    /// GetComponent in Awake. Stats are NOT on the EntityDefinitionSO — they live here.
    /// </summary>
    public class EnemyStats : MonoBehaviour
    {
        [Tooltip("Max hit points.")]
        [SerializeField] private IntReference health = new IntReference(3);

        [Tooltip("Move speed in m/s (ground robots).")]
        [SerializeField] private FloatReference moveSpeed = new FloatReference(3f);

        [Tooltip("Shots per second.")]
        [SerializeField] private FloatReference fireRate = new FloatReference(1f);

        [Tooltip("Contact / projectile damage dealt to the player.")]
        [SerializeField] private IntReference damage = new IntReference(1);

        [Tooltip("Launch speed of fired projectiles in m/s.")]
        [SerializeField] private FloatReference projectileSpeed = new FloatReference(12f);

        [Tooltip("Detection / firing range in metres.")]
        [SerializeField] private FloatReference range = new FloatReference(30f);

        [Tooltip("Meta currency granted when this enemy dies.")]
        [SerializeField] private IntReference rewardCurrency = new IntReference(1);

        public int Health => health;
        public float MoveSpeed => moveSpeed;
        public float FireRate => fireRate;
        public int Damage => damage;
        public float ProjectileSpeed => projectileSpeed;
        public float Range => range;
        public int RewardCurrency => rewardCurrency;
    }
}

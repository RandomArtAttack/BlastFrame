using UnityEngine;

namespace BlastFrame.Core.Entities
{
    /// <summary>
    /// Factory recipe for a spawnable entity: a prefab + a string id. Stats are NOT here —
    /// they live as Float/Int Reference fields on the entity's own MonoBehaviour. The id matches
    /// the PoolId used in PoolConfigSO.
    /// </summary>
    [CreateAssetMenu(fileName = "EntityDefinition", menuName = "Blast Frame/Entities/Entity Definition")]
    public class EntityDefinitionSO : ScriptableObject
    {
        [Tooltip("Unique string id for this entity. Must match its PoolId in PoolConfigSO.")]
        [SerializeField] private string id;

        [Tooltip("Prefab spawned for this entity.")]
        [SerializeField] private GameObject prefab;

        public string Id => id;
        public GameObject Prefab => prefab;
    }
}

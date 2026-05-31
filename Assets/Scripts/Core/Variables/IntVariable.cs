using UnityEngine;

namespace BlastFrame.Core.Variables
{
    /// <summary>A ScriptableObject wrapping a single int value (Ryan Hipple pattern).</summary>
    [CreateAssetMenu(fileName = "IntVariable", menuName = "Blast Frame/Variables/Int Variable")]
    public class IntVariable : ScriptableObject
    {
        [Tooltip("The int value this asset holds. Designer-tunable. Read-only at runtime for shared assets.")]
        [SerializeField] private int value;

        public int Value
        {
            get => value;
            set => this.value = value;
        }
    }
}

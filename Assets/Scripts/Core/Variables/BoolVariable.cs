using UnityEngine;

namespace BlastFrame.Core.Variables
{
    /// <summary>A ScriptableObject wrapping a single bool value (Ryan Hipple pattern).</summary>
    [CreateAssetMenu(fileName = "BoolVariable", menuName = "Blast Frame/Variables/Bool Variable")]
    public class BoolVariable : ScriptableObject
    {
        [Tooltip("The bool value this asset holds. Designer-tunable. Read-only at runtime for shared assets.")]
        [SerializeField] private bool value;

        public bool Value
        {
            get => value;
            set => this.value = value;
        }
    }
}

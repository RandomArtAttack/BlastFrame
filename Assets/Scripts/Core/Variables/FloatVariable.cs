using UnityEngine;

namespace BlastFrame.Core.Variables
{
    /// <summary>A ScriptableObject wrapping a single float value (Ryan Hipple pattern).
    /// Shared variable assets are read-only at runtime — do not write back to assets you
    /// did not create as runtime clones.</summary>
    [CreateAssetMenu(fileName = "FloatVariable", menuName = "Blast Frame/Variables/Float Variable")]
    public class FloatVariable : ScriptableObject
    {
        [Tooltip("The float value this asset holds. Designer-tunable. Read-only at runtime for shared assets.")]
        [SerializeField] private float value;

        public float Value
        {
            get => value;
            set => this.value = value;
        }
    }
}

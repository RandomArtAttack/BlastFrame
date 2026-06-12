using System;
using UnityEngine;

namespace BlastFrame.Core.Variables
{
    /// <summary>Serializable struct: choose a raw constant OR a FloatVariable asset in the Inspector.</summary>
    [Serializable]
    public struct FloatReference
    {
        [Tooltip("If true, use the typed constant below. If false, read from the Variable asset.")]
        public bool UseConstant;

        [Tooltip("Constant value used when UseConstant is true.")]
        public float ConstantValue;

        [Tooltip("FloatVariable asset read when UseConstant is false.")]
        public FloatVariable Variable;

        public FloatReference(float value)
        {
            UseConstant = true;
            ConstantValue = value;
            Variable = null;
        }

        public float Value => UseConstant ? ConstantValue : Variable.Value;

        public static implicit operator float(FloatReference reference) => reference.Value;
    }

    /// <summary>Serializable struct: choose a raw constant OR an IntVariable asset in the Inspector.</summary>
    [Serializable]
    public struct IntReference
    {
        [Tooltip("If true, use the typed constant below. If false, read from the Variable asset.")]
        public bool UseConstant;

        [Tooltip("Constant value used when UseConstant is true.")]
        public int ConstantValue;

        [Tooltip("IntVariable asset read when UseConstant is false.")]
        public IntVariable Variable;

        public IntReference(int value)
        {
            UseConstant = true;
            ConstantValue = value;
            Variable = null;
        }

        public int Value => UseConstant ? ConstantValue : Variable.Value;

        public static implicit operator int(IntReference reference) => reference.Value;
    }

    /// <summary>Serializable struct: choose a raw constant OR a BoolVariable asset in the Inspector.</summary>
    [Serializable]
    public struct BoolReference
    {
        [Tooltip("If true, use the typed constant below. If false, read from the Variable asset.")]
        public bool UseConstant;

        [Tooltip("Constant value used when UseConstant is true.")]
        public bool ConstantValue;

        [Tooltip("BoolVariable asset read when UseConstant is false.")]
        public BoolVariable Variable;

        public BoolReference(bool value)
        {
            UseConstant = true;
            ConstantValue = value;
            Variable = null;
        }

        public bool Value => UseConstant ? ConstantValue : Variable.Value;

        public static implicit operator bool(BoolReference reference) => reference.Value;
    }
}

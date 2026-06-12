using UnityEditor;
using UnityEngine;

namespace BlastFrame.EditorTools.Variables
{
    /// <summary>
    /// Base drawer for FloatReference/IntReference/BoolReference. Shows a small popup to toggle
    /// between "Use Constant" and "Use Variable", then draws the relevant field inline.
    /// One base class, one thin subclass per type (per CLAUDE.md).
    /// </summary>
    public abstract class VariableReferenceDrawer : PropertyDrawer
    {
        private static readonly string[] PopupOptions = { "Use Constant", "Use Variable" };
        private static GUIStyle _popupStyle;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (_popupStyle == null)
            {
                _popupStyle = new GUIStyle(GUI.skin.GetStyle("PaneOptions")) { imagePosition = ImagePosition.ImageOnly };
            }

            label = EditorGUI.BeginProperty(position, label, property);
            position = EditorGUI.PrefixLabel(position, label);

            var useConstant = property.FindPropertyRelative("UseConstant");
            var constantValue = property.FindPropertyRelative("ConstantValue");
            var variable = property.FindPropertyRelative("Variable");

            int indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            var buttonRect = new Rect(position);
            buttonRect.yMin += _popupStyle.margin.top;
            buttonRect.width = _popupStyle.fixedWidth + _popupStyle.margin.right;
            position.xMin = buttonRect.xMax;

            EditorGUI.BeginChangeCheck();
            int result = EditorGUI.Popup(buttonRect, useConstant.boolValue ? 0 : 1, PopupOptions, _popupStyle);
            if (EditorGUI.EndChangeCheck())
            {
                useConstant.boolValue = result == 0;
            }

            EditorGUI.PropertyField(position, useConstant.boolValue ? constantValue : variable, GUIContent.none);

            EditorGUI.indentLevel = indent;
            EditorGUI.EndProperty();
        }
    }

    [CustomPropertyDrawer(typeof(BlastFrame.Core.Variables.FloatReference))]
    public class FloatReferenceDrawer : VariableReferenceDrawer { }

    [CustomPropertyDrawer(typeof(BlastFrame.Core.Variables.IntReference))]
    public class IntReferenceDrawer : VariableReferenceDrawer { }

    [CustomPropertyDrawer(typeof(BlastFrame.Core.Variables.BoolReference))]
    public class BoolReferenceDrawer : VariableReferenceDrawer { }
}

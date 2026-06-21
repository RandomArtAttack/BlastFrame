using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Inspector for "RAA Materials/RAA URP Lit Projection". URP's stock Lit inspector
/// (UnityEditor.Rendering.Universal.ShaderGUI.LitShader) is INTERNAL, so it can't be subclassed —
/// instead this instantiates it by reflection through the public ShaderGUI base and delegates the
/// whole inspector to it, then appends the triplanar-projection controls the fork adds. Global
/// namespace so the shader's CustomEditor string ("RAALitProjectionShaderGUI") resolves.
/// </summary>
public class RAALitProjectionShaderGUI : ShaderGUI
{
    private const string LitGuiTypeName = "UnityEditor.Rendering.Universal.ShaderGUI.LitShader";

    private ShaderGUI _litGui;
    private bool _litGuiResolved;

    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
    {
        if (!_litGuiResolved)
        {
            _litGui = ResolveLitGui();
            _litGuiResolved = true;
        }

        // Full stock URP Lit inspector (handles surface/blend/workflow + map keyword management).
        if (_litGui != null)
            _litGui.OnGUI(materialEditor, properties);
        else
            base.OnGUI(materialEditor, properties); // fallback: default inspector if URP editor type moved

        EditorGUILayout.Space();
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("RAA Triplanar Projection", EditorStyles.boldLabel);

            MaterialProperty projection = FindProperty("_Projection", properties, false);
            MaterialProperty objectSpace = FindProperty("_ProjectionObjectSpace", properties, false);
            MaterialProperty offset = FindProperty("_ProjectionOffset", properties, false);
            MaterialProperty worldScale = FindProperty("_ProjectionWorldScale", properties, false);

            if (projection != null)
                materialEditor.ShaderProperty(projection, "Projection (world triplanar, off = mesh UVs)");
            if (objectSpace != null)
                materialEditor.ShaderProperty(objectSpace, "Project In Object Space (follows object rotation)");
            if (offset != null)
                materialEditor.ShaderProperty(offset, "Projection Offset (XYZ, world or object space)");
            if (worldScale != null)
                materialEditor.ShaderProperty(worldScale, "Units Scale (units per tile XYZ, default 4)");

            // Keep keywords in sync (the [Toggle] drawers also do this; belt-and-suspenders in
            // case URP's material validation re-runs after the toggles are drawn).
            foreach (UnityEngine.Object target in materialEditor.targets)
            {
                if (target is Material material)
                {
                    if (projection != null)
                        CoreUtils.SetKeyword(material, "_RAA_PROJECTION_ON", projection.floatValue >= 0.5f);
                    if (objectSpace != null)
                        CoreUtils.SetKeyword(material, "_RAA_OBJECT_SPACE", objectSpace.floatValue >= 0.5f);
                }
            }
        }
    }

    private static ShaderGUI ResolveLitGui()
    {
        Type type = Type.GetType(LitGuiTypeName + ", Unity.RenderPipelines.Universal.Editor");
        if (type == null)
        {
            foreach (System.Reflection.Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = asm.GetType(LitGuiTypeName);
                if (type != null) break;
            }
        }
        return type != null ? Activator.CreateInstance(type) as ShaderGUI : null;
    }
}

using UnityEditor;
using UnityEngine;
using BlastFrame.Gameplay.Environment;

namespace BlastFrame.EditorTools
{
    /// <summary>
    /// Creates a lava hazard damage volume (waterfall / splash zone). Spawns a GameObject at the
    /// Scene-view pivot with a trigger BoxCollider + LavaHazardVolume on the Default layer (so the
    /// player motor ignores it — you can dash through it and take a tick, and projectiles pass through).
    /// Size the box over the waterfall and tune the damage/interval on the component.
    /// </summary>
    public static class LavaHazardVolumeWizard
    {
        [MenuItem("Tools/Blast Frame/Environment/Create Lava Hazard Volume")]
        private static void CreateLavaHazardVolume()
        {
            var go = new GameObject("LavaHazardVolume");
            go.layer = 0; // Default — trigger volumes are ignored by the motor; Lava layer stays "raycastable surface"

            var view = SceneView.lastActiveSceneView;
            go.transform.position = view != null ? view.pivot : Vector3.zero;

            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;

            go.AddComponent<LavaHazardVolume>();

            Undo.RegisterCreatedObjectUndo(go, "Create Lava Hazard Volume");
            Selection.activeObject = go;
            EditorGUIUtility.PingObject(go);
        }
    }
}

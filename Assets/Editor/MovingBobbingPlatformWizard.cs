using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using BlastFrame.Gameplay.Environment;
using BlastFrame.Gameplay.Platforms;

namespace BlastFrame.EditorTools
{
    /// <summary>
    /// Creates a MOVING + BOBBING lava platform: a cube with both MovingPlatform (path travel) and
    /// RockBobber (lava bob). RockBobber owns the final move — it takes the path XZ and layers the eased
    /// bob on top, re-sampling the lava under its moving position each tick. Spawns the two child
    /// waypoints MovingPlatform needs (WP0 at the platform, WP1 offset), wires the Lava Mask + Lava River
    /// material, and pings it. Drop it so WP0 sits over the V.4 river (a Lava-layer collider below it).
    /// </summary>
    public static class MovingBobbingPlatformWizard
    {
        private const string RiverMaterialPath = "Assets/Art/Materials/Lava World Materials/Lava River.mat";

        [MenuItem("Tools/Blast Frame/Hazards/Create Moving Bobbing Lava Platform")]
        private static void Create()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "MovingBobbingLavaPlatform";

            var view = SceneView.lastActiveSceneView;
            go.transform.position = view != null ? view.pivot : Vector3.zero;

            // Both behaviours (each RequireComponent(Rigidbody); MovingPlatform also needs a Collider,
            // satisfied by the cube's BoxCollider). RockBobber auto-detects the sibling mover in Awake.
            var bobber = go.AddComponent<RockBobber>();
            var mover = go.AddComponent<MovingPlatform>();

            // Two child waypoints: WP0 at the platform (no start teleport), WP1 offset +X.
            Transform wp0 = NewWaypoint(go.transform, "WP0", Vector3.zero);
            Transform wp1 = NewWaypoint(go.transform, "WP1", new Vector3(5f, 0f, 0f));

            var moverSo = new SerializedObject(mover);
            var wps = moverSo.FindProperty("waypoints");
            wps.arraySize = 2;
            wps.GetArrayElementAtIndex(0).objectReferenceValue = wp0;
            wps.GetArrayElementAtIndex(1).objectReferenceValue = wp1;
            moverSo.ApplyModifiedPropertiesWithoutUndo();

            // Wire RockBobber: Lava Mask -> "Lava" layer, River Material -> the V.4 Lava River material.
            var bobberSo = new SerializedObject(bobber);
            int lavaLayer = LayerMask.NameToLayer("Lava");
            if (lavaLayer != -1)
                bobberSo.FindProperty("lavaMask").intValue = 1 << lavaLayer;

            var river = AssetDatabase.LoadAssetAtPath<Material>(RiverMaterialPath);
            if (river != null)
                bobberSo.FindProperty("riverMaterial").objectReferenceValue = river;
            bobberSo.ApplyModifiedPropertiesWithoutUndo();

            Undo.RegisterCreatedObjectUndo(go, "Create Moving Bobbing Lava Platform");
            Selection.activeObject = go;
            EditorGUIUtility.PingObject(go);

            if (lavaLayer == -1)
                Debug.LogWarning("[MovingBobbingPlatformWizard] No 'Lava' layer found — set RockBobber's Lava Mask to your lava layer.");
            if (river == null)
                Debug.LogWarning($"[MovingBobbingPlatformWizard] River material not found at {RiverMaterialPath} — assign RockBobber's River Material by hand.");
            Debug.Log("[MovingBobbingPlatformWizard] Created. Move WP0/WP1 to lay out the path (keep them over the river), then Play.");
        }

        private static Transform NewWaypoint(Transform parent, string name, Vector3 localPos)
        {
            var wp = new GameObject(name).transform;
            wp.SetParent(parent, false);
            wp.localPosition = localPos;
            return wp;
        }
    }
}

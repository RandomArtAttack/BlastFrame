using UnityEditor;
using UnityEngine;
using BlastFrame.Core.Entities;
using BlastFrame.Gameplay.Enemies;

namespace BlastFrame.EditorTools
{
    /// <summary>
    /// Wizards for turret enemy prefabs. Each wizard builds a prototype GameObject (cylinder base +
    /// cube barrel) with the required components, assigns the EntityRegistry SO, and pings the
    /// result in the Hierarchy. Save as prefab manually after tweaking.
    /// </summary>
    public static class EnemyWizards
    {
        private const string RegistryPath = "Assets/ScriptableObjects/Entities/EntityRegistry.asset";

        // ----- Missile Turret ------------------------------------------------------------------

        [MenuItem("Tools/Blast Frame/Enemies/Create Missile Turret")]
        private static void CreateMissileTurret()
        {
            var registry = AssetDatabase.LoadAssetAtPath<EntityRegistrySO>(RegistryPath);

            // Root — cylinder body.
            var root = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            root.name = "MissileTurret";
            root.transform.localScale = new Vector3(1f, 0.5f, 1f);

            // Add required core components.
            root.AddComponent<BlastFrame.Gameplay.Enemies.EnemyStats>();
            var core = root.AddComponent<BlastFrame.Gameplay.Enemies.EnemyCore>();
            var behavior = root.AddComponent<BlastFrame.Gameplay.Enemies.EnemyBehaviorMissileTurret>();

            // Barrel — cube child aligned forward.
            var barrel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            barrel.name = "Barrel";
            barrel.transform.SetParent(root.transform, false);
            barrel.transform.localPosition = new Vector3(0f, 0.6f, 0.6f);
            barrel.transform.localScale = new Vector3(0.2f, 0.2f, 0.8f);

            // Muzzle — empty child at barrel tip.
            var muzzle = new GameObject("Muzzle");
            muzzle.transform.SetParent(barrel.transform, false);
            muzzle.transform.localPosition = new Vector3(0f, 0f, 0.5f);

            // Wire EntityRegistry into EnemyCore and behavior via SerializedObject.
            if (registry != null)
            {
                var coreSo = new SerializedObject(core);
                coreSo.FindProperty("entityRegistry").objectReferenceValue = registry;
                coreSo.ApplyModifiedPropertiesWithoutUndo();

                var behaviorSo = new SerializedObject(behavior);
                behaviorSo.FindProperty("entityRegistry").objectReferenceValue = registry;
                behaviorSo.FindProperty("muzzle").objectReferenceValue = muzzle.transform;
                behaviorSo.ApplyModifiedPropertiesWithoutUndo();
            }
            else
            {
                Debug.LogWarning("[EnemyWizards] EntityRegistry asset not found at " + RegistryPath +
                                 " — wire it manually into EnemyCore and EnemyBehaviorMissileTurret.");

                // Still wire the muzzle.
                var behaviorSo = new SerializedObject(behavior);
                behaviorSo.FindProperty("muzzle").objectReferenceValue = muzzle.transform;
                behaviorSo.ApplyModifiedPropertiesWithoutUndo();
            }

            Selection.activeGameObject = root;
            EditorGUIUtility.PingObject(root);
            Debug.Log("[EnemyWizards] MissileTurret created. Assign EntityRegistry if prompted, then save as a prefab.");
        }

        // ----- Arc Predict Turret --------------------------------------------------------------

        [MenuItem("Tools/Blast Frame/Enemies/Create Arc Predict Turret")]
        private static void CreateArcPredictTurret()
        {
            var registry = AssetDatabase.LoadAssetAtPath<EntityRegistrySO>(RegistryPath);

            // Root — cylinder body.
            var root = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            root.name = "ArcPredictTurret";
            root.transform.localScale = new Vector3(1f, 0.5f, 1f);

            // Add required core components.
            root.AddComponent<BlastFrame.Gameplay.Enemies.EnemyStats>();
            var core = root.AddComponent<BlastFrame.Gameplay.Enemies.EnemyCore>();
            var behavior = root.AddComponent<BlastFrame.Gameplay.Enemies.EnemyBehaviorArcPredict>();

            // Barrel — cube child angled slightly upward to suggest arc firing.
            var barrel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            barrel.name = "Barrel";
            barrel.transform.SetParent(root.transform, false);
            barrel.transform.localPosition = new Vector3(0f, 0.6f, 0.5f);
            barrel.transform.localScale = new Vector3(0.25f, 0.25f, 0.9f);
            barrel.transform.localEulerAngles = new Vector3(-20f, 0f, 0f);

            // Muzzle — empty child at barrel tip.
            var muzzle = new GameObject("Muzzle");
            muzzle.transform.SetParent(barrel.transform, false);
            muzzle.transform.localPosition = new Vector3(0f, 0f, 0.5f);

            // Wire EntityRegistry and muzzle via SerializedObject.
            if (registry != null)
            {
                var coreSo = new SerializedObject(core);
                coreSo.FindProperty("entityRegistry").objectReferenceValue = registry;
                coreSo.ApplyModifiedPropertiesWithoutUndo();

                var behaviorSo = new SerializedObject(behavior);
                behaviorSo.FindProperty("entityRegistry").objectReferenceValue = registry;
                behaviorSo.FindProperty("muzzle").objectReferenceValue = muzzle.transform;
                behaviorSo.ApplyModifiedPropertiesWithoutUndo();
            }
            else
            {
                Debug.LogWarning("[EnemyWizards] EntityRegistry asset not found at " + RegistryPath +
                                 " — wire it manually into EnemyCore and EnemyBehaviorArcPredict.");

                var behaviorSo = new SerializedObject(behavior);
                behaviorSo.FindProperty("muzzle").objectReferenceValue = muzzle.transform;
                behaviorSo.ApplyModifiedPropertiesWithoutUndo();
            }

            Selection.activeGameObject = root;
            EditorGUIUtility.PingObject(root);
            Debug.Log("[EnemyWizards] ArcPredictTurret created. Assign EntityRegistry if prompted, then save as a prefab.");
        }
    }
}

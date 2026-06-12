using UnityEditor;
using UnityEngine;
using BlastFrame.Core.Entities;
using BlastFrame.Gameplay.Enemies;
using BlastFrame.Gameplay.Enemies.Bosses;

namespace BlastFrame.EditorTools
{
    /// <summary>
    /// Wizards for boss prefabs. Each wizard builds a large-cube prototype GameObject with the
    /// required components, assigns the EntityRegistry SO, and pings the result. Save as a prefab
    /// manually after configuring phases, drop prefab, and event references.
    /// </summary>
    public static class BossWizards
    {
        private const string RegistryPath = "Assets/ScriptableObjects/Entities/EntityRegistry.asset";

        // ----- Mini Boss -----------------------------------------------------------------------

        [MenuItem("Tools/Blast Frame/Enemies/Create Mini Boss")]
        private static void CreateMiniBoss()
        {
            var registry = AssetDatabase.LoadAssetAtPath<EntityRegistrySO>(RegistryPath);

            // Root — large cube to visually distinguish from regular enemies.
            var root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            root.name = "MiniBoss";
            root.transform.localScale = new Vector3(2f, 2f, 2f);

            // Required components.
            root.AddComponent<EnemyStats>();
            var core = root.AddComponent<MiniBossCore>();

            // Two placeholder behaviors that the designer swaps into phases via the Inspector.
            var behaviorA = root.AddComponent<EnemyBehaviorMissileTurret>();
            var behaviorB = root.AddComponent<EnemyBehaviorArcPredict>();

            // Start with behaviorA enabled, behaviorB disabled (phase 0 default).
            behaviorA.enabled = true;
            behaviorB.enabled = false;

            // Wire EntityRegistry into EnemyCore (inherited field on MiniBossCore).
            if (registry != null)
            {
                var coreSo = new SerializedObject(core);
                coreSo.FindProperty("entityRegistry").objectReferenceValue = registry;
                coreSo.ApplyModifiedPropertiesWithoutUndo();

                var soA = new SerializedObject(behaviorA);
                soA.FindProperty("entityRegistry").objectReferenceValue = registry;
                soA.ApplyModifiedPropertiesWithoutUndo();

                var soB = new SerializedObject(behaviorB);
                soB.FindProperty("entityRegistry").objectReferenceValue = registry;
                soB.ApplyModifiedPropertiesWithoutUndo();
            }
            else
            {
                Debug.LogWarning("[BossWizards] EntityRegistry asset not found at " + RegistryPath +
                                 " — wire it manually into MiniBossCore and behavior components.");
            }

            Selection.activeGameObject = root;
            EditorGUIUtility.PingObject(root);
            Debug.Log("[BossWizards] MiniBoss created. Configure phases in MiniBossCore, assign drop prefab, " +
                      "wire onBossDefeated / onWeaponUnlocked events, then save as a prefab.");
        }

        // ----- Boss ----------------------------------------------------------------------------

        [MenuItem("Tools/Blast Frame/Enemies/Create Boss")]
        private static void CreateBoss()
        {
            var registry = AssetDatabase.LoadAssetAtPath<EntityRegistrySO>(RegistryPath);

            // Root — even larger cube to visually distinguish from mini-bosses.
            var root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            root.name = "Boss";
            root.transform.localScale = new Vector3(3f, 3f, 3f);

            // Required components.
            root.AddComponent<EnemyStats>();
            var core = root.AddComponent<BossCore>();

            // Three placeholder behaviors covering a three-phase fight.
            var behaviorA = root.AddComponent<EnemyBehaviorMissileTurret>();
            var behaviorB = root.AddComponent<EnemyBehaviorArcPredict>();
            // Third phase reuses missile turret — duplicate intentional to give the designer
            // independent Inspector configuration for the final phase (e.g. faster fire rate).
            var behaviorC = root.AddComponent<EnemyBehaviorMissileTurret>();

            behaviorA.enabled = true;
            behaviorB.enabled = false;
            behaviorC.enabled = false;

            if (registry != null)
            {
                var coreSo = new SerializedObject(core);
                coreSo.FindProperty("entityRegistry").objectReferenceValue = registry;
                coreSo.ApplyModifiedPropertiesWithoutUndo();

                var soA = new SerializedObject(behaviorA);
                soA.FindProperty("entityRegistry").objectReferenceValue = registry;
                soA.ApplyModifiedPropertiesWithoutUndo();

                var soB = new SerializedObject(behaviorB);
                soB.FindProperty("entityRegistry").objectReferenceValue = registry;
                soB.ApplyModifiedPropertiesWithoutUndo();

                var soC = new SerializedObject(behaviorC);
                soC.FindProperty("entityRegistry").objectReferenceValue = registry;
                soC.ApplyModifiedPropertiesWithoutUndo();
            }
            else
            {
                Debug.LogWarning("[BossWizards] EntityRegistry asset not found at " + RegistryPath +
                                 " — wire it manually into BossCore and behavior components.");
            }

            Selection.activeGameObject = root;
            EditorGUIUtility.PingObject(root);
            Debug.Log("[BossWizards] Boss created. Configure three phases in BossCore, assign drop prefab, " +
                      "wire onBossDefeated / onLevelUnlocked / onWeaponUnlocked events, then save as a prefab.");
        }
    }
}

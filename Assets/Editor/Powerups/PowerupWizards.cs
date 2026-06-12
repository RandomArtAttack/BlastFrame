using System.IO;
using UnityEditor;
using UnityEngine;
using BlastFrame.Core.Entities;
using BlastFrame.Gameplay.Powerups;

namespace BlastFrame.EditorTools
{
    /// <summary>
    /// Editor wizards for the Powerup system.
    ///   Tools/Blast Frame/Powerups/Create Powerup (SO)    — creates a new PowerupSO asset.
    ///   Tools/Blast Frame/Powerups/Create Powerup Pickup  — builds a prototype pickup GameObject.
    /// </summary>
    public static class PowerupWizards
    {
        private const string SoFolder       = "Assets/ScriptableObjects/Powerups";
        private const string RegistryAsset  = "Assets/ScriptableObjects/Entities/EntityRegistry.asset";

        // ------------------------------------------------------------------------------------
        [MenuItem("Tools/Blast Frame/Powerups/Create Powerup (SO)")]
        private static void CreatePowerupSO()
        {
            EnsureFolder(SoFolder);

            string name = "NewPowerup";
            name = EditorUtility.DisplayDialog("Create Powerup SO",
                $"A PowerupSO asset will be created in {SoFolder}/.\n\nEdit id, displayName, effect, and magnitude in the Inspector.",
                "Create", "Cancel")
                ? name
                : null;

            if (name == null) return;

            string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{SoFolder}/{name}.asset");
            var so = ScriptableObject.CreateInstance<PowerupSO>();
            AssetDatabase.CreateAsset(so, assetPath);
            AssetDatabase.SaveAssets();

            Selection.activeObject = so;
            EditorGUIUtility.PingObject(so);
            Debug.Log($"[PowerupWizards] Created PowerupSO at {assetPath}. Set id, displayName, effect, and magnitude in the Inspector.");
        }

        // ------------------------------------------------------------------------------------
        [MenuItem("Tools/Blast Frame/Powerups/Create Powerup Pickup")]
        private static void CreatePowerupPickup()
        {
            // Build a small capsule with a trigger collider and PowerupPickup component.
            var root = new GameObject("PowerupPickup");
            root.transform.position = Vector3.zero;

            // Visual: small glowing capsule (prototype stand-in).
            var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "Visual";
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            visual.transform.localScale = new Vector3(0.4f, 0.4f, 0.4f);

            // The primitive already has a CapsuleCollider — remove it from the visual.
            // The trigger lives on the root so PlayerMotor's sweep contacts it.
            var visualCol = visual.GetComponent<CapsuleCollider>();
            if (visualCol != null) Object.DestroyImmediate(visualCol);

            // Trigger collider on root.
            var triggerCol = root.AddComponent<SphereCollider>();
            triggerCol.isTrigger = true;
            triggerCol.radius = 0.8f;
            triggerCol.center = new Vector3(0f, 0.5f, 0f);

            // PowerupPickup component.
            var pickup = root.AddComponent<PowerupPickup>();

            // Wire EntityRegistry if the asset already exists.
            var reg = AssetDatabase.LoadAssetAtPath<EntityRegistrySO>(RegistryAsset);
            if (reg != null)
            {
                var so = new SerializedObject(pickup);
                so.FindProperty("_entityRegistry").objectReferenceValue = reg;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            else
            {
                Debug.LogWarning($"[PowerupWizards] EntityRegistry asset not found at {RegistryAsset}. Assign it manually on the PowerupPickup component.");
            }

            Selection.activeObject = root;
            EditorGUIUtility.PingObject(root);
            Debug.Log("[PowerupWizards] PowerupPickup GameObject created. Assign a PowerupSO to the '_powerup' field in the Inspector, then save as a prefab.");
        }

        // ------------------------------------------------------------------------------------
        private static void EnsureFolder(string assetPath)
        {
            var parts = assetPath.Split('/');
            string cur = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = cur + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(cur, parts[i]);
                cur = next;
            }
        }
    }
}

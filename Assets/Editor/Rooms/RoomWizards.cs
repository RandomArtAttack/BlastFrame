using System.IO;
using UnityEditor;
using UnityEngine;
using BlastFrame.Gameplay.Rooms;

namespace BlastFrame.EditorTools
{
    /// <summary>
    /// One-click editor wizards for the Room Variant framework.
    /// Creates RoomVariantSO assets, optional variant prefabs, and RoomController slot GameObjects.
    /// </summary>
    public static class RoomWizards
    {
        // ------------------------------------------------------------------
        [MenuItem("Tools/Blast Frame/Rooms/Create Room Variant")]
        private static void CreateRoomVariant()
        {
            // Prompt for a variant id.
            string variantId = "Level01_Room01_VariantA";

            // Make sure destination folders exist.
            EnsureAssetFolder("Assets/ScriptableObjects/RoomVariants");
            EnsureAssetFolder("Assets/Prefabs/RoomVariants");

            // Ask the user whether to also create an empty variant prefab.
            bool createPrefab = EditorUtility.DisplayDialog(
                "Create Room Variant",
                "Also create an empty variant prefab in Assets/Prefabs/RoomVariants/?\n\n" +
                "(You can always drag in an existing prefab manually.)",
                "Yes — create prefab",
                "No — SO only");

            // Build the SO.
            string soPath = $"Assets/ScriptableObjects/RoomVariants/{variantId}.asset";
            RoomVariantSO so = AssetDatabase.LoadAssetAtPath<RoomVariantSO>(soPath);
            if (so == null)
            {
                so = ScriptableObject.CreateInstance<RoomVariantSO>();
                AssetDatabase.CreateAsset(so, soPath);
            }

            GameObject prefab = null;
            if (createPrefab)
            {
                string prefabPath = $"Assets/Prefabs/RoomVariants/{variantId}.prefab";
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab == null)
                {
                    // Create a minimal root GameObject and save as prefab.
                    var root = new GameObject(variantId);
                    prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                    Object.DestroyImmediate(root);
                    Debug.Log($"[RoomWizard] Empty variant prefab created at {prefabPath}.");
                }
            }

            // Wire id and prefab into the SO via SerializedObject.
            var serialized = new SerializedObject(so);
            serialized.FindProperty("_id").stringValue = variantId;
            if (prefab != null)
                serialized.FindProperty("_variantPrefab").objectReferenceValue = prefab;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = so;
            EditorGUIUtility.PingObject(so);
            Debug.Log($"[RoomWizard] RoomVariantSO '{variantId}' created at {soPath}. " +
                      "Set the _id and _variantPrefab fields in the Inspector before use.");
        }

        // ------------------------------------------------------------------
        [MenuItem("Tools/Blast Frame/Rooms/Create Room Slot")]
        private static void CreateRoomSlot()
        {
            // Create the RoomController root.
            var root = new GameObject("RoomSlot");

            // Add RoomController component.
            root.AddComponent<RoomController>();

            // Add child SpawnAnchor.
            var anchor = new GameObject("SpawnAnchor");
            anchor.transform.SetParent(root.transform, false);
            anchor.transform.localPosition = Vector3.zero;

            // Wire the SpawnAnchor into the RoomController.
            var so = new SerializedObject(root.GetComponent<RoomController>());
            so.FindProperty("_spawnAnchor").objectReferenceValue = anchor.transform;
            so.ApplyModifiedPropertiesWithoutUndo();

            Undo.RegisterCreatedObjectUndo(root, "Create Room Slot");

            Selection.activeObject = root;
            EditorGUIUtility.PingObject(root);
            Debug.Log("[RoomWizard] RoomSlot created with child SpawnAnchor. " +
                      "Assign 3 RoomVariantSO assets to the Variants list in the Inspector.");
        }

        // ------------------------------------------------------------------ helpers
        private static void EnsureAssetFolder(string assetPath)
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

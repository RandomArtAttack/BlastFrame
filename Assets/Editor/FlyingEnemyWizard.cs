using UnityEditor;
using UnityEngine;
using BlastFrame.Core.Entities;
using BlastFrame.Core.Events;
using BlastFrame.Gameplay.Enemies;

namespace BlastFrame.EditorTools
{
    /// <summary>
    /// Builds the FlyingEnemy ("Hoverbot") prefab: kinematic interpolated Rigidbody + SphereCollider
    /// root with EnemyStats / EnemyCore / EnemyBehaviorFlyer, a scaled sphere Body (collider stripped),
    /// and a Muzzle child wired into the behavior. Auto-assigns the EntityRegistry SO to both EnemyCore
    /// and the behavior, and the OnEnemyKilled event to EnemyCore if present. Saves to
    /// Assets/Prefabs/Enemies/FlyingEnemy.prefab and pings it.
    /// </summary>
    public static class FlyingEnemyWizard
    {
        private const string PrefabFolder = "Assets/Prefabs/Enemies";
        private const string PrefabPath = "Assets/Prefabs/Enemies/FlyingEnemy.prefab";

        [MenuItem("Tools/Blast Frame/Enemies/Create Flying Enemy Prefab")]
        private static void CreateFlyingEnemy()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
            {
                bool overwrite = EditorUtility.DisplayDialog(
                    "Flying Enemy Prefab Exists",
                    "A prefab already exists at " + PrefabPath + ".\n\nOverwrite it?",
                    "Overwrite", "Cancel");
                if (!overwrite) return;
            }

            // --- Root -------------------------------------------------------------------------------
            var root = new GameObject("FlyingEnemy");
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            if (enemyLayer != -1) root.layer = enemyLayer;
            else Debug.LogWarning("[FlyingEnemyWizard] No 'Enemy' layer found — set the root layer manually.");

            var rb = root.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            var sphere = root.AddComponent<SphereCollider>();
            sphere.radius = 0.5f;

            root.AddComponent<EnemyStats>();
            var core = root.AddComponent<EnemyCore>();
            var behavior = root.AddComponent<EnemyBehaviorFlyer>();

            // --- Body (visual only — collider stripped) --------------------------------------------
            var body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localScale = new Vector3(1f, 0.6f, 1f);
            var bodyCollider = body.GetComponent<Collider>();
            if (bodyCollider != null) Object.DestroyImmediate(bodyCollider);

            // --- Muzzle -----------------------------------------------------------------------------
            var muzzle = new GameObject("Muzzle");
            muzzle.transform.SetParent(root.transform, false);
            muzzle.transform.localPosition = new Vector3(0f, -0.2f, 0.5f);

            // --- SO wiring --------------------------------------------------------------------------
            var registry = FindFirstAsset<EntityRegistrySO>();
            var killedEvent = FindEventAsset("OnEnemyKilled");

            var coreSo = new SerializedObject(core);
            if (registry != null) coreSo.FindProperty("entityRegistry").objectReferenceValue = registry;
            if (killedEvent != null) coreSo.FindProperty("onEnemyKilledEvent").objectReferenceValue = killedEvent;
            coreSo.ApplyModifiedPropertiesWithoutUndo();

            var behaviorSo = new SerializedObject(behavior);
            if (registry != null) behaviorSo.FindProperty("entityRegistry").objectReferenceValue = registry;
            behaviorSo.FindProperty("muzzle").objectReferenceValue = muzzle.transform;
            behaviorSo.ApplyModifiedPropertiesWithoutUndo();

            if (registry == null)
                Debug.LogWarning("[FlyingEnemyWizard] No EntityRegistrySO asset found — wire it manually " +
                                 "into EnemyCore and EnemyBehaviorFlyer.");

            // --- Save as prefab ---------------------------------------------------------------------
            EnsureFolder(PrefabFolder);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);

            if (prefab != null)
            {
                Selection.activeObject = prefab;
                EditorGUIUtility.PingObject(prefab);
                Debug.Log("[FlyingEnemyWizard] FlyingEnemy prefab saved to " + PrefabPath + ".");
            }
            else
            {
                Debug.LogError("[FlyingEnemyWizard] Failed to save prefab at " + PrefabPath + ".");
            }
        }

        private static T FindFirstAsset<T>() where T : Object
        {
            var guids = AssetDatabase.FindAssets("t:" + typeof(T).Name);
            if (guids.Length == 0) return null;
            return AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        private static GameEventSO FindEventAsset(string assetName)
        {
            var guids = AssetDatabase.FindAssets(assetName + " t:GameEventSO");
            for (int i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var evt = AssetDatabase.LoadAssetAtPath<GameEventSO>(path);
                if (evt != null && evt.name == assetName) return evt;
            }
            return null;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;

            var parts = folder.Split('/');
            var current = parts[0]; // "Assets"
            for (int i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}

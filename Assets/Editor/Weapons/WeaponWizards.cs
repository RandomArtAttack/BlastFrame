using System.IO;
using UnityEditor;
using UnityEngine;
using BlastFrame.Gameplay.Projectiles;
using BlastFrame.Gameplay.Weapons;

namespace BlastFrame.EditorTools
{
    /// <summary>
    /// Editor wizards for weapon prefab setup. Each item creates a prefab from primitives,
    /// attaches the required runtime components, and saves under Assets/Prefabs/Projectiles/.
    /// These are the same prefabs that Fix011 references when wiring up pools.
    /// </summary>
    public static class WeaponWizards
    {
        private const string PrefabFolder = "Assets/Prefabs/Projectiles";

        // ---- PlayerProjectile prefab --------------------------------------------------------

        [MenuItem("Tools/Blast Frame/Weapons/Create Player Projectile Prefab")]
        private static void CreatePlayerProjectilePrefab()
        {
            EnsureFolder(PrefabFolder);

            const string path = PrefabFolder + "/PlayerProjectile.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            {
                if (!EditorUtility.DisplayDialog("Overwrite?",
                    "PlayerProjectile.prefab already exists. Overwrite it?", "Overwrite", "Cancel"))
                    return;
            }

            // Build in-scene prototype.
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "PlayerProjectile";
            go.transform.localScale = Vector3.one * 0.25f;

            // Kinematic Rigidbody (no physics-driven movement — ProjectileBase moves it).
            var rb = go.GetComponent<Rigidbody>();
            if (rb == null) rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            // Trigger collider (sphere from CreatePrimitive — just set isTrigger).
            var col = go.GetComponent<SphereCollider>();
            if (col == null) col = go.AddComponent<SphereCollider>();
            col.isTrigger = true;

            // Runtime component.
            go.AddComponent<PlayerProjectile>();

            // Save as prefab.
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);

            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
            Debug.Log($"[WeaponWizards] PlayerProjectile prefab saved to {path}");
        }

        // ---- Explosion VFX prefab -----------------------------------------------------------

        [MenuItem("Tools/Blast Frame/Weapons/Create Explosion VFX Prefab")]
        private static void CreateExplosionVFXPrefab()
        {
            EnsureFolder(PrefabFolder);

            const string path = PrefabFolder + "/Explosion.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            {
                if (!EditorUtility.DisplayDialog("Overwrite?",
                    "Explosion.prefab already exists. Overwrite it?", "Overwrite", "Cancel"))
                    return;
            }

            // Sphere visual — represents the blast wave expanding.
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Explosion";
            go.transform.localScale = Vector3.one;

            // Remove the default collider — explosion damage is handled by OverlapSphereNonAlloc.
            var col = go.GetComponent<SphereCollider>();
            if (col != null) Object.DestroyImmediate(col);

            // Runtime component.
            go.AddComponent<AoeExplosion>();

            // Save as prefab.
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);

            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
            Debug.Log($"[WeaponWizards] Explosion VFX prefab saved to {path}");
        }

        // ---- helpers ------------------------------------------------------------------------

        private static void EnsureFolder(string assetPath)
        {
            // Walk each path segment and create folders as needed.
            var parts = assetPath.Split('/');
            string current = parts[0]; // "Assets"
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}

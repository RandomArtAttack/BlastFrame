using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;

namespace BlastFrame.EditorTools
{
    /// <summary>
    /// Finds (or creates) a NavMeshSurface in the active scene and bakes it, persisting the result as a
    /// NavMeshData asset the surface references so it survives editor restarts. Ground enemies use
    /// NavMeshAgents, so every level scene that hosts them needs this run once.
    /// </summary>
    public static class NavMeshBakeTool
    {
        private const string NavMeshDataFolder = "Assets/NavMeshData";

        [MenuItem("Tools/Blast Frame/Level/Bake NavMesh")]
        private static void BakeNavMesh()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogError("[NavMeshBakeTool] No valid active scene to bake.");
                return;
            }

            NavMeshSurface surface = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                surface = root.GetComponentInChildren<NavMeshSurface>(true);
                if (surface != null) break;
            }

            if (surface == null)
            {
                var go = new GameObject("NavMesh");
                EditorSceneManager.MoveGameObjectToScene(go, scene);
                surface = go.AddComponent<NavMeshSurface>();
                surface.collectObjects = CollectObjects.All;
                surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
                Undo.RegisterCreatedObjectUndo(go, "Create NavMesh Surface");
                Debug.Log("[NavMeshBakeTool] Created a NavMeshSurface (collect All, PhysicsColliders) in " + scene.name + ".");
            }

            surface.BuildNavMesh();

            if (surface.navMeshData != null)
            {
                if (!AssetDatabase.IsValidFolder(NavMeshDataFolder))
                    AssetDatabase.CreateFolder("Assets", "NavMeshData");
                string path = NavMeshDataFolder + "/NavMesh_" + scene.name + ".asset";
                var existing = AssetDatabase.LoadAssetAtPath<NavMeshData>(path);
                if (existing != null && existing != surface.navMeshData) AssetDatabase.DeleteAsset(path);
                if (AssetDatabase.GetAssetPath(surface.navMeshData) == string.Empty)
                    AssetDatabase.CreateAsset(surface.navMeshData, path);
                AssetDatabase.SaveAssets();
            }

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[NavMeshBakeTool] Baked NavMesh for scene '" + scene.name + "'. Save the scene to keep it.");
        }
    }
}

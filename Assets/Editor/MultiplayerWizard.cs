using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using BlastFrame.Core.Entities;
using BlastFrame.Gameplay.Coop;

namespace BlastFrame.EditorTools
{
    /// <summary>
    /// Editor tooling for the multiplayer systems. Idempotent Core-scene setup for the local co-op
    /// manager. (Online NetworkManager setup lives in NetworkWizard once the NGO package is installed.)
    /// </summary>
    public static class MultiplayerWizard
    {
        private const string CorePath = "Assets/Scenes/Core.unity";
        private const string RegistryPath = "Assets/ScriptableObjects/Entities/EntityRegistry.asset";

        [MenuItem("Tools/Blast Frame/Multiplayer/Add Local Coop Manager To Core")]
        public static void AddLocalCoopManagerToCore()
        {
            Scene core = EnsureCoreOpen(out bool openedHere);

            var existing = Object.FindObjectsByType<LocalCoopManager>(FindObjectsSortMode.None);
            LocalCoopManager mgr;
            if (existing.Length > 0)
            {
                mgr = existing[0];
                Debug.Log("[MultiplayerWizard] LocalCoopManager already present — re-wiring.");
            }
            else
            {
                var go = new GameObject("LocalCoopManager");
                SceneManager.MoveGameObjectToScene(go, core);
                mgr = go.AddComponent<LocalCoopManager>();
            }

            var registry = AssetDatabase.LoadAssetAtPath<EntityRegistrySO>(RegistryPath);
            if (registry == null)
                Debug.LogWarning($"[MultiplayerWizard] EntityRegistry not found at {RegistryPath} — assign it by hand.");
            else
            {
                var so = new SerializedObject(mgr);
                so.FindProperty("entityRegistry").objectReferenceValue = registry;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorSceneManager.MarkSceneDirty(core);
            EditorSceneManager.SaveScene(core);
            EditorGUIUtility.PingObject(mgr);
            Debug.Log("[MultiplayerWizard] LocalCoopManager wired into Core and saved.");
        }

        private static Scene EnsureCoreOpen(out bool openedHere)
        {
            openedHere = false;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (s.path == CorePath) return s;
            }
            openedHere = true;
            return EditorSceneManager.OpenScene(CorePath, OpenSceneMode.Additive);
        }
    }
}

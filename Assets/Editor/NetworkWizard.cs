// ---------------------------------------------------------------------------------------------
// ONLINE CO-OP FOUNDATION — editor setup, guarded by BLASTFRAME_NGO.
// Compiles to nothing until NGO is installed and BLASTFRAME_NGO is defined. Written against NGO 1.x;
// NOT verified (package install blocked by environment TLS error). See HANDOFF-008 report.
// ---------------------------------------------------------------------------------------------
#if BLASTFRAME_NGO
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using BlastFrame.Networking;

namespace BlastFrame.EditorTools
{
    /// <summary>
    /// Creates an idle NetworkManager (+ UnityTransport + NetworkSessionLauncher) in the Core scene.
    /// Single-player never starts it; the F10 panel does. You must still assign NetworkManager's
    /// PlayerPrefab to a networked Player prefab (NetworkObject + ClientNetworkTransform +
    /// NetworkPlayerController) and register it in the NetworkPrefabs list.
    /// </summary>
    public static class NetworkWizard
    {
        private const string CorePath = "Assets/Scenes/Core.unity";

        [MenuItem("Tools/Blast Frame/Multiplayer/Add Network Manager To Core")]
        public static void AddNetworkManagerToCore()
        {
            Scene core = EnsureCoreOpen();

            if (Object.FindFirstObjectByType<NetworkManager>() != null)
            {
                Debug.Log("[NetworkWizard] NetworkManager already present in a loaded scene.");
                return;
            }

            var go = new GameObject("NetworkManager");
            SceneManager.MoveGameObjectToScene(go, core);

            var nm = go.AddComponent<NetworkManager>();
            var utp = go.AddComponent<UnityTransport>();
            utp.SetConnectionData("127.0.0.1", 7777);
            nm.NetworkConfig.NetworkTransport = utp;
            go.AddComponent<NetworkSessionLauncher>();

            EditorSceneManager.MarkSceneDirty(core);
            EditorSceneManager.SaveScene(core);
            EditorGUIUtility.PingObject(go);
            Debug.Log("[NetworkWizard] NetworkManager wired into Core (idle). Assign PlayerPrefab by hand.");
        }

        private static Scene EnsureCoreOpen()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (s.path == CorePath) return s;
            }
            return EditorSceneManager.OpenScene(CorePath, OpenSceneMode.Additive);
        }
    }
}
#endif

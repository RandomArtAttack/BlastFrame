using UnityEditor;
using UnityEngine;
using BlastFrame.CameraRig;

namespace BlastFrame.EditorTools
{
    /// <summary>
    /// Adds the CameraFeel juice component to the player's first-person camera in the currently open
    /// scene(s). Finds the camera by its FirstPersonCamera component (the player look rig), skips if
    /// CameraFeel is already present, marks the scene dirty and pings the object.
    /// </summary>
    public static class CameraFeelWizard
    {
        [MenuItem("Tools/Blast Frame/Player/Add Camera Feel")]
        private static void AddCameraFeel()
        {
#if UNITY_2023_1_OR_NEWER
            var rigs = Object.FindObjectsByType<FirstPersonCamera>(FindObjectsSortMode.None);
#else
            var rigs = Object.FindObjectsOfType<FirstPersonCamera>();
#endif
            if (rigs == null || rigs.Length == 0)
            {
                EditorUtility.DisplayDialog(
                    "No First-Person Camera Found",
                    "No FirstPersonCamera exists in any open scene. Open the scene that holds the player " +
                    "camera (it may live in the Core scene) and try again.",
                    "OK");
                return;
            }

            int added = 0;
            GameObject last = null;
            foreach (var rig in rigs)
            {
                last = rig.gameObject;
                if (rig.TryGetComponent<CameraFeel>(out _))
                {
                    Debug.Log("[CameraFeelWizard] '" + rig.name + "' already has CameraFeel — skipped.");
                    continue;
                }
                Undo.AddComponent<CameraFeel>(rig.gameObject);
                EditorUtility.SetDirty(rig.gameObject);
                added++;
                Debug.Log("[CameraFeelWizard] Added CameraFeel to '" + rig.name + "'.");
            }

            if (last != null)
            {
                Selection.activeObject = last;
                EditorGUIUtility.PingObject(last);
            }

            if (added > 0)
                UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        }
    }
}

using System.IO;
using UnityEditor;
using UnityEngine;
using BlastFrame.Core.Audio;

namespace BlastFrame.EditorTools.Audio
{
    /// <summary>
    /// Editor wizards for the Blast Frame audio system.
    /// Menu: Tools / Blast Frame / Audio / ...
    /// </summary>
    public static class AudioWizards
    {
        private const string AudioCueFolder = "Assets/ScriptableObjects/Audio";

        // -----------------------------------------------------------------------------------------
        [MenuItem("Tools/Blast Frame/Audio/Create Audio Cue")]
        private static void CreateAudioCue()
        {
            EnsureFolder(AudioCueFolder);

            string name = "NewAudioCue";
            string path = AssetDatabase.GenerateUniqueAssetPath($"{AudioCueFolder}/{name}.asset");

            var cue = ScriptableObject.CreateInstance<AudioCueSO>();
            AssetDatabase.CreateAsset(cue, path);
            AssetDatabase.SaveAssets();

            Selection.activeObject = cue;
            EditorGUIUtility.PingObject(cue);
            Debug.Log($"[AudioWizards] Created AudioCueSO at {path}. Assign clips and tune volume/pitch in the Inspector.");
        }

        // -----------------------------------------------------------------------------------------

        private static void EnsureFolder(string assetPath)
        {
            // assetPath is "Assets/a/b/c" — create each segment that doesn't exist.
            string[] parts = assetPath.Split('/');
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

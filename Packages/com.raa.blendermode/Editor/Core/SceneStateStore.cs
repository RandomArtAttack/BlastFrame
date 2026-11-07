using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RAA.BlenderMode
{
    /// <summary>A stored SceneView bookmark (pivot/rotation/size/ortho).</summary>
    [Serializable]
    public class CameraBookmark
    {
        public string name;
        public Vector3 pivot;
        public Vector3 rotationEuler;
        public float size = 10f;
        public bool orthographic;
        public bool assigned;
    }

    /// <summary>A named selection set stored as GlobalObjectId strings (survive reload; stale ids skipped).</summary>
    [Serializable]
    public class SelectionSet
    {
        public string name;
        public List<string> objectIds = new List<string>();
    }

    /// <summary>A user-defined transform orientation captured from an object's rotation (Blender custom orientation).</summary>
    [Serializable]
    public class CustomOrientation
    {
        public string name;
        public Vector3 rotationEuler;
    }

    /// <summary>Persisted Blender Mode data for a single scene, keyed by that scene's asset GUID.</summary>
    [Serializable]
    public class PerSceneData
    {
        public string sceneGuid;

        // 3D cursor
        public bool cursorAssigned;
        public Vector3 cursorPosition;
        public Vector3 cursorRotationEuler;

        // Custom orientations (Phase 2), bookmarks, selection sets.
        public List<CustomOrientation> customOrientations = new List<CustomOrientation>();
        public List<CameraBookmark> bookmarks = new List<CameraBookmark>();
        public List<SelectionSet> selectionSets = new List<SelectionSet>();
    }

    /// <summary>
    /// Project-persisted store (ProjectSettings asset) holding per-scene Blender Mode data such as the 3D
    /// cursor, camera bookmarks and selection sets. Scenes are keyed by asset GUID so data survives
    /// domain reloads and scene reopen. Populated by later phases; the container lives here in Phase 1.
    /// </summary>
    [FilePath("ProjectSettings/BlenderModeSceneState.asset", FilePathAttribute.Location.ProjectFolder)]
    public sealed class SceneStateStore : ScriptableSingleton<SceneStateStore>
    {
        [SerializeField] List<PerSceneData> _scenes = new List<PerSceneData>();

        /// <summary>Asset GUID of the active scene, or an empty-string key for unsaved scenes.</summary>
        public static string ActiveSceneGuid()
        {
            // Prefab stage acts like a scene; key it by the prefab asset path.
            PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && !string.IsNullOrEmpty(stage.assetPath))
                return AssetDatabase.AssetPathToGUID(stage.assetPath);

            Scene scene = SceneManager.GetActiveScene();
            if (!string.IsNullOrEmpty(scene.path))
                return AssetDatabase.AssetPathToGUID(scene.path);

            return string.Empty;
        }

        /// <summary>Get the data for a scene GUID, or null if none exists yet.</summary>
        public PerSceneData Find(string sceneGuid)
        {
            for (int i = 0; i < _scenes.Count; i++)
                if (_scenes[i].sceneGuid == sceneGuid)
                    return _scenes[i];
            return null;
        }

        /// <summary>Get-or-create the data for a scene GUID.</summary>
        public PerSceneData GetOrCreate(string sceneGuid)
        {
            PerSceneData data = Find(sceneGuid);
            if (data == null)
            {
                data = new PerSceneData { sceneGuid = sceneGuid };
                _scenes.Add(data);
            }
            return data;
        }

        /// <summary>Convenience accessor for the active scene's data.</summary>
        public PerSceneData ActiveData() => GetOrCreate(ActiveSceneGuid());

        /// <summary>Flush changes to disk.</summary>
        public void Persist() => Save(true);
    }
}

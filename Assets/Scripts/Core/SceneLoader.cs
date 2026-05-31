using UnityEngine;
using UnityEngine.SceneManagement;
using BlastFrame.Core.Services;

namespace BlastFrame.Core
{
    /// <summary>
    /// Service class (not a MonoBehaviour) for async additive scene transitions. Additive loading
    /// exclusively — the Core scene is never unloaded. Registered by CoreBootstrap.
    /// </summary>
    public class SceneLoader : ISceneLoader
    {
        public async Awaitable LoadAdditiveAsync(string sceneName)
        {
            if (IsLoaded(sceneName)) return;
            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            while (op != null && !op.isDone) await Awaitable.NextFrameAsync();
        }

        public async Awaitable UnloadAsync(string sceneName)
        {
            if (!IsLoaded(sceneName)) return;
            var op = SceneManager.UnloadSceneAsync(sceneName);
            while (op != null && !op.isDone) await Awaitable.NextFrameAsync();
        }

        public bool IsLoaded(string sceneName)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.name == sceneName && scene.isLoaded) return true;
            }
            return false;
        }
    }
}

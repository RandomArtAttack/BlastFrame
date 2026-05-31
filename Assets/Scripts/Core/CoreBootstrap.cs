using UnityEngine;
using BlastFrame.Core.Services;

namespace BlastFrame.Core
{
    /// <summary>
    /// Registers all non-MonoBehaviour Core services before any other scene loads. Lives on a
    /// Core-scene GameObject. Service registration happens in Awake; consumers must Get in Start.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class CoreBootstrap : MonoBehaviour
    {
        private SceneLoader _sceneLoader;
        private GameStateMachine _stateMachine;

        private void Awake()
        {
            _sceneLoader = new SceneLoader();
            _stateMachine = new GameStateMachine();
            ServiceLocator.Register<ISceneLoader>(_sceneLoader);
            ServiceLocator.Register<IGameStateMachine>(_stateMachine);
        }

        private void OnDestroy()
        {
            ServiceLocator.Unregister<ISceneLoader>(_sceneLoader);
            ServiceLocator.Unregister<IGameStateMachine>(_stateMachine);
        }
    }
}

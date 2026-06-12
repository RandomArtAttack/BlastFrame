using UnityEngine;
using BlastFrame.Core.Services;

namespace BlastFrame.Core
{
    /// <summary>
    /// After Core finishes registering services, loads the initial gameplay scene additively and
    /// moves the state machine into Run. For the prototype this is the TestLevel; later this is
    /// the MainMenu/HQ flow. Boots in Start so all services are registered first.
    /// </summary>
    public class BootLoader : MonoBehaviour
    {
        [Tooltip("Scene to load additively at boot. Use TestLevel for the prototype test bed.")]
        [SerializeField] private string initialScene = SceneNames.TestLevel;

        [Tooltip("Game state to enter once the initial scene is loaded.")]
        [SerializeField] private GameState initialState = GameState.Run;

        private async void Start()
        {
            var loader = ServiceLocator.Get<ISceneLoader>();
            await loader.LoadAdditiveAsync(initialScene);
            ServiceLocator.Get<IGameStateMachine>().TransitionTo(initialState);
        }
    }
}

using UnityEngine;
using UnityEngine.Events;

namespace BlastFrame.Core.Events
{
    /// <summary>
    /// Scene-side listener for a GameEventSO. Drop on a GameObject, assign the event asset and a
    /// UnityEvent response. Keeps wiring in the Inspector without cross-object code references.
    /// </summary>
    public class GameEventListener : MonoBehaviour
    {
        [Tooltip("The GameEventSO asset to listen to.")]
        [SerializeField] private GameEventSO gameEvent;

        [Tooltip("Invoked when the event is raised.")]
        [SerializeField] private UnityEvent response;

        private void OnEnable() => gameEvent?.Register(OnRaised);
        private void OnDisable() => gameEvent?.Unregister(OnRaised);

        private void OnRaised() => response?.Invoke();
    }
}

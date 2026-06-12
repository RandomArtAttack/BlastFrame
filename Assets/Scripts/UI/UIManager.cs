using UnityEngine;

namespace BlastFrame.UI
{
    /// <summary>
    /// Minimal service-locator-registered holder for the UI root. Lives on the HUD Canvas in the
    /// Core scene. Other systems can reach the UI root via ServiceLocator.Get&lt;UIManager&gt;() but
    /// should prefer event/SO communication over direct calls.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        private void Awake()
        {
            BlastFrame.Core.ServiceLocator.Register<UIManager>(this);
        }

        private void OnDestroy()
        {
            BlastFrame.Core.ServiceLocator.Unregister<UIManager>(this);
        }
    }
}

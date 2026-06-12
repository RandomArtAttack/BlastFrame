using System;
using System.Collections.Generic;
using UnityEngine;

namespace BlastFrame.Core.Events
{
    /// <summary>
    /// Parameterless event channel. Any system raises it; any system listens. No direct
    /// references between systems that only need to react to each other. Listeners are
    /// GameEventListener components or raw C# subscribers.
    /// </summary>
    [CreateAssetMenu(fileName = "OnEvent", menuName = "Blast Frame/Events/Game Event")]
    public class GameEventSO : ScriptableObject
    {
        private readonly List<Action> _listeners = new List<Action>();

        public void Raise()
        {
            for (int i = _listeners.Count - 1; i >= 0; i--)
            {
                _listeners[i]?.Invoke();
            }
        }

        public void Register(Action listener)
        {
            if (!_listeners.Contains(listener)) _listeners.Add(listener);
        }

        public void Unregister(Action listener) => _listeners.Remove(listener);
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

namespace BlastFrame.Core.Events
{
    /// <summary>Typed event channel for passing a payload (GameEventSO&lt;T&gt;).</summary>
    public abstract class GameEventSO<T> : ScriptableObject
    {
        private readonly List<Action<T>> _listeners = new List<Action<T>>();

        public void Raise(T payload)
        {
            for (int i = _listeners.Count - 1; i >= 0; i--)
            {
                _listeners[i]?.Invoke(payload);
            }
        }

        public void Register(Action<T> listener)
        {
            if (!_listeners.Contains(listener)) _listeners.Add(listener);
        }

        public void Unregister(Action<T> listener) => _listeners.Remove(listener);
    }

    [CreateAssetMenu(fileName = "OnIntEvent", menuName = "Blast Frame/Events/Int Event")]
    public class IntGameEventSO : GameEventSO<int> { }

    [CreateAssetMenu(fileName = "OnFloatEvent", menuName = "Blast Frame/Events/Float Event")]
    public class FloatGameEventSO : GameEventSO<float> { }

    [CreateAssetMenu(fileName = "OnStringEvent", menuName = "Blast Frame/Events/String Event")]
    public class StringGameEventSO : GameEventSO<string> { }
}

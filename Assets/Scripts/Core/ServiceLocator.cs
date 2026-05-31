using System;
using System.Collections.Generic;

namespace BlastFrame.Core
{
    /// <summary>
    /// Static service registry. Services register themselves in Awake and deregister in
    /// OnDestroy. Consumers call Get&lt;T&gt;() in Start (never Awake) to guarantee registration
    /// order. Fails loud: Get throws if the service is missing — no silent nulls.
    /// Singletons are banned; this is the only globally reachable access point.
    /// </summary>
    public static class ServiceLocator
    {
        private static readonly Dictionary<Type, object> Services = new Dictionary<Type, object>();

        public static void Register<T>(T service) where T : class
        {
            var type = typeof(T);
            if (Services.ContainsKey(type))
            {
                UnityEngine.Debug.LogWarning($"[ServiceLocator] Overwriting already-registered service {type.Name}.");
            }
            Services[type] = service;
        }

        public static void Unregister<T>(T service) where T : class
        {
            var type = typeof(T);
            if (Services.TryGetValue(type, out var existing) && ReferenceEquals(existing, service))
            {
                Services.Remove(type);
            }
        }

        public static T Get<T>() where T : class
        {
            if (Services.TryGetValue(typeof(T), out var service))
            {
                return (T)service;
            }
            throw new InvalidOperationException(
                $"[ServiceLocator] Service '{typeof(T).Name}' is not registered. " +
                "Register it in Awake before any consumer calls Get in Start.");
        }

        public static bool IsRegistered<T>() where T : class => Services.ContainsKey(typeof(T));

        public static bool TryGet<T>(out T service) where T : class
        {
            if (Services.TryGetValue(typeof(T), out var raw))
            {
                service = (T)raw;
                return true;
            }
            service = null;
            return false;
        }

        /// <summary>Clears all services — call only on full teardown / play-mode exit.</summary>
        public static void Clear() => Services.Clear();
    }
}

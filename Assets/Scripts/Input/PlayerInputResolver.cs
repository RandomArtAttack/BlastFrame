using UnityEngine;
using BlastFrame.Core;

namespace BlastFrame.Input
{
    /// <summary>
    /// Resolves the correct <see cref="IPlayerInput"/> for a consumer component based on the
    /// <see cref="PlayerIdentity"/> found on it or a parent. Falls back to index 0 (and to the global
    /// service if no provider is registered) so single-player and any legacy consumer behave exactly as
    /// before. Consumers call this in Start (never Awake) — the provider registers in Awake.
    /// </summary>
    public static class PlayerInputResolver
    {
        public static IPlayerInput Resolve(Component consumer)
        {
            int index = 0;
            var id = consumer.GetComponentInParent<PlayerIdentity>();
            if (id != null) index = id.PlayerIndex;

            if (ServiceLocator.TryGet<IPlayerInputProvider>(out var provider))
            {
                var input = provider.GetInput(index);
                if (input != null) return input;
            }

            return ServiceLocator.Get<IPlayerInput>();
        }
    }
}

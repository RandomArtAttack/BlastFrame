using System;
using BlastFrame.Core.Services;

namespace BlastFrame.Core
{
    /// <summary>
    /// Enum-driven game flow FSM. Orchestrates transitions and raises OnStateChanged — it holds no
    /// gameplay logic. Systems react to the event rather than querying state, but CurrentState is
    /// exposed for the rare direct check.
    /// </summary>
    public class GameStateMachine : IGameStateMachine
    {
        public GameState CurrentState { get; private set; } = GameState.Boot;

        public event Action<GameState> OnStateChanged;

        public void TransitionTo(GameState next)
        {
            if (next == CurrentState) return;
            CurrentState = next;
            OnStateChanged?.Invoke(next);
        }
    }
}

namespace BlastFrame.Core
{
    /// <summary>Top-level game flow states (Boot → MainMenu → HQ → Run, etc.).</summary>
    public enum GameState
    {
        Boot,
        MainMenu,
        HQ,
        Loading,
        Run,
        Paused,
        Death,
        RunComplete,
        GameOver
    }

    /// <summary>Run difficulty, chosen at HQ. Scales enemy count/stats/hazards/reward.</summary>
    public enum Difficulty
    {
        Easy,
        Medium,
        Hard
    }

    /// <summary>Waypoint traversal mode for MovingPlatform.</summary>
    public enum PathMode
    {
        Cycle,              // loop: after the last waypoint, travel back to the first as a path segment
        PingPong,           // reverse direction at each end
        ReuseLoopTeleport   // on reaching the last waypoint, INSTANTLY warp back to the first and continue
                            // (one-way conveyor; lets a platform vanish into an inaccessible area and reappear)
    }
}

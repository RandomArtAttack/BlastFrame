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
        Cycle,
        PingPong
    }
}

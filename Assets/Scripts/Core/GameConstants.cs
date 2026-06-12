namespace BlastFrame.Core
{
    /// <summary>Scene name constants — no magic strings for scene loading.</summary>
    public static class SceneNames
    {
        public const string Core = "Core";
        public const string MainMenu = "MainMenu";
        public const string HQ = "HQ";
        public const string TestLevel = "TestLevel";
        public const string Level01 = "Level01";
    }

    /// <summary>Pool id constants — must match PoolConfigSO entries and EntityDefinitionSO ids.</summary>
    public static class PoolIds
    {
        public const string PlayerProjectile = "PlayerProjectile";
        public const string EnemyMissile = "EnemyMissile";
        public const string ArcProjectile = "ArcProjectile";
        public const string ArcExplosion = "ArcExplosion";
        public const string Explosion = "Explosion";
    }

    /// <summary>Audio mixer exposed parameter names — no magic strings for mixer params.</summary>
    public static class AudioMixerParams
    {
        public const string MasterVolume = "MasterVolume";
        public const string MusicVolume = "MusicVolume";
        public const string SfxVolume = "SfxVolume";
    }

    /// <summary>Input action + map names — no magic strings when looking up actions.</summary>
    public static class InputActionNames
    {
        public const string PlayerMap = "Player";
        public const string Move = "Move";
        public const string Look = "Look";
        public const string Jump = "Jump";
        public const string Dash = "Sprint";
        public const string Fire = "Attack";
        public const string Interact = "Interact";
    }
}

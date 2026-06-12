namespace BlastFrame.Gameplay
{
    /// <summary>
    /// Anything that can take damage. Player projectiles call this on what they hit; enemy attacks
    /// call it on the player. Decouples weapons from specific health implementations.
    /// </summary>
    public interface IDamageable
    {
        void TakeDamage(int amount);
        bool IsDead { get; }
    }
}

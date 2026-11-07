namespace BlastFrame.Gameplay
{
    /// <summary>
    /// Anything that can take damage. Player projectiles call this on what they hit; enemy attacks
    /// call it on the player. Decouples weapons from specific health implementations.
    /// </summary>
    public interface IDamageable
    {
        void TakeDamage(int amount);
        /// <summary>Damage with source info. Default forwards to the plain overload so existing
        /// implementers (enemies) are unaffected; PlayerHealth overrides it.</summary>
        void TakeDamage(in DamageInfo info) => TakeDamage(info.Amount);
        bool IsDead { get; }
    }
}

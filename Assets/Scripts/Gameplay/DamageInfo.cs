namespace BlastFrame.Gameplay
{
    /// <summary>Category of a damage event — UI and audio react differently per kind.</summary>
    public enum DamageKind { Unknown = 0, Projectile, Explosion, Contact, Hazard, Submersion }

    /// <summary>
    /// Payload describing one damage event. SourcePosition is the world point the damage came FROM
    /// (projectile impact origin, explosion center, hazard volume point) so UI can show direction.
    /// HasSource=false means "no meaningful direction" (e.g. submersion) — indicators must skip it.
    /// </summary>
    public readonly struct DamageInfo
    {
        public readonly int Amount;
        public readonly UnityEngine.Vector3 SourcePosition;
        public readonly DamageKind Kind;
        public readonly bool HasSource;

        public DamageInfo(int amount, UnityEngine.Vector3 sourcePosition, DamageKind kind)
        { Amount = amount; SourcePosition = sourcePosition; Kind = kind; HasSource = true; }

        public DamageInfo(int amount, DamageKind kind)
        { Amount = amount; SourcePosition = default; Kind = kind; HasSource = false; }
    }
}

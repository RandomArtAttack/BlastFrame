# SPEC-000 — Shared Damage Pipeline (DamageInfo + player i-frames)

**Status:** foundation spec — PLAN-002/003/004 all depend on this. Implement FIRST.
**Author:** Fable 5, 2026-07-07. **Implementer target:** any Claude model, no conversation context needed.

## Why

Three upcoming features (lava damage, directional damage indicators, flying enemy) all need to know
*where* damage came from and *what kind* it is. Today every damage path funnels through
`IDamageable.TakeDamage(int amount)` (`Assets/Scripts/Gameplay/IDamageable.cs`) with no source
information, and `PlayerHealth` has no invulnerability window — a burst (explosion + contact in the
same tick) can chunk multiple of the player's 5 HP instantly. This spec adds a `DamageInfo` payload
and player i-frames once, so the three features don't invent three incompatible damage paths.

## Decisions made for you (veto before running if wrong)

1. **Default interface method, not a breaking change.** `IDamageable` gains an overload with a
   default implementation that forwards to the old method. Enemy code (`EnemyCore`) needs zero
   changes. C# 9 / .NET Standard 2.1 in Unity 6 supports DIM.
2. **I-frames are player-only** (on `PlayerHealth`), default **0.5 s**, as a `FloatReference`.
   Enemies never get i-frames.
3. **Lava DoT still ticks through i-frames by design spacing**, not by bypass: lava tick interval
   (0.75 s, see PLAN-003) is simply longer than the i-frame window. No special-case code.

## Changes

### 1. New file: `Assets/Scripts/Gameplay/DamageInfo.cs`

```csharp
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
```

### 2. `Assets/Scripts/Gameplay/IDamageable.cs` — add DIM overload

```csharp
public interface IDamageable
{
    void TakeDamage(int amount);
    /// <summary>Damage with source info. Default forwards to the plain overload so existing
    /// implementers (enemies) are unaffected; PlayerHealth overrides it.</summary>
    void TakeDamage(in DamageInfo info) => TakeDamage(info.Amount);
    bool IsDead { get; }
}
```

### 3. `Assets/Scripts/Gameplay/Player/PlayerHealth.cs`

- Add `[SerializeField] FloatReference invulnerabilitySeconds = new FloatReference(0.5f);` with a
  Tooltip ("Seconds of invulnerability after taking any damage. 0 disables.").
- Add `public bool IsInvulnerable => Time.time < _invulnUntil;` (private `float _invulnUntil`).
- Add `public event Action<DamageInfo> OnDamagedInfo;`
- Implement `public void TakeDamage(in DamageInfo info)` as THE canonical path:
  - early-out if `info.Amount <= 0 || IsDead || IsInvulnerable`;
  - apply damage exactly as the current `TakeDamage(int)` body does (clamp, `OnHealthChanged`,
    `onDamagedEvent?.Raise()`, death events);
  - set `_invulnUntil = Time.time + invulnerabilitySeconds.Value;`
  - invoke `OnDamagedInfo?.Invoke(info)` (after `OnHealthChanged`, before death check is fine —
    keep death events last).
- Change the existing `TakeDamage(int amount)` to a one-liner:
  `TakeDamage(new DamageInfo(amount, DamageKind.Unknown));`
- `Heal`/`ResetHealth` unchanged.

### 4. Call-site updates (pass source where the caller knows it)

- `Assets/Scripts/Gameplay/Projectiles/EnemyMissile.cs` `OnTriggerEnter`:
  `damageable?.TakeDamage(new DamageInfo(_damage, transform.position, DamageKind.Projectile));`
- `Assets/Scripts/Gameplay/Weapons/AoeExplosion.cs` `OnSpawn` loop:
  `damageable.TakeDamage(new DamageInfo(damage.Value, transform.position, DamageKind.Explosion));`
- `Assets/Scripts/Gameplay/Projectiles/PlayerProjectile.cs` `OnTriggerEnter`:
  `damageable?.TakeDamage(new DamageInfo(_damage, transform.position, DamageKind.Projectile));`
  (enemies ignore the info today; uniformity is the point).

## Conventions that apply (from CLAUDE.md — the implementer must follow)

- Every `[SerializeField]` gets a `[Tooltip]`. Use `FloatReference` for the i-frame duration.
- No `Find`, no singletons, no allocations in hot paths (`DamageInfo` is a readonly struct — pass
  with `in`).
- MonoBehaviour lifecycle methods stay private.

## Acceptance

- Project compiles (verify in Unity later — editor closed at implementation time).
- Enemy damage behavior unchanged (EnemyCore untouched).
- Two `TakeDamage` calls on PlayerHealth within 0.5 s: second is ignored.
- `OnDamagedInfo` fires with the same info the caller passed.

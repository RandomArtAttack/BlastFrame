# PLAN-001 — Structural Review Findings

**Status:** assessment (read-only) — findings are prioritized and individually scoped; no
mega-refactor. **Author:** Fable 5, 2026-07-07, from direct reads of the code listed at the bottom.

## Verdict up front

The codebase is in unusually good shape. A smell-grep across `Assets/Scripts` for the project's own
banned patterns (`FindObjectOfType`, `GameObject.Find`, `Camera.main`, `DontDestroyOnLoad`, static
`Instance`, LINQ) returns **zero runtime hits**. Pooling, no-alloc buffers, FloatReference tuning,
self-containment, and the registry/event patterns are applied consistently. There is no structural
debt worth a dedicated refactor pass. The findings below are targeted, not systemic.

## Findings (priority order)

### F1 — No damage-source pipeline; no player i-frames  `P0 · being implemented tonight (SPEC-000)`
All damage flows through `IDamageable.TakeDamage(int)` with no source position or kind, and
`PlayerHealth` (5 HP) has no invulnerability window — an `AoeExplosion` (default damage 3) plus a
missile in the same tick can near-kill from full health. Blocks lava damage, directional
indicators, and clean flying-enemy damage. Fix: `DamageInfo` struct + DIM overload + i-frames.
See `SPEC-000-damage-pipeline.md`. Effort: S.

### F2 — Projectiles detonate on ANY trigger volume  `P0 · being implemented tonight (PLAN-003)`
`EnemyMissile.OnTriggerEnter`, `ArcProjectile.OnTriggerEnter`, `PlayerProjectile.OnTriggerEnter`
all treat any trigger contact as an impact. The moment invisible gameplay trigger volumes exist
(lava hazard volumes in PLAN-003, future secret/room triggers), missiles will explode on thin air.
Fix (one line per file): ignore colliders where `other.isTrigger` is true and no `IDamageable`
ancestor exists. Effort: S.

### F3 — V4 lava CPU-mirror logic will be duplicated  `P1 · being implemented tonight (PLAN-003)`
`RockBobber` contains the only CPU mirror of the `RAA_LavaFlow_V4` vertex displacement
(`CacheV4` + `SampleNoiseDisp`, RockBobber.cs:335-370). The lava submersion detector needs the
identical math; duplicating it means the two drift apart when the shader changes. Fix: extract a
static `LavaV4Sampler` (params struct + TryCache + SampleDisp), refactor RockBobber to consume it
behavior-identically. Verified safe: `LavaFlowRandomizer` never randomizes `_NoiseMap_ST` /
`_NoiseMove` via its MaterialPropertyBlock, so reading `sharedMaterial` noise params remains
correct per-instance. Effort: S-M.

### F4 — AoeExplosion damages through walls and trusts prefab layer config  `P1 · recommendation`
`AoeExplosion.OnSpawn` overlaps a sphere and damages every `IDamageable` with no occlusion check;
`damageLayers` defaults to `~0` (everything). A charged shot exploding at a wall damages enemies
behind it; an enemy `ArcExplosion` prefab mis-configured to `~0` would damage other enemies
(glossary rule: enemy weapons must not hit enemies). Recommend: (a) optional bool `requireLineOfSight`
that raycasts explosion→target and skips occluded targets; (b) an editor validation (Implement Fix)
that asserts the ArcExplosion prefab's `damageLayers` is exactly the Player layer and the player
Explosion prefab's excludes Player. Effort: S-M. Not done tonight — touches hand-tuned prefabs.

### F5 — Pooled projectiles resolve IPoolManager in Start()  `P2 · recommendation`
`EnemyMissile`, `ArcProjectile`, `ProjectileBase` fetch the pool in `Start()`. A pooled instance
spawned and impacting in the same tick (point-blank shot) runs `OnTriggerEnter` before `Start` →
`_pool` is null → `Despawn()` sets `_despawned` but never returns the object; it idles active until
lifetime code is also dead (it is — `_despawned` gates FixedUpdate), leaving an orphan visual and a
leaked pool slot. Fix: resolve lazily (`_pool ??= ServiceLocator.Get<IPoolManager>()` inside
Despawn) or in `OnSpawn`. Effort: S.

### F6 — HUD widgets poll Update() until the player registers  `P2 · recommendation`
`HealthDisplay` (and siblings) retry binding every frame via `Update()`. Works, but each new widget
copies the pattern (PLAN-003/004 add two more). Recommend `EntityRegistrySO` expose
`event Action<Transform> OnPlayerRegistered` raised in `RegisterPlayer`, and widgets subscribe
instead of polling. Effort: S. Tonight's new widgets follow the EXISTING polling pattern for
consistency; migrate all at once later if desired.

### F7 — Line-of-sight logic is private to ArcPredict  `P2 · handled pragmatically in PLAN-002`
`EnemyBehaviorArcPredict.HasLineOfSight/IsPointVisible` (multi-point bounds sampling) is exactly
what the flying enemy needs. PLAN-002 extracts a static `EnemyLineOfSight` helper for the flyer.
Refactoring ArcPredict to call it is OPTIONAL and deferred — the turret is tuned and shipping
behavior; don't destabilize it in the same pass. Effort of deferred refactor: S.

### F8 — VSync default vs 60-cap flip-flop  `P3 · design flag only`
`GameManager` ships `vSyncCount = 1` (VSync on, `targetFrameRate` ignored). The glossary lists this
as unresolved. No action — just be aware the code currently answers the question as "VSync".

### Non-findings (checked, fine)
- `MovingPlatform` accretion (smoothing, reuse-loop, enable/disable lists) is contained: sealed
  class, self-owned state, external-driver contract with RockBobber is explicit. Leave it.
- `GameManager.AlignPhysicsToRefreshRate` divisor math is correct (144→72 Hz, 240→60 Hz, 60→60 Hz).
- `PlayerMotor` sweep/depenetration ordering and no-alloc buffers are sound.
- `PlatformRiderModule` grace/suppress logic is correct against jump-snapshot design.

## Review boundary (not audited)
`SaveManager`, `GameStateMachine`, `RunManager`, `LevelController`, `RoomController/VariantSelector`,
HQ/Shop/Economy, `ChargeShot`/`PlayerShooter`, `JumpModule`/`DashModule`/`WallSlideModule`/
`LedgeVaultModule`, `AudioManager`, `BossCore`/`MiniBossCore`. Skimmed or unread — no claims made.

## Files read for this review
PlayerMotor, PlayerController, PlayerHealth, IDamageable, EnemyCore, EnemyStats, EnemyBehaviorBase,
EnemyBehaviorArcPredict, ArcProjectile, EnemyMissile, PlayerProjectile, ProjectileBase, AoeExplosion,
PoolManager, GameManager, GameConstants, EntityRegistrySO, HealthDisplay, HUDController, UIManager,
FirstPersonCamera, IMovementModule, PlatformRiderModule, MovingPlatform, RockBobber,
LavaFlowRandomizer, RAA_LavaFlow_V4.shader, TagManager.asset (layers: Player 6, Enemy 7,
Viewmodel 8, PlayerWeapon 9, EnemyWeapon 10, Lava 31).

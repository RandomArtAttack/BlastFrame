# Blast Frame — Game Design Doc (running design log)

Newer entries at the top. Never reorder or delete old entries.

---

[2026-05-31] — HQ / ECONOMY / SHOP: ARCHITECTURE DECISIONS
Meta-currency (metaCurrency) is the single economy unit: earned per run, spent only at HQ on permanent upgrades. CurrencyManager owns the live wallet and registers as ICurrencyManager; it bootstraps from SaveData if a SaveManager is present and falls back to 0 otherwise, so the shop works without a save system wired. Permanent upgrades are authoring-time SOs (PermanentUpgradeSO) with IntReference cost and FloatReference magnitude — constants by default, optionally driven by Variable SO assets if a designer wants global tuning. ShopManager owns purchase logic and defers ownership persistence to ISaveManager (TryGet — optional), keeping the shop functional even without persistence. ShopUI is fully event-driven: it never polls in Update; OnCurrencyChanged and onPurchasedEvent drive all row refreshes. The "cannot afford" state is a visual dim, not a hidden row, so players can see what they are working toward.

[2026-05-31] — LEVEL & RUN MANAGEMENT: ORCHESTRATION DESIGN
RunManager is the single source of truth for run state (active, difficulty, level/room index). It sits in Core and registers as IRunManager so any system can reach it via ServiceLocator without a direct reference. LevelController lives per-level-scene and drives room advancement by listening to a GameEventSO rather than calling RunManager directly — this keeps it fully decoupled from the room feature. Difficulty is chosen at StartRun() call time and exposed as a read-only property; room-count and difficulty-scaling multipliers live on the LevelDefinitionSO so designers can tune each of the 9 levels independently.

[2026-05-31] — POWERUPS: RUN-SCOPE AND STUB STRATEGY
All pickups (Heal, MoveSpeedBuff, MaxHealthUp) are run-scoped and lost on death — they must never be written to save data. Heal is fully implemented via PlayerHealth.Heal. MoveSpeedBuff and MaxHealthUp are intentionally stubbed with TODO comments: mutating a shared FloatVariable without a modifier/overlay layer would permanently corrupt the stat for every subsequent run. A stat-modifier system (additive/multiplicative layers that reset on death) will be the correct unlock point for those effects.

[2026-05-31] — HUD: WIDGET BINDING STRATEGY
HUD widgets bind defensively to player components via EntityRegistrySO — they retry each frame in Update until the registry reports HasPlayer, then subscribe and stop polling. This means the HUD can live in Core and start before the player without race conditions. Each widget also accepts a direct registry injection via SetRegistry() from HUDController.Awake as a faster path when both exist simultaneously. The ChargeBar starts hidden and only becomes visible when charge > 0, keeping the screen clean during normal movement.

[2026-05-31] — PLAYER SHOOTING: CHARGE SYSTEM DESIGN
The charge shot is designed as a continuous spectrum rather than a binary tap/charge. Any release fires a projectile; charge level 0..1 linearly scales damage (base 1 to 5), size (0.25x to 1x), and speed (30 m/s tap down to 22 m/s fully charged — slower = heavier feel). The AoE explosion triggers at any charge ≥ 0.5, making partial charges a meaningful choice. Full charge time defaults to 1 second. These numbers are all FloatReference/IntReference fields and designer-tunable without code changes.

[2026-05-31] — PLAYER SHOOTING: POOLING STRATEGY
PlayerProjectile and AoeExplosion are fully pooled (no runtime Instantiate during play). PlayerProjectile prewarms 20 instances (fast tap-fire feel without expansion), Explosion prewarms 8. Both expand by half their prewarm count when exhausted and log a warning. The player camera IS the muzzle — no separate muzzle transform needed since the FPS view means the camera forward IS the aim direction.

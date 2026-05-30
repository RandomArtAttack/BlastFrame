# Blast Frame — Design & Architecture Reference

> **Purpose.** Canonical architecture spec + build checklist for Blast Frame. Source of truth for
> *how the game is built*. `Assets/GameDesign.md` is the plain-language *why* log; `Assets/GameInfo.md`
> is the editor *how-to-wire* reference; `LAST_LEFT_OFF.txt` is the transient "where's the needle" snapshot.
> Claude reads this when it has an architecture question and updates it when architecture changes.
> Where this file and CLAUDE.md disagree, **DESIGN.md wins** (it reflects later decisions).
>
> **How to use the checklist.** Build one item at a time, top to bottom, each provable in an empty
> test scene before integration.

---

## 0. Project Reality (2026-05-30)

- **Greenfield.** Despite CLAUDE.md / LAST_LEFT_OFF.txt describing built movement/combat, **no game
  code exists** — `Assets/` holds only URP-template files, git is a single initial commit, and only
  `SampleScene.unity` exists (no Core/Level01). The detailed CLAUDE.md Player Architecture is treated
  as the **canonical written movement spec** (see §4) — there is no code to read, only a spec to build.
- A misplaced nested Unity project `Blood and Bark/` was found at the root and **deleted** (empty URP
  template, untracked, no work lost).

---

## 1. Pillars / Identity

- **Genre:** 3D first-person shooter + precision platformer, run-based **roguelike**, Mega-Man-flavored.
- **Pitch:** Traverse 9 robot-infested areas. Run/jump/dash/wall-slide and charge-shot through rooms
  that re-roll their layout, enemies, and enemy *variants* every run. Bosses drop weapons/abilities.
  Death → HQ to spend meta currency on permanents, then run again. **Beating areas changes other areas.**
- **Stressed design value:** *modular variability* — one reusable seeded-roll system drives room
  layouts, spawn choices, and enemy variants (§6).

---

## 2. Tech Stack (from CLAUDE.md — load-bearing)

- Unity 6 (6000.3.10f1) URP 3D, C# 9. Newtonsoft.Json. TextMeshPro (no UI Toolkit).
- **Player:** custom **kinematic interpolated Rigidbody** motor (NOT CharacterController, NOT AddForce).
- Input: Unity Input System via `InputSystem_Actions.inputactions`, wrapped by `PlayerInputHandler`.
- Camera: custom first-person rig (no Cinemachine).
- Numbers: int/float only (never double).

---

## 3. Cross-Cutting Conventions

Component self-containment (no cross-scene Inspector refs — use EntityRegistrySO / ServiceLocator /
GameEventSO); FloatReference/IntReference stats; services are interfaces; generic `Pool<T>` pre-warmed;
no singletons/Find/DontDestroyOnLoad; no magic strings; additive scenes (Core always loaded, Player
persists across level loads within a run); AES-encrypted JSON save (IDs only, resolved via registries);
editor tooling ships with each feature (`Tools/Blast Frame/...`); numbered `ImplementFix.cs` actions.

---

## 4. Movement (canonical spec — build to match; decisions locked 2026-05-30)

Kinematic Rigidbody motor performing a manual move/collide sweep each FixedUpdate; exposes
grounded/wall state. Modules are sibling components on the Player root, orchestrated by `PlayerController`.
**All numbers below are `FloatReference`/`IntReference` — tune in-editor; the values are design targets.**

- **Ground feel — snappy / near-instant.** Horizontal velocity snaps to the target each tick and stops
  near-instantly. NO ground accel ramp or friction-slide. Momentum is expressed only through dash and
  air, never through ground sliding (keeps precision landings tight).
- **JumpModule** — base kit is a **single** jump (wall jumps + dash-jump add the rest; double-jump is
  an unlockable boss ability, NOT base).
  - Full-hold apex **= 4 units (~4m)**, ~2× player height. **Gravity is tuned UP** to keep air time
    tight and avoid float at that height.
  - **Variable height:** releasing jump while still rising clamps upward velocity (short hop).
  - Coyote time (~0.1s) + jump buffer (~0.12s) for feel. Terminal fall-velocity cap.
  - Air control = full directional steering (rotate/redirect horizontal velocity).
- **DashModule — ground-only burst.**
  - Direction = **movement-input direction** (WASD relative to facing), **flattened to horizontal**;
    no input → dash straight forward. Direction **locks at dash start** (not steerable as a raw dash).
  - Fixed `dashDuration` (~0.72s) at `dashSpeed` (~15.4) → ~11m; then `dashCooldown` (~5s).
  - **Jump-cancellable** (see dash-jump). Raises OnDashStarted / OnDashCooldownChanged for the UI ring.
  - **No air-dash** at base. (A deliberate air-dash is the only realistic way back to a just-left wall.)
  - OPEN TUNING: dash distance ~11m at speed 15.4 — keep, or drop to ~11 m/s for ~8m. Decide in prototype.
- **Dash→jump momentum — maintain full, steerable.** Jumping during an active dash launches at the
  **full dash speed held for the whole arc (no bleed)**; air control **rotates the velocity vector** so
  the player curves/aims the landing; **normal speed resumes on landing**. Do NOT zero horizontal
  velocity on jump. (Design level gaps around the full dash-jump distance.)
- **WallSlideModule — press-toward, chimney-oriented.**
  - **Slide** engages only while airborne **AND holding into the wall** → clamp downward velocity to
    `wallSlideSpeed`. (No auto-stick on incidental wall contact.)
  - **Wall jump** = up + **STRONG horizontal away impulse** along the wall normal, with a **~0.45s
    air-steer lock** so the player clears the wall before control returns.
  - **Single-wall climbing is intentionally impractical:** the away impulse is large enough that by the
    time steer-lock releases you are out of re-grab range — returning to the *same* wall realistically
    requires spending an air-dash. **Chimney-jumping between two opposing walls is the vertical mechanic.**
    No artificial "must alternate" rule or diminishing-height count — physics (away force) does the gating.
  - **Dash-jump off a wall** applies an even stronger away+up impulse.
- **PlatformRiderModule** — Moving (inherit platform velocity *snapshot* at jump), Rotating (carried
  while grounded; inherit instantaneous linear tangential velocity on jump), Treadmill (per-tick push),
  SpringBoard (launch along configured vector). See CLAUDE.md Platform Architecture for exact rules.

---

## 5. Combat

- **Base attack — charge-blaster (always available, unlimited):** `PlayerShooter` + `ChargeShot`.
  Tap = small fast shot, no AoE. Hold = charge scales damage + projectile size; charged shot detonates
  with `AoeExplosion` on impact. Charge level drives `ChargeBarUI`. Pooled `PlayerProjectile`.
- **Boss weapons — Mega Man cycle-all + per-weapon energy:** every unlocked weapon is available every
  run; cycle through all (bumper/keys); each has its own **energy meter** (refilled by pickups / slow
  regen). Weapon-vs-boss weakness bonuses come from the cross-area effect graph (§8).
- **Abilities — separate boss equips** (active or passive; e.g. shield burst, double-jump, dash
  upgrade), owned forever, bound separately from the weapon cycle. Distinct from weapons.
- **Damage/status:** via `IDamageable` interface — never type-check the hit object.

---

## 6. Modular Variant System (the stressed centerpiece)

One reusable **seeded weighted-roll** drives every layer of variability.

- **`VariantSetSO`** — a weighted list of entries + a roll API. Given a seed it picks one entry.
  Reused for: **room layout** (which `RoomVariantSO` prefab), **spawn choice** (which enemy spawns),
  and **enemy variant**.
- **Enemy variant = `VariantProfileSO` applied to ONE base prefab in `OnSpawn`:**
  stat overrides (via EnemyStats), behavior-module enable/disable, scale, material/color, optional
  child toggle (e.g. shield). Pooling stays one pool per base enemy type — the profile re-skins on spawn.
  - Example (ant): `red{+hp,+size}`, `blue{+shield child, slower}`, `green{+speed,-size}`.
  - **Constraint:** profiles do *re-skin*, not *restructure*. Radically structural variants = a
    different enemy type/prefab, not a profile.
- **Room variants:** unchanged framework — a `RoomController` has ordered room slots; each slot has a
  `VariantSetSO` of `RoomVariantSO` layout prefabs (platforming / enemy-rush / hazard setups), each a
  self-contained prefab (geometry, hazards, spawn points, hidden secrets, powerup placements). No per-room code.

---

## 7. Run Seeding / Determinism

- **Single run seed** stored in `runState.runSeed`. A `SeedService` derives every roll:
  `roll(areaId, roomIndex, slotId) = Hash(runSeed, areaId, roomIndex, slotId)`.
- **No global `UnityEngine.Random`** for content rolls.
- A resumed run re-derives **identical** content. Enables seed-sharing / daily-runs later.

---

## 8. Area Structure & Cross-Area Effects (#7)

### 8.1 Mega Man weakness-web (not linear)
- Some areas open at start; **beating an area unlocks AND alters others.** A graph, not a line.
- 9 areas, tiered intent: first 2 easy, next 3 medium, then hard, then extreme — tiering guides which
  edges open when, it is not a hard linear gate.

### 8.2 Declarative effect graph (data-driven, derived from completion)
- **`AreaDefinitionSO.onComplete → effects[]`**, each effect **typed** and targeting another area:
  `UnlockArea`, `AlterSpawnTable`, `OpenShortcut`, `ApplyBossWeakness(targetBoss, weaponId)`,
  `PriceCut`, … (a finite, extensible effect vocabulary).
- **Applied on area ENTER**, recomputed fresh: read the saved **completed-area set**, apply every
  effect whose source area is completed. **Area state is derived, never stored stale.**
- Classic Mega Man weapon-weakness is just the `ApplyBossWeakness` effect type.

---

## 9. Roguelike Loop, Powerups, Economy

### 9.1 Loop
HQ → equip/buy → pick an open area + difficulty → play rooms (rolled layouts/enemies/variants) →
mini-boss → rooms → boss → boss drops weapon/ability + applies onComplete effects + banks payout →
death OR clear → back to HQ (run powerups lost, metaCurrency + unlocks kept).

### 9.2 Powerup tiers
- **Run powerups (temporary, lost on death):** **hybrid acquisition** —
  - *Minor* (`PowerupSO.tier = Minor`): health/energy/currency auto-apply instantly on touch.
  - *Major* (`tier = Major`): **choose 1 of 3** draft at room-clear / chest (build-defining).
- **Boss powerups:** unique, dropped by bosses (weapon/ability — see §5).
- **Permanent powerups (HQ shop):** bought with metaCurrency, persist across all runs.
- **Single meta currency:** `metaCurrency`, earned per run, spent only at HQ. Difficulty scales payout.

### 9.3 Difficulty
Each area has Easy/Medium/Hard variants (scales enemy count/stats, hazard intensity, reward). Chosen
at HQ before the run; higher = more metaCurrency.

---

## 10. Enemies, Bosses, Platforms

- **EnemyCore** (health/damage/death/reward/pool-return) + **EnemyStats** (FloatReference fields) +
  stacked **behavior modules** (MissileTurret, ArcPredict, Patrol, Chase, Melee, Shoot…). Behaviors
  read EnemyStats, react to EnemyCore events, never reference each other; target via EntityRegistrySO.
- **Turrets:** MissileTurret (slow→accelerating missiles), ArcPredictTurret (ballistic, leads predicted
  straight-line position).
- **Bosses:** MiniBossCore / BossCore extend EnemyCore with health-threshold phase swaps. On death:
  spawn unique powerup + grant weapon/ability id + raise OnBossDefeated → run the area's onComplete effects.
- **Platforms:** Moving (Cycle/PingPong waypoints, exposes CurrentVelocity), Rotating, Treadmill,
  SpringBoard — own child waypoints only, no cross-object wiring.

---

## 11. Save Data (amended from CLAUDE.md)

AES-encrypted JSON, IDs only, plain C# `SaveData` (resolve to SOs via registries).
- `metaCurrency: int`
- `purchasedPermanentIds: List<string>`
- `completedAreaIds: HashSet<string>`  ← **replaces** linear `unlockedLevelIndex` (drives the effect graph §8)
- `unlockedWeaponIds: List<string>`, `unlockedAbilityIds: List<string>`
- `runState: RunSaveData?` (only if a run is mid-progress & resumable)
  - `runSeed`  ← **added** (deterministic content, §7)
  - `currentAreaId, currentRoomIndex, difficulty (Easy|Medium|Hard), currentHealth`
  - `activeRunPowerupIds: List<string>` (temporary — cleared on death)
  - `weaponEnergy: Dictionary<string,float>` (per-weapon energy state)
- `statsTotals`, audio volumes, mouseSensitivity/invertY.
- **NOT persisted past death:** run powerups, heals, run currency-equivalents, weapon energy.

---

## 12. Game State, Scenes, Input, Audio, UI

- **States:** Boot, MainMenu, HQ, Loading, Run, Paused, Death, RunComplete, GameOver. `GameStateMachine`
  service; transitions raise events.
- **Scenes:** Core (persistent systems + Player) always loaded; HQ hub; 9 area scenes loaded additively
  per run, rooms are in-scene slots with per-run variant prefab swap.
- **Input:** `PlayerInputHandler` wraps generated class — Move, Look, OnJumpPressed/Released (variable
  jump needs release), OnDashPressed, OnFirePressed/Released (charge), OnCycleWeapon, OnInteract.
- **Audio:** AudioCueSO-driven, AudioManager listens to events, pooled AudioSources, mixer groups (Master/Music/SFX).
- **UI:** Canvas + TMP. HUD: HealthDisplay (int, start 5), DashCooldownUI (ring), ChargeBarUI, weapon +
  energy indicator. HQ: ShopUI (permanents), run-start panel (area + difficulty). Major-powerup draft
  card UI. UI reacts to events/SO data only.

---

## 13. Recommended Additions (was your "other suggestions?")

- **Hit feedback / i-frames:** brief player invuln + hitstop/flash on damage (5 HP is unforgiving).
- **Telegraphed boss attacks:** wind-up tells; phases should read clearly in first-person.
- **Run-summary screen:** on death/clear show kills, rooms, currency earned, seed (shareable).
- **Energy economy tie-in:** make weapon-energy pickups a meaningful drop so cycle-all weapons have cost.
- **Secret-route payoff loop:** hidden secrets should grant Major-tier draft picks or weapon energy, not just currency.
- **Accessibility:** the existing Difficulty Director idea (auto-scale fire-rate/count) as an opt-in assist.
- **Deferred:** seed-sharing/daily-run UI, structural enemy variants beyond profiles, meta beyond permanents.

---

## 14. BUILD CHECKLIST (top-down, one at a time, each in a test scene)

### Milestone A — Core Framework
- [ ] ServiceLocator (interfaces, Start-time resolve, fail-loud).
- [ ] GameEventSO + GameEventListener (+ typed `GameEventSO<T>`).
- [ ] Variable SOs (Float/Int/Bool) + Reference structs + PropertyDrawers.
- [ ] Generic `Pool<T>` + `PoolManagerSO` + `PoolConfigSO` + `IPoolable`.
- [ ] EntityDefinitionSO + EntityRegistrar/EntityRegistrySO.
- [ ] **SeedService** (deterministic Hash-based rolls) + `VariantSetSO`.
- [ ] GameStateMachine + state events; SceneLoader + BootLoader + SceneNames (Core additive boot).

### Milestone B — Player (movement first — the thing you care about)
- [ ] PlayerInputHandler (Move/Look/Jump press+release/Dash/Fire press+release/CycleWeapon/Interact).
- [ ] Kinematic Rigidbody PlayerMotor (sweep/collide, grounded/wall state) + PlayerStats + PlayerHealth(5).
- [ ] FirstPersonCamera (look, pitch clamp, cached ref, shake).
- [ ] JumpModule (variable height, coyote, buffer, air control).
- [ ] DashModule (ground-only, momentum carry, cooldown ring events).
- [ ] WallSlideModule (slide + wall jump + dash-jump-off).
- [ ] PlatformRiderModule + the 4 platform types.
- [ ] **Prototype test scene to validate movement feel** (resolve the dash-distance open Q).

### Milestone C — Combat
- [ ] IDamageable; pooled PlayerProjectile; PlayerShooter + ChargeShot + AoeExplosion; ChargeBarUI.
- [ ] Weapon system: cycle-all owned weapons + per-weapon energy meter + HUD; AbilitySO equips.
- [ ] EnemyCore + EnemyStats + behavior modules; MissileTurret + ArcPredictTurret; pooled enemy projectiles.

### Milestone D — Variability & Content
- [ ] VariantProfileSO + apply-on-spawn (stat/behavior/scale/color/shield); enemy variant rolls.
- [ ] RoomController + room slots + RoomVariantSO (variant prefabs: platforming / enemy-rush / hazard).
- [ ] Encounter/spawn rolls wired to SeedService.
- [ ] Run powerups: PowerupSO (Minor/Major tiers), instant pickups + 1-of-3 Major draft UI.

### Milestone E — Areas, Meta, Bosses, Save
- [ ] AreaDefinitionSO + onComplete typed effect vocabulary + effect-graph applier (on area enter).
- [ ] RunManager (area select, difficulty, room progression, death→HQ); completion set tracking.
- [ ] HQ: HQController, ShopManager, PermanentUpgradeSO/Registry, CurrencyWallet, run-start panel.
- [ ] MiniBossCore + BossCore (phases) + boss drop (weapon/ability) + onComplete trigger.
- [ ] SaveManager service (AES JSON, completedAreaIds, runSeed, weapon energy, unlocks) + save triggers.

### Milestone F — Polish
- [ ] AudioManager + AudioCueSO + mixer groups.
- [ ] Full HUD, Pause, MainMenu, run-summary screen, hit feedback/i-frames, boss telegraphs.

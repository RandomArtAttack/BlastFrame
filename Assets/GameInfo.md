# Blast Frame — Game Info (living setup reference)

Entries are cumulative. Never remove. Amend if a pattern changes.

---

## Level & Run Management

### LevelDefinitionSO
**Namespace:** `BlastFrame.Gameplay.Levels`
**File:** `Assets/Scripts/Gameplay/Levels/LevelDefinitionSO.cs`
**Type:** ScriptableObject — `[CreateAssetMenu]` path: `Blast Frame/Levels/Level Definition`
**Purpose:** Designer-authored recipe for one of the 9 levels. Stores `levelIndex` (0–8), `displayName`, `roomCount`, and three difficulty-scaling `FloatReference` fields (`enemyCountScale`, `enemyStatScale`, `rewardScale`). Does NOT store room-variant content — that lives on `RoomController` GameObjects in the level scene.
**Required SOs:** none (self-contained)
**Key fields:**
- `_levelIndex` — zero-based level index; must match RunManager expectations
- `_displayName` — shown in HQ run-start UI
- `_roomCount` — how many room slots LevelController steps through before raising level-cleared
- `_enemyCountScale` / `_enemyStatScale` / `_rewardScale` — FloatReference, designer-tunable per-level multipliers
**Setup steps:**
1. Run `Tools > Blast Frame > Implement Fix > 017` to create `Level01.asset` with levelIndex=0 (only creates if missing — safe to re-run)
2. Duplicate for Levels 02–09; set `_levelIndex` and `_displayName` on each
**Gotchas:** Never auto-overwrite an existing asset — Fix017 guards this. Stats on enemy prefabs are scaled by these multipliers at runtime; the SO itself only stores the multiplier, not the base stat.

---

### RunManager
**Namespace:** `BlastFrame.Gameplay.Levels`
**File:** `Assets/Scripts/Gameplay/Levels/RunManager.cs`
**Type:** MonoBehaviour on a `RunManager` GameObject in the **Core** scene
**Implements:** `IRunManager` (registered as service in `Awake`)
**Services registered:** `IRunManager`
**Services used (TryGet — all optional):** `IGameStateMachine`, `ISceneLoader`, `ICurrencyManager`
**Key state:**
- `RunActive` — true while a run is in progress
- `Difficulty` — chosen at `StartRun()`
- `CurrentLevelIndex` / `CurrentRoomIndex` — updated by `AdvanceRoom()` and `CompleteLevel()`
**Serialized GameEventSO fields (all null-safe):**
- `_onRunStarted` — raised at `StartRun()`; wire `OnRunStarted.asset`
- `_onReturnedToHQ` — raised at `EndRun(true)`; wire `OnReturnedToHQ.asset`
- `_onLevelCleared` — raised at `CompleteLevel()`; wire `OnLevelCleared.asset`
**Public helpers (beyond IRunManager):** `AdvanceRoom()`, `CompleteLevel()`
**Setup steps:**
1. Run `Tools > Blast Frame > Implement Fix > 017` — adds the GameObject to Core.unity automatically
2. In the Inspector wire the three GameEventSO fields (create assets under `Assets/Events/` if not yet present)
**Gotchas:**
- Register Awake / Get Start ordering strictly observed — services resolved in `Start` via `TryGet`
- HQ scene load after death is fully guarded: logs a warning and returns if `ISceneLoader` is absent or HQ scene does not exist
- Difficulty is chosen at `StartRun()` call time — not persisted on the SO

---

### LevelController
**Namespace:** `BlastFrame.Gameplay.Levels`
**File:** `Assets/Scripts/Gameplay/Levels/LevelController.cs`
**Type:** MonoBehaviour — lives in a **level scene** (not Core), on a `LevelController` GameObject
**Services used:** `IRunManager` via `ServiceLocator.TryGet` (called at level-cleared time only)
**Serialized fields:**
- `_levelDefinition` — `LevelDefinitionSO` asset for this scene (required; logs error if null)
- `_onEnemiesCleared` — `GameEventSO` raised by an enemy counter / trigger volume; LevelController listens to advance the room. Leave null to call `AdvanceRoom()` manually.
- `_onRoomCleared` — `GameEventSO` raised when a room is done and the next begins
- `_onLevelCleared` — `GameEventSO` raised when all rooms are cleared; also calls `RunManager.CompleteLevel()`
**Setup steps:**
1. Place a `LevelController` GameObject in each level scene
2. Assign the matching `LevelDefinitionSO` to `_levelDefinition`
3. Wire `_onEnemiesCleared` to whatever signal your room uses (enemy counter, trigger, etc.)
4. Create and wire `_onRoomCleared` / `_onLevelCleared` event assets if you need listeners
**Gotchas:**
- Does not reference `RoomController` or any Room feature types directly — avoids cross-feature compile deps
- `AdvanceRoom()` is a public method for manual / editor-test advancement when no event is wired
- `CompleteLevel()` on RunManager is called via TryGet (not cached in Start) to avoid ordering issues with a service that may not be present in early prototyping

---

## Player Shooting

### ChargeShot
**Namespace:** `BlastFrame.Gameplay.Weapons`
**File:** `Assets/Scripts/Gameplay/Weapons/ChargeShot.cs`
**Required components (same GameObject):** PlayerShooter
**Implements:** `IChargeReadout` (HUD ChargeBarUI reads `Charge01` + `OnChargeChanged`)
**Required SOs:** none — reads `IPlayerInput` from ServiceLocator in Start
**Key fields:**
- `chargeTime` (FloatReference, default 1s) — seconds to reach full charge
**Events:**
- `OnReleased(float charge)` — raised on fire-button release; PlayerShooter subscribes
- `OnChargeChanged(float charge)` — raised every frame charge changes; HUD subscribes
**Setup steps:** Add to Player/Camera child (Fix 012 does this automatically)
**Gotchas:**
- Must be on the same GameObject as PlayerShooter (RequireComponent enforces this)
- IPlayerInput must be registered before Start fires (PlayerInputHandler registers in Awake — safe)

---

### PlayerShooter
**Namespace:** `BlastFrame.Gameplay.Player`
**File:** `Assets/Scripts/Gameplay/Player/PlayerShooter.cs`
**Required components (same GameObject):** ChargeShot (RequireComponent)
**Required SOs:** none — gets IPoolManager from ServiceLocator in Start
**Key fields (FloatReference / IntReference):**
- `baseDamage` — damage of a tap (no-charge) shot (default 1)
- `chargedDamageBonus` — extra damage added at full charge (default 4)
- `aoeChargeThreshold` — charge ≥ this triggers AoE explosion (default 0.5)
- `minProjectileSize` — localScale multiplier at charge 0 (default 0.25)
- `maxProjectileSize` — localScale multiplier at charge 1 (default 1.0)
- `tapProjectileSpeed` — m/s for tap shot (default 30)
- `chargedProjectileSpeed` — m/s for full charge (default 22, slower = heavier feel)
**Setup steps:**
1. Run `Tools > Blast Frame > Implement Fix > 011` (creates prefabs, EntityDefs, PoolConfig entries, wires PoolManager)
2. Run `Tools > Blast Frame > Implement Fix > 012` (adds ChargeShot + PlayerShooter to Player/Camera)
**Gotchas:**
- The camera IS the muzzle — projectiles spawn at the camera's transform position/rotation
- PoolManager must have its `config` field assigned (Fix 011 does this)
- Projectiles must be on a layer that can trigger against enemies and geometry — set Layer on the PlayerProjectile prefab in the Inspector

---

### ProjectileBase
**Namespace:** `BlastFrame.Gameplay.Weapons`
**File:** `Assets/Scripts/Gameplay/Weapons/ProjectileBase.cs`
**Purpose:** Abstract base for all pooled projectiles. Provides forward movement (FixedUpdate, kinematic), lifetime countdown, and auto-despawn. Subclasses call `SetSpeed()` from `Initialize()` and override `HandleHit()` or use `OnTriggerEnter` to react to hits.
**Requires:** Rigidbody (RequireComponent)
**Key fields:** `lifetime` (FloatReference, default 4s)
**Gotchas:**
- `Pool` property is populated in `Start` via ServiceLocator — not available in Awake
- `_despawned` guard prevents double-despawn on rapid hits

---

### PlayerProjectile
**Namespace:** `BlastFrame.Gameplay.Projectiles`
**File:** `Assets/Scripts/Gameplay/Projectiles/PlayerProjectile.cs`
**Prefab path:** `Assets/Prefabs/Projectiles/PlayerProjectile.prefab`
**Pool id:** `"PlayerProjectile"` (see `PoolIds.PlayerProjectile`)
**EntityDefinitionSO:** `Assets/ScriptableObjects/Entities/PlayerProjectile.asset`
**Required components on prefab:** Rigidbody (kinematic, no gravity, interpolated), SphereCollider (isTrigger = true), PlayerProjectile script
**Initialize(int damage, float size, bool aoe, float speed)** — called by PlayerShooter after Spawn
**Behaviour:** Moves forward kinematically; on trigger enter damages first IDamageable in parent chain; if aoe=true spawns Explosion from pool; then despawns.
**Gotchas:**
- Collider must be a trigger — it is kinematic, so physics engine won't call OnCollisionEnter
- Skips other PlayerProjectile colliders to prevent self-hitting from nearby shots
- Layer setup is manual: set the prefab's layer so it collides with enemies and world geometry but not with the player capsule

---

### AoeExplosion
**Namespace:** `BlastFrame.Gameplay.Weapons`
**File:** `Assets/Scripts/Gameplay/Weapons/AoeExplosion.cs`
**Prefab path:** `Assets/Prefabs/Projectiles/Explosion.prefab`
**Pool id:** `"Explosion"` (see `PoolIds.Explosion`)
**EntityDefinitionSO:** `Assets/ScriptableObjects/Entities/Explosion.asset`
**Required components on prefab:** AoeExplosion script (no collider — uses OverlapSphereNonAlloc)
**Key fields:**
- `radius` (FloatReference, default 3m)
- `damage` (IntReference, default 3)
- `visualLifetime` (FloatReference, default 0.35s)
- `damageLayers` (LayerMask, default All) — restrict to enemies + world if desired
**Behaviour:** On OnSpawn, immediately overlaps a sphere and applies damage to all IDamageable within radius, then shrinks the visual sphere and despawns.
**Gotchas:**
- `_overlapBuffer` is a static array (32 slots) shared across all AoeExplosion instances — safe as long as two explosions don't spawn in the same frame on the same physics thread (Unity's FixedUpdate is single-threaded)
- The explosion prefab has no collider — damage is applied via OverlapSphereNonAlloc in OnSpawn, not by physics contact
- `_baseScale` is set in Start; if the prefab is in the pool before Start fires, the first spawn may get Vector3.zero scale — the OnDespawn guard `_baseScale != Vector3.zero ? _baseScale : Vector3.one` catches this

---

## HUD Widgets

### Overview
The HUD lives on a "HUD" Canvas (Screen Space Overlay) in the Core scene. `HUDController` is the orchestrator; the three widget scripts bind defensively to player components via `EntityRegistrySO` — they retry each frame in `Update` until the player is registered, so no spawn-order contract is needed.

**Setup:** Run `Tools > Blast Frame > Implement Fix > 013 - Build HUD In Core` (idempotent).

---

### UIManager
**Namespace:** `BlastFrame.UI`
**File:** `Assets/Scripts/UI/UIManager.cs`
**Required components:** On the HUD Canvas root
**Purpose:** Minimal service holder; registers itself with ServiceLocator as `UIManager` so other systems can reach the UI root if needed.

---

### HUDController
**Namespace:** `BlastFrame.UI`
**File:** `Assets/Scripts/UI/HUDController.cs`
**Required components:** On the HUD Canvas root (same GO as UIManager)
**Required SOs:** `EntityRegistrySO` — assign `EntityRegistry.asset`
**SerializedObject fields:**
- `entityRegistry` — the shared EntityRegistry asset
- `healthDisplay` — child HealthDisplay component
- `dashCooldownUI` — child DashCooldownUI component
- `chargeBarUI` — child ChargeBarUI component
**Purpose:** Awake propagates the registry to each widget via `SetRegistry()`. Widgets also hold their own registry ref as a fallback.

---

### HealthDisplay
**Namespace:** `BlastFrame.UI`
**File:** `Assets/Scripts/UI/HealthDisplay.cs`
**Required components (own GO):** `TextMeshProUGUI`
**Required SOs:** `EntityRegistrySO`
**SerializedObject fields:**
- `entityRegistry` — the shared EntityRegistry asset
- `healthText` — own-GameObject TextMeshProUGUI
**Behaviour:** Subscribes to `PlayerHealth.OnHealthChanged(int current, int max)` and sets `healthText.text = current.ToString()`. Positioned top-left (anchored 0,1).
**Gotchas:**
- TMP Essentials must be imported first: `Window > TextMeshPro > Import TMP Essential Resources`
- `_bound` flag stops the Update retry loop once binding succeeds

---

### DashCooldownUI
**Namespace:** `BlastFrame.UI`
**File:** `Assets/Scripts/UI/DashCooldownUI.cs`
**Required components (own GO):** `UnityEngine.UI.Image` (Type = Filled, Method = Radial360)
**Required SOs:** `EntityRegistrySO`
**SerializedObject fields:**
- `entityRegistry` — the shared EntityRegistry asset
- `dashRingImage` — own-GameObject Image
**Behaviour:** Subscribes to `DashModule.OnCooldownChanged(float readiness)` and sets `fillAmount = readiness`. If no DashModule on the player, stays at fillAmount 1 (graceful degradation).
**Gotchas:**
- The Image must be set to Type Filled / Radial360 — Fix 013 does this automatically
- Starts at `fillAmount = 1` in Start so it does not flash empty at boot

---

### ChargeBarUI
**Namespace:** `BlastFrame.UI`
**File:** `Assets/Scripts/UI/ChargeBarUI.cs`
**Required components (own GO):** `UnityEngine.UI.Image` (Type = Filled, Method = Horizontal)
**Required SOs:** `EntityRegistrySO`
**SerializedObject fields:**
- `entityRegistry` — the shared EntityRegistry asset
- `chargeBarImage` — own-GameObject Image
**Behaviour:** Subscribes to `IChargeReadout.OnChargeChanged(float charge)` (found via `GetComponentInChildren<IChargeReadout>()` on the player). Sets `fillAmount = charge`. Hides the bar's GameObject when charge == 0 via `SetActive(false)`.
**Gotchas:**
- The charge source is `IChargeReadout`, not `ChargeShot` directly — any weapon that implements the interface works
- If no IChargeReadout is found (no charge weapon equipped yet), bar stays hidden and `_bound = true` — no update loop cost
- The ChargeBar child GameObject starts inactive in Edit mode (Fix 013 sets it); ChargeBarUI re-activates it when charge > 0

---

## Pool Setup (Combat)

**PoolConfigSO:** `Assets/ScriptableObjects/Pooling/PoolConfig.asset`
**PoolManager GameObject:** lives in Core scene, `BlastFrame.Core.Pooling.PoolManager` component
**Entries added by Fix 011:**
| Pool Id | Prefab | Prewarm | Expand |
|---|---|---|---|
| PlayerProjectile | Assets/Prefabs/Projectiles/PlayerProjectile.prefab | 20 | 10 |
| Explosion | Assets/Prefabs/Projectiles/Explosion.prefab | 8 | 4 |

**Run Fix 011** to build all of the above in one click. Idempotent — safe to re-run.
**Run Fix 012** to add ChargeShot + PlayerShooter to Player/Camera. Idempotent.

---

## Powerup System

### PowerupSO
**Namespace:** `BlastFrame.Gameplay.Powerups`
**File:** `Assets/Scripts/Gameplay/Powerups/PowerupSO.cs`
**Asset path:** `Assets/ScriptableObjects/Powerups/`
**Create via:** `Tools > Blast Frame > Powerups > Create Powerup (SO)` or right-click > `Blast Frame/Powerups/Powerup`
**Key fields (all [SerializeField] private with public getters):**
- `_id` (string) — unique id; must not change after authoring; used by registries and save data
- `_displayName` (string) — shown in UI
- `_effect` (enum PowerupEffect) — `Heal`, `MoveSpeedBuff`, `MaxHealthUp` (add more values as the roster grows)
- `_magnitude` (FloatReference) — numeric strength (HP restored, speed added, etc.)
- `_duration` (FloatReference) — seconds the effect lasts; 0 = instant or full-run
**Run-scope:** ALL powerups are run-scoped. They are NOT persisted to save data. They are cleared on death when the player returns to HQ.
**Sample asset:** `Assets/ScriptableObjects/Powerups/Heal.asset` (id=heal_basic, magnitude=2, instant) — created by Fix 015 if missing.

---

### PowerupRegistrySO
**Namespace:** `BlastFrame.Gameplay.Powerups`
**File:** `Assets/Scripts/Gameplay/Powerups/PowerupRegistrySO.cs`
**Purpose:** Master list of all PowerupSOs. Systems look up powerups by string id via `GetById(string id)`.
**Setup:** Create one registry asset (`Assets/ScriptableObjects/Powerups/`), then drag every authored PowerupSO into its `_powerups` list in the Inspector.
**Gotchas:**
- This is not auto-populated; add each new PowerupSO manually after authoring it.
- `GetById` iterates linearly (fine for the small roster expected).

---

### PowerupPickup
**Namespace:** `BlastFrame.Gameplay.Powerups`
**File:** `Assets/Scripts/Gameplay/Powerups/PowerupPickup.cs`
**Required components (own GO):** Collider with `isTrigger = true` (SphereCollider on root recommended)
**Required SOs:**
- `_powerup` — a PowerupSO asset
- `_entityRegistry` — the shared EntityRegistry asset (`Assets/ScriptableObjects/Entities/EntityRegistry.asset`)
**Optional SOs:**
- `_onPickedEvent` — GameEventSO raised after effect is applied (for audio/VFX/HUD flash); leave null if not needed
**Create via:** `Tools > Blast Frame > Powerups > Create Powerup Pickup` — builds a prototype capsule pickup with trigger + PowerupPickup wired to EntityRegistry. Assign `_powerup` in the Inspector, then save as a prefab.
**Behaviour:**
- On `OnTriggerEnter`: compares `other.transform` against `EntityRegistrySO.PlayerTransform` (and its root). Only reacts to the player.
- Applies effect: `Heal` calls `PlayerHealth.Heal(int)` — fully implemented. `MoveSpeedBuff` / `MaxHealthUp` are stubbed with TODO comments (require a stat-modifier layer).
- Raises `_onPickedEvent` if assigned, then `Destroy(gameObject)`.
**Fix 015:** `Tools > Blast Frame > Implement Fix > 015 - Place Test Powerup In TestLevel`
- Creates `Heal.asset` if missing (create-only, never overwrites tuned values).
- Opens TestLevel.unity, drops a `TestHealPickup` GameObject at (0, 0.5, 4) under "Content" (if present) with Heal.asset + EntityRegistry wired.
- Idempotent: no-ops if pickup already exists or scene is missing.
**Gotchas:**
- Run powerups are NOT persisted past death — do not write them to SaveData.
- `MoveSpeedBuff` and `MaxHealthUp` are intentionally stubbed; mutating the shared FloatVariable without a modifier layer would permanently corrupt the stat.
- The trigger lives on the root, not on the visual child — the visual capsule's collider is stripped in the wizard.
- Fix 015 never overwrites an existing Heal.asset — it bails with a log if the asset is already present.

---

## Bosses

### BossPhase
**Namespace:** `BlastFrame.Gameplay.Enemies.Bosses`
**File:** `Assets/Scripts/Gameplay/Enemies/Bosses/BossPhase.cs`
**Type:** `[System.Serializable]` plain class — used as a list element on MiniBossCore / BossCore
**Fields:**
- `healthFraction` (float, 0–1) — threshold at or below which this phase activates (e.g. 0.5 = 50% health)
- `behaviorsToEnable` (`List<EnemyBehaviorBase>`) — sibling behavior components to ENABLE; all other EnemyBehaviorBase siblings are disabled
**Ordering rule:** List phases from highest `healthFraction` to lowest (phase 0 = full health / spawn-in, last = near-death). The boss evaluates from last to first to find the deepest threshold that has been crossed.
**Gotchas:**
- `behaviorsToEnable` references are sibling components on the **same GameObject** — self-contained prefab wiring, not cross-object references.
- Phases are evaluated on every `OnDamaged` event and once on `OnEnable` (spawn); no polling.
- If the list is empty the boss spawns with all behaviors enabled (no phase management).

---

### MiniBossCore
**Namespace:** `BlastFrame.Gameplay.Enemies.Bosses`
**File:** `Assets/Scripts/Gameplay/Enemies/Bosses/MiniBossCore.cs`
**Extends:** `EnemyCore` (which requires `EnemyStats` via `[RequireComponent]`)
**Required components (same GO):** `EnemyStats`, one or more `EnemyBehaviorBase` subclasses
**Required SO / prefab refs (serialized):**
- `entityRegistry` — `EntityRegistrySO` asset (inherited from EnemyCore; wire `EntityRegistry.asset`)
- `dropPrefab` — `GameObject` prefab instantiated at boss position on death (e.g. a PowerupPickup prefab); leave null for no drop
- `onBossDefeated` — `GameEventSO` raised on death (room-clear, level-unlock hook, etc.)
- `onWeaponUnlocked` — `StringGameEventSO` raised with the weapon/ability ID string on death
- `weaponUnlockId` — string ID matched by the weapon-unlock registry (e.g. `"weapon_rocket"`)
**Phase contract:**
- `phases` — `List<BossPhase>`, ordered highest-to-lowest `healthFraction`
- Subscribes to `OnDamaged` in `OnEnable`, unsubscribes in `OnDisable`
- `EvaluatePhases` fires on every damage event and once at `OnEnable` (spawn)
- `ApplyPhase` enables listed behaviors and disables all others — no Animator, no nested switch
**Death sequence:** instantiate `dropPrefab` → raise `onWeaponUnlocked` (if `weaponUnlockId` non-empty) → raise `onBossDefeated` → call `base.Die()` (grants currency, unregisters, destroys/despawns)
**Setup:** `Tools > Blast Frame > Enemies > Create Mini Boss` — builds a 2×2×2 cube with EnemyStats + MiniBossCore + placeholder behaviors (MissileTurret enabled, ArcPredict disabled), wires EntityRegistry. Configure phases and assign `dropPrefab` / event SOs in the Inspector, then save as a prefab.
**Test fix:** `Tools > Blast Frame > Implement Fix > 020 - Place Test Boss In TestLevel`
**Gotchas:**
- `dropPrefab` is a `GameObject` asset reference (not a MonoBehaviour reference) — avoids compile dependency on the Powerups feature.
- `weaponUnlockId` is a plain string raised via `StringGameEventSO` — the unlock registry listens; no direct coupling.
- `base.Die()` destroys or despawns the GO; spawn the drop BEFORE calling it.
- Phase `healthFraction` of 1.0 activates immediately on spawn (fraction == 1.0 satisfies `fraction <= 1.0`).

---

### BossCore
**Namespace:** `BlastFrame.Gameplay.Enemies.Bosses`
**File:** `Assets/Scripts/Gameplay/Enemies/Bosses/BossCore.cs`
**Extends:** `EnemyCore` — same contract as MiniBossCore plus one extra event
**Required components (same GO):** `EnemyStats`, one or more `EnemyBehaviorBase` subclasses
**Required SO / prefab refs (serialized):**
- `entityRegistry` — `EntityRegistrySO` asset (wire `EntityRegistry.asset`)
- `dropPrefab` — `GameObject` prefab instantiated on death; leave null for no drop
- `onBossDefeated` — `GameEventSO` raised on death (parameterless)
- `onLevelUnlocked` — `GameEventSO` raised on death to unlock the next level (wire `OnLevelUnlocked.asset`)
- `onWeaponUnlocked` — `StringGameEventSO` raised with weapon/ability ID string on death
- `weaponUnlockId` — string ID, e.g. `"weapon_charge_cannon"`
**Death sequence:** instantiate `dropPrefab` → raise `onWeaponUnlocked` → raise `onBossDefeated` → raise `onLevelUnlocked` → call `base.Die()`
**Setup:** `Tools > Blast Frame > Enemies > Create Boss` — builds a 3×3×3 cube with EnemyStats + BossCore + three placeholder behaviors (MissileTurret × 2 + ArcPredict), wires EntityRegistry. Configure three phases, assign drop/events, save as prefab.
**Test fix:** `Tools > Blast Frame > Implement Fix > 020 - Place Test Boss In TestLevel`
**Gotchas:** Same as MiniBossCore plus: `onLevelUnlocked` must be wired to advance the run — without it the next level is never unlocked. Typical full-boss health is higher than mini-boss; set `EnemyStats.health` accordingly.

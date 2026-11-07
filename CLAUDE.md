# =============================================
# PROJECT CONFIG â edit per project
# =============================================

# â­ ARCHITECTURE SOURCE OF TRUTH
# `DESIGN.md` (repo root) is the canonical architecture spec + build checklist.
# ALWAYS consult DESIGN.md for how a system is built, its data shapes, and build order.
# UPDATE DESIGN.md whenever an architectural decision changes (and log the dated rationale in
# Assets/GameDesign.md). Where DESIGN.md and this file disagree, DESIGN.md wins (it is newer).
# NOTE: as of 2026-05-30 the project is greenfield â the movement/combat described below is a
# WRITTEN SPEC, not yet implemented code. HANDOFF.md tracks status; LAST_LEFT_OFF.txt is the needle.

# Project Overview
Unity 6 (6000.3.10f1) â Universal Render Pipeline (URP 17.3.0), 3D
(Viewmodel/gun uses URP camera stacking, not custom passes.)
Genre: 3D first-person shooter / precision platformer, run-based roguelike
Name: Blast Frame

# High Concept
Traverse 9 levels of robot-infested rooms. Run, jump, dash, wall-slide and
charge-shot your way through. Each level ends with a mini-boss then a boss.
Every level has Easy/Medium/Hard variants and every room has 3 variant setups
with hidden secrets and powerups â every run feels fresh. Bosses drop unique
powerups plus a new weapon/ability. Death sends the player back to HQ to
restock and buy permanent powerups, then run again.

# =============================================
# GLOSSARY (authoritative definitions â if a prompt conflicts with these, ask)
# =============================================
# DRAFT 2026-07-02, mined from session history â developer still reviewing.
# Where an entry conflicts with the older spec text below, THE GLOSSARY WINS
# (these are the developer's latest in-game rulings).

## Movement
- **Standing long jump**: clears 7 Unity units horizontal from standstill â the level-layout budget.
- **Dash / dash jump**: clears 10 units. Dash is HELD-button (release ends it immediately), not fixed-duration â overrides the `dashDuration` spec below. Dash momentum can be spent into a wall jump for extra distance.
- **Dash-jump air control**: after a dash-jump, normal air movement applies EXCEPT pressing forward must not decelerate you; left/right steering and braking-by-pressing-back still work.
- **Wall slide**: triggers on being ADJACENT to a wall while airborne â no push-into-wall required (overrides the WallSlideModule spec below). Descent ~half normal fall speed. Wall detection is level geometry only â never the player's own gun.
- **Wall jump**: jump (or dash) while airborne and wall-adjacent launches away from the wall automatically, no toward-input needed. Dash variant launches at dash speed.
- **Coyote time**: ~0.25 s (tunable) after walking off a ledge where jump still fires â leniency is intentional.
- **Step-up**: auto-climb steps below a height threshold instead of stopping dead.
- **Vault**: while wall-sliding, if a raycast shows the gun cleared the ledge but the feet missed, give a small ~0.5-unit assisted hop while forward is held. NOT full ledge-grabbing.
- **Player Test Mode**: togglable player script allowing jumps without ground contact (multi-jump) to reach anywhere for testing.

## Combat
- **Turret Shooter**: rotates to face player, pivots barrel up (always â¥45Â°), lobs a gravity-affected projectile from the barrel tip; fires only with line of sight to any exposed part of the player; bullets pooled.
- **Turret projectile (tuned)**: constant linear velocity along barrel orientation â must NOT inherit turret/player velocity; 3Ã base size, ~half travel speed, gravity at 20% of max, explodes on impact with 1.5-unit radius.
- **Gun barrel**: player bullets spawn from the gun-barrel transform, oriented to it; gun renders in front of everything (never clips walls).
- **Weapon layers**: enemy weapons don't collide with enemies; player weapons don't collide with the player.

## Environment
- **Moving Platform**: path mover; rider inherits velocity. Features accreted: Reuse Loop (teleport snap lastâfirst), Start Index, Enable/Disable component lists per path index, Path Smoothing (decelerate into / accelerate out of points, separate deceleration power).
- **RockBobber**: bobs rocks on lava by sampling the lava mesh's moving VERTICES (not collider) via downward raycast on the Lava layer; lerps ~0.5 s into bobbing (exposed follow speed); height comes from the LAVA RIVER only, never Lava Ripple surfaces.
- **Lava-bobbing platform**: RockBobber + Moving Platform combined; player must reliably ride it (temporary parenting stops slip on the upward stroke).
- **Fog Manager**: default fog config + volume list; entering a volume applies overrides, exiting reverts.
- **Emission Pulse**: oscillates a material's serialized emission value.

## Shaders (RAA)
- **RAA URP Lit Projection**: URP/Lit + triplanar projection replacing UVs; one tile = constant world size (2048Â² per 4Ã4 units; "World Units Scale", default 4Ã4Ã4). Respects rotation, ignores scale.
- **RAA Lava Flow v1âv4**: unlit-emission scrolling lava. v2 +tint/alpha map; v3 = two independent layers; v4 = v3 + noise-driven vertical vertex displacement. Per-object random offset at Start so instances differ.
- **Lava Ripple**: renamed from "Lava River V.2" on 6/18 â continuous center-oscillating ripples, per-object random phase + variable amplitude. Pre-6/18 "Lava River" references are ambiguous â disambiguate by date.

## Workflow
- **unity-jiggler**: PowerShell keep-alive (F15 + 1px nudge, foreground-only) for the editor loop; superseded by bridge-side auto-wake in our unity-mcp.
- **MCP writes**: fully allowed (ruled 2026-07-02) â our own raa-unity bridge handles reads AND writes (see RAA Unity MCP section). The old read-only-then-implement-fix policy applied to the third-party mcp-unity era and is dead.
- **Implement fix**: numbered one-click editor action (see Implement Fix section) â still the right tool for one-time bulk wiring/prefab setup the developer may want to re-run; not a required substitute for MCP writes anymore.

## Unresolved â ask before assuming
- **VSync vs 60 FPS cap**: flip-flopped; shipping target unresolved.
- **Ripple displacement origin**: object center = sine minimum vs midpoint â inconsistent across shaders; ask per shader.


# Project Structure
- `Assets/Scripts/` - all C# scripts
- `Assets/Scripts/Core/` - ServiceLocator, GameManager, BootLoader, SceneLoader, GameState
- `Assets/Scripts/Gameplay/` - gameplay systems
- `Assets/Scripts/Gameplay/Player/` - PlayerController, PlayerMotor, PlayerShooter, PlayerStats, PlayerHealth
- `Assets/Scripts/Gameplay/Player/Movement/` - JumpModule, DashModule, WallSlideModule, PlatformRiderModule
- `Assets/Scripts/Gameplay/Weapons/` - WeaponCore, ChargeShot, ProjectileBase, AoeExplosion
- `Assets/Scripts/Gameplay/Enemies/` - EnemyCore, EnemyStats, behavior components
- `Assets/Scripts/Gameplay/Enemies/Turrets/` - MissileTurret, ArcPredictTurret
- `Assets/Scripts/Gameplay/Enemies/Bosses/` - MiniBossCore, BossCore, boss phase components
- `Assets/Scripts/Gameplay/Projectiles/` - PlayerProjectile, EnemyMissile, ArcProjectile
- `Assets/Scripts/Gameplay/Platforms/` - MovingPlatform, RotatingPlatform, TreadmillPlatform, SpringBoard
- `Assets/Scripts/Gameplay/Rooms/` - RoomController, RoomVariantSelector, RoomVariantSO
- `Assets/Scripts/Gameplay/Levels/` - LevelController, LevelDefinitionSO, RunManager
- `Assets/Scripts/Gameplay/Powerups/` - PowerupPickup, PowerupSO, PowerupRegistrySO
- `Assets/Scripts/Gameplay/HQ/` - HQController, ShopManager, PermanentUpgradeSO, PermanentUpgradeRegistrySO
- `Assets/Scripts/Gameplay/Economy/` - CurrencyWallet, CurrencyManager
- `Assets/Scripts/UI/` - UIManager, HUDController, HealthDisplay, DashCooldownUI, ChargeBarUI, ShopUI, PauseMenuUI
- `Assets/Scripts/Input/` - PlayerInputHandler
- `Assets/Scripts/Audio/` - AudioManager, AudioCueSO
- `Assets/Scripts/Camera/` - FirstPersonCamera, CameraShake
- `Assets/Scripts/Debug/` - TestStat
- `Assets/Scenes/` - Unity scenes
- `Assets/Prefabs/` - prefabs
- `Assets/Prefabs/Enemies/` - enemy + turret + boss prefabs
- `Assets/Prefabs/Projectiles/` - projectile prefabs
- `Assets/Prefabs/Platforms/` - platform prefabs
- `Assets/Prefabs/RoomVariants/` - room variant prefabs (3 per room)
- `Assets/Prefabs/Powerups/` - powerup pickup prefabs
- `Assets/Prefabs/UI/` - UI prefabs
- `Assets/Art/` - models, textures, materials (cubes/cylinders for prototype)
- `Assets/ScriptableObjects/` - SO assets
- `Assets/ScriptableObjects/Variables/` - FloatVariable, IntVariable, BoolVariable assets
- `Assets/ScriptableObjects/Entities/` - EntityDefinitionSO assets
- `Assets/ScriptableObjects/Levels/` - LevelDefinitionSO assets (9 levels)
- `Assets/ScriptableObjects/RoomVariants/` - RoomVariantSO assets
- `Assets/ScriptableObjects/Powerups/` - PowerupSO assets
- `Assets/ScriptableObjects/Permanents/` - PermanentUpgradeSO assets (HQ shop)
- `Assets/Events/` - GameEventSO assets
- `Assets/Editor/` - editor scripts and wizards
- `Assets/Settings/` - URP render pipeline assets + volume profiles (template default)

# Tech Stack
- Unity 6 URP 3D
- C# 9
- Physics: Unity 3D (Rigidbody, Collider) â no third-party physics
- Player controller: custom kinematic Rigidbody (interpolated, manual move/collide) â NOT CharacterController, NOT dynamic-force-driven
- Input: Unity Input System (new) â `Assets/InputSystem_Actions.inputactions` already present â not legacy Input Manager
- JSON: Newtonsoft.Json
- Camera: custom first-person camera rig (no Cinemachine â direct mouse/stick look)
- UI: Canvas + TextMeshPro â not UI Toolkit, not legacy UnityEngine.UI.Text
- Numbers: int/float for health, damage, currency â NOT an idle game, no double needed

# Game States
Boot, MainMenu, HQ, Loading, Run, Paused, Death, RunComplete, GameOver

# Scene List
- Core (always loaded): GameManager, AudioManager, UIManager, PlayerInputHandler, EventSystem
- HQ (hub): restock, permanent powerup shop, run start portal
- Level scenes: 9 level scenes, each loaded additively for a run; rooms are
  in-scene sections, room variant chosen per run via prefab swap
- Player is spawned once and persists across additive level loads within a run

# Save Data Shape
- metaCurrency: int (earned per run, spent at HQ)
- purchasedPermanentIds: List<string> (HQ permanent powerups â persist forever)
- completedAreaIds: HashSet<string> (REPLACES unlockedLevelIndex â drives the Mega Man weakness-web
  + cross-area effect graph; see DESIGN.md Â§8. Area unlock/alter state is DERIVED from this set, not stored)
- unlockedWeaponIds: List<string> (weapons dropped by bosses â cycle-all, unlimited, trade-off-balanced
  FPS weapon types; NO energy/ammo. See DESIGN.md Â§5)
- unlockedAbilityIds: List<string> (abilities dropped by bosses â equipped separately from weapons)
- runState: nullable RunSaveData (only if a run is mid-progress and resumable)
  - runSeed (deterministic content derivation via SeedService â see DESIGN.md Â§7)
  - currentAreaId: string
  - currentRoomIndex: int
  - difficulty: enum Easy|Medium|Hard
  - currentHealth: int
  - activeRunPowerupIds: List<string> (temporary â cleared on death)
- statsTotals: bestLevelReached, totalRuns, totalDeaths, totalKills
- audioVolumeMaster/Music/SFX: float
- mouseSensitivity: float, invertY: bool
- NOTE: in-run powerups, heals, and run currency-equivalents are NOT persisted
  past death â only metaCurrency and permanent purchases survive a death.

# Audio Mixer Groups
Master, Music, SFX (UI routes to SFX)

# Entity Types
- Player: kinematic Rigidbody FPS body, 2m tall, first-person camera
- Enemy robots: composable behavior system (see Enemy Architecture below)
- MissileTurret: rotating turret, fires slow-then-accelerating missiles
- ArcPredictTurret: aims at player or predicted straight-line position, fires arced ball
- MiniBoss / Boss: multi-phase, drops unique powerup + new weapon/ability
- PlayerProjectile: fired by weapon, charge-scaled, pooled, AoE on charged
- EnemyMissile / ArcProjectile: pooled enemy projectiles
- Powerup pickups: temporary run buffs / heals
- Platforms: Moving, Rotating, Treadmill, SpringBoard


# =============================================
# GAME DESIGN â core loop
# =============================================

# Core Loop
1. Start at HQ. Spend metaCurrency on permanent powerups, pick a weapon loadout.
2. Enter a run: play through the current level's rooms (3 variant possibilities each).
3. Fight robots, collect temporary powerups/heals, find hidden secrets.
4. Clear all rooms â mini-boss â more rooms â boss.
5. Boss drops a unique powerup + a new weapon/ability, and runs the area's onComplete effects
   (Mega Man weakness-web: unlock AND alter OTHER areas â not a linear "next level"). See DESIGN.md Â§8.
6. Death at any point â back to HQ, keep metaCurrency + permanents, lose run powerups.
7. Repeat, pushing deeper across the 9 levels.

# Difficulty Variants
- Each of the 9 levels has Easy / Medium / Hard variants
- Difficulty scales enemy count, enemy stats, hazard intensity, reward payout
- Difficulty is chosen at HQ before the run (higher = more metaCurrency)

# Room Variant Framework (CRITICAL â must be flexible)
- A level scene contains an ordered list of room slots (`RoomController`)
- Each room slot has exactly 3 variant prefabs (`RoomVariantSO` references)
- `RoomVariantSelector` picks one variant per room per run (seeded random)
- A variant prefab is a self-contained room layout: geometry, hazards,
  enemy spawn points, platform configs, hidden secret + powerup placements
- Example variants for one room: (A) lava pit spitting lava chunks,
  (B) moving platforms over lava, (C) flying enemies over a gap
- Variants must be authorable purely as prefabs + SO data â no per-room code
- Hidden secrets are designer-placed trigger volumes inside the variant prefab

# Death & Retry
- Player health starts at 5 (integer). Damage reduces it; 0 = death.
- Death â fade â load HQ scene â run powerups cleared, metaCurrency banked
- No mid-run checkpoint by default; resumable run only if explicitly designed
- Permanent powerups bought at HQ make subsequent runs easier (the meta-progression)

# Powerup Tiers
- Run powerups (temporary): HYBRID acquisition â Minor (health/energy/currency) auto-apply on touch;
  Major (build-defining) are a CHOOSE-1-OF-3 draft at room-clear/chest. PowerupSO.tier = Minor|Major.
  See DESIGN.md Â§9.2. (Picked up mid-run, lost on death.)
- Boss powerups (unique): dropped by bosses, powerful, still run-scoped unless noted
- Permanent powerups (HQ shop): bought with metaCurrency, persist across all runs
- Single meta currency: metaCurrency, earned per run, spent only at HQ

# Controls
- Keyboard/Mouse: WASD move, mouse look, Space jump (hold = higher), Shift dash,
  LMB shoot (hold = charge), interact key at HQ/secrets
- Controller: left stick move, right stick look, A/cross jump, B/circle or
  shoulder dash, right trigger shoot (hold = charge)
- Variable jump: releasing jump early cuts upward velocity (timed jump height)
- Air control: full move steering while airborne (it's a platformer)


# =============================================
# PLAYER ARCHITECTURE â kinematic platformer FPS
# =============================================

# Player Setup
- Root "Player" GameObject: kinematic interpolated Rigidbody + CapsuleCollider
  (2m tall â capsule height 2, so default jump clears 2m-tall obstacles when
  jump is held to full height)
- Camera child: first-person camera at eye height (~1.8m), `FirstPersonCamera`
  handles look (mouse/stick), pitch clamp, no Cinemachine
- `PlayerController` on root â orchestrates input â movement modules
- `PlayerMotor` on root â owns velocity, performs the kinematic move/collide
  sweep each FixedUpdate, resolves collisions, exposes grounded/wall state
- `PlayerStats` on root â FloatReference fields: moveSpeed, jumpForce,
  dashSpeed, dashDuration, dashCooldown, gravity, wallSlideSpeed, airControl
- `PlayerHealth` on root â IntReference currentHealth (start 5), raises events
- Movement modules (see below) are sibling components read by PlayerController

# Movement Modules (composable, on Player root)
- `JumpModule` â variable-height jump. Apply jumpForce on press; if jump
  released while still rising, clamp upward velocity (short hop). Air control
  via PlayerStats.airControl. Coyote time + jump buffer for feel.
- `DashModule` â short burst of speed in input/look direction for
  dashDuration, then 5s cooldown (PlayerStats.dashCooldown). Raises
  OnDashStarted / OnDashCooldownChanged events for the UI ring.
- Dash-jump momentum carry: if a jump is initiated while a dash is still
  active, the dash velocity is preserved into the jump arc (do NOT zero
  horizontal velocity on jump). Momentum bleeds via normal air drag.
- `WallSlideModule` â when airborne and pressing into a wall, clamp downward
  velocity to wallSlideSpeed (slow slide). Wall jump: jump while wall-sliding
  launches the player away from the wall (up + away). Dash-jump off the wall
  applies a stronger away+up impulse.
- `PlatformRiderModule` â tracks the platform the player stands on:
  - Moving platform: inherit the platform's *current* linear velocity at the
    moment of jump (jump straight up â travel the platform's path). If the
    platform changes direction mid-air the player does NOT inherit the new
    velocity (snapshot at jump, not continuous).
  - Rotating platform: while grounded, move/rotate WITH the platform (no
    sliding off). On jump, inherit only the *linear* tangential velocity at
    that instant â not continued angular motion.
  - Treadmill: while standing on it, add the treadmill's push velocity to the
    player each tick (direction + force from the platform).
  - SpringBoard: on contact, launch the player in the board's configured
    direction at its configured force (overrides current velocity along that axis).

# Player Shooting
- `PlayerShooter` on camera/weapon â fires `PlayerProjectile` from muzzle
- `ChargeShot` â **discrete charge tiers** (not continuous): Lv0 tap = small fast
  shot (no AoE); holding crosses fixed time thresholds â Lv1/Lv2/Lv3, each a distinct
  projectile, with a tick cue per tier. AoE (`AoeExplosion`) on impact at the upper
  tiers (Lv2/Lv3). See DESIGN.md Â§5.
- Charge level drives `ChargeBarUI`. Projectiles are pooled.
- Weapons are CYCLE-ALL, UNLIMITED, trade-off-balanced FPS types â no energy/ammo.
  The blaster (above) is the starter; boss-drop weapons differ by handling, not resource.


# =============================================
# ENEMY ARCHITECTURE â composable behaviors
# =============================================

# Composable Behavior System
- Each enemy prefab has an `EnemyCore` MonoBehaviour â health, damage, death,
  reward payout, pool return
- Each enemy prefab has an `EnemyStats` MonoBehaviour â FloatReference fields
  for health, speed, fire rate, damage, projectile speed, etc.
- Behavior components are stacked to compose enemy types:
  - `EnemyBehaviorMissileTurret` â rotates to track target; fires missiles
    that start slow and accelerate over their lifetime
  - `EnemyBehaviorArcPredict` â rotates to face the player OR the player's
    predicted straight-line future position; fires an arced (ballistic)
    projectile that lands on the player's current or predicted ground position
  - `EnemyBehaviorPatrol` / `EnemyBehaviorChase` â ground robot movement
  - `EnemyBehaviorMelee` / `EnemyBehaviorShoot` â contact / ranged attack
  - (extend with more behaviors as enemy roster grows)

# Prediction Math (ArcPredict turret)
- Sample player velocity; if player is moving roughly straight, lead the aim
  to where they'll be after projectile travel time (iterative solve for arc)
- If player is not moving predictably, target current position
- Arc projectile uses a ballistic solution (fixed launch speed, solve angle)
  so it lands ON the target ground point

# Behavior Communication
- Behaviors read stats from sibling `EnemyStats` (GetComponent in Awake)
- `EnemyCore` raises C# events (OnDamaged, OnDeath) behaviors subscribe to
- Behaviors never reference each other directly â read EnemyStats, react to
  EnemyCore events
- Target acquisition: behaviors get the player via EntityRegistrySO, never Find

# Bosses
- `MiniBossCore` / `BossCore` extend the EnemyCore contract with phase logic
- Phases are composable behavior swaps driven by health thresholds
- On death: spawn unique powerup pickup + grant a new weapon/ability id +
  raise OnBossDefeated â add area to completedAreaIds â apply the area's onComplete
  effect graph (unlock/alter other areas â DESIGN.md Â§8). NOT a linear next-level unlock.


# =============================================
# PLATFORM ARCHITECTURE
# =============================================

# Path Platforms
- `MovingPlatform` â moves between an ordered list of waypoint Transforms
  (empties). `enum PathMode { Cycle, PingPong }` selects traversal.
  Exposes CurrentVelocity for PlatformRiderModule to snapshot on jump.
- `RotatingPlatform` â rotates about an axis. Player riding it is carried
  (position + yaw) while grounded; exposes instantaneous linear velocity at
  the player's contact point for jump inheritance.
- `TreadmillPlatform` â configured push direction + force; applies push
  velocity to anything standing on it each physics tick.
- `SpringBoard` â configured launch direction + force; on stand/contact,
  launches the player along that vector.
- Waypoints are child/empty Transforms referenced by the platform itself â
  no cross-scene Inspector wiring to other GameObjects.


# =============================================
# UI ARCHITECTURE â HUD + HQ
# =============================================

# HUD (in-run)
- `HealthDisplay` â current health as a number (starts at 5), reacts to
  PlayerHealth events
- `DashCooldownUI` â radial/fill cooldown indicator, reacts to
  DashModule OnDashCooldownChanged events
- `ChargeBarUI` â charge level while holding fire
- HUD reacts to events/SO data only â never queries managers directly

# HQ UI
- `ShopUI` â lists `PermanentUpgradeSO` from `PermanentUpgradeRegistrySO`,
  shows cost in metaCurrency, buy button, owned state
- Run-start panel: pick level (unlocked) + difficulty (Easy/Medium/Hard)


# =============================================
# CLAUDE BEHAVIOR â how to interact with me
# =============================================

- Be concise and technical. No filler, no fluff.
- If my approach violates Unity best practices or C# conventions, say so directly before implementing it.
- If my implementation request is suboptimal, suggest a better approach with a brief trade-off explanation before writing code.
- Don't add unsolicited comments in code unless the logic is non-obvious.
- Prefer showing diffs or targeted edits over rewriting entire files.
- If a request is ambiguous or could have architectural implications, ask one clarifying question before proceeding.
- Never pad responses with "Great question!" or summaries of what you just did.

# Output formatting â manual Unity steps (STRICT)
- Anything I need to do by hand in the Unity Editor â attach a script, wire an Inspector reference, run a `Tools/...` menu item, drag a prefab, set a SerializeField, open a scene, bake lighting, toggle a component, restart Play mode, etc. â goes in a dedicated `## Manual Steps` section at the END of the response.
- Use markdown only. Do NOT emit raw ANSI escape codes (`\e[...m`) â they don't render in this terminal and show up as literal `[92m...[0m` garbage.
- Each step is a checkbox: `- [ ] <action>`. Checkboxes let me mentally tick items off as I work and not lose place in long lists.
- One concrete action per checkbox â don't bundle. If a step has multiple parts ("open scene X, find GameObject Y, set field Z"), break it into separate checkboxes so none gets skipped.
- Phrase as imperative: "Attach TestStat.cs to a GameObject in the Run scene", "Run Tools > Blast Frame > Implement Fix > 012", "Set the `_playerStats` field on the PlayerController to the PlayerStats component".
- If there are NO manual steps for the response, omit the section entirely. Don't write a "## Manual Steps" header with "(none)" underneath.
- The section appears at the very end, after any code-change summary â it's the last thing I read before closing the response.
- Example:
  ```
  ## Manual Steps
  - [ ] Open Assets/Scenes/Level01.unity.
  - [ ] Attach TestStat.cs to the TestManager GameObject.
  - [ ] Run Tools > Blast Frame > Implement Fix > 012 - Wire PlayerStats into PlayerController.
  - [ ] Press Play and confirm the dash cooldown ring fills over 5 seconds.
  ```


# =============================================
# ARCHITECTURE â reusable across projects
# =============================================

# Coding Conventions
- Every `[SerializeField]` field must have a `[Tooltip("...")]` attribute explaining what it does and what value to put in it
- Use `[SerializeField] private` instead of `public` for Inspector-exposed fields
- Prefer ScriptableObjects for shared data (stats, config)
- Use `TryGetComponent` instead of `GetComponent` where null is possible
- No `Find()` or `FindObjectOfType()` at runtime â cache references in Awake/Start
- Null checks with `?.` and `??` operators preferred
- Async/await over coroutines where possible (Unity 6 supports Awaitable)
- All MonoBehaviour lifecycle methods must be private
- Never use `Update()` for things that can be event-driven (movement physics in FixedUpdate is fine)

# Component Self-Containment Rule (STRICT)
- Every component must work with ONLY its own components (GetComponent) and SO asset references â no Inspector drag-and-drop to other scene GameObjects ever
- `[SerializeField]` fields may only reference: ScriptableObjects, primitives, structs, and UnityEngine asset types (Mesh, Material, AudioClip, ParticleSystem prefab, etc.)
- `[SerializeField]` fields must NEVER reference a MonoBehaviour or GameObject from another scene object
- Other scene objects are found at runtime via EntityRegistrySO (for entities), ServiceLocator (for services), or GameEventSO (for notifications)
- Own sibling/child components are resolved via GetComponent/GetComponentInParent/GetComponentInChildren in Awake or Start â never via Inspector
- Exception: a platform may reference its OWN child waypoint Transforms (they are part of the same prefab) â that is self-containment, not cross-object wiring
- If a component cannot function without a specific other scene object, that is a design smell â refactor to use a registry or event

# Performance
- Pool frequently instantiated objects (projectiles, missiles, VFX, enemies)
- Avoid allocations in Update()/FixedUpdate() â no LINQ, no string concatenation
- Use `Physics.OverlapSphereNonAlloc` / `RaycastNonAlloc` over allocating variants
- Player kinematic sweep uses cached buffers â no per-frame allocations
- Cache the camera reference â no Camera.main at runtime

# ScriptableObject Conventions
- SOs are for shared runtime data that multiple systems need to read (transforms, active entity lists, game state flags) â not for object configuration
- Object configuration (speed, damage, range, etc.) uses FloatReference/IntReference fields on the MonoBehaviour â not config SOs per entity
- Do not create SOs just to avoid variables â if only one system owns the data, it lives on that MonoBehaviour
- Use SOs for: variable values, game events, powerup definitions, audio cues, entity definitions, level definitions, room variant definitions, permanent upgrade definitions
- Runtime sets: SO that holds a List<T> of active objects instead of FindObjectsOfType
- Never store runtime mutable state in SOs that ship as assets â create runtime clones if needed

# Variable SOs and References (Ryan Hipple Pattern)
- Variable SOs: `FloatVariable`, `IntVariable`, `BoolVariable` â a SO wrapping a single primitive value
- Reference structs: `FloatReference`, `IntReference`, `BoolReference` â serializable struct with `bool UseConstant`, a constant value field, and a Variable SO field
- In the Inspector a dropdown lets the designer choose: type a raw constant OR plug in a Variable SO asset â zero friction
- Variable SOs live in `Assets/ScriptableObjects/Variables/` â named by what they represent, e.g. `PlayerMoveSpeed`, `DashCooldown`, `PlayerStartHealth`
- Use `FloatReference`/`IntReference` (not raw float/int) for any stat field on a MonoBehaviour that a designer might ever want to share or tune globally
- This is an FPS, not an idle game â use int/float, never double
- Shared variable SOs are read-only at runtime â never write back to them from gameplay code
- Custom PropertyDrawer lives in `Assets/Editor/Variables/` â one base class, one thin subclass per type

# EntityDefinitionSO
- `EntityDefinitionSO` is the factory recipe for a spawnable entity â holds the prefab reference and a string ID
- Stats are NOT on the EntityDefinitionSO â they live as FloatReference/IntReference fields on the entity's MonoBehaviour
- Spawners and pool systems take an `EntityDefinitionSO` as input â they are the cook, the SO is the recipe
- EntityDefinitionSOs live in `Assets/ScriptableObjects/Entities/`
- The ID on EntityDefinitionSO matches the PoolId used in PoolConfigSO â use the same string constant from a static class
- Registrars (EntityRegistrar + EntityRegistrySO) track active instances at runtime regardless of how the entity was spawned

# GameEventSO Pattern
- GameEventSO is a SO with a UnityAction â raised by any system, listened to by any system
- No direct references between systems that only need to react to each other
- Events live in Assets/Events/, named by action: OnPlayerDamaged, OnPlayerDeath, OnEnemyKilled, OnBossDefeated, OnRoomCleared, OnLevelCleared, OnPowerupPicked, OnPermanentPurchased, OnRunStarted, OnReturnedToHQ
- Listeners are MonoBehaviour components (GameEventListener) placed on relevant GameObjects in scene
- Complex event data uses typed variants: GameEventSO<T> for passing payloads

# Service Locator
- Static ServiceLocator class lives in Core scene bootstrap
- Systems register themselves in Awake, deregister in OnDestroy
- Never call ServiceLocator.Get<T>() in Awake â use Start() to guarantee registration order
- Fail loud: ServiceLocator.Get<T>() throws an exception if service not found â no silent nulls
- Services are interfaces not concrete classes â IAudioManager, ISaveManager, ISceneLoader, ICurrencyManager, IRunManager, IShopManager
- Singletons are banned â use Service Locator or SO reference

# Dependency Rules
- Rule of thumb: if a system needs to trigger something, use GameEventSO. If it needs a return value or direct API, use Service Locator
- Singletons are banned â no static Instance properties
- MonoBehaviours never hold direct references to manager classes â go through ServiceLocator.Get<T>() or GameEventSO
- Core scene bootstrapper registers all services before any other scene loads

# Scene Management
- Additive scene loading exclusively â never LoadSceneMode.Single except initial boot
- Core scene always loaded â never put persistent systems in level/HQ scenes
- Use a SceneLoader service (not MonoBehaviour) for async scene transitions
- Boot sequence: Core loads first, then MainMenu/HQ additively via BootLoader
- A run loads one level scene at a time additively; the Player object persists across level transitions within a run (held in Core, not in a level scene)
- Scene names referenced via static SceneNames class â no magic strings
- Never use DontDestroyOnLoad â if it persists, it belongs in Core

# Save System
- SaveData is a plain C# class â serializable, flat where possible. No MonoBehaviour, no SO
- SOs are never serialized â save IDs only, resolve to SO references at runtime via registry
- SaveManager is a service class registered via Service Locator
- Save on: permanent purchased, level unlocked, weapon/ability unlocked, return to HQ, application pause/quit
- Encrypt save file with AES â not for security, to prevent casual editing

# Audio System
- All audio driven by AudioCueSO references â never reference AudioClips directly in game code
- AudioCueSO holds: AudioClip[] (for variation), float volume, float pitchMin/pitchMax, bool loop
- Multiple clips in one SO = random selection on play â use for shots, hits, footsteps, etc.
- AudioManager listens to GameEventSOs â systems never hold a reference to AudioManager
- AudioManager maintains a pool of AudioSources for SFX â no runtime instantiation
- 3D SFX: position the pooled AudioSource at the event location (spatial blend per cue)
- Music plays on a dedicated AudioSource pair for crossfading
- Never set AudioSource.volume directly â always route through mixer groups
- Mixer parameter names referenced via static AudioMixerParams class â no magic strings

# State Machines â Game State
- Enum-driven FSM managed by GameStateMachine service registered via Service Locator
- Transitions raise GameEventSOs â nothing queries game state directly, systems react to events
- GameStateMachine exposes CurrentState property for direct checks only when necessary
- No gameplay logic inside GameStateMachine â it orchestrates, it does not execute

# Input System
- Use the existing `Assets/InputSystem_Actions.inputactions` asset; enable "Generate C# Class"
- Wrap the generated class in a PlayerInputHandler MonoBehaviour in Core scene â nothing else touches the generated class directly
- PlayerInputHandler exposes clean C# events / value getters: Move, Look, OnJumpPressed, OnJumpReleased, OnDashPressed, OnFirePressed, OnFireReleased, OnInteract
- Jump must expose both press AND release (variable jump height needs the release)
- Input supports keyboard/mouse and controller
- Disable input via PlayerInputHandler.SetInputEnabled(bool) â never disable the asset directly
- Don't modify the Input System asset from code â use the action wrapper

# Object Pooling
- Generic Pool<T> class â not MonoBehaviour specific, not per-object-type implementations
- PoolManagerSO holds named pools â registered at boot, accessed via ServiceLocator.Get<IPoolManager>()
- Pools are pre-warmed at scene load â no runtime instantiation during gameplay
- Every poolable object implements IPoolable with OnSpawn() and OnDespawn() methods
- Objects return themselves to pool via OnDespawn() â callers never manually return objects
- Pool size defined per prefab in a PoolConfigSO â never hardcoded
- If pool is exhausted: expand by configured increment and log a warning â never silently fail or drop objects
- Pooled types: PlayerProjectile, EnemyMissile, ArcProjectile, hit/explosion VFX, common enemy variants

# Camera
- Custom first-person camera rig â no Cinemachine (direct mouse/right-stick look, pitch clamp)
- Screen shake via camera-local shake on player damage / explosions / heavy landings â keep it simple
- Cache the camera reference at startup â no Camera.main at runtime
- URP: gameplay code does not touch render pipeline assets; volume tuning is asset-side


# =============================================
# GLOBAL AVOID â never do these
# =============================================

- No FindObjectOfType() anywhere at runtime
- No GameObject.Find() at runtime
- No SendMessage()
- No singleton pattern â no static Instance properties
- No DontDestroyOnLoad
- No direct cross-system MonoBehaviour references set via Inspector across scenes
- No [SerializeField] MonoBehaviour or GameObject references to other scene objects â use EntityRegistrySO, ServiceLocator, or GameEventSO instead
- No CharacterController for the player â use the custom kinematic Rigidbody motor
- No dynamic-force/AddForce-driven player movement â the motor sets velocity explicitly
- No AudioSource.PlayClipAtPoint() â instantiates at runtime
- No AudioSource components on enemy/player prefabs â route through AudioManager pool
- No Animator as logic FSM
- No nested switch statements for state logic
- No magic strings anywhere â scene names, mixer params, ids all use constants or SO
- No legacy Input Manager
- No UI Toolkit â use Canvas + TextMeshPro
- No legacy UnityEngine.UI.Text â use TextMeshProUGUI
- No LINQ or string concatenation in Update()/FixedUpdate()
- Don't modify the Input System asset directly â use action wrappers
- No Camera.main access at runtime â cache camera reference
- No hardcoded stat, damage, cost, or tuning values â FloatReference/IntReference or SO
- No magic numbers for gameplay tuning â everything designer-tunable
- No per-object-type pool implementations â use generic Pool<T>
- No runtime pool instantiation during gameplay â pre-warm at scene load
- No UI logic that queries managers directly â UI reacts to events and SO data only
- No per-room bespoke scripts â room variability is prefab + RoomVariantSO data driven
- No double for anything â this is an FPS, use int/float


# =============================================
# EDITOR TOOLING â always generate alongside features
# =============================================

- Every new entity type, system, or prefab setup must ship with a corresponding Editor menu item
- Menu path convention: `Tools/Blast Frame/[Category]/[Action]` (e.g. `Tools/Blast Frame/Enemies/Create Turret Prefab`)
- Editor scripts live in `Assets/Editor/` â never in runtime folders
- Wizards must: create the GameObject, add required components, assign default SOs where possible, ping the created object in the hierarchy
- If a wizard needs user input (enemy type, pool size, platform path mode, etc.), use `EditorUtility.DisplayDialog` or a simple `EditorWindow` â not just hardcoded defaults
- Wizards are not optional â if code is written for a system, the editor tool ships in the same session


# =============================================
# IMPLEMENT FIX â numbered one-click editor actions
# =============================================

- `Assets/Editor/ImplementFix.cs` hosts all one-click fixes as **numbered** `[MenuItem]` methods
- Menu path for every fix: `Tools > Blast Frame > Implement Fix > NNN - <short description>`
- Numbering is sequential and never reused: `001`, `002`, `003`, ... Find the next number by scanning existing `[MenuItem]` attributes in the file
- Each fix is **SINGLE-PURPOSE** â it does exactly the one thing the developer asked for that session, nothing else
- Fixes MUST NOT rewrite/re-author user-editable assets (PowerupSO / PermanentUpgradeSO tuning fields like cost, magnitude, etc.). A prior monolithic implement-fix on another project reverted hand-tuned values â never again. If a fix needs to touch an asset the developer may have hand-edited, bail out with a warning instead
- Each fix is **ADDITIVE** â never delete, edit, or renumber existing fix methods. Old fixes stay so they can be re-run or referenced. The file grows; it is not rewritten
- Each fix has its own private static method named `FixNNN`. Put helpers inline inside the method or in a private static helper class at the bottom of the file â never share helpers across fixes (one fix changing a helper must not change another fix's behavior)
- When the developer says "implement fix" / "make an implement fix for X", Claude appends a new `FixNNN` method with the next available number and leaves every existing fix untouched
- Useful for: one-time bulk Inspector wiring, prefab setup, adding a toggle to a scene, SO reference fixups â things that are tedious to do by hand once
- Not useful for: anything that belongs in runtime code, or anything that mutates asset fields the developer may tune manually
- ImplementFix.cs is committed â the numbered history is intentional


# =============================================
# TESTSTAT â debug testing convention
# =============================================

- `Assets/Scripts/Debug/TestStat.cs` is the scratch test script
- Attach it to any GameObject in the scene to run ad-hoc tests
- The script body is INTENTIONALLY REWRITTEN each time a new thing needs testing â it is not cumulative
- Uses EntityRegistrySO to reach the player or other entities at runtime
- When asked to write a new test, Claude rewrites TestStat.cs entirely for the new purpose
- Never commit TestStat.cs â it is always throwaway code


# =============================================
# GAME INFORMATION DOC â living setup reference
# =============================================

- File lives at `Assets/GameInfo.md`
- Claude updates this file every session when new entity types, systems, or setup patterns are introduced
- Format per entry: entity/system name, required components, required SOs, setup steps, gotchas
- Entries are cumulative â never remove old entries, only amend if a pattern changes
- Claude must explicitly confirm at end of session if GameInfo.md was updated or note what needs adding


# =============================================
# GAME DESIGN DOC â running design log
# =============================================

- File lives at `Assets/GameDesign.md`
- A quick-read living log of all design decisions made between Claude and the developer
- Newer entries at the top, older entries at the bottom â never reorder existing entries
- Entry format:
  [DATE] â [TOPIC]
  [2-4 sentence summary of what was decided and why]
- Claude auto-updates this every session when design decisions are made
- At session end, Claude confirms whether GameDesign.md was updated or states what needs adding
- This is a design reference, not a technical one â write in plain language, not code
- Never delete old entries â if a decision is reversed, add a new entry noting the change and why


# =============================================
# LAST LEFT OFF â session handoff scratchpad
# =============================================

- File lives at `LAST_LEFT_OFF.txt` in the PROJECT ROOT (next to CLAUDE.md) â NOT under `Assets/` (a scratchpad must not become an imported/shipped Unity asset with a .meta)
- **READ IT AT SESSION START**, right after CLAUDE.md, before doing any work â it is the "where is the needle right now" pointer
- It is TRANSIENT state, not a log: a single always-current snapshot. OVERWRITE it each time, never append. It does not duplicate GameInfo.md / GameDesign.md (those are the durable docs) â it points at them
- TRIGGER PHRASES â the two canonical commands (treat as unambiguous, act immediately):
  - **"park it"** â OVERWRITE LAST_LEFT_OFF.txt with the current snapshot, then stop
  - **"where were we"** â READ LAST_LEFT_OFF.txt and summarize state + propose the next step
  - (also honor the obvious natural equivalents: "wrapping up"/"done for now" = park it; "catch me up" = where were we)
- WRITE/OVERWRITE it when:
  - the developer uses a write trigger phrase above, or closes the session
  - context is about to be summarized/compacted (write proactively so nothing is lost across a context reset)
- Fixed structure (keep it short â a cold reader must reorient in ~20 seconds):
  ```
  LAST LEFT OFF â [YYYY-MM-DD HH:MM]
  LAST ACTION: [the one thing just completed]
  CURRENT STATE: [what works / what is half-done right now]
  NEXT STEP: [the exact next action to take]
  PENDING MANUAL STEPS: [Unity-editor steps written but NOT yet confirmed run by the dev â list the Tools menu items / wiring, or "none"]
  BLOCKERS / OPEN QUESTIONS: [anything waiting on the dev, or "none"]
  ```
- On session start, after reading it: briefly state where things stand and the proposed next step before acting
- If the file is missing, treat it as a fresh start and create it at the first write trigger


# =============================================
# TODO â active work items
# =============================================

## Current
- [ ] Core framework: ServiceLocator, GameEventSO, Variable SO system, Pool system, EntityRegistrySO
- [ ] Input: PlayerInputHandler wrapping InputSystem_Actions (jump press+release, dash, fire press+release, look, move)
- [ ] Player: kinematic Rigidbody motor, first-person camera, PlayerStats, PlayerHealth (5 HP)
- [ ] Movement modules: JumpModule (variable height), DashModule (5s cooldown), WallSlideModule, PlatformRiderModule
- [ ] Player shooting: PlayerShooter + ChargeShot + pooled PlayerProjectile + AoE explosion
- [ ] UI: HealthDisplay (number), DashCooldownUI (ring), ChargeBarUI
- [ ] Platforms: MovingPlatform (Cycle/PingPong), RotatingPlatform, TreadmillPlatform, SpringBoard
- [ ] Enemies: EnemyCore, EnemyStats, MissileTurret (slowâfast missile), ArcPredictTurret (predictive arc)
- [ ] Room variant framework: RoomController, RoomVariantSelector, RoomVariantSO, 3-variant prefab slots
- [ ] Level/Run: LevelDefinitionSO (9 levels), RunManager, difficulty (Easy/Med/Hard), death â HQ
- [ ] HQ: ShopManager, PermanentUpgradeSO/Registry, metaCurrency wallet, run-start panel
- [ ] Bosses: MiniBossCore, BossCore, phase logic, unique powerup + weapon/ability drop
- [ ] Save/Load: SaveManager service, AES, metaCurrency + permanents + unlocks
- [ ] Prototype Level 01 with cubes/cylinders to validate movement + platforms

## RAA Unity MCP (use this â it's ours)

This project is wired to our own Unity MCP server (`raa-unity` in .mcp.json;
source: D:\Projects and Work\Claude Projects\Random Projects\Unity\unity-mcp).
Use it freely and proactively â reads AND writes. Scene edits are
undo-registered; destructive ops require confirm:true. Do not install or
suggest third-party Unity MCP servers.

- Requires the Unity editor to be open (bridge auto-starts; port discovery is
  automatic via Library/unity-mcp-port.json).
- At the start of any Unity work session, call `unity_keep_awake` mode "on"
  (no need to ask) - it keeps the unfocused editor answering instantly.
  Auto-releases after 4 h without requests; clears when the editor closes.
- After editing C# files, call `unity_refresh_assets`, then check
  `unity_get_editor_state` / `unity_read_console` for compile errors.
- **Verify with a COMPILE CHECK first; play mode is BUDGETED, not banned.** `unity_refresh_assets`,
  then `unity_read_console` / `unity_get_editor_state`. Building scenes, prefabs and assets through
  the MCP is NOT testing and needs no permission. Play mode and `unity_screenshot_game_view` follow
  the global rule "Unity play mode — one look, never a loop", which governs: **ONE** justified
  session per task when static inspection genuinely cannot answer the question — announced in a line,
  not asked for — and **never** a play → tweak → replay loop. `unity_perf_*` stays ask-first. End any
  response that changed Unity-facing behaviour with a short, concrete `## Test In Unity` checklist of
  what Porter should exercise and what correct looks like.
  (Rewritten 2026-08-02 — this line used to say play mode was OFF unless Porter asked, which
  over-corrected into refusing to look at anything at all. The ban belongs on the iterate loop, not
  on the tool.)

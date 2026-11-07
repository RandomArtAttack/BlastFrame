# Blast Frame — Game Info (living setup reference)

Entries are cumulative. Never remove. Amend if a pattern changes.

---

## Level Design — reach measurement, jump gizmo, reachability lint (2026-11-07)

All editor-only (`Assets/Editor/LevelDesign/`). Nothing here enters play mode.

### Measure Player Reach — `Tools/Blast Frame/Level Design/Measure Player Reach (real motor)`
- Copies the Core Player's movement components (PlayerStats, PlayerMotor, Jump/Dash/WallSlide/LedgeVault/
  PlatformRider) onto a headless rig (`MotorSimRig`) and drives the REAL code with scripted input +
  `Physics.Simulate`, inside a temporary empty scene (your scenes are saved-prompted and restored).
- Binary-searches lip-to-lip gaps for StandingJump / RunJump / DashJump at Δheight −6…+3 m, "perfect" (any
  frame works) and "comfortable" (≥100 ms timing window; standing = toes 0.25 m back), plus jump apex,
  tallest ledge (jump + vault) and wall-jump chimney widths.
- Result cached at `Library/BlastFrame/ReachProfile.json`, keyed by a hash of the player tuning. Tools say
  "stale — re-measure" when the loaded Player's tuning differs.
- Measured 2026-11-07 (Core tuning: jump 12, gravity 24, move 7, dash 14 × 1 s, coyote 0.1):
  flat gaps — standing 7.73 / 7.00 comfy, run 8.88 / 8.25, dash-jump 16.86 / 15.73 m;
  +3 m up — run 6.64, dash 12.38; apex 3.14 m; tallest ledge 4.03 m; chimneys 1.5–5 m climbable, 6 m not.
- Gotcha: PlayerTestMode is deliberately NOT copied (multi-jump would make every gap reachable).

### Jump Reach Gizmo — `Tools/Blast Frame/Level Design/Jump Reach Gizmo`
- Ctrl+Shift+Click a surface in the Scene view to drop a takeoff point (faces the camera's look direction),
  or "Takeoff = selected object". Drag the handle / disc to move and aim.
- Draws the real motor's arcs per move kind, linecasts them against your geometry, marks the landing
  (red "WALL HIT" if it lands on an unwalkable face), and optional flat-ground gap rings.

### Reachability Lint — `Tools/Blast Frame/Level Design/Reachability Lint (open scene)`
- Samples every standable surface (0.5 m grid, headroom-checked), groups walk-connected islands, links islands
  with the measured jump/dash table, and colours surfaces by the HARDEST move needed from the start:
  Walk / Jump / Perfect jump / Dash jump / Perfect dash / Unreachable (red); LavaHazardVolume surfaces orange.
- Start = selected object (toggle) → Entry `RoomMarker` → largest island.
- "Only analyse inside the selected object's bounds" skips skyboxes / backdrops (Area1LavaBridge: 5 s whole scene).
- Gotchas: gaps are estimated from cell centres (optimistic by ≤ one cell) — hand-test anything near a limit.
  Moving/riding platforms are analysed at their CURRENT pose only.

### Room Variant Lint — `Tools/Blast Frame/Level Design/Lint Room Variants`
- Every RoomController in open scenes has exactly 3 non-null variants; RoomVariantSO ids present + unique;
  prefab assigned, has colliders, contains no Player / Camera / AudioListener / EventSystem; and (headless)
  every Exit / Secret `RoomMarker` inside each variant prefab is reachable from its Entry marker.
- 2026-11-07 result: Demo_VariantA/B/C have NO prefab assigned (only errors in the project).

### RoomMarker (runtime, inert)
- `Assets/Scripts/Gameplay/Rooms/RoomMarker.cs`. Kind: Entry / Exit / Secret / Checkpoint + optional label.
- No runtime behaviour. Drop inside room variant prefabs so the lints know where the player arrives and what
  must be reachable.

---

## Seeding — SeedService + VariantSetSO (DESIGN.md §6–7) (2026-11-07)

### SeedService (`Assets/Scripts/Core/Seeding/SeedService.cs`)
- `new SeedService(runSeed)`; `Roll(areaId, roomIndex, slotId)` / `Range(..., count)` / `Value01(...)`.
- Stable hash (FNV-1a + SplitMix64) — NOT string.GetHashCode. `SeedService.NewRunSeed()` for a fresh run.
- Golden values pinned in `InfrastructureTests.Seed_GoldenValues_NeverChange`: changing the hash changes every
  saved/shared run's content — only do it on purpose with a save version bump.
- NOT YET WIRED: nothing registers an ISeedService or calls `RoomController.Initialize(seed)` yet, so rooms
  still use their `_fallbackSeed` (same variant every run). RunManager should create one per run and store
  `runState.runSeed`.

### VariantSetSO<T> / RoomVariantSetSO
- Weighted seeded pick; weight 0 or empty item = never picked. `set.Pick(seeds, areaId, roomIndex, slotId)`.
- Create: `Tools/Blast Frame/Rooms/Create Room Variant Set` (pre-fills with selected RoomVariantSOs) or
  Create > Blast Frame > Rooms > Room Variant Set. One subclass per item type (Unity can't make open generics).

---

## Areas — AreaDefinitionSO + effect graph (DESIGN.md §8) (2026-11-07)

- `AreaDefinitionSO`: id (permanent — saved), displayName, tier, startsUnlocked, sceneName, onComplete effects.
- Effect vocabulary (`AreaEffectType`, serialized by value — append new kinds at the END only):
  UnlockArea(target) · AlterSpawnTable(target, key) · OpenShortcut(target, key) ·
  ApplyBossWeakness(target, key = weapon id, amount = multiplier, default 2) · PriceCut(amount = %, total clamped 90).
- `AreaRegistrySO` (`Assets/ScriptableObjects/Areas/AreaRegistry.asset`) lists every area.
- `AreaGraph.Derive(areas, completedAreaIds)` → per-area Unlocked / Completed / Alterations / Shortcuts /
  BossWeaknesses / UnlockedBy + global PriceCutPercent. Pure, recomputed on area enter, never saved.
- `AreaGraph.Validate` / `Tools/Blast Frame/Areas/Validate Area Graph`: duplicate/empty ids, unknown or self
  targets, missing keys, no start area, areas no completion order can unlock.
- `Tools/Blast Frame/Areas/Create Area Definition` creates + registers (first one starts unlocked).
- NOT YET WIRED into RunManager/LevelController (those still use linear LevelDefinitionSO/level index).

---

## Save — DESIGN.md §11 shape (2026-11-07)

- SaveData gained `completedAreaIds` (HashSet), `unlockedAbilityIds`; RunSaveData gained `runSeed`,
  `currentAreaId`. Retired: `unlockedLevelIndex`, `currentLevelIndex` (nothing read them). Old saves still load.
- Saves are write-then-swap (`.tmp` + File.Replace) — a crash mid-write keeps the previous save.
- An unreadable save is copied to `blastframe.sav.unreadable-<timestamp>` before starting fresh (the next
  pause/quit save would otherwise overwrite it).
- `SaveManager.Encode/Decode` expose the exact on-disk codec for tests/tools.
- CurrencyManager now mirrors the wallet into `metaCurrency` on every change.

---

## Tests (2026-11-07)

- Index + headless command: `Assets/Editor/Tests/README.md`. All EditMode; ~75 tests; default gate
  `-testCategory "!Report;!Slow"` ≈ 2 min including editor startup.
- `ProjectDataTests.CorePlayer_TestModeIsOff` is INCONCLUSIVE (a warning) while PlayerTestMode multi-jump is on.

---

## Camera — CameraFeel (movement juice)

### CameraFeel
**Namespace:** `BlastFrame.CameraRig`
**File:** `Assets/Scripts/Camera/CameraFeel.cs`
**Type:** MonoBehaviour on the player's first-person **Camera** GameObject (`Player/YawPivot/Camera`), sibling to `FirstPersonCamera` and `CameraShake`. `[RequireComponent(Camera)]`, `[DefaultExecutionOrder(100)]` so its LateUpdate composes AFTER the look rig and shake each frame.
**Purpose:** Pure feedback layer — makes speed *feel* like speed. Reads existing motor/module state (no mechanics changes) and applies: **Dash FOV kick** (FOV +`dashFovKick` while `DashModule.IsDashing`, fast rise / slow decay), **Speed FOV** (additive FOV scaled by horizontal speed above `PlayerStats.MoveSpeed`), **Landing dip** (camera dips local-Y on hard landings, impact-scaled with a dead-zone, critically-damped spring back), **Wall-slide lean** (rolls away from `PlayerMotor.WallNormal` while `WallSlideModule.IsSliding`), **Strafe roll** (subtle roll opposite lateral velocity while grounded). **No head-bob** (deliberately omitted — it fights precision platforming).
**Self-containment:** caches its own `Camera` + sibling `CameraShake` via `GetComponent`, and `PlayerMotor` / `DashModule` / `WallSlideModule` / `PlayerStats` via `GetComponentInParent` (camera is a child of the Player root). Null-tolerant: any missing sibling just disables that effect — no Inspector cross-wiring.
**Composition (how it avoids fighting the look rig):** `FirstPersonCamera` re-sets `localRotation` to pure pitch every LateUpdate and `CameraShake` re-sets `localPosition` every LateUpdate; CameraFeel runs last (order 100) and **post-multiplies** roll onto the current rotation and **adds** the dip onto the current position, so nothing accumulates. If NO `CameraShake` sibling exists it instead reconstructs position from a captured rest pose. FOV is written on THIS (base) camera only — the stacked overlay **ViewmodelCamera** keeps constant FOV during a dash by design (desirable).
**Tuning (all `FloatReference`, set any magnitude to 0 to disable that effect):** `dashFovKick`(12) `dashFovRiseTime`(0.06) `dashFovDecayTime`(0.2) `speedFovMax`(4) `speedFovRange`(8) `speedFovSmoothTime`(0.1) `landDipMax`(0.25) `landDipDeadZone`(6) `landDipFullSpeed`(18) `landDipSpringHz`(8) `wallLeanDegrees`(5) `wallLeanSmoothTime`(0.15) `strafeRollDegrees`(1.5) `strafeRollSmoothTime`(0.12).
**Setup steps:**
1. **Tools > Blast Frame > Player > Add Camera Feel** — adds CameraFeel to every `FirstPersonCamera` in the open scene(s). The player lives in the **Core** scene and persists across additive level loads, so adding it once to the Core player camera covers ALL level scenes (Sandbox, Area1LavaBridge, …). There is a single FirstPersonCamera in the project.
2. Save the Core scene.
3. Tune magnitudes in the Inspector (live in Play mode).
**Gotchas:**
- **Wired on the Core player only.** Sandbox / Area1LavaBridge do NOT have their own player camera — they use Core's persistent player. Don't add a duplicate.
- **FloatReference defaults & fresh-Play timing.** Defaults come from the `= new FloatReference(x)` field initializers and ARE serialized to the scene (verified in `Core.unity`). If you add the component via the wizard and press Play *before the recompile settles*, the play-mode instance can transiently load all-zero (effects appear disabled). Stop, let compilation finish, Play again — the saved scene has the correct values. Same pattern as `PlayerStats`.
- **Roll composition depends on `FirstPersonCamera` resetting rotation each frame** (it does, once input is available). It post-multiplies roll and does not touch pitch/yaw.
- Setting every magnitude to 0 makes the camera behave EXACTLY as before the component existed (base FOV, look rotation, shake position only) — verified.

---

## Player Movement — LedgeVaultModule (mantle assist)

### LedgeVaultModule
**Namespace:** `BlastFrame.Gameplay.Player.Movement`
**File:** `Assets/Scripts/Gameplay/Player/Movement/LedgeVaultModule.cs`
**Type:** MonoBehaviour `IMovementModule` on the Player root (Order = `MovementOrder.LedgeVault` = 35).
**Purpose:** Small automatic hop over a "near-miss" ledge — the player is wall-sliding and steering into the wall, the gun (upper) clears the ledge but the feet catch it. Detection: upper forward ray (gun height) CLEAR + lower forward ray BLOCKED + a downward ray past the lip finds a walkable top within `maxVaultHeight` of the feet. Pop is computed to clear the measured lip (`sqrt(2·Gravity·(clearHeight+margin))`) plus `vaultUpBonus` (the "mini jump" feel), with `vaultForwardSpeed` nudging the player onto the surface. Probes use the motor's Rigidbody position; gravity is read from sibling `PlayerStats`.
**Behaviour:** Automatic (no input button) — reads `WishDir` to require pushing into the wall. Runs AFTER WallSlide so it overrides that tick's slide clamp; yields to an explicit jump/wall-jump (skips if `JumpFired` already set). Sets `JumpFired` so PlatformRider releases any carry. Cooldown (`vaultCooldown`) prevents per-tick re-fire. Tall ledges (> `maxVaultHeight`) are ignored → player wall-jumps those.
**Required components:** Player root must have `PlayerMotor` (Rigidbody + CapsuleCollider) and `PlayerStats` (all siblings, resolved via GetComponent).
**Setup steps:**
1. Run **Tools > Blast Frame > Implement Fix > 052 - Add Ledge Vault Module To Player** (adds the component in Core and sets `vaultMask` to exclude Player/Viewmodel).
2. Tune on the component: `maxVaultHeight` (how tall a ledge auto-vaults), `vaultUpBonus` (the little pop), `vaultForwardSpeed`, `upperProbeHeight`/`lowerProbeHeight`/`probeDistance`, `pushIntoWallDot`.
**Key fields:** `vaultMask`, `upperProbeHeight`, `lowerProbeHeight`, `probeDistance`, `maxVaultHeight`, `minVaultHeight`, `maxLedgeAngle`, `pushIntoWallDot`, `vaultClearMargin`, `vaultUpBonus`, `vaultForwardSpeed`, `vaultCooldown`.
**Gotchas:**
- **`vaultMask` must exclude Player and Viewmodel** or the gun/own capsule reads as a wall. Fix 052 sets this; if you add it by hand, clear those layers.
- **The hop is auto-sized to clear the lip**, so `vaultUpBonus` is only the extra feel — it is NOT the whole jump. To stop tall ledges from auto-vaulting, lower `maxVaultHeight`, don't lower the bonus.
- It only fires while wall-sliding and falling (`Velocity.y <= 0.1`), so a player still rising into a ledge clears (or doesn't) on their own momentum.

## Platforms — MovingPlatform Path Smoothing (ease in/out)
**File:** `Assets/Scripts/Gameplay/Platforms/MovingPlatform.cs`
**Fields:** `pathSmoothing` (bool) + `decelerationPower` (FloatReference, m/s²). When on, the platform decelerates approaching each waypoint and accelerates after it, via `v = sqrt(2·power·distance)` against whichever segment end is nearer, floored at 0.1 m/s (always arrives) and capped at `Speed`. The eased speed feeds `CurrentVelocity`, so rider jump-momentum stays correct; works in all path modes and through the RockBobber external-drive path.
**Gotcha:** ramp length ≈ `Speed² / (2·power)`. Fast platforms need a high power — e.g. Speed 32 wants `decelerationPower` ~128 (≈4 m ramp); at the default 8 it would crawl (~64 m ramp, longer than the segment). Slow platforms (~Speed 3) feel right at the default 8.

---

## Environment — RockBobber (float on lava's visual surface)

### RockBobber
**Namespace:** `BlastFrame.Gameplay.Environment`
**File:** `Assets/Scripts/Gameplay/Environment/RockBobber.cs`
**Type:** MonoBehaviour (attach to any rock/object that should ride the lava)
**Purpose:** The lava's surface is displaced in the VERTEX SHADER (GPU) — it never updates the CPU mesh or the lava collider, so you can't raycast/read-back the moving surface. RockBobber mirrors the shader's displacement math on the CPU: at Rebind it raycasts straight down (Lava layer) to find the lava mesh + material, auto-detects the lava shader, and captures the REAL mesh UV (`RaycastHit.textureCoord`) and undisplaced surface height (`hit.point.y`) under the rock; each FixedUpdate it samples the SAME noise the GPU samples AT THAT UV and seats on baseHeight + displacement. (Reading the real mesh UV — not a local-XZ guess — is what makes it track the surface on any imported mesh.) It never modifies the lava collider.
**Collision / standing on it:** RockBobber is a KINEMATIC-RIGIDBODY MOVING PLATFORM. It auto-adds + configures a kinematic, interpolated, no-gravity Rigidbody in Awake and moves it via `MovePosition`/`MoveRotation` (NOT `transform.position`). It implements `IRidePlatform`, so the existing `PlatformRiderModule` carries a grounded player up/down with the bob — same path as `MovingPlatform`. The rock collider must be solid (not a trigger) and on a layer in the player's `PlayerMotor.collisionMask` (Default works; current mask `m_Bits: 55` = layers 0,1,2,4,5).
**What drives the bob:** ONLY the V.4 lava river (`"RAA Materials/RAA Lava Flow V.4"`) turbulence. There is **NO sine fallback** and **NO "rest in place over a ripple/V.3"** — if the V.4 river can't be resolved the rock **HOLDS STILL and logs why** (so a broken setup is obvious while testing, not masked by a fake bob). The V.4 noise params (strength/scroll/texture) are read from the **River Material** field if assigned, else from the surface directly beneath the rock. Needs Read/Write on BOTH the noise texture AND the lava mesh (for `textureCoord`).
**No-bob diagnostics (read the Console — the warning names the cause):** "No Lava-layer collider within Nm" then a maskless probe report ("Nearest surface below is 'X' on layer 'L' at Dm, shader 'S'" or "Nothing… over a gap") — tells you if the river simply has no collider, is on the wrong layer, or the rock is too high/over a void. "Found lava below … but no usable V.4 river params" = it hit a non-V.4 surface (assign River Material or move onto the river). "mesh NOT Read/Write" = run Implement Fix 046. NOTE: both lava FBXs import with **addColliders: 0**, so the render-only river sub-mesh has NO collider unless one was added — that's the usual "No Lava-layer collider" cause.
**Required components:** none (self-contained; finds lava at runtime via raycast — no Inspector cross-refs).
**Setup steps:**
1. Put the lava surface's collider on the **Lava** layer (the component's Lava Mask defaults to it).
2. Add RockBobber to the rock; position the rock so it sits over the lava (a Lava-layer collider must be below it within Ray Max Distance).
3. Assign **River Material** = the `Lava River` (V.4) material so the bob is sourced from the river even over another lava piece — or run **Implement Fix 047** to wire it into every rock at once.
4. Enable Read/Write on the lava meshes (**Implement Fix 046**) so `textureCoord` works.
5. Tune `Height Offset` (negative = half-submerged), `Displacement Multiplier` (1 = exact surface), optional `Align To Surface`.
6. Or use **Tools > Blast Frame > Hazards > Create Bobbing Rock** to spawn a placeholder cube already wired.
**Key fields:** `lavaMask`, `rayStartHeight`/`rayMaxDistance` (lava search), `heightOffset`, `displacementMultiplier`, `followSpeed` (heft/lag), `enableLerpDuration` (on-enable blend-in seconds), `alignToSurface`/`alignStrength`, `riverMaterial` (V.4 noise source), `noiseIsSRGB`.
**Gotchas:**
- **V.4 path needs Read/Write on BOTH the noise texture AND the lava mesh.** The bob reads the real mesh UV under the rock via `RaycastHit.textureCoord`, which only works on a Read/Write, non-convex MeshCollider; the noise sample needs the texture readable too. Run **Tools > Blast Frame > Implement Fix > 046** to enable Read/Write on the lava FBXs — Rebind logs a warning naming any mesh that isn't readable (it would otherwise return UV (0,0) and track the wrong point).
- **`displacementMultiplier` = 1 now means EXACTLY the surface.** The old local-XZ UV guess undershot, so scene rocks were fudged up (2–5); with the real UV, set it back to 1 for a true match and lower only to make a heavy rock partly follow.
- **sRGB noise is linearised on the CPU** to match the GPU's sampler in a linear-space project (`noiseIsSRGB`, on by default). If you set the noise texture's import to Linear, untick it.
- **On-enable blend (`enableLerpDuration`, default 0.5s):** when the component is enabled — notably re-enabled mid-path by a MovingPlatform's enable-at-index — it smoothstep-blends from the body's current Y into the live bob over this many seconds instead of snapping, INDEPENDENT of `followSpeed`. Set 0 for the old instant snap. The blend's start Y is read from `_rb.position` (not `_prevY`, which is stale after being disabled while the platform moved the body).
- **Heft / weight (`followSpeed`) — the tuning knob:** a plain serialized `FloatReference` on RockBobber. The rock EASES toward the surface via framerate-independent exponential smoothing (`blend = 1 - exp(-followSpeed * dt)`, no overshoot) rather than snapping. It's a SPEED/rate: higher = snappier/lighter, lower = slower/heavier; 0 = frozen, ~3 floaty, ~8 heavy (default), ~15 snappy, ~25 near-instant. Edit it in the Inspector (live in Play mode). The ride velocity handed to the player is the rock's ACTUAL eased motion (`(newY - prevY)/dt`), NOT the target, so the carry stays in sync. A heavy (low-speed) rock can briefly ride below a fast cresting wave — the lag IS the weight; raise `followSpeed` to hug the surface tighter.
- **Ripple per-object phase** is a world-origin hash (fp `sin` of a large number); GPU vs CPU may differ slightly. If a rock floats a constant amount off the surface, set the lava material's **Phase Desync (`_RipplePhaseRandom`) to 0** for exact seating.
- It re-binds only at Start. After moving the rock or changing the lava material in-editor, use the component's **Rebind To Lava Below** context-menu item.
- Only the rock's own kinematic Rigidbody moves; the LAVA collider is left untouched.
- **Carry up/down:** rising is carried by `IRidePlatform` (rider matches the upward velocity); descending relies on the motor's ground-snap (`groundSnapDistance` ~0.35). RockBobber has `[DefaultExecutionOrder(-50)]` so it moves and publishes the ride velocity BEFORE PlayerController each tick — this is REQUIRED for the up-carry: without it the rider reads last tick's stale velocity at the bob's trough, the rising rock penetrates the player's feet, the ground SphereCast starts inside the rock and fails, and the player loses grounding (the "upward stops it working" bug). Don't remove the attribute. If a very fast/large bob makes the player leave the rock on the DOWN-stroke (descent faster than ground-snap), lower `Displacement Multiplier` (scene rocks ship at 5) or raise the motor's ground-snap.
- **Pass-through is fixed at the motor, not here:** PlayerMotor runs a depenetration pass (`Physics.ComputePenetration` over `OverlapCapsule`) at the start of every `Move`, so a player who is NOT being carried (jumping onto the rock, hitting its side, or a fast up-stroke rising into an airborne player) is pushed out of the rock instead of tunneling through. This applies to all moving kinematic platforms, not just rocks. If you ever see pass-through return, confirm the rock's collider is on a layer included in the motor's **Collision Mask**.
- **Moving + bobbing lava platform (RockBobber + MovingPlatform on one object) — supported.** RockBobber auto-detects a sibling MovingPlatform in Awake and calls `SetExternallyDriven(true)`: MovingPlatform then computes the path + publishes `NextPosition`/`CurrentVelocity` but does NOT `MovePosition` (no more two-mover fight). RockBobber becomes the single position owner — takes the path XZ, RE-SAMPLES the lava under its moving XZ each tick, and moves to (pathXZ, easedBobY). Carry composes via the rider-sum: horizontal from MovingPlatform, vertical bob from RockBobber. Requirements/notes: MovingPlatform needs **≥2 child waypoints** (else it disables itself and the rock falls back to stationary bobbing); **WP0 must start over the V.4 river**; the **waypoints' Y is irrelevant** (the lava surface sets height — keep the path travelling over lava). Execution order: MovingPlatform(-60) → RockBobber(-50) → PlayerController(0). Build one with **Tools > Blast Frame > Hazards > Create Moving Bobbing Lava Platform**.
- **MovingPlatform Start Index.** `startIndex` (serialized int) sets which waypoint the platform begins on; it then proceeds in the normal order, wrapping per Path Mode (e.g. 10 waypoints, start at 8 → runs 8→9 then wraps). Clamped to the count. Edge handling: PingPong starting on the last waypoint heads back down; Reuse Loop Teleport starting on the last begins at the first instead (avoids an instant warp). Works with the moving+bobbing combo (RockBobber reads the resulting NextPosition).
- **MovingPlatform PathMode = Reuse Loop (Teleport).** Besides Cycle (travel back to start) and PingPong (reverse at ends), there's `ReuseLoopTeleport`: on reaching the LAST waypoint the platform INSTANTLY warps back to the first and continues — a one-way conveyor that can vanish into an inaccessible area and reappear (warp via `Rigidbody.position`, not MovePosition). On the warp tick `CurrentVelocity` is forced to 0 so a rider isn't flung by the warp delta (they're left behind, by design), and a sibling RockBobber detects the large XZ jump and snaps (no bob smear across the warp). Needs ≥2 waypoints; works with the moving+bobbing combo. Caveat: with Rigidbody Interpolate on, a warp shown in the open can streak for one render frame — keep the warp point out of sight (its intended use) or ask for a seamless variant.
- **Carry survives the bob (ride grace).** A bobbing platform's ground check flickers false for a tick on fast up-strokes; on a *moving* platform a skipped carry tick lets it slide out from under the player. `PlatformRiderModule` keeps carrying for `rideGraceTime` (~0.12s) after grounding is lost while still over the platform, and `PlayerController` treats "riding" as grounded for the horizontal reset (so the carry doesn't double-add). A real jump (MoveState.JumpFired from Jump/WallSlide) suppresses the carry until landing, so the jump still takes a clean platform-velocity snapshot. If the player still gets left behind, raise `Ride Grace Time` or lower the bob's `Follow Speed`/`Displacement Multiplier` (gentler up-strokes = fewer flickers).
- **Blender / Animator clips:** the bob owns the ROOT's world Y (and tilt when Align To Surface is on). To combine with an imported animation, put RockBobber on a PARENT and the animated model as a CHILD — the Animator then drives the child freely (sub-motion, crumble, spin) while the parent bobs. A clip that drives the same root world-Y/rotation will fight the bob; child-level animation composes cleanly. Disable Animator "Apply Root Motion" if it moves the root.

### LavaFlowRandomizer
**File:** `Assets/Scripts/Gameplay/Environment/LavaFlowRandomizer.cs`
**Purpose:** Several lava falls sharing ONE "RAA Lava Flow" material (V.1–V.4) all scroll in lockstep and look identical. At Start this randomises each lava-flow renderer's texture OFFSET (its starting frame) and optionally jitters its scroll SPEED, via a **MaterialPropertyBlock** — so the rendered instance varies WITHOUT cloning or editing the shared material asset. Detects the flow shader by the `_BottomMove` property; randomises `_EmissionMap_ST`/`_TopEmissionMap_ST` (.zw offset, preserving the designer's tiling .xy) and scales `_BottomMove`/`_TopMove`.
**Setup:** add the component to a lava fall (or a parent of several, with **Include Children** on) — or run **Tools > Blast Frame > Implement Fix > 048** to add it to every lava-flow renderer in the open scene at once.
**Key fields:** `includeChildren`, `randomizeOffset`, `speedJitter` (±fraction, 0 = offset-only), `seed`.
**Gotchas:**
- Variety is **seeded by world position** → each fall differs from the others but looks the SAME every play. Change `Seed` to reshuffle them all.
- A MaterialPropertyBlock opts that renderer **out of SRP batching** — fine for a handful of static falls; don't blanket it onto hundreds of objects.
- Offset-only (`speedJitter = 0`) gives a constant phase difference (they look different but stay in sync rate-wise); raise `speedJitter` so they also drift apart over time.
- Detection is property-based (`_BottomMove`), so it also works on V.4 river/turbulence materials — but it only changes the EMISSION offset, not a RockBobber's noise sampling (that reads `_NoiseMap`/`_NoiseMove` from the shared material), so it won't desync a bob.

### EmissionPulse
**File:** `Assets/Scripts/Gameplay/Environment/EmissionPulse.cs`
**Purpose:** Pulses a renderer's emission colour back and forth between a low and high intensity (glow throb). Drives it through a **MaterialPropertyBlock** (per-object — never edits the shared material asset), so it composes with `LavaFlowRandomizer` on the same renderer and lets many objects sharing one material pulse independently. Works on any shader with an HDR emission colour property (URP Lit `_EmissionColor`, RAA Lava Flow `_EmissionColor`, etc.).
**Setup:** Add Component → Emission Pulse on any object with a Renderer. Set the base `Emission Color` (or right-click the component header → **Copy Emission From Material** to grab the material's current value). Tune Min/Max Intensity + Pulse Speed.
**Key fields:** `emissionProperty` (default `_EmissionColor`), `emissionColor` (HDR base), `minIntensity`/`maxIntensity` (bounce endpoints, multipliers of the base), `pulseSpeed` (rad/sec; ~6.3 ≈ 1 bounce/sec), `smooth` (sine vs hard ping-pong), `includeChildren`, `phaseOffset`/`randomizePhase` (stagger several pulsers).
**Gotchas:**
- Caches its renderer/sub-material targets in Awake (so `sharedMaterials` isn't allocated every frame); `Update` only does `GetPropertyBlock`/`SetColor`/`SetPropertyBlock` (no per-frame alloc). Reads the existing property block first, so it won't wipe another MPB user's props.
- For **URP Lit**, emission must be enabled on the material (the `_EMISSION` keyword) or the `_EmissionColor` won't show — MPB can't toggle keywords. The custom RAA Lava Flow shaders are always emissive, so they're fine.
- If it can't find a material with the emission property it logs a warning and disables itself.

### Lava Falls Tube (ejection glass) — gotchas
- **Glass not rendering / only the opaque cutout shows = back-face culling.** The ejection has two materials (opaque alpha-cutout + transparent glass). The glass is a single-sided surface facing away, so URP back-face culls it. Fix: glass material **Render Face = Both** (`_Cull` 0) — **Implement Fix 049**. If it then looks solid, lower its Base Color/Base Map alpha.
- **"PC_Renderer is missing RendererFeatures" console error:** `PC_Renderer.asset` references a custom `LavaDepthRendererFeature` (`BlastFrame.Rendering.LavaDepthRendererFeature`, lava layer) whose **script file is missing** (lost). It does NOT hide the glass (the glass is stock URP Lit, rendered in the normal transparent pass). Fix by restoring the script or removing the dead feature from PC_Renderer / Mobile renderer (manual — the "−" button; editing the URP feature list via script risks corrupting it).

---

## Collision Layers — weapon vs owner

### Layers & matrix
**Layers:** `Player`, `Enemy`, `PlayerWeapon`, `EnemyWeapon` (created by Implement Fix 045; slots assigned to first free user slots, typically 6/7/9/10).
**Collision matrix (Project Settings > Physics):**
- `PlayerWeapon × Player` = OFF — player projectiles pass through the player.
- `EnemyWeapon × Enemy` = OFF — enemy projectiles pass through enemies (no friendly-fire / self-detonation).
**Why it works without code:** projectiles are trigger colliders; Unity's layer collision matrix governs trigger events (`OnTriggerEnter`) too, so disabling a pair stops the trigger from firing.

### Who sits on which layer
- **Player** layer → the Player root (capsule collider). Set surgically: only the root + any collider GameObjects move — the camera/viewmodel children have no colliders, so they keep the `Viewmodel` layer and the gun overlay stays intact.
- **Enemy** layer → every GameObject with an `EnemyCore` (turrets, robots, bosses) + its child colliders.
- **PlayerWeapon** layer → `PlayerProjectile.prefab`, `Explosion.prefab`.
- **EnemyWeapon** layer → `EnemyMissile.prefab`, `ArcProjectile.prefab`, `ArcExplosion.prefab`.

### AoE blast self-damage (separate from the matrix)
`AoeExplosion` deals damage via `Physics.OverlapSphereNonAlloc` filtered by its `damageLayers` LayerMask — NOT collision — so the matrix cannot stop a blast catching its own owner. Fix 045 also narrows the masks:
- `Explosion.prefab` (player charged-shot blast): `damageLayers` excludes **Player**.
- `ArcExplosion.prefab` (enemy arc blast): `damageLayers` excludes **Enemy**.
**Gotcha:** when adding a NEW enemy or weapon prefab, put it on the correct layer (and exclude the owner from any new AoE blast's `damageLayers`), or it'll self-collide / self-splash. New scene enemies need the `Enemy` layer — re-run Fix 045 with the scene open, or set it by hand.

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

---

## HQ / Economy / Shop

### CurrencyManager
**Namespace:** `BlastFrame.Gameplay.Economy`
**File:** `Assets/Scripts/Gameplay/Economy/CurrencyManager.cs`
**Registers as:** `ICurrencyManager` (ServiceLocator, Awake)
**Required components (own GO):** none
**Optional SOs:** `onCurrencyChangedEvent` (IntGameEventSO) — raised after every balance change; C# event `OnCurrencyChanged` fires regardless
**Setup:** Run Fix 018 (`Tools > Blast Frame > Implement Fix > 018 - Add Currency Manager To Core`).
**Behaviour:** Loads balance from `ISaveManager.Data.metaCurrency` in Start via TryGet (falls back to 0 if no save system). `Add` / `TrySpend` mutate the internal `CurrencyWallet` struct and notify all listeners.
**Gotchas:** ISaveManager is optional (TryGet). Consumers call `ServiceLocator.Get<ICurrencyManager>()` in Start, never Awake.

---

### CurrencyWallet
**Namespace:** `BlastFrame.Gameplay.Economy`
**File:** `Assets/Scripts/Gameplay/Economy/CurrencyWallet.cs`
**Type:** plain `struct` (not MonoBehaviour, not SO). Internal to CurrencyManager. Wraps a non-negative int; exposes `Add`, `TrySpend`, `SetBalance`. No outside system accesses this directly.

---

### PermanentUpgradeSO
**Namespace:** `BlastFrame.Gameplay.HQ`
**File:** `Assets/Scripts/Gameplay/HQ/PermanentUpgradeSO.cs`
**Asset path:** `Assets/ScriptableObjects/Permanents/`
**Create via:** right-click > `Blast Frame/HQ/Permanent Upgrade`; Fix 019 creates ExtraHealth + FasterDash samples.
**Key fields:** `id` (string, unique), `displayName` (string), `description` (TextArea), `cost` (IntReference), `magnitude` (FloatReference), `effect` (PermanentUpgradeEffect enum: None, MaxHealthBonus, DashCooldownReduce, MoveSpeedBonus, DamageBonus, StartingCurrency)
**Gotchas:** Fix 019 is create-only — never overwrites an existing asset. Permanent upgrades persist across deaths (saved in SaveData.purchasedPermanentIds). Do NOT store in-run state on these SOs.

---

### PermanentUpgradeRegistrySO
**Namespace:** `BlastFrame.Gameplay.HQ`
**File:** `Assets/Scripts/Gameplay/HQ/PermanentUpgradeRegistrySO.cs`
**Asset path:** `Assets/ScriptableObjects/Permanents/PermanentRegistry.asset` (created by Fix 019)
**API:** `Upgrades` (IReadOnlyList), `GetById(string id)`
**Setup:** Fix 019 creates asset and populates it with ExtraHealth + FasterDash only if the list is currently empty.
**Gotchas:** Fix 019 never repopulates an already-populated list. ShopManager and ShopUI both reference this; missing assignment silently no-ops with a LogWarning.

---

### ShopManager
**Namespace:** `BlastFrame.Gameplay.HQ`
**File:** `Assets/Scripts/Gameplay/HQ/ShopManager.cs`
**Registers as:** `IShopManager` (ServiceLocator, Awake)
**Required SOs:** `registry` (PermanentUpgradeRegistrySO) — wired by Fix 019 if null
**Optional SOs:** `onPurchasedEvent` (GameEventSO) — raised after any successful purchase
**Setup:** Fix 019 creates "ShopManager" GameObject in Core and wires the registry if the field is null.
**Behaviour:** `IsOwned` checks `ISaveManager.Data.purchasedPermanentIds` (TryGet) or in-memory HashSet fallback. `TryPurchase` spends currency, marks owned, calls `ISaveManager.Save()` if present, raises event.
**Gotchas:** Without ISaveManager, ownership is in-memory only (lost on quit). Wire Fix 021 (SaveManager) for persistence. Fix 019 does not overwrite a registry field that is already set.

---

### ShopUI
**Namespace:** `BlastFrame.UI`
**File:** `Assets/Scripts/UI/ShopUI.cs`
**Required services:** `ICurrencyManager`, `IShopManager` (fetched via ServiceLocator.Get in Start)
**Required SOs:** `registry` (PermanentUpgradeRegistrySO — same asset as ShopManager)
**Required Inspector fields:** `rowContainer` (RectTransform with VerticalLayoutGroup), `currencyLabel` (TextMeshProUGUI)
**Optional SOs:** `onPurchasedEvent` (GameEventSO — same as ShopManager's; subscribe to refresh rows on code-driven purchases)
**Setup:** Add Canvas to the HQ/test scene with a VerticalLayoutGroup panel; add ShopUI; assign registry, rowContainer, currencyLabel. No automated fix for the full canvas yet — build manually.
**Behaviour:** Builds rows at Start from registry. Each row has name, description, cost, owned-status, and Buy button. All refreshes are event-driven (OnCurrencyChanged / onPurchasedEvent) — no Update polling. Cannot-afford rows dim cost to grey and disable Buy. OWNED rows hide Buy and show "OWNED" in green.
**Gotchas:** rowContainer and registry must be assigned or BuildRows silently no-ops. The onPurchasedEvent is optional but recommended so TestStat purchases reflect immediately in the UI. ContentSizeFitter on rowContainer is needed for auto-height scrolling.

---

## Enemies — Arc-Predict Heavy Mortar Turret

### EnemyBehaviorArcPredict (heavy lob configuration)
**Namespace:** `BlastFrame.Gameplay.Enemies`
**File:** `Assets/Scripts/Gameplay/Enemies/EnemyBehaviorArcPredict.cs`
**Type:** behavior component, stacked on a turret with `EnemyCore` + `EnemyStats`
**Required SOs:** `entityRegistry` (EntityRegistrySO — wired by Fix 033/034)
**Required Inspector fields:** `muzzle` (Transform at barrel tip), `barrelPivot` (Transform that pitches on local X)
**Key behavior:**
- Body yaws toward the predicted target at `rotationSpeed`; barrel pitches to the HIGH ballistic arc (always ≥ 45°)
- Projectile fires at constant `EnemyStats.projectileSpeed` along the muzzle's CURRENT facing — the solver steers the barrel, never bypasses it
- Fires only with line of sight to ANY part of the player (5-point check on collider bounds: center/head/feet/edges)
- `projectileGravityScale` scales gravity for both solver and projectile (must match or shots miss); `maxLeadTime` caps movement prediction
**Setup steps:**
1. Build/name the turret with a child whose name contains "barrel"/"gun"/"cannon"
2. Run `Tools > Blast Frame > Implement Fix > 034` — adds components, wires registry/muzzle/barrelPivot (creates Muzzle empty at barrel tip if missing)
3. Run Fix 035 (heavy lob tuning) and Fix 036 (gravity 0.2x) for the mortar feel
**Gotchas:** Max ballistic reach = v²/(g·scale); at speed 6 + scale 0.2 that's ≈ 18 units — targets beyond get a best-effort 45° lob that lands short. Renaming the barrel child breaks Fix 034's heuristic.

### ArcExplosion
**Prefab:** `Assets/Prefabs/Projectiles/ArcExplosion.prefab` (created by Fix 035)
**Pool id:** `PoolIds.ArcExplosion` ("ArcExplosion"), prewarm 4 / expand 2
**Components:** `AoeExplosion` (radius 1.5, damage 1 — tuned on the prefab, NOT on the turret's EnemyStats)
**Behavior:** spawned by `ArcProjectile` on any impact; projectile deals NO direct contact damage — all damage is blast radius
**Gotchas:** `AoeExplosion` damages every `IDamageable` in radius — including other enemies (and the turret itself at point-blank). `EnemyStats.damage` on the turret is currently unused by this projectile path.

### RAA URP Lit Projection (shader)
**Shader name:** `RAA Materials/RAA URP Lit Projection`
**Files (all in `Assets/RAA Materials/Shaders/`):** `RAA_URP_Lit_Projection.shader`, `RAALitInput.hlsl`, `RAALitForwardPass.hlsl`, `RAALitDepthNormalsPass.hlsl` + Inspector `Assets/Editor/RAALitProjectionShaderGUI.cs`
**What it is:** A LITERAL FORK of URP 17.3.0's `Universal Render Pipeline/Lit`. The `.shader` is a verbatim copy of URP's `Lit.shader` (ALL passes: ForwardLit, ShadowCaster, GBuffer, DepthOnly, DepthNormals, Meta, Universal2D, MotionVectors, XRMotionVectors — full Lit feature set: specular/metallic workflow, parallax, detail maps, clear-coat stubs, full GI/lightmaps/APV). `RAALitInput.hlsl` = verbatim `LitInput.hlsl` + `_ProjectionOffset` in the CBUFFER + triplanar helpers. `RAALitForwardPass.hlsl` = verbatim `LitForwardPass.hlsl` + triplanar injection (3 edits marked `RAA:`). All non-forward passes include the STOCK URP pass files; only `RAALitInput.hlsl` is swapped in everywhere (for a consistent CBUFFER).
**Projection option:** `Projection` toggle (keyword `_RAA_PROJECTION_ON`, **ON by default**). Injected into the ForwardLit + DepthNormals passes (renderers here are Forward + Forward+, so the GBuffer/deferred path never runs). ON = triplanar sampling of every map. OFF = stock URP UV path (`InitializeStandardLitSurfaceData`).
**Projection space:** `Project In Object Space` toggle (keyword `_RAA_OBJECT_SPACE`, **OFF by default**). OFF = world space (texture fixed in the world; object slides through it when moved/rotated). ON = object's rotation frame about its origin, **with scale ignored** — texture follows the object's rotation + position but keeps constant world-unit density no matter how the object is scaled. Offset and Units Scale stay in world units in both modes. Implemented via `RAA_ObjectBasis` (normalized columns of ObjectToWorld = pure rotation axes + origin); `RAA_GetProjection` projects `positionWS - origin` onto those axes (dot products = scale-free world-unit distances), and `RAA_ProjectedNormalWS` rotates the whiteout normal back to world with the same basis. Assumes no shear (uniform/axis-aligned scale).
**World Units Scale:** `World Units Scale (units per tile XYZ)` (`_ProjectionWorldScale`, Vector, default `(4,4,4)`) — world units one full tile spans on each axis. Smaller = denser on that axis (e.g. `(4,4,1)` repeats every 1 unit on Z). World pos is divided per-axis by this (guarded against 0).
**Offset option:** `Projection Offset (World XYZ)` (`_ProjectionOffset`, Vector) added to world position before projecting.
**Inspector:** `RAALitProjectionShaderGUI` (in `Assets/Editor/`). URP's `LitShader` GUI is `internal` so it can't be subclassed — the GUI instantiates it by reflection through the public `ShaderGUI` base, delegates the whole inspector to it, then appends a "RAA Triplanar Projection" section. Falls back to the default inspector if the URP editor type ever moves.
**Create a material:** `Tools > Blast Frame > Implement Fix > 038` → `Assets/RAA Materials/M_RAA_LitProjection.mat` (triplanar ON). Bails if it already exists.
**Gotchas:**
- **FORK — locked to URP 17.3.0.** It will NOT track URP upgrades. After a URP update, re-copy `Lit.shader` / `LitInput.hlsl` / `LitForwardPass.hlsl` from the package and re-apply the `RAA:`-marked edits.
- Triplanar is injected into ForwardLit (shading) and DepthNormals (camera-normals buffer → SSAO/screen-space, so the normal map matches the forward look on UV'd meshes). ShadowCaster/DepthOnly alpha-clip and the Meta (lightmap-bake) pass still use the stock UV path — fine for opaque blockout; cutout shadows and baked-GI on triplanar would need the same injection ported there.
- "Per object" offset is a *material* property — share-material objects share it. Use material instances or a `MaterialPropertyBlock` (`_ProjectionOffset`) for truly per-object offsets.
- Projection scale is per-axis via `_ProjectionWorldScale` (units per tile, default 4). Values are clamped to ≥1e-4 in `RAA_ProjectedPos` to avoid divide-by-zero on an empty axis.
- Triplanar normal mapping uses the whiteout blend (approximation on steeply angled faces). Detail maps + parallax are skipped in projected mode (UV-space features).

### RAA Lava Flow (shader) — WIP
**Shader name:** `RAA Materials/RAA Lava Flow`
**File:** `Assets/RAA Materials/Shaders/RAA_LavaFlow.shader` (standalone — NOT related to the triplanar projection fork).
**What it is:** A from-scratch URP **Unlit** shader being built up piece by piece for lava. Current state: scrolling emission — samples `_EmissionMap` (with tiling/offset) × `[HDR] _EmissionColor` and outputs it unlit (+ fog). Default inspector (no custom GUI).
**Properties:** `_EmissionMap` (2D, "white"), `[HDR] _EmissionColor` (default white), `_Move` (Vector, default `(0,0)`, "Move" header) — UV scroll speed units/sec (x,y), applied in the vertex shader as `uv += _Move.xy * _Time.y`; positive moves UV in +, texture appears to slide the opposite way. (Stretch/scale is handled by the emission map's own tiling fields.)
**Planned (not yet built):** distortion/flow map, secondary maps, etc. — added incrementally.

**V.3 variant — `RAA Materials/RAA Lava Flow V.3`** (`Assets/RAA Materials/Shaders/RAA_LavaFlow_V3.shader`): V.1 and V.2 stacked into one **opaque** unlit shader. TOP layer = V.2 (`_TopEmissionMap` × `_TopEmissionColor` × `_TopTint`, own `_TopMove` scroll, `_TopAlphaMap`) composited over BOTTOM layer = V.1 (`_EmissionMap` × `_EmissionColor`, own `_BottomMove` scroll) via `color = lerp(bottom, top, topAlpha)` where `topAlpha = _TopAlphaMap.r * _TopTint.a`. Each layer has independent texture/tiling, emission color (intensity), and flow speed. Inspector shows the Top-layer block first, then the Bottom-layer block (Headers "Top Layer V2" / "Bottom Layer V1"). Output is opaque (bottom is the base); the alpha map only controls where the top reveals the bottom. Both layers share mesh UV0; top alpha shares the top's scrolled UV.

**V.4 variant — `RAA Materials/RAA Lava Flow V.4`** (`Assets/RAA Materials/Shaders/RAA_LavaFlow_V4.shader`): exactly V.3 (opaque) plus **vertex turbulence**. A scrolling noise texture displaces the mesh vertices up/down along local up to churn the surface. `_NoiseMap` (own tiling/offset via `_NoiseMap_ST`), `_NoiseMove` (Vector, UV scroll units/sec like Top/Bottom Move), `_TurbulenceStrength` (Float, default 0 = flat). In the vertex shader: `positionOS.y += noise * _TurbulenceStrength` (noise kept in `[0,1]` so the rest plane / object origin is the displacement MINIMUM — pushes up only, never below the base), sampled with **`SAMPLE_TEXTURE2D_LOD(..., 0)`** (mandatory in the vertex stage — no mip derivatives there). Needs a **subdivided** plane. Unlit, so no normal recompute after displacement (turbulence is silhouette-only, won't relight). Emission/layering identical to V.3.

**River variant — `RAA Materials/RAA Lava River`** (`Assets/RAA Materials/Shaders/RAA_LavaRiver.shader`): standalone unlit **opaque** lava for a flowing river. Uses the **same two-layer emission/layering as V.3 with identical property reference names** (`_TopEmissionMap`/`_TopEmissionColor`/`_TopTint`/`_TopAlphaMap`, `_EmissionMap`/`_EmissionColor`) so a V.3 material's properties paste straight in ("Paste Material Properties") and the textures/colors/emissions match exactly. `color = lerp(bottom, top, topAlpha)`, opaque output. River-specific additions (absent from V.3, so a paste leaves them at the river's values):
- **Flow (fragment):** `_FlowMap` ([NoScaleOffset], default "grey" = zero flow) drives the scroll of BOTH layers. RG → 2D direction; two phase-offset samples (`frac(t)`, `frac(t+0.5)`) cross-faded by a triangle wave (`abs(1 - 2*phaseA)`) so the scroll never snaps. `_FlowStrength` = UV distortion, `_FlowSpeed` = cycles/sec. Emission + alpha of each layer are flowed with the same field so they stay aligned. Paint a flow map to make it flow.
- **Ripple (vertex):** continuous concentric waves around `_ImpactPoint` (world XYZ — where the lavafall lands). Vertices displace along **world up** by `sin(dist*_RippleFrequency - _Time.y*_RippleSpeed) * _RippleAmplitude * exp(-dist*_RippleDecay)`. Assumes a flat, horizontal, **subdivided** mesh. `_ImpactPoint` set by hand for now; a transform-driven component is the planned next step.
- **Difference from V.3:** `_TopAlphaMap` defaults to **"black"** (top hidden) instead of V.3's "white", so an existing single-layer river isn't covered by an all-white top before the top layer is assigned/pasted. Parity is exact once a V.3 material (whose top alpha is assigned) is pasted in.
- Unlit, so displacing verts needs no normal recompute; ripple is silhouette-only.

**Lava Ripple — `RAA Materials/Lava Ripple`** (`Assets/RAA Materials/Shaders/RAA_LavaRipple.shader`; formerly "RAA Lava River V.2"): a duplicate of **Lava Flow V.3** (two-layer emission, per-layer `_TopMove`/`_BottomMove` scroll, same property names + defaults incl. `_TopAlphaMap` "white") **plus a centered ripple**. No `_FlowMap` (flow = V.3 Move scroll) and no `_ImpactPoint` — the ripple is **object-space**, centered at the local origin: `dist = length(positionOS.xz)`, displaced along local up by `sin(dist*_RippleFrequency - _Time.y*_RippleSpeed + phaseOffset) * amplitude * exp(-dist*_RippleDecay) * angularMod`. Assumes a flat plane lying in its local XZ (Unity Plane convention). **Angular variation** (so the ring height isn't uniform around the circumference): `angle = atan2(z,x)`, `angularMod = 1 + sin(angle*_RippleAngularFrequency + _Time.y*_RippleAngularSpeed + phaseOffset) * angularStrength` where `angularStrength = randAngular * _RippleAngularStrength` (a third per-object hash, so each ripple is lobed differently). `_RippleAngularStrength` is the **max** (default 0 = circular ring), `_RippleAngularFrequency` (use whole numbers to avoid a seam at the ±π wrap), `_RippleAngularSpeed` (rotates the pattern over time). **Per-object variation** (so separate placed planes don't ripple in lockstep): two randoms are hashed from the object's world origin (`GetObjectToWorldMatrix()._m03_m13_m23`) — `_RipplePhaseRandom` (Range 0..1, default 1) scales a random phase offset up to 2π; `_RippleAmplitudeRandom` (Float, default 0) adds a random `±` offset so amplitude ∈ `[_RippleAmplitude - x, _RippleAmplitude + x]`. Both constant per object/instance. Place a subdivided plane wherever you want a rippling lava pool. Pastes V.3 properties exactly. **Opaque** (no contact/intersection blending — the depth-based soft-blend system was tried and removed, see GameDesign).

### RAA Heat Distortion (shader) + Heat Shimmer particle
**Shader:** `RAA Materials/RAA Heat Distortion` (`Assets/RAA Materials/Shaders/RAA_HeatDistortion.shader`) — refractive heat-haze for billboard PARTICLES. Unlit, transparent (`Blend SrcAlpha OneMinusSrcAlpha`, ZWrite Off, Cull Off). Samples `_CameraOpaqueTexture` (`SampleSceneColor`, **needs Opaque Texture on the URP asset — it is**) at a screen-UV offset from a scrolling distortion texture (`_DistortionMap` RG, grey = none), so the scene behind wobbles. Offset + blend scale by the particle's **vertex-color alpha**, so Color-over-Lifetime eases the shimmer in/out. Props: `_DistortionMap`, `_DistortionStrength` (default 0.02), `_DistortionScroll` (XY/sec), `_Tiling`, `_Softness`. **`_Softness`** (Range 0..1) is a **procedural** soft circle (no texture): `r = length(rawUV - 0.5)*2`, `mask = 1 - smoothstep(1 - _Softness, 1, r)` — rounds off the square particle edges, always a circle regardless of `_Tiling`. 0 = hard circle, 1 = fades from center. The mask × the particle's lifetime alpha drives both the distortion strength and the blend, so edges fade smoothly.
**Particle prefab:** built by `Tools > Blast Frame > Implement Fix > 044`. Creates `Assets/RAA Materials/M_HeatShimmer.mat` (if missing) + `Assets/Prefabs/VFX/HeatShimmer.prefab` — a world-space, looping, Box-emitter system that rises (Velocity-over-Lifetime Y) with sideways waver, grows (Size-over-Lifetime), and fades in/out (Color-over-Lifetime alpha → drives the shimmer). Billboard, View-aligned, uses the distortion material. Drag onto a lava surface, scale the emitter Box to fit, tune Distortion Strength. Fix bails if the prefab exists (won't clobber tuned settings).

**V.2 variant — `RAA Materials/RAA Lava Flow V.2`** (`Assets/RAA Materials/Shaders/RAA_LavaFlow_V2.shader`): same scrolling unlit emission, but **transparent** (alpha-blended: `Blend SrcAlpha OneMinusSrcAlpha`, `ZWrite Off`, Queue Transparent) and adds two properties. `_Tint` (Color, default white) multiplies the emission so a colored result shows even with no emission map (which defaults to "white"); final emission = `_EmissionMap * _EmissionColor * _Tint`. `_AlphaMap` ([NoScaleOffset], default "white") drives transparency — alpha = `_AlphaMap.r * _Tint.a`, sampled with the **same scrolled UV** as the emission (so the alpha flows with the lava; it has no independent tiling).

### Player Camera child (Core) — rebuild
**Rebuild fix:** `Tools > Blast Frame > Implement Fix > 037` reconstructs the deleted `Player/Camera` child in Core exactly as Fix 004 + Fix 012 built it (Camera + AudioListener + FirstPersonCamera[body→Player] + CameraShake + ChargeShot + PlayerShooter).
**Gotcha:** The camera transform IS the weapon muzzle, so deleting the camera also deletes the shooter rig — Fix 037 restores both. Fix 004 cannot rebuild it (aborts when a Player already exists).

## Damage Pipeline — DamageInfo + player i-frames (2026-07-07)

### DamageInfo / DamageKind
**Namespace:** `BlastFrame.Gameplay` — **File:** `Assets/Scripts/Gameplay/DamageInfo.cs`
Readonly struct: `Amount`, `SourcePosition` (world point the damage came FROM), `Kind`
(Projectile/Explosion/Contact/Hazard/Submersion), `HasSource`. Two ctors: with source position, and
sourceless (`HasSource=false` — directional UI skips these).
**IDamageable** gained `void TakeDamage(in DamageInfo info)` with a DEFAULT interface implementation
forwarding to `TakeDamage(int)` — enemies need no changes; PlayerHealth overrides it.
**PlayerHealth additions:** `invulnerabilitySeconds` FloatReference (0.5), `IsInvulnerable`,
`event Action<DamageInfo> OnDamagedInfo`. The info overload is the canonical path (i-frame early-out,
sets the window, raises OnDamagedInfo after OnHealthChanged, death events last).
**Setup:** nothing — pure code. **Gotcha:** DoT sources must tick SLOWER than the 0.5 s i-frames
(house default 0.75 s) or ticks get eaten; that spacing is the design, don't bypass i-frames.

## Environment — Lava damage (hazard volumes + submersion) (2026-07-07)

### LavaV4Sampler (shared CPU mirror of the V.4 vertex churn)
**File:** `Assets/Scripts/Gameplay/Environment/LavaV4Sampler.cs` (static).
`TryCache(material, noiseIsSRGB, out V4Params)` + `SampleDisp(in params, meshUV, t)` — the exact
math RockBobber used privately; RockBobber now consumes this. Any future system that needs the
visual lava height MUST go through it (single source of truth with the shader).

### LavaHazardVolume (waterfall / splash touch damage)
**Required:** any Collider (forced isTrigger in Awake) + `LavaHazardVolume`. Default layer, NOT Lava.
**Behavior:** immediate 1-dmg tick on player entry, then per `tickInterval` (0.75) while inside;
damage kind Hazard with source = ClosestPoint (feeds directional indicators). Pass-through by design
(motor ignores triggers) — dash through a lava waterfall and take one tick.
**Setup:** `Tools > Blast Frame > Environment > Create Lava Hazard Volume`, size the box over the fall.

### LavaSubmersionDetector (head under the displaced river surface)
**Required:** on the Player root (with PlayerHealth). Added by **Implement Fix 053**, which ALSO
removes the Lava layer from PlayerMotor.collisionMask (player now falls INTO the river).
**Behavior:** FixedUpdate ray down on the Lava layer from head+probeHeight; displaced surface =
`hit.point.y + LavaV4Sampler.SampleDisp(hit.textureCoord) * lossyScale.y`; head below → submerged:
`OnSubmergedChanged`, DoT (Submersion kind, sourceless) per 0.75 s, exit hysteresis 0.05, miss-grace 0.3.
**Gotchas:** river collider must be ON the Lava layer and its mesh Read/Write (Fix 046) or
textureCoord is (0,0); non-V4 lava materials degrade gracefully to the flat collider height.

### DamageOverlayUI (orange screen)
**Required:** HUD canvas child with a full-screen Image; added by **Implement Fix 054**.
**Behavior:** translucent orange flash on ANY damage (0.4 s fade); near-opaque orange wash while
submerged (fade in 0.15 / out 0.3); submersion floor wins (max of alphas); Image disabled at alpha 0;
raycastTarget forced off. Binds via EntityRegistry (HealthDisplay pattern); submersion detector optional.

### Projectiles vs trigger volumes (companion fix)
`EnemyMissile`, `ArcProjectile`, `PlayerProjectile` now IGNORE trigger colliders that have no
IDamageable ancestor — projectiles no longer detonate on invisible gameplay volumes (lava hazards,
future secret/room triggers).

## Enemies — Flying Enemy "Hoverbot" (2026-07-07)

### EnemyBehaviorFlyer
**Namespace:** `BlastFrame.Gameplay.Enemies` — **File:** `Assets/Scripts/Gameplay/Enemies/EnemyBehaviorFlyer.cs`
**Required components (root):** kinematic interpolated Rigidbody, SphereCollider (r 0.5), EnemyStats,
EnemyCore, EnemyBehaviorFlyer, `Muzzle` child assigned. Layer **Enemy**.
**Required SOs:** EntityRegistry on EnemyCore AND the behavior (separate fields).
**Stats used:** MoveSpeed (fly, ~7), FireRate (bursts/s, ~0.5), Damage (1), ProjectileSpeed, Range (30).
**Behavior:** orbit ring at `preferredRange` (10) around the player at eye+`hoverHeight` (2.5) with
random strafe flips + hover bob; 3-missile bursts (pooled `EnemyMissile`, `leadFactor` 0.5 linear lead)
gated on range band + `EnemyLineOfSight`; dodges laterally when the player CAMERA aims at it
(`aimDotThreshold` 0.985 held `aimReactionTime` 0.18 s, cooldown 1.2 s) + 35% micro-dodge after bursts;
repositions (height/angle re-roll) every 4 s. Spherecast obstacle slide; no pathfinding.
**Setup:** `Tools > Blast Frame > Enemies > Create Flying Enemy Prefab` →
`Assets/Prefabs/Enemies/FlyingEnemy.prefab`.
**Gotchas:** PoolConfig needs an `EnemyMissile` entry sized for bursts (prewarm ≥ 12); the
Enemy×EnemyWeapon layer-matrix pair must stay disabled so its own missiles never clip it; dodge
needs the player camera (found via registry player's children — never Camera.main).

### EnemyLineOfSight (shared helper)
**File:** `Assets/Scripts/Gameplay/Enemies/EnemyLineOfSight.cs` (static, non-allocating).
Multi-point LOS vs a target's collider bounds (center/top/bottom/sides), self+target skipped as
blockers — the generalized port of the arc turret's private check (turret intentionally NOT
refactored onto it yet; see Handoffs/PLAN-001 F7).

## HUD — Directional Damage Indicators (2026-07-07)

### DamageIndicatorUI
**Namespace:** `BlastFrame.UI` — **File:** `Assets/Scripts/UI/DamageIndicatorUI.cs`
**Required:** child `DamageIndicators` (centered, zero-size) under the HUD canvas + the
`DamageIndicator.prefab` UI asset (arrow/knob Image, authored pointing UP). Both created by
**Implement Fix 055**.
**Behavior:** on `PlayerHealth.OnDamagedInfo` with `HasSource`, claims one of 6 pooled slots (oldest
recycled) and shows an arrow on a 140 px ring at the source's bearing relative to the player
CAMERA forward (bearing 0 = top of ring); re-aims every frame while alive (stays world-accurate as
you turn); fades over the last 60% of a 1.6 s life. Sourceless damage (submersion) shows nothing.
**Setup:** run Fix 055 in the scene with the HUD canvas.
**Gotchas:** camera cached from the registry player's children (never Camera.main); if the arrow
sprite is replaced, author it pointing UP or the `-bearing` z-rotation reads wrong; all Images are
non-raycast.

---

## Pathfinding Enemies (2026-07-09) — NavMesh + Lobber / Skirmisher / Missiler / Biter / Bat

### NavMesh baking
**Tool:** `Tools > Blast Frame > Level > Bake NavMesh` (`Assets/Editor/NavMeshBakeTool.cs`).
Finds or creates a `NavMeshSurface` (collectObjects=All, useGeometry=PhysicsColliders) in the ACTIVE
scene, bakes it, and persists a `NavMeshData` asset at `Assets/NavMeshData/NavMesh_<scene>.asset` that
the surface references (survives editor restarts). Uses `com.unity.ai.navigation` 2.0.10 (already in
manifest). Ground enemies need this run once per level scene that hosts them; then Save the scene.
**Gotchas:**
- `collectObjects=All` bakes ALL loaded scenes' physics geometry. If another scene is loaded additively
  and overlaps the play area (e.g. Area1LavaBridge overlaps Sandbox), it pollutes the NavMesh AND
  `EnemyLineOfSight` raycasts (which test all layers). Unload the overlapping scene before baking/testing.
- The Sandbox test arena: default 10×10 Ground was scaled to 60×60 (localScale 6,1,6), 5 cover cubes
  added (`Cover` root, `M_SandboxRed`), the default `Main Camera` deleted (player camera from Core is the
  MainCamera), then baked. Sandbox NavMesh = `Assets/NavMeshData/NavMesh_Sandbox.asset`.

### EnemyNavMotor
**Namespace:** `BlastFrame.Gameplay.Enemies` — **File:** `Assets/Scripts/Gameplay/Enemies/EnemyNavMotor.cs`
Thin wrapper owning a `NavMeshAgent` (`[RequireComponent]`). API for behaviors: `SetDestination` (repath
throttled ~0.25s), `SetSpeedMultiplier` (dash/lunge), `Stop`/`Resume`, `HasArrived`, `ApplyBaseSpeed`
(pulls `EnemyStats.MoveSpeed`), `IsOnNavMesh`, `Velocity`. In Awake it forces any sibling Rigidbody
kinematic with **interpolation None** (interpolation smooths MovePosition, not the agent's direct
transform writes — leaving it on jitters), and disables agent rotation (behaviors face the player).
**Gotcha:** the enemy must SPAWN on the NavMesh (agent auto-snaps within its height). Place ground
enemies at y≈0 over baked NavMesh.

### The five enemy behaviors (all in `Assets/Scripts/Gameplay/Enemies/`, extend `EnemyBehaviorBase`)
Each prefab: root layer **Enemy**, kinematic Rigidbody, a non-trigger body collider, `EnemyStats`,
`EnemyCore` (registry + `OnEnemyKilled` wired), the behavior (registry wired), an `EntityDefinitionSO`
in `Assets/ScriptableObjects/Entities/`. Ground units also carry `EnemyNavMotor` (+ `NavMeshAgent`).
Built + wired by their wizards under `Tools > Blast Frame > Enemies > Create <X> Prefab`
(`Assets/Editor/PathfindingEnemyWizards.cs`). Distinct URP/Lit materials auto-created in
`Assets/Art/Materials/` (M_Lobber, M_Skirmisher, M_Missiler, M_Biter, M_Bat, …).

- **Lobber** (`EnemyBehaviorLobber`): NavMotor holds a band around `preferredRangeFactor`×Range
  (0.75) ±`rangeBandFactor`×Range; lobs a pooled `ArcProjectile` on a high-arc ballistic solve at the
  player (`projectileGravityScale` must match the ArcProjectile). Fires on LOS at `FireRate`. Muzzle
  child. Defaults: hp4, move3.5, fireRate0.6, projSpeed16, range22.
- **Skirmisher** (`EnemyBehaviorSkirmisher`): cover-user FSM (ToCover→InCover→ToPeek→Firing). Ring-samples
  NavMesh (`NavMesh.SamplePosition`) for the nearest point with LOS to player BLOCKED (cover) or WITH LOS
  (peek). Every `attackInterval` pops out, fires a burst of `burstCount` pooled `EnemyMissile`s, returns.
  Dashes (`SetSpeedMultiplier` ~3 for `dashDuration`) when the player is within `playerCloseDistance` (→
  farther cover). Muzzle child. Defaults: hp3, move4.5, projSpeed26, range24.
- **Missiler** (`EnemyBehaviorMissiler`): slow tanky vehicle. NavMotor creeps to `stopDistanceFactor`
  (0.7)×Range. Child **Turret** transform tracks player (turret + muzzle children). Two independent
  cooldowns: (a) fast explosive shot = pooled `ArcProjectile` at **gravityScale 0** (straight, explodes on
  impact) at `explosiveSpeedFactor`×ProjectileSpeed; (b) a `volleyCount` volley of pooled `HomingMissile`s
  fanned upward. Defaults: hp10, move1.5, projSpeed18, range28.
- **Biter** (`EnemyBehaviorBiter`): fast melee. NavMotor-chases with a shrinking sine weave; within
  `lungeRange` bursts speed; within `biteRange` deals `Stats.Damage` contact damage via the DamageInfo
  pipeline (`DamageKind.Contact`, source = its position, so the directional indicator points at it) on a
  per-target `biteCooldown`. Defaults: hp2, move6, range30.
- **Bat** (`EnemyBehaviorBat`): flying bomber, NO NavMesh (kinematic MovePosition steering + Perlin
  wander like the Flyer). Ammo loop: flies to `hoverHeight` above the player, on LOS + within
  `dropTolerance` drops its ONE bomb (pooled `ArcProjectile`, zero launch velocity = gravity drop), then
  returns to its home point (spawn pos, or child `Roost`) to reload (`reloadTime`) before re-arming.
  `bombVisual` child sphere toggles with ammo. dropPoint child. Defaults: hp3, move6.

### HomingMissile (pooled projectile)
**File:** `Assets/Scripts/Gameplay/Projectiles/HomingMissile.cs` — **Prefab:**
`Assets/Prefabs/Projectiles/HomingMissile.prefab` — **Pool id:** `PoolIds.HomingMissile`.
Layer **EnemyWeapon**, kinematic RB, trigger SphereCollider. Launches along a supplied dir (Missiler
fires upward), flies straight for `homingDelay`, then curves toward the player at a clamped
`turnDegPerSec` while accelerating to `cruiseSpeed`. **Detonate** (reach player / hit world / lifetime) →
pooled `ArcExplosion` (enemy blast, damages player). **Shootable:** implements `IDamageable` (1 HP); a
PlayerProjectile hit → `PopHarmless` → pooled `HomingPop`. Resets all state in `OnSpawn`.
Wizard: `Tools > Blast Frame > Enemies > Create Homing Missile (Projectile + Pool)` — builds the missile
+ HomingPop prefabs, EntityDefinitions, and PoolConfig entries (prewarm 8 each).

### HomingPop / VfxPop (harmless shot-down VFX)
**File:** `Assets/Scripts/Gameplay/Weapons/VfxPop.cs` — **Prefab:**
`Assets/Prefabs/Projectiles/HomingPop.prefab` — **Pool id:** `PoolIds.HomingPop`.
Pure-visual pooled pop: scales to zero over `lifetime` then despawns. **NO physics, NO damage.**
**Why not an AoeExplosion (CRITICAL gotcha):** `HomingMissile.PopHarmless` can fire RE-ENTRANTLY — an
`ArcExplosion` (mask `-129` = all-except-Enemy, which INCLUDES the EnemyWeapon layer) catches nearby
HomingMissiles in its OnSpawn damage loop → their `TakeDamage` → `PopHarmless`. If the pop were an
`AoeExplosion`, its `OnSpawn` runs `Physics.OverlapSphereNonAlloc` into `AoeExplosion`'s **shared static
`_overlapBuffer`**, corrupting the outer explosion's in-flight iteration so it then damages whatever the
nested overlap found (this was silently killing the Biter through the -129 mask). VfxPop touches no
physics, so it is safe to spawn from inside another explosion's loop. Do NOT swap it back to AoeExplosion.

### Dropped bomb (DESIGN NOTE — reused, not a new type)
The spec's "DroppedBomb" and the Missiler's "fast explosive shot" both **reuse `ArcProjectile`**
(`Initialize(damage, launchVelocity, gravityScale)`): the Bat's bomb = zero launch velocity + gravity;
the Missiler's straight shot = gravityScale 0. No `DroppedBomb.cs` was created (spec said "prefer reuse").

---

## Multiplayer — Local split-screen co-op

### Per-player input (PlayerInputHandler → provider, PlayerInputReader, PlayerIdentity, PlayerInputResolver)
**Namespace:** `BlastFrame.Input` — **Files:** `Assets/Scripts/Input/PlayerInputHandler.cs`, `PlayerInputReader.cs`, `PlayerIdentity.cs`, `PlayerInputResolver.cs`
**What changed:** `PlayerInputHandler` (still on the `PlayerInputHandler` GameObject in **Core**) is no longer the single `IPlayerInput`. It is now an `IPlayerInputProvider` that owns a list of `PlayerInputReader`s (one per local player) and polls them each frame. Reader 0 (P1) is registered as the global `IPlayerInput` for back-compat; the provider is registered as `IPlayerInputProvider`.
- `PlayerInputReader` (plain class, implements `IPlayerInput`): wraps its own `InputSystem_Actions`, optionally paired to a device set via `InputUser.PerformPairingWithDevice` + `actions.devices`. `null` devices = read ALL devices (original single-player behaviour — a solo gamepad still drives P1).
- `PlayerIdentity` (component on a player root): holds the player index (0 = P1, default). LocalCoopManager sets index 1 on P2.
- `PlayerInputResolver.Resolve(Component)`: the 7 input consumers (PlayerController, FirstPersonCamera, ChargeShot, DashModule, JumpModule, WallSlideModule, PlayerTestMode) now call this in **Start** instead of `ServiceLocator.Get<IPlayerInput>()`. It reads the `PlayerIdentity` in the parent → asks the provider for that index → falls back to index 0 / the global service. Single-player resolves to reader 0, unchanged.
**Gotcha:** consumers must resolve in Start, never Awake (provider registers in Awake). Adding a second player at runtime restricts P1's reader to keyboard+mouse so the joining gamepad no longer double-drives P1.

### LocalCoopManager
**Namespace:** `BlastFrame.Gameplay.Coop` — **Files:** `Assets/Scripts/Gameplay/Coop/LocalCoopManager.cs`, `CoopPlayerHealthLabel.cs`
**Type:** service MonoBehaviour on a `LocalCoopManager` GameObject in **Core** (wired by `Tools/Blast Frame/Multiplayer/Add Local Coop Manager To Core`). Registers `ILocalCoopManager`.
**Required refs:** `entityRegistry` = `EntityRegistry.asset` (wired by the wizard). `playerPrefab` OPTIONAL — leave empty to spawn P2 by cloning the live P1 (default, proven path).
**Behaviour:** a code-created `InputAction` (`<Gamepad>/start` + `/buttonSouth`) fires join while a player exists → `AddPlayer` pairs the joining gamepad to a new input slot, restricts P1 to keyboard+mouse, clones/spawns P2 at `spawnOffset` from P1, sets its `PlayerIdentity` to the new index, disables P2's AudioListener, de-tags P2's camera, and splits viewports (P1 = top `Rect(0,0.5,1,0.5)` depth 0; P2 = bottom `Rect(0,0,1,0.5)` depth 1 — only the URP **Base** camera; the ViewmodelCamera overlay inherits it). A **hold** `InputAction` (1s) on P2's Start leaves → destroys P2, restores P1 to full-screen + all devices, `RemovePlayer`.
**Co-op death (provisional v1):** subscribes both players' `PlayerHealth.OnDeath`; a dead player whose ally is alive respawns after `respawnDelay` (5s) at the ally's position (`PlayerMotor.Teleport` + `ResetHealth`); both dead → normal death flow untouched.
**Gotchas:** two URP Base cameras need DISTINCT depths to composite on the real display (equal depth still works but is non-deterministic). Only ONE AudioListener may be enabled — P2's is disabled. The MCP `unity_screenshot_game_view` tool renders only `Camera.main`, so a rect-limited main camera screenshots as "top half + black" — that is a capture artifact, NOT a render bug (verify split by rendering each base camera to a shared RenderTexture, or on a real display).

### Online co-op foundation (Netcode for GameObjects) — GUARDED, NOT ACTIVE
**Namespace:** `BlastFrame.Networking` — **Files:** `Assets/Scripts/Networking/ClientNetworkTransform.cs`, `NetworkPlayerController.cs`, `NetworkSessionLauncher.cs`, `Assets/Editor/NetworkWizard.cs`
**State:** ALL guarded by `#if BLASTFRAME_NGO` and compile to nothing by default. `com.unity.netcode.gameobjects` could NOT be installed in this environment — UPM fails with "unable to verify the first certificate" (TLS) against packages.unity.com, and the offline cache has no usable copy. Written against the **NGO 1.x** API; UNVERIFIED (never compiled against the package).
**To activate:** (1) install `com.unity.netcode.gameobjects` (a machine with working registry TLS; NGO 2.x preferred for Unity 6 — the one API note in `ClientNetworkTransform` covers the 1.x→2.x authority change), (2) add `BLASTFRAME_NGO` to Project Settings > Player > Scripting Define Symbols, (3) run `Tools/Blast Frame/Multiplayer/Add Network Manager To Core` (idle NetworkManager + UnityTransport + F10 connect panel), (4) make a networked Player prefab (NetworkObject + `ClientNetworkTransform` + `NetworkPlayerController`) and assign it as NetworkManager's PlayerPrefab + in the NetworkPrefabs list.
**Design:** owner-authoritative — the owner runs the full motor/camera/input and streams its transform via the owner-auth `ClientNetworkTransform`; `NetworkPlayerController.OnNetworkSpawn` strips input/camera/motor/modules on non-owners. Shooting stays on the firing client's sim (local damage); `ReportShot`→ServerRpc→ClientRpc spawns a cosmetic-only tracer on remotes. Connect panel: F10 (F9 is PlayerTestMode), Host / Join(IP) / Disconnect, default 127.0.0.1:7777. NetworkManager is idle by default — single-player never starts a session. Enemy sync, server-auth projectiles, relay/NAT = follow-ups; LAN/loopback only.

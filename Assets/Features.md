# Blast Frame — Feature & Test Guide

This is the layered build guide. Every feature is generated as runtime code + a
numbered **Implement Fix** you run from `Tools > Blast Frame > Implement Fix > NNN`.
Features are designed to layer in one at a time so you can test each in isolation
before stacking the next.

- The **Core** scene + **TestLevel** scene + the **Player** are the shared test bed.
  Everything after Fix 004 adds to that same Player / those same scenes.
- **Always play-test by opening `Assets/Scenes/Core.unity` and pressing Play.**
  The BootLoader additively loads `TestLevel` and the persistent Player lives in Core.
- Fixes are **idempotent** — re-running one is safe. They never overwrite hand-tuned
  ScriptableObject values (cost, magnitude, stats).

---

## Step 0 — One-time prerequisites (do these BEFORE running any fix)

The project will not compile (and no `Tools` menu will appear) until these are done:

- [ ] Select `Assets/InputSystem_Actions.inputactions`. In the Inspector enable
      **Generate C# Class**, click **Apply**. This creates the `InputSystem_Actions`
      wrapper that `PlayerInputHandler` compiles against.
- [ ] Let Unity resolve packages (the save system added **Newtonsoft.Json**,
      `com.unity.nuget.newtonsoft-json`, to `Packages/manifest.json`).
- [ ] When prompted (after building the HUD), run
      **Window > TextMeshPro > Import TMP Essential Resources**.

Once the project compiles, the full `Tools > Blast Frame > Implement Fix` menu appears.

---

## Recommended layering order

Run **001 → 008** first to get a playable, moving player, testing after each.
Then layer the gameplay systems (009+) in any order you like; each notes its own
prerequisites.

---

## 001 — Core Scene & Services
**Run:** Fix 001.
**Adds:** `Core.unity` with `GameManager`, `Bootstrap` (CoreBootstrap + BootLoader),
the `EntityRegistry` asset, and Core registered as build scene 0.
**Test:** Open `Core.unity`. Confirm the two GameObjects exist. Not playable yet.

## 002 — TestLevel Scene
**Run:** Fix 002.
**Adds:** `TestLevel.unity` — floor, a 2 m wall, a high ledge, a wall-jump gap, a
directional light, and an empty `Content` parent for placed test content. Added as
build scene 1.
**Test:** Open `TestLevel.unity` and confirm the geometry. The BootLoader loads it
additively when you play from Core.

## 003 — Player Input Handler
**Run:** Fix 003.
**Adds:** `PlayerInputHandler` (wraps the generated input class, registers `IPlayerInput`).
**Test:** Confirm a `PlayerInputHandler` GameObject is in Core. No visible change yet.

## 004 — Player (kinematic FPS body + camera)
**Run:** Fix 004.
**Adds:** the persistent **Player** (kinematic Rigidbody motor, CapsuleCollider,
PlayerStats, PlayerHealth, PlayerController) with a first-person **Camera** child.
**Test:** Play from Core. WASD to move, mouse to look. Gravity keeps you on the floor;
you collide with walls and the high ledge. Cursor is locked/hidden.

## 005 — Jump (variable height + coyote + buffer)
**Run:** Fix 005.
**Test:** Play from Core. Tap Space = short hop; hold Space = full jump that clears the
2 m wall. Step off a ledge and jump within a tenth of a second (coyote time).

## 006 — Dash (ground-only, 5 s cooldown, momentum carry)
**Run:** Fix 006.
**Test:** Play. Shift dashes in your move/look direction (ground only — no air dash).
Dash then jump mid-dash: you keep the dash speed into the arc. Dash again only after
the 5 s cooldown. (Controller dash = East button / B / Circle.)

## 007 — Wall Slide & Wall Jump
**Run:** Fix 007.
**Test:** Play. Jump into the wall-jump gap (the two tall slabs). While airborne and
pressing into a wall you slide slowly; press Space to launch up-and-away from the wall.

## 008 — Platform Rider
**Run:** Fix 008. *(Required before Platforms, Fix 009.)*
**Adds:** `PlatformRiderModule` on the Player so it can ride/inherit platform motion.
**Test:** No visible change until platforms exist (Fix 009).

## 009 — Platforms (Moving / Rotating / Treadmill / SpringBoard)
**Run:** Fix 009 (needs Fix 002 + Fix 008).
**Adds:** one of each platform type in TestLevel (blue moving ~Z3, orange rotating ~Z6,
green treadmill ~Z9, red springboard ~Z13).
**Test:** Play. Moving platform carries you and, on jump, throws you along its travel
direction (momentum snapshot). Rotating carries you and flings you tangentially on jump.
Treadmill pushes you while you stand on it. SpringBoard launches you up on contact (once
per landing). Wizards: `Tools > Blast Frame > Platforms/*`.

## 010 — Audio
**Run:** Fix 010.
**Adds:** `AudioManager` (pooled AudioSources, `IAudioManager`) in Core.
**Manual:** Create an AudioMixer (`Assets > Create > Audio Mixer`) with groups
`Master/Music/SFX`, expose params `MasterVolume`/`MusicVolume`/`SfxVolume`, and assign
the mixer + groups on the AudioManager. Create cues via
`Tools > Blast Frame > Audio > Create Audio Cue`.
**Test:** Bind a cue to an existing `GameEventSO` in the AudioManager inspector, raise
that event, and hear it route through the SFX group.

## 011 / 012 — Player Shooting (charge + AoE, pooled)
**Run:** Fix 011 (prefabs + pools + PoolManager), then Fix 012 (shooter on camera).
**Test:** Play. Tap LMB = small fast shot, no explosion. Hold LMB to charge, release for
a bigger shot that detonates an AoE on impact. Shoot a wall/box to see impacts.
(Hold to watch the HUD charge bar once Fix 013 is in.)

## 013 — HUD (health, dash ring, charge bar)
**Run:** Fix 013. *(Import TMP Essentials if prompted.)*
**Adds:** a Canvas in Core with a health number (top-left), dash cooldown ring
(bottom-left), and charge bar (bottom-center). Widgets bind to the Player via the
EntityRegistry.
**Test:** Play. Health shows 5. Dash and watch the ring refill over 5 s. Hold fire and
watch the charge bar fill, then vanish on release.

## 014 — Enemies (Missile Turret + Arc-Predict Turret)
**Run:** Fix 014 (needs Fix 002; Fix 011/012 to kill them).
**Adds:** a missile turret (`-8,1,8`) and an arc-predict turret (`8,1,8`) in TestLevel,
plus their pooled projectiles.
**Test:** Play. The missile turret tracks you and fires slow-then-accelerating missiles.
The arc turret leads your movement and lobs arced shots where you're heading (and aims at
your feet when you stand still). Shoot them to destroy them. Wizards:
`Tools > Blast Frame > Enemies/*`.

## 015 — Powerups (run-scoped)
**Run:** Fix 015 (needs Fix 001 + Fix 002).
**Adds:** a Heal pickup in TestLevel (`Heal.asset` created if missing).
**Test:** Play. Take damage (walk into a turret), then walk into the capsule pickup —
health restores and the pickup disappears. Powerups are lost on death (never saved).

## 016 — Room Variant Framework
**Run:** Fix 016 (needs Fix 002).
**Adds:** 3 self-contained variant prefabs + 3 RoomVariantSO + a `RoomSlot_Demo` in
TestLevel that picks one variant by seed.
**Test:** Play; one of the three variants spawns at the anchor (`0,0,20`). Change the
slot's seed in the Inspector and replay — a different (deterministic) variant appears.

## 017 — Level & Run Management
**Run:** Fix 017.
**Adds:** `RunManager` (`IRunManager`) in Core + a sample `Level01` definition.
**Test:** Play. From `TestStat.cs` call `ServiceLocator.Get<IRunManager>().StartRun(0,
Difficulty.Medium)` then `EndRun(true)`; confirm `RunActive` flips and the run events fire.

## 018 / 019 — HQ / Economy / Shop
**Run:** Fix 018 (currency), then Fix 019 (shop demo + sample permanent upgrades).
**Test:** Play. From `TestStat.cs`: `Get<ICurrencyManager>().Add(100)`, then
`Get<IShopManager>().TryPurchase("ExtraHealth")` — currency drops to 50 and
`IsOwned("ExtraHealth")` is true; a second purchase returns false.

## 020 — Bosses
**Run:** Fix 020 (needs Fix 002; Fix 011/012 to fight). 
**Adds:** a `TestBoss` (BossCore + phase behaviors) in TestLevel at `0,1.5,15`.
**Test:** Play and shoot the boss. Phase swaps log to the console as health crosses
thresholds (configure phases on the BossCore in the Inspector). On death it spawns its
drop and raises OnBossDefeated / OnLevelUnlocked. Wizards: `Tools > Blast Frame > Enemies/
Create Mini Boss | Create Boss`.

## 021 — Save / Load
**Run:** Fix 021.
**Adds:** `SaveManager` (`ISaveManager`, AES-encrypted JSON in `persistentDataPath`).
**Test:** Play. From `TestStat.cs` set `Get<ISaveManager>().Data.metaCurrency = 999`,
call `Save()`. Stop, Play again — `Load()` (in Awake) restores 999. Log
`Application.persistentDataPath` to find `blastframe.sav` (binary/encrypted).

---

## Notes
- Detailed component/setup/gotcha reference lives in `Assets/GameInfo.md`.
- Design decisions are logged in `Assets/GameDesign.md`.
- `TestStat.cs` (referenced above) is the throwaway debug script — rewrite its body per
  test; it is not committed.

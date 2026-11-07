# HANDOFF-005 — Bug/inefficiency review pass + verify last session's features

## Goal
The gameplay codebase (~4,300 lines under `Assets/Scripts/`) has been reviewed for correctness
bugs and inefficiencies and the real ones are fixed; the features landed by the previous overnight
session (lava damage pipeline, flying enemy, damage overlay/indicators) are verified live in the
Unity editor in play mode with zero console errors. The working tree compiles clean.

## Root cause / design
The last two sessions landed a lot of new code fast (see `Handoffs/INDEX.md` and the git status:
modified `IDamageable.cs`, `PlayerHealth.cs`, all three projectiles, `AoeExplosion.cs`,
`RockBobber.cs`; new `DamageInfo.cs`, `LavaHazardVolume.cs`, `LavaV4Sampler.cs`,
`LavaSubmersionDetector.cs`, `EnemyBehaviorFlyer.cs`, `EnemyLineOfSight.cs`, `DamageIndicatorUI.cs`,
`DamageOverlayUI.cs`). It was code-reviewed on paper but the developer has not play-tested it.
`LAST_LEFT_OFF.txt` (project root) records the exact live state — read it first. The specs for the
new code are `Handoffs/SPEC-000-damage-pipeline.md` and `Handoffs/PLAN-002/003/004`.
`Handoffs/PLAN-001-structural-review.md` has open recommendations F4/F5/F6 — evaluate them; apply
any that are cheap and clearly right, skip and note the rest.

## Spec
1. **Review scope**: every script under `Assets/Scripts/` plus `Assets/Editor/` wizards. Priority
   order: (a) the new/modified files above, (b) player movement (`PlayerMotor`, `Movement/*`),
   (c) enemies/projectiles/pooling, (d) the rest.
2. **Fix**: real correctness bugs (event subscribe/unsubscribe leaks, pooled-object state not reset
   in OnSpawn/OnDespawn, null-service crashes, physics layer mistakes, off-by-one damage, race
   conditions between OnEnable ordering) and real inefficiencies (allocations in
   Update/FixedUpdate, LINQ in hot paths, uncached GetComponent per frame, allocating physics
   queries). Every fix must respect the conventions in `CLAUDE.md` (Tooltip on every SerializeField,
   FloatReference/IntReference for tunables, no Find, no singletons, self-containment rule).
3. **Do NOT**: restyle working code, rename public APIs, touch shader files, re-tune designer
   values in .asset files, or re-run `Tools > Blast Frame > Implement Fix > 055` (it has a benign
   known error and is one-time).
4. **Verify the previous session's features live** (this doubles as the regression test for your
   fixes) — see acceptance tests.
5. Apply the two optional tunings from LAST_LEFT_OFF if trivially safe: (a) reorder DamageOverlay
   sibling index so HealthText/DashRing/ChargeBar render above the orange wash; (b) move the
   FlyingEnemy from (-23,-22,15) up near the bridge so it engages sooner. Save the scene after.

## Acceptance tests
Run all of these yourself via the raa-unity MCP tools:
1. `unity_refresh_assets` then `unity_get_editor_state` → `isCompiling:false`, zero compile errors.
2. `unity_read_console` after a fresh `unity_clear_console` + refresh → no errors (the one-time
   "Failed to find UI/Skin/Arrow.psd" only appears if Fix 055 is re-run — don't re-run it).
3. Open `Assets/Scenes/Core.unity` then `Assets/Scenes/Lava World/Area1LavaBridge.unity` additively
   (`unity_manage_scenes`). Enter play mode. Confirm via `unity_screenshot_game_view` +
   `unity_read_console`: scene renders, no runtime errors over ≥30 seconds of play.
4. Drive the player (`unity_execute_csharp` may teleport the player via PlayerMotor.Teleport) into
   a Lava Falls hazard volume → PlayerHealth drops by 1 and the orange overlay flashes.
5. Teleport the player into the lava river/pool → submersion DoT ticks + opaque orange overlay;
   surfacing clears it.
6. FlyingEnemy engages when the player is within range (fires EnemyMissile; pool prewarm 12).
7. Take a hit whose source is off-screen → a directional damage indicator arrow appears.
8. Exit play mode → no errors; scene saved if you changed it.

## Environment notes
- Unity 6000.3.10f1 must be open (it was launched already; the raa-unity bridge auto-starts, port
  discovery via `Library/unity-mcp-port.json`). If `unity_get_editor_state` fails, wait and retry —
  first boot import can take minutes. Never install any other Unity MCP server.
- After editing C#: `unity_refresh_assets`, then poll `unity_get_editor_state` until compile
  finishes, then `unity_read_console`.
- Scene edits via MCP are undo-registered; save scenes explicitly when done.
- Do NOT commit to git. Leave changes in the working tree.
- The developer is away — make sensible calls, document them, keep going.

## Definition of done
- All acceptance tests pass and are individually reported (pass/fail + evidence).
- A written findings list: every bug found, what was fixed, what was found-but-skipped (with
  reason), including the PLAN-001 F4/F5/F6 disposition.
- `Assets/GameInfo.md` amended if any setup pattern changed.
- Update `Handoffs/INDEX.md` Code column for this doc.

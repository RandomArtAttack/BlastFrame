# HANDOFF-006 — Movement feel pass ("make movement feel as awesome as possible")

## Goal
First-person movement reads as fast, weighty and responsive: dashing kicks the FOV, hard landings
dip the camera, wall-sliding leans the camera away from the wall, strafing adds subtle roll — all
as one new self-contained camera component plus (only if play-testing shows a concrete problem)
small tuning fixes in the movement modules. Every effect magnitude is designer-tunable via
FloatReference and setting it to 0 disables that effect. Single-player behavior is otherwise
unchanged.

## Root cause / design
The kinematic motor and movement modules already implement the mechanics well (dash-jump momentum
carry, coyote time ~0.25s, jump buffer, wall adjacency detection, step-up, ledge vault, ground
snap — see the CLAUDE.md Glossary, it is authoritative on intended feel). What's missing is
*feedback*: the camera is static, so speed doesn't feel like speed. The fix is camera juice driven
by existing state/events, not mechanics changes.

Relevant code: `Assets/Scripts/Gameplay/Player/PlayerMotor.cs` (exposes `Velocity`, `IsGrounded`,
`WasGroundedLastTick`, `IsOnWall`, `WallNormal`), `Assets/Scripts/Gameplay/Player/Movement/DashModule.cs`
(has OnDashStarted / cooldown events per CLAUDE.md), `WallSlideModule.cs`, `JumpModule.cs`,
`Assets/Scripts/Camera/FirstPersonCamera.cs` (custom rig, owns look pitch/yaw — do not fight it:
apply feel offsets on a separate transform layer or additively after look, never write into its
look state). The camera is a child of the Player root, so `GetComponentInParent` reaches the motor
and modules — that satisfies the self-containment rule (no cross-object Inspector wiring).

## Spec
1. New `Assets/Scripts/Camera/CameraFeel.cs` on the camera GameObject (namespace matching
   neighbors). It caches its own Camera and, via GetComponentInParent in Awake/Start: PlayerMotor,
   DashModule, WallSlideModule. Null-tolerant: any missing sibling just disables that effect.
2. Effects (each with FloatReference magnitude + speed where sensible, Tooltips on everything):
   - **Dash FOV kick**: on dash start, lerp FOV up by `dashFovKick` (default 12) in ~0.06s; while
     dash held, hold it; on dash end decay back over ~0.2s. Base FOV read from the camera at Start.
   - **Speed FOV**: additive FOV scaled by horizontal speed above run speed (subtle, default max +4)
     so dash-jump chains keep the sense of speed after the dash ends.
   - **Landing dip**: on `WasGroundedLastTick==false && IsGrounded==true`, dip camera local Y by
     an amount scaled by impact |velocity.y| (default max 0.25 units at hard falls, dead-zone below
     ~6 m/s so normal hops don't dip), spring back critically damped (~8 Hz feel).
   - **Wall-slide lean**: while wall-sliding, roll the camera ~5° away from `WallNormal` side,
     smooth in/out ~0.15s.
   - **Strafe roll**: roll ~1.5° opposite lateral input while grounded, smoothed. Keep subtle.
   - **NO head-bob.** Explicitly out — it fights precision platforming.
   - All roll/dip applied as a local offset composed AFTER FirstPersonCamera's look each
     LateUpdate (script execution order or explicit composition — verify no jitter with the
     interpolated rigidbody).
3. Optional (only if play-testing reveals it): tiny mechanics tuning inside existing modules using
   existing FloatReference fields or new ones. Any glossary-defined behavior (dash = held-button,
   7-unit standing long jump, 10-unit dash jump, forward-press must not decelerate after
   dash-jump) must NOT regress.
4. Ship the editor tooling per CLAUDE.md: a `Tools/Blast Frame/Player/Add Camera Feel` wizard that
   adds the component to the player camera in the open scene, plus an ImplementFix entry ONLY if
   one-time scene wiring is needed (next free number — scan `Assets/Editor/ImplementFix.cs`).
5. Add the component to the player in the Sandbox scene AND Area1LavaBridge player (whichever
   player object those scenes use — check; the player may live in Core). Save scenes.

## Acceptance tests
1. Compile clean (`unity_refresh_assets` → `unity_get_editor_state`), console clean.
2. Play mode in Sandbox (Core + Sandbox additive if that's how it's wired — check how Sandbox was
   built; it may be standalone): screenshot before/during a dash → visible FOV difference.
3. Drive the player off a high ledge → landing dip visible (screenshot pair or read the camera
   local position via `unity_execute_csharp` during the dip).
4. Wall slide → camera roll while sliding, returns to 0 after.
5. Set all FloatReferences to 0 via `unity_execute_csharp` → play: camera behaves exactly as
   before the change (no residual offsets).
6. ≥60 seconds of mixed movement (run/jump/dash/wall-slide) with zero console errors.
7. Glossary regression: standing long jump still clears ~7 units, dash-jump ~10 (measure by
   teleporting to a known position, jumping, reading positions via `unity_execute_csharp` —
   approximate is fine, flag if wildly off).

## Environment notes
- Unity 6000.3.10f1 open, raa-unity MCP bridge (see CLAUDE.md "RAA Unity MCP"). After C# edits:
  refresh, poll editor state, read console.
- Camera stack: viewmodel/gun uses URP camera stacking — FOV changes on the base camera do NOT
  need to hit the overlay gun camera (gun keeping constant FOV during dash is actually desirable);
  note what you did.
- HANDOFF-005 ran immediately before this in the same working tree — expect its fixes, don't
  revert them.
- Do NOT commit to git.

## Definition of done
- All acceptance tests reported pass/fail with evidence.
- Chosen default tuning values listed in the report (the developer will re-tune by hand).
- `Assets/GameInfo.md` gains a CameraFeel entry (components, SOs, setup, gotchas).
- `Handoffs/INDEX.md` updated for this doc.

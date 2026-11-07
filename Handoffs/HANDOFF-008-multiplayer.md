# HANDOFF-008 — Multiplayer: local split-screen 2P (full) + online co-op (foundation)

## Goal
Two-player support exists in two tiers, with single-player completely unaffected when neither is
activated: (1) **Local co-op, fully working**: a second player joins on a gamepad, horizontal
split-screen, both players move/jump/dash/shoot independently, enemies target the nearest player.
(2) **Online co-op foundation**: Netcode for GameObjects installed and wired — a host can start,
a client can join by IP, both see each other's player moving and shooting. Online enemy/projectile
sync is explicitly a documented follow-up, not required here.

## Root cause / design
The codebase is single-player by construction, in exactly three load-bearing places:
1. `Assets/Scripts/Input/PlayerInputHandler.cs` — ONE `InputSystem_Actions` instance registered as
   the ONE `IPlayerInput` service; it reads all devices at once.
2. `EntityRegistrySO` (`Assets/Scripts/Core/Entities/`) — a single `PlayerTransform` / `HasPlayer`;
   every enemy behavior targets it via `EnemyBehaviorBase.Player`.
3. The Player object + camera live as one instance (check whether the player is a prefab or a
   scene object in Core/Sandbox — if it isn't a prefab yet, prefab-izing it is step one).
Everything else (motor, modules, shooter, health) is per-instance component code and multiplies
cleanly. Design: make those three points N-player aware with P1-compatible defaults, then build
local co-op on device pairing and online on NGO with client-authoritative movement (the motor is
a client-side kinematic sweep — server-auth would mean rewriting it; not worth it for co-op).

## Spec
### Phase A — de-singleton the three points (single-player must still pass a full regression)
1. **Input**: refactor to per-player input. Each player gets its own `InputSystem_Actions`
   instance paired to specific devices via `InputUser.PerformPairingWithDevice` +
   `actions.devices = ...`. P1 = keyboard+mouse (plus gamepad #1 when solo — preserve today's
   behavior). P2 = first unclaimed gamepad. Keep `IPlayerInput` as the interface; the existing
   ServiceLocator registration remains P1's (back-compat). A player-index-aware provider
   (`IPlayerInputProvider` service or equivalent) hands instance N its input. PlayerController
   must take its input source by player index instead of assuming the global service.
2. **Registry**: `EntityRegistrySO` gains a players list: `RegisterPlayer/UnregisterPlayer`,
   `PlayerTransform` keeps returning player 0 (back-compat), add
   `GetNearestPlayer(Vector3 from)`. Update `EnemyBehaviorBase` so `Player` resolves the nearest
   player each access (cheap: ≤2 entries). Damage indicators/overlay must bind to THEIR player's
   health only.
3. **Player prefab**: ensure the full player (root + camera + viewmodel stack) is a prefab
   spawnable at a chosen transform. HUD binding: keep the existing single canvas bound to P1; P2
   gets a minimal health number on their half (clone the health widget; full dual HUD is a
   follow-up if time is short — note it).

### Phase B — local split-screen co-op (must be fully working)
4. `LocalCoopManager` (Core scene, service): watches for a join input (gamepad Start/South button
   while in gameplay — use an InputAction, not polling `Gamepad.current` state hacks) → spawns the
   P2 player prefab near P1, pairs the gamepad, splits both cameras to horizontal halves
   (viewport rects: P1 top, P2 bottom), including each player's URP overlay/viewmodel camera
   stack. A leave/despawn path (hold same button) restores full-screen P1.
5. Death handling in co-op: v1 rule — a dead player respawns after 5s at the living player's
   position; if both are dead, normal death flow. Record this in GameDesign.md as a provisional
   ruling.
6. Audio/UI managers must tolerate two players (no exceptions thrown); perfection not required.

### Phase C — online foundation (NGO)
7. Add `com.unity.netcode.gameobjects` (latest verified for Unity 6000.3) to the manifest. Let it
   pull Unity Transport.
8. `NetworkManager` + UnityTransport on a new GameObject in Core, **disabled/idle by default** —
   single-player never starts it.
9. Player prefab variant (or the same prefab + NetworkObject): owner-authoritative movement —
   owner runs the full kinematic motor + camera + input; non-owners disable input/camera/motor
   simulation and receive position/rotation via a client-authority NetworkTransform (NGO 2.x:
   `NetworkTransform` with owner authority; older name ClientNetworkTransform sample). Shooting:
   owner spawns its own local projectiles AND sends an RPC so remotes see a cosmetic projectile
   (server-auth projectile damage is the documented follow-up; for now damage applies on the
   firing client's simulation — fine for co-op).
10. Minimal connect UI (Canvas + TMP, Core scene, hidden during normal play, toggled with F9 or a
    menu key): Host / Join + IP field / Disconnect / status label. Default 127.0.0.1:7777.
11. Enemies online: v1 = enemies simply exist per-host (host runs them; clients see nothing or
    host-only) — document clearly what a client does and does not see. Do NOT attempt full enemy
    sync if it endangers the rest; scaffolding + honest notes beat a broken tree.

### Hard constraints
- Single-player path must behave identically when no second device joins and networking is idle.
- All CLAUDE.md conventions hold (no singletons — NetworkManager is Unity's own, that's allowed;
  services via ServiceLocator; tooltips; no Find at runtime).
- If Phase C turns out to be too unstable to finish, STOP it cleanly: leave the package installed,
  the code compiling, single-player + local co-op intact, and write up exactly where it stands.
  Do not sacrifice Phases A/B to Phase C.

## Acceptance tests
1. Compile clean, console clean.
2. **Single-player regression** (most important): play Sandbox solo ≥60s — movement, dash, shoot,
   enemy aggro, lava damage overlay all work, zero errors, no split-screen, no network activity.
3. **Local co-op**: in play mode, simulate a gamepad join. No physical gamepad is attached, so use
   the Input System's test framework or `InputSystem.AddDevice<Gamepad>()` +
   `InputSystem.QueueStateEvent` via `unity_execute_csharp` to create a virtual gamepad and press
   its join button. Verify: P2 spawns, screen splits into two viewports (screenshot), queued
   virtual-gamepad stick input moves ONLY P2 while keyboard moves ONLY P1 (read both player
   positions via `unity_execute_csharp`).
4. P2 leaves → full screen restored, P2 despawned, no errors.
5. **Online**: start Host via the connect UI in play mode → local player respawns/binds as a
   NetworkObject owner, moves normally, no errors. (Two-instance testing isn't feasible in one
   editor — verify the client path by code review + NGO's connection approval logs, and say so
   honestly in the report.)
6. Disconnect → back to offline single-player cleanly in the same session.

## Environment notes
- Unity 6000.3.10f1 open; raa-unity MCP. Adding the NGO package: edit `Packages/manifest.json`,
  then `unity_refresh_assets` and WAIT for the long import/compile; check editor state repeatedly.
- Input System 1.18: virtual devices via `InputSystem.AddDevice<Gamepad>()` work in play mode in
  the editor — use for tests.
- HANDOFF-005/006/007 all ran before this in the same working tree (new enemies exist and target
  players through EnemyBehaviorBase — your registry change feeds them automatically).
- This is the riskiest handoff: work in small compiling increments; never leave the tree broken.
  Do NOT commit to git.

## Definition of done
- Acceptance tests reported pass/fail with evidence, including an honest list of what online co-op
  does NOT yet do (enemy sync, projectile authority, relay/NAT — LAN/loopback only).
- `Assets/GameInfo.md`: entries for LocalCoopManager, per-player input, NetworkManager setup.
- `Assets/GameDesign.md`: dated entries — multiplayer scope decisions + provisional co-op respawn
  rule.
- `Handoffs/INDEX.md` updated; `## Manual Steps` checklist for the developer (e.g., real-gamepad
  test, two-machine LAN test).

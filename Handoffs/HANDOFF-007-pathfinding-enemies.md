# HANDOFF-007 — Five new enemy types with pathfinding

## Goal
Five new enemy types exist as prefabs in `Assets/Prefabs/Enemies/`, each built from the composable
behavior architecture, each with working pathfinding, each spawnable via its own editor wizard,
all placed and verified fighting the player in the Sandbox scene: **Lobber**, **Skirmisher**,
**Missiler**, **Biter**, **Bat**. A NavMesh exists in the Sandbox scene. New projectile types
(homing missile, dropped bomb) are pooled.

## Root cause / design
The enemy architecture is ready for this (`Assets/Scripts/Gameplay/Enemies/EnemyBehaviorBase.cs`
— caches EnemyStats/EnemyCore, resolves the player via EntityRegistrySO; `EnemyCore.cs` — health/
death/pool-return; `EnemyStats.cs` — FloatReference tunables; `EnemyLineOfSight.cs`;
`EnemyBehaviorFlyer.cs` is a 422-line reference implementation of a complete airborne behavior;
`EnemyBehaviorArcPredict.cs` has the ballistic lob + prediction math to reuse for the Lobber).
`com.unity.ai.navigation` 2.0.10 is already in the manifest but **no NavMesh has ever been baked**
— ground enemies get NavMeshAgent-driven movement, and the Sandbox scene needs a NavMeshSurface
baked as part of this work. The Bat flies, so it steers directly (like the Flyer) instead of
using the NavMesh.

Study `Assets/Editor/FlyingEnemyWizard.cs` as the template for the five wizards, and check
`Assets/GameInfo.md` for the established enemy/pool setup patterns (pool ids, EntityDefinitionSO,
projectile layer rules: enemy weapons don't collide with enemies, player weapons don't collide
with the player — inspect the physics collision matrix and existing projectile prefabs/layers
before inventing anything).

## Spec
### Shared infrastructure (do first)
1. **NavMesh**: add a NavMeshSurface to the Sandbox scene ground, agent radius fitting a ~1-unit
   enemy, bake it (`unity_execute_csharp` can call `surface.BuildNavMesh()`). Add an editor menu
   `Tools/Blast Frame/Level/Bake NavMesh` that finds/adds the surface in the open scene and bakes.
2. **`EnemyNavMotor.cs`** (new, `Assets/Scripts/Gameplay/Enemies/`): thin wrapper owning a
   NavMeshAgent — SetDestination with repath throttling (~0.25s), speed from EnemyStats.MoveSpeed,
   stop/resume, `HasArrived`. Behaviors use it instead of touching the agent directly. Enemy
   Rigidbody (if any) kinematic so the agent drives the transform.
3. **`HomingMissile.cs`** (new, `Assets/Scripts/Gameplay/Projectiles/`): pooled projectile,
   accelerates toward the player with a clamped turn rate (FloatReference turnDegPerSec, speed,
   lifetime), **shootable**: it implements IDamageable with 1 HP (or carries a small EnemyCore —
   pick the simpler that fits pooling) and dies to a PlayerProjectile hit → small AoeExplosion
   VFX, no damage to player. Layer must be hittable by player projectiles yet not collide with
   enemies. On reaching the player / lifetime end: explode (reuse `AoeExplosion`).
4. **`DroppedBomb.cs`** (new, same folder): pooled, gravity-only fall (or reuse `ArcProjectile`
   with zero horizontal velocity if it supports that cleanly — prefer reuse), explodes on impact.
5. Pool entries for both in the PoolConfig SO(s), prewarm ~8 each. EntityDefinitionSO per enemy in
   `Assets/ScriptableObjects/Entities/`.

### The five enemies (behavior component + prefab + wizard each)
Prefabs are primitive-built (cubes/spheres/cylinders, distinct materials — there's
`M_SandboxRed.mat`; make simple sibling materials as needed). All tunables through EnemyStats or
new FloatReference fields with Tooltips. All target acquisition via EntityRegistrySO. All fire
only with line of sight (`EnemyLineOfSight`) where shooting is involved.

1. **Lobber** (`EnemyBehaviorLobber`): ground unit. Uses EnemyNavMotor to hold a preferred band
   (~60–90% of Range) from the player — advances when too far, backs off when too close. Lobs an
   `ArcProjectile` on an arc at the player's position (reuse the ArcPredict ballistic solve;
   simple non-predictive aim is fine as the default). Fire rate from stats.
2. **Skirmisher** (`EnemyBehaviorSkirmisher`): ground unit, cover-user. Finds cover: samples
   NavMesh positions in a ring (~8–16 candidates within Range) and picks the nearest one where
   line of sight to the player is BLOCKED. Hides there. Every `attackInterval` it pops out
   (moves to a sampled position WITH LOS), fires a burst of 2–3 straight shots (reuse
   EnemyMissile or PlayerProjectile-style straight pooled shot — whichever existing enemy shot
   fits), then returns to cover. Occasionally (cooldown FloatReference) it *dashes*: NavMeshAgent
   speed multiplied ~3× for ~0.3s while repositioning. If the player gets close (< ~6 units) it
   dashes to a farther cover point.
3. **Missiler** (`EnemyBehaviorMissiler`): slow vehicle (MoveSpeed default ~1.5). Chassis
   (NavMesh) slowly advances toward the player, stopping at ~70% Range. Child "Turret" transform
   rotates to track the player. Two attacks on independent cooldowns: (a) high-speed explosive
   shot — straight, fast (2× ProjectileSpeed), explodes on impact with AoeExplosion; (b) a volley
   of 3–4 `HomingMissile`s launched upward in a spread, which then home. Both from turret muzzle
   points. Tanky: Health default ~10.
4. **Biter** (`EnemyBehaviorBiter`): small fast melee chaser. NavMesh-chases the player with a
   little intelligence: while approaching, offsets its destination sideways (alternating/perlin
   zigzag) so it doesn't run a straight line; when within ~2.5 units it lunges (brief agent speed
   burst toward the player) and bites — deals contact damage (Stats.Damage) with a per-target
   cooldown (~1s) using the DamageInfo pipeline (see `Assets/Scripts/Gameplay/DamageInfo.cs` /
   SPEC-000) so the directional damage indicator points at it. Low health (default 2), fast
   (default ~6 m/s).
5. **Bat** (`EnemyBehaviorBat`): flying bomber with an ammo loop. Home point = spawn position (or
   optional child "Roost" transform). Carries ONE bomb: flies (direct steering + smooth noise
   wander like the Flyer, hover height FloatReference ~8 above the player) to above the player,
   drops the `DroppedBomb`, then must fly BACK to its home point to rearm (brief ~1s "reload"
   pause there) before attacking again. Visibly carries the bomb (a small child sphere toggled
   on/off with ammo state).

### Verification scene
Place one of each in Sandbox (spread out, Lobber/Skirmisher/Missiler/Biter on the baked NavMesh,
Bat in the air), save the scene, play, and watch them all engage the player.

## Acceptance tests
1. Compile clean, console clean.
2. NavMesh visibly baked in Sandbox (query `NavMesh.SamplePosition` via `unity_execute_csharp`
   returns hits on the ground).
3. Play mode ≥90s in Sandbox with all five placed, zero errors, and per-enemy observations
   (screenshots + position/state reads via `unity_execute_csharp`):
   - Lobber holds distance and lands arcing shots near the player.
   - Skirmisher is unhittable behind cover most of the time, emerges, fires a burst, returns;
     dashes when the player closes in.
   - Missiler creeps forward, turret tracks, fires both the fast explosive shot and a homing
     volley; shooting a HomingMissile in flight destroys it mid-air.
   - Biter zigzags in and damages the player on contact, with cooldown (health drops by 1, not
     continuously).
   - Bat drops a bomb over the player, returns home, rearms, repeats.
4. Killing each enemy returns it to pool / destroys cleanly, no errors.
5. Each wizard menu item creates a correctly-wired instance in an empty test usage (run each once,
   then undo/delete).
6. All five die to player shots (IDamageable path works).

## Environment notes
- Unity 6000.3.10f1 open, raa-unity MCP (CLAUDE.md "RAA Unity MCP" section). After C# edits:
  `unity_refresh_assets` → poll `unity_get_editor_state` → `unity_read_console`.
- HANDOFF-005/006 ran before this in the same tree; build on their state.
- Pooling rules in CLAUDE.md are strict (IPoolable OnSpawn/OnDespawn, prewarm, no runtime
  instantiation) — homing missiles MUST reset their homing/velocity state on spawn.
- NavMeshAgent + kinematic Rigidbody: keep the RB kinematic and let the agent move the transform;
  never AddForce.
- The developer is away: choose sensible defaults, list every tuning value you set, do NOT commit.

## Definition of done
- All acceptance tests reported pass/fail with evidence; Sandbox scene saved with the five enemies.
- `Assets/GameInfo.md`: one entry per new enemy type + HomingMissile/DroppedBomb + NavMesh baking
  (components, SOs, setup steps, gotchas).
- `Assets/GameDesign.md`: dated entry summarizing the five enemy designs.
- `Handoffs/INDEX.md` updated. A `## Manual Steps` checklist in your report for anything only the
  developer can do.

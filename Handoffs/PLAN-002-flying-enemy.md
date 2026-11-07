# PLAN-002 — Flying Enemy ("Hoverbot"): approach, orbit, shoot, dodge, reposition

**Depends on:** SPEC-000 (DamageInfo) already implemented.
**Author:** Fable 5, 2026-07-07. **Implementer target:** any Claude model, no context needed.
**Unity is closed:** code + editor wizard only; compile/prefab creation happen later.

## What it is

The project's first *moving* enemy (only static turrets exist). A hovering robot that flies toward
the player, holds a preferred range band, strafes/orbits with random direction flips and a hover
bob, fires short bursts of pooled `EnemyMissile`s with mild lead, **dodges sideways when the player
aims at it**, and periodically repositions. Composable behavior on the existing
`EnemyCore`/`EnemyStats` contract.

## Design rationale (why these choices — keep in the doc for the dev)

- **Range band + orbit** instead of hover-in-place: a static aerial target is a free hit in an FPS;
  orbiting at 10 u (player standing long jump = 7 u) keeps it out of trivial reach but inside
  readable engagement range.
- **Dodge triggers on being AIMED AT** (camera-forward dot, held briefly), not on incoming
  projectiles: it reads as "it dodges when I aim", which is the fantasy, and is far cheaper than
  projectile prediction. Reaction delay (~0.18 s) + cooldown (~1.2 s) keep it beatable — flick
  shots and charged AoE (Lv2/Lv3) are the counterplay.
- **Burst fire with gaps** (3 shots, 0.12 s spacing, then full fire-rate cooldown) gives the player
  clear dodge windows; `EnemyMissile`'s slow-then-accelerate profile already telegraphs nicely.
- **Kinematic MovePosition steering** matches every other mover in the project (no AddForce, per
  CLAUDE.md), with a single spherecast for obstacle deflection — full pathfinding is overkill for
  open combat rooms.

## Files

### 1. New: `Assets/Scripts/Gameplay/Enemies/EnemyLineOfSight.cs`
Static helper — a faithful port of `EnemyBehaviorArcPredict.HasLineOfSight`/`IsPointVisible`
(EnemyBehaviorArcPredict.cs:124-168), generalized:

```csharp
namespace BlastFrame.Gameplay.Enemies
{
    /// <summary>Multi-point line-of-sight test against a target's collider bounds (center, top,
    /// bottom, both lateral edges) so a partially covered target still registers. Non-allocating.</summary>
    public static class EnemyLineOfSight
    {
        // static readonly RaycastHit[] buffer (size 32), same skip rules as the turret version:
        // hits that are children of `self` or of `target` are not blockers.
        public static bool HasLineOfSight(Transform self, Vector3 origin, Transform target, Collider targetCollider);
    }
}
```
Do NOT modify `EnemyBehaviorArcPredict` (deferred by design — see PLAN-001 F7).

### 2. New: `Assets/Scripts/Gameplay/Enemies/EnemyBehaviorFlyer.cs`

`public class EnemyBehaviorFlyer : EnemyBehaviorBase` — extends the existing base (registry, Stats,
Core cached there). `[RequireComponent(typeof(Rigidbody))]`.

**Rigidbody setup in Awake** (mirror RockBobber.cs:142-145): kinematic, no gravity, interpolated.

**Uses from `EnemyStats`:** `MoveSpeed` (fly speed, suggest asset value ~7), `FireRate` (bursts/sec,
~0.5), `Damage` (per missile, 1), `ProjectileSpeed` (missile handles its own accel — pass through),
`Range` (engage/disengage radius, ~30).

**Own serialized tuning (ALL FloatReference/IntReference + Tooltips):**

| field | default | meaning |
|---|---|---|
| preferredRange | 10 | orbit ring radius around player |
| rangeBand | 3 | acceptable ± around preferredRange before correcting |
| hoverHeight | 2.5 | meters above player eye the flyer tries to hold |
| hoverBobAmplitude / hoverBobFrequency | 0.4 / 1.2 | idle sine bob (per-instance random phase from `GetInstanceID()`) |
| strafeSpeedFactor | 0.7 | orbit tangential speed = MoveSpeed × this |
| strafeFlipIntervalMin / Max | 1.5 / 3.5 | random seconds between orbit direction flips |
| dodgeSpeed | 9 | lateral burst speed during a dodge |
| dodgeDuration | 0.25 | seconds a dodge impulse lasts |
| dodgeCooldown | 1.2 | min seconds between dodges |
| aimDotThreshold | 0.985 | player-camera-forward · to-flyer dot that counts as "aimed at" |
| aimReactionTime | 0.18 | seconds the aim must be held before the dodge fires |
| repositionInterval | 4 | seconds between picking a new orbit height/angle offset |
| burstCount (Int) | 3 | missiles per burst |
| burstSpacing | 0.12 | seconds between missiles in a burst |
| leadFactor | 0.5 | fraction of full linear lead applied when aiming |
| avoidProbeDistance | 2 | spherecast length ahead for obstacle deflection |

Plus `[SerializeField] Transform muzzle;` (own child — allowed by self-containment).

**Behavior (all in FixedUpdate, no allocations — cache buffers as static readonly like the turret):**

1. **Acquire:** `HasPlayer`/`Player` from base. Outside `Stats.Range` → drift to a stop, idle bob
   only. Cache the player's `Camera` once via `Player.GetComponentInChildren<Camera>(true)` when the
   player first appears (needed for aim detection; NEVER `Camera.main`). Cache player root collider
   for LOS the way the turret does.
2. **Desired position:** point on the ring: `player.pos + ringDir * preferredRange` at
   `player eye Y + hoverHeight`, where `ringDir` rotates around the player at
   `strafeSpeedFactor × MoveSpeed / preferredRange` rad/s in the current orbit direction; flip
   direction at random intervals in [min,max]. Add the sine bob to Y. Every `repositionInterval`,
   re-roll a height offset in ±1.5 and an angle jump of 30–90° (feels deliberate, not orbital-clock).
3. **Steering:** `desiredVel = (desiredPos - pos).clamped to MoveSpeed`; smooth current velocity
   toward it (`Vector3.MoveTowards`, accel ~2×MoveSpeed per second). Spherecast (radius = collider
   radius) along the velocity for `avoidProbeDistance`; on hit, project velocity onto the hit plane
   (slide). `_rb.MovePosition(pos + vel * dt)`. Face the player (yaw-only Slerp, ~360°/s
   — reuse the turret's yaw pattern EnemyBehaviorArcPredict.cs:97-103).
4. **Fire:** cooldown = `1/FireRate` between bursts. When elapsed AND within band AND
   `EnemyLineOfSight.HasLineOfSight(...)`: fire `burstCount` missiles spaced `burstSpacing` apart
   (timer-driven state, no coroutines). Aim point = player position + flat player velocity ×
   (dist/ProjectileSpeed... use `EnemyMissile` max speed sense: just `dist / Stats.ProjectileSpeed`)
   × `leadFactor`; player velocity sampled by position delta exactly like the turret
   (EnemyBehaviorArcPredict.cs:84-88). Spawn: `_pool.Spawn(PoolIds.EnemyMissile, muzzle.position,
   look-at-aim)` then `Initialize(Stats.Damage, aimDir)` — same contract as existing callers.
   Resolve `IPoolManager` in `Start()` via ServiceLocator (pattern: EnemyBehaviorArcPredict.cs:76).
5. **Dodge:** each tick compute `aimDot = dot(playerCam.forward, (flyerPos - camPos).normalized)`.
   If `aimDot > threshold` continuously for `aimReactionTime` and cooldown elapsed → dodge: velocity
   override = (random left/right along the camera-perpendicular) × dodgeSpeed, plus up-bias
   (+0.3 dodgeSpeed on Y half the time), for `dodgeDuration`. Also 35% chance of a micro-dodge
   right after a burst ends. During a dodge, steering (step 3) is suspended; obstacle slide still runs.
6. **Death:** subscribe `Core.OnDeath` in Awake (unsubscribe OnDestroy) → set `_dead`, stop moving
   (pattern: EnemyBehaviorArcPredict.cs:63-72, 265). EnemyCore already handles reward/despawn.

### 3. New: `Assets/Editor/FlyingEnemyWizard.cs`
Menu item `Tools/Blast Frame/Enemies/Create Flying Enemy Prefab`:
- Builds root `FlyingEnemy` (layer **Enemy**): kinematic interpolated Rigidbody,
  SphereCollider (radius 0.5), `EnemyStats`, `EnemyCore`, `EnemyBehaviorFlyer`.
- Child `Body`: default sphere primitive scaled (1, 0.6, 1) — remove its collider. Child `Muzzle`
  at (0, -0.2, 0.5) — assign to the behavior's muzzle field via SerializedObject.
- Auto-assign SO refs by `AssetDatabase.FindAssets`: `EntityRegistry` asset → `entityRegistry`
  fields (both EnemyCore and behavior, they're separate serialized fields), `OnEnemyKilled` event
  if one exists → EnemyCore.
- Save to `Assets/Prefabs/Enemies/FlyingEnemy.prefab` (create folders if missing), ping it.
- If a prefab already exists at that path, `DisplayDialog` ask overwrite/cancel.

## Explicit conventions (CLAUDE.md — MUST hold)
Tooltips on every SerializeField; Float/IntReference for all tuning; no Find/Camera.main (camera
comes from the registry player's children, cached); no allocations in FixedUpdate (static buffers,
no LINQ); behaviors read `EnemyStats`, react to `EnemyCore` events, never reference other behaviors;
lifecycle methods private; pool ids via `PoolIds`.

## Out of scope tonight (needs the editor)
Running the wizard, creating the prefab, placing it in a scene, and PoolConfig sizing for
EnemyMissile under multi-flyer load.

## Acceptance
- Compiles (checked next Unity session).
- Zero per-tick allocations by inspection (no `new`, no LINQ in Tick paths).
- With a stationary player and open room (paper-trace): flyer closes to 10 u, orbits with flips,
  bursts of 3 with LOS, dodges when aimed at, never exceeds MoveSpeed except during dodge.

## Manual steps (for the dev, later)
- [ ] Run Tools > Blast Frame > Enemies > Create Flying Enemy Prefab.
- [ ] Confirm PoolConfig has an `EnemyMissile` entry with prewarm ≥ 12 (bursts × flyers).
- [ ] Place a FlyingEnemy in Sandbox.unity and play: verify approach → orbit → burst → dodge-on-aim.
- [ ] Tune preferredRange/dodge values on the prefab to taste.

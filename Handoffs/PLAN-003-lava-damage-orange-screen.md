# PLAN-003 — Lava Damage + Orange Screen (waterfall touch + river submersion)

**Depends on:** SPEC-000 (DamageInfo, i-frames) already implemented.
**Author:** Fable 5, 2026-07-07. **Implementer target:** any Claude model, no context needed.
**Unity is closed:** code + ImplementFix entries only; scene wiring runs later.

## What it is

Two lava damage modes plus a screen effect:
1. **Waterfall (touch damage):** designer-placed trigger volumes deal ticking damage on contact.
2. **River (submersion):** if the player's HEAD goes under the lava river's *visually displaced*
   surface (the GPU vertex churn of `RAA Lava Flow V.4`), they take damage over time and the whole
   screen turns opaque orange so they can't see — surfacing clears it.
3. **Any-damage orange flash** on the same overlay (brief, translucent) — the damage-feedback layer
   the HUD currently lacks.

## The key technical fact (verified in code — do not re-litigate)

The V4 river's vertical displacement is **GPU-only** (`RAA_LavaFlow_V4.shader` vert, lines 107-110:
`positionOS.y += noise(uv * ST + move * time) * strength`); the CPU mesh/collider never move.
`RockBobber` already mirrors this exactly on the CPU (`CacheV4` + `SampleNoiseDisp`,
RockBobber.cs:335-370) by raycasting the Lava-layer collider for `hit.textureCoord` +
`hit.point.y`, then sampling the same scrolling noise. **Submersion detection reuses that exact
technique** via a shared extraction. Also verified: `LavaFlowRandomizer` never randomizes
`_NoiseMap_ST`/`_NoiseMove` in its MaterialPropertyBlocks, so reading `sharedMaterial` noise params
is correct for every instance.

## Decisions made for you (veto before running if wrong)

1. **Waterfalls are NOT solid** — they are damage trigger volumes you can dash through (taking a
   tick of damage). Dashing through a lava waterfall is good roguelike-platformer texture. If a
   specific waterfall must block, the designer adds a plain invisible box collider (Default layer).
2. **The player must fall THROUGH the river surface**, so the Lava layer is removed from
   `PlayerMotor.collisionMask` (Inspector change — Implement Fix 053). Rocks/projectiles are
   unaffected (RockBobber raycasts the Lava layer explicitly; missiles interact via the physics
   matrix, unchanged).
3. **Overlay is a Canvas Image**, not a URP pass — matches "gameplay code doesn't touch the render
   pipeline" and ships tonight. A fancier underwater shader can replace the visual later without
   touching damage logic.
4. **Tick tuning:** hazard volumes 1 dmg / 0.75 s (first tick immediate on entry); submersion
   1 dmg / 0.75 s. Both intervals > the 0.5 s i-frames, so ticks always land. All FloatReference.
5. **Hazard volumes live on the Default layer** (not Lava): the motor ignores triggers everywhere
   (`QueryTriggerInteraction.Ignore`), and F2's projectile fix (below) stops missiles detonating
   on them. Keeping Lava layer = "raycastable lava surface" keeps RockBobber semantics clean.

## Files

### 1. New: `Assets/Scripts/Gameplay/Environment/LavaV4Sampler.cs` (extraction — PLAN-001 F3)

```csharp
namespace BlastFrame.Gameplay.Environment
{
    /// <summary>CPU mirror of the RAA Lava Flow V.4 vertex displacement. Single source of truth —
    /// RockBobber and LavaSubmersionDetector both sample through this.</summary>
    public static class LavaV4Sampler
    {
        public struct V4Params
        {
            public Texture2D NoiseTex; public Vector2 Scale, Offset, Move;
            public float Strength; public bool Linearize; public bool IsValid;
        }
        /// <summary>Reads _NoiseMap/_NoiseMove/_TurbulenceStrength from a V.4 material. False if
        /// not V.4, zero strength, or noise not CPU-readable (logs nothing — caller logs context).</summary>
        public static bool TryCache(Material m, bool noiseIsSRGB, out V4Params p);
        /// <summary>Object-space Y displacement at a mesh UV at time t — EXACT port of
        /// RockBobber.SampleNoiseDisp (RockBobber.cs:363-370) incl. sRGB linearize.</summary>
        public static float SampleDisp(in V4Params p, Vector2 meshUV, float t);
    }
}
```
Port the bodies verbatim from RockBobber (`CacheV4` RockBobber.cs:335-356, `SampleNoiseDisp`
:363-370), including the `QualitySettings.activeColorSpace == ColorSpace.Linear` check.

### 2. Refactor: `Assets/Scripts/Gameplay/Environment/RockBobber.cs`
Replace its private `CacheV4`/`SampleNoiseDisp` and the mirrored fields with `LavaV4Sampler.V4Params`.
**Behavior-identical**: keep every warning message and the `noiseIsSRGB` field exactly as is. The
existing warnings that referenced "no usable V.4 river params" must still fire in the same cases.

### 3. New: `Assets/Scripts/Gameplay/Environment/LavaHazardVolume.cs`
Trigger-volume DoT (waterfalls, lava splash zones):
- Requires a Collider (set `isTrigger` in Awake, warn if none).
- `OnTriggerEnter`: `other.GetComponentInParent<PlayerHealth>()` (cache it + the collider; single
  cached target is fine — only the player takes hazard damage) → immediate first tick.
- `OnTriggerStay`: if cached target matches and `Time.time >= _nextTick` → damage.
- `OnTriggerExit`: clear cache.
- Damage call: `TakeDamage(new DamageInfo(damagePerTick, closestPoint, DamageKind.Hazard))` where
  `closestPoint = _collider.ClosestPoint(playerPos)` (gives the indicator a direction).
- Serialized: `IntReference damagePerTick = 1`, `FloatReference tickInterval = 0.75f` (+Tooltips).
- No allocations after the Enter-tick caching.

### 4. New: `Assets/Scripts/Gameplay/Player/LavaSubmersionDetector.cs`
On the Player root (self-contained). Each FixedUpdate:
- Ray origin = `transform.position + Vector3.up * (eyeHeight + probeHeight)`; cast DOWN on the Lava
  layer (resolve mask exactly like RockBobber.cs:279: serialized LayerMask, fallback
  `LayerMask.GetMask("Lava")`), length `probeHeight + probeDepth`.
- On hit: resolve the renderer under the hit (pattern RockBobber.cs:290-292); `LavaV4Sampler.TryCache`
  its sharedMaterial (cache per-material — re-TryCache only when the hit renderer changes). Displaced
  surface Y = `hit.point.y + SampleDisp(params, hit.textureCoord, Time.timeSinceLevelLoad) *
  lavaTransform.lossyScale.y`. Non-V4 lava (waterfall/ripple materials, or no V4 props) → displacement
  0, flat `hit.point.y` — still correct.
- Head Y = `transform.position.y + eyeHeight`. Submerged when `headY < surfaceY`; surfacing requires
  `headY > surfaceY + exitHysteresis` (0.05) to avoid flicker at the wave boundary.
- If the ray misses while submerged, HOLD the submerged state for `missGrace` (0.3 s) before
  clearing (brief gaps between river pieces).
- While submerged: tick `TakeDamage(new DamageInfo(damagePerTick, DamageKind.Submersion))` (the
  sourceless ctor — indicators skip it) every `tickInterval`, first tick immediate.
- Public `bool IsSubmerged { get; }` and `event Action<bool> OnSubmergedChanged;`
- Serialized (+Tooltips, Float/IntReference): `eyeHeight = 1.8f`, `probeHeight = 5f`,
  `probeDepth = 10f`, `exitHysteresis = 0.05f`, `missGrace = 0.3f`, `damagePerTick = 1 (Int)`,
  `tickInterval = 0.75f`, `LayerMask lavaMask = 0`, `bool noiseIsSRGB = true`.
- The mesh Read/Write requirement (`hit.textureCoord`) is already satisfied for lava meshes
  (Implement Fix 046 ran for RockBobber). Mention in the class summary.

### 5. New: `Assets/Scripts/UI/DamageOverlayUI.cs`
HUD widget, follows the `HealthDisplay` bind pattern exactly (HealthDisplay.cs:26-46: SetRegistry
injection, Start+Update TryBind until player registered, unsubscribe in OnDestroy):
- Serialized: `EntityRegistrySO entityRegistry`, `UnityEngine.UI.Image overlayImage` (own child),
  `Color hitFlashColor` (default 1.0, 0.35, 0.0, a=0.35), `Color submergedColor` (default
  1.0, 0.35, 0.0, a=0.95), `FloatReference hitFlashDuration = 0.4f`,
  `FloatReference submergeFadeIn = 0.15f`, `FloatReference submergeFadeOut = 0.3f` (+Tooltips).
- Binds to `PlayerHealth.OnDamagedInfo` → start a hit flash (alpha from flash color → 0 over
  duration). Binds to `LavaSubmersionDetector.OnSubmergedChanged` (component may be absent —
  handle null) → lerp overlay toward submergedColor / back.
- Submersion FLOOR wins over flash: effective alpha = max(flashAlpha, submergeAlpha).
- Drive in Update; when both alphas are 0 set `overlayImage.enabled = false` (no overdraw).
- `overlayImage.raycastTarget` must be false.

### 6. Companion fix (PLAN-001 F2): projectiles ignore non-damageable triggers
In `EnemyMissile.OnTriggerEnter`, `ArcProjectile.OnTriggerEnter`, `PlayerProjectile.OnTriggerEnter`,
first lines (after the despawn/grace guards):
```csharp
if (other.isTrigger && other.GetComponentInParent<IDamageable>() == null) return;
```
(ArcProjectile needs the `BlastFrame.Gameplay` using for IDamageable.)

### 7. Editor tooling
- New `Assets/Editor/LavaHazardVolumeWizard.cs`: menu `Tools/Blast Frame/Environment/Create Lava
  Hazard Volume` — creates a GO with BoxCollider (isTrigger) + LavaHazardVolume at scene pivot,
  Default layer, pings it.
- Append to `Assets/Editor/ImplementFix.cs` (ADDITIVE — never touch existing fixes; next free
  numbers are 053/054):
  - `053 - Add LavaSubmersionDetector to Player + exclude Lava from PlayerMotor collision mask`:
    finds the Player in the open scene (by PlayerController component on a root, NOT by Find-by-name
    at runtime — editor-only code may use Object.FindFirstObjectByType), adds the detector if
    missing, and clears the Lava bit from the motor's `collisionMask` via SerializedObject. Bail
    with a dialog if no player in the scene.
  - `054 - Add DamageOverlayUI to the HUD canvas`: finds the HUD canvas (HUDController), creates a
    full-screen stretched child `DamageOverlay` with an Image (alpha 0, raycastTarget off, LAST
    sibling so it renders on top), adds DamageOverlayUI, wires the image + EntityRegistry asset via
    SerializedObject.

## Conventions (CLAUDE.md — MUST hold)
Tooltips everywhere; Float/IntReference for tuning; no Find at runtime (editor-only fix code may);
Canvas+TMP UI; UI reacts to events only; no per-tick allocations (cache PlayerHealth, material
params, mask); lifecycle methods private.

## Acceptance
- Compiles next Unity session.
- Paper-trace: player walking under a waterfall volume takes 1 dmg immediately, then 1 per 0.75 s;
  leaving stops it. Player falling into the river sinks through (mask excludes Lava), overlay snaps
  to ~opaque orange, 1 dmg per 0.75 s while head is under the DISPLACED surface, clears on surfacing.
- RockBobber behavior unchanged after the sampler extraction (same warnings, same math).

## Manual steps (for the dev, later)
- [ ] Run Tools > Blast Frame > Implement Fix > 053 (Player detector + motor mask) in the scene with the Player.
- [ ] Run Tools > Blast Frame > Implement Fix > 054 (HUD overlay).
- [ ] Run Tools > Blast Frame > Environment > Create Lava Hazard Volume and size it over the waterfall; repeat per waterfall.
- [ ] Verify the lava RIVER collider object is on the Lava layer (submersion raycast needs it) and its mesh is Read/Write (Fix 046).
- [ ] Play: dash through a waterfall (1 tick), fall in the river (orange + DoT), surface (clears).

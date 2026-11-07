# PLAN-004 — Directional Damage Indicators (HUD)

**Depends on:** SPEC-000 (DamageInfo with SourcePosition) already implemented.
**Author:** Fable 5, 2026-07-07. **Implementer target:** any Claude model, no context needed.
**Unity is closed:** code + ImplementFix entry only; wiring runs later.

## What it is

When the player takes damage that has a world source (missile impact, explosion, hazard volume), a
small indicator appears on a ring around the screen center pointing at the damage source, stays
world-accurate as the player turns, and fades out. Sourceless damage (`DamageInfo.HasSource ==
false`, e.g. lava submersion) shows nothing — the orange overlay covers that case.

## Decisions made for you

1. **Fixed slot pool of 6** indicators, created once in Awake from a serialized prefab-asset
   reference (a prefab is a UnityEngine asset — allowed by self-containment). Oldest slot is
   recycled when all are busy.
2. **Angle source = the player's camera forward, flattened.** The camera is found once via
   `PlayerTransform.GetComponentInChildren<Camera>(true)` (registry player; never `Camera.main`).
   Root yaw would break if the yaw pivot ("orientation") child is used, so camera it is.
3. **Indicators re-aim every frame while alive** (store the source WORLD position per slot,
   recompute bearing in Update) — turning the camera keeps them pointing at the true source. This
   is the difference between "readable" and "decorative".

## Files

### 1. New: `Assets/Scripts/UI/DamageIndicatorUI.cs`

HUD widget on the HUD canvas. Binding follows the `HealthDisplay` pattern exactly
(HealthDisplay.cs:26-46): `SetRegistry` injection + Start/Update TryBind polling until the player
registers, unsubscribe in OnDestroy. Subscribes `PlayerHealth.OnDamagedInfo`.

- Serialized (+Tooltips): `EntityRegistrySO entityRegistry`; `GameObject indicatorPrefab`
  (a UI prefab asset: RectTransform + Image); `FloatReference ringRadius = 140f` (px from screen
  center); `FloatReference lifetime = 1.6f`; `FloatReference fadePortion = 0.6f` (last fraction of
  lifetime that fades); `IntReference slotCount = 6`.
- Awake: instantiate `slotCount` slots as children, pivot/anchor centered, disabled. Slot struct
  array: `{ RectTransform rect; Image image; Vector3 sourceWorld; float dieAt; bool active; }` —
  plain array, no per-event allocation.
- `OnDamagedInfo(in DamageInfo info)`: if `!info.HasSource` return. Claim a free slot (or the one
  with the earliest `dieAt`), set `sourceWorld = info.SourcePosition`, `dieAt = Time.time +
  lifetime`, enable.
- Update (every frame, only over active slots):
  - `flatToSource = (sourceWorld - playerPos)` with y zeroed; if near-zero, point down (damage from
    below reads as behind — acceptable).
  - `bearing = Vector3.SignedAngle(camForwardFlat, flatToSource, Vector3.up)` — bearing 0 = source
    dead ahead = indicator at the TOP of the ring.
  - Place: `rect.anchoredPosition = new Vector2(sin(bearing°), cos(bearing°)) * ringRadius;`
    `rect.localEulerAngles = (0, 0, -bearing)` so an arrow sprite authored pointing UP points
    outward at the source.
  - Alpha: 1 until the fade window, then linear to 0; at `dieAt`, disable the slot.
- If the camera child is missing, fall back to `PlayerTransform.forward` (flattened).
- All images `raycastTarget = false`.

### 2. New: `Assets/Editor/DamageIndicatorSetup.cs` helper + ImplementFix entry

Append to `Assets/Editor/ImplementFix.cs` (ADDITIVE; use the next free number — **055** if PLAN-003
took 053/054, otherwise renumber to the actual next; scan the file first per house rules):

- `055 - Add DamageIndicatorUI to the HUD canvas`:
  1. Find the HUD canvas via the `HUDController` in the open scene (editor-only find is allowed).
  2. Create `Assets/Prefabs/UI/DamageIndicator.prefab` if missing: a 24×48 RectTransform with an
     Image using the built-in `Arrow` sprite if resolvable, else the built-in `Knob`
     (`AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd")`), color white→transparent
     gradient not required — flat color (1, 0.25, 0.1, 1).
  3. Create child `DamageIndicators` (centered, size 0) under the canvas, add `DamageIndicatorUI`,
     wire `indicatorPrefab` + `entityRegistry` (find `EntityRegistry` asset via AssetDatabase) with
     SerializedObject.
  4. Bail with a dialog if no HUDController in the open scene.

## Conventions (CLAUDE.md — MUST hold)
Tooltips on every SerializeField; Float/IntReference for tuning; Canvas UI (no UI Toolkit); UI
reacts to events only (never queries managers); no `Camera.main`; no per-frame allocations (slot
array reused; SignedAngle is alloc-free); lifecycle methods private.

## Acceptance
- Compiles next Unity session.
- Paper-trace: missile hits player from the east while player faces north → indicator appears on
  the right of the ring; player turns to face east → indicator slides to top. Submersion damage
  (sourceless) → no indicator. 7 simultaneous events → oldest of 6 slots recycled, no allocation.

## Manual steps (for the dev, later)
- [ ] Run Tools > Blast Frame > Implement Fix > 055 (HUD indicators) in the scene with the HUD canvas.
- [ ] Play, take a turret hit from the side, confirm the arrow points at the turret and tracks as you turn.
- [ ] Tune ringRadius/lifetime on the DamageIndicators object to taste.

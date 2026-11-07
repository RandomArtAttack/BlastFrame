# Blender Mode for Unity

Make the Unity Scene view behave like Blender: modal transforms (G/R/S), Blender-style
navigation and numpad views, snapping, a 3D cursor, pie menus, an N-panel, camera bookmarks,
selection sets and align tools — all behind a single master toggle. Editor-only, no runtime code,
no third-party dependencies. Target: **Unity 6000.3**.

> Personal-use addon. If you drop real Blender icon PNGs into `Editor/Icons/` they are used;
> otherwise simple procedural icons are generated.

---

## Install

1. Copy the `com.raa.blendermode` folder into your project's `Packages/` directory.
2. Unity imports it as an embedded package (`com.raa.blendermode`, "Blender Mode").
3. Enable it: **Tools ▸ Blender Mode ▸ Enabled** (or the toolbar overlay button, or `Ctrl+Alt+B`).

When the master toggle is **off**, the addon consumes no input and Unity behaves 100% stock.

---

## Master toggle & suspension

- **Master toggle**: `Ctrl+Alt+B`, the `Tools/Blender Mode/Enabled` menu item, or the toolbar overlay.
- Input is suspended while in Play Mode (pref, default on), while editing a text field, and when the
  pointer is outside the focused Scene view.
- Every module can be disabled individually in **Preferences ▸ Blender Mode ▸ Modules**.

---

## Keymap reference

Bindings live in Blender Mode's own keymap (not Unity's ShortcutManager) and are fully rebindable in
Preferences. Numpad keys are distinct from the top row; turn on **Emulate Numpad** to fold the top-row
`1..0` onto the numpad view actions.

### Navigation

| Input | Action |
|---|---|
| MMB drag | Orbit |
| Shift+MMB | Pan |
| Ctrl+MMB | Zoom |
| Wheel | Dolly (zoom-to-mouse pref) |
| Alt+MMB | Snap to nearest axis view |
| Numpad 1 / Ctrl+1 | Front / Back |
| Numpad 3 / Ctrl+3 | Right / Left |
| Numpad 7 / Ctrl+7 | Top / Bottom |
| Numpad 5 | Toggle ortho/perspective |
| Numpad 9 | Flip view 180° |
| Numpad 4/6/8/2 | Orbit yaw/pitch ±15° |
| Ctrl+Numpad 4/6/8/2 | Pan |
| Numpad 0 | Look through Main Camera |
| Numpad `.` | Frame selected |
| Home | Frame all |
| `/` | Toggle local view (isolate) |
| Ctrl+Alt+Q | Quad view (stub) |

### Transforms

| Input | Action |
|---|---|
| G / R / S | Grab / Rotate / Scale (modal) |
| X / Y / Z | Axis constraint (again = local, third = clear) |
| Shift+X/Y/Z | Plane constraint |
| Ctrl (held) | Snap (see modifier table below) |
| Shift (held) | Precision (10× slower) when snapping is off |
| Ctrl+Shift | Fine snap (1/10 the increment) |
| type digits | Numeric entry (Tab = next component) |
| Enter / LMB | Confirm — Esc / RMB | Cancel |
| Alt+G / Alt+R / Alt+S | Clear position / rotation / scale |
| Shift+D / Alt+D | Duplicate / linked-duplicate (then grab) |
| Shift+R | Repeat last transform |
| F9 | Adjust-last popup |
| W / E / R (Unity tools) | Blender-aware Move / Rotate / Scale gizmos (honor pivot mode + snapping) |
| Alt+Z | X-Ray (toggle scene view wireframe and back) |

### Snapping & cursor

| Input | Action |
|---|---|
| Magnet toolbar button | Always-on snapping (Ctrl then disables) |
| Shift+RMB | Place 3D cursor on surface |
| Shift+C | Cursor to origin + frame all |
| Shift+S | Snap pie |

### Selection

| Input | Action |
|---|---|
| A / Alt+A / Ctrl+I | Select all / none / invert |
| B | Box select (Shift add, Ctrl subtract) |
| C | Circle select (LMB paint, MMB erase, wheel resize) |
| Ctrl+RMB drag | Lasso select |
| H / Shift+H / Alt+H | Hide selected / unselected / unhide all |
| X / Delete | Delete (confirm pref) |
| Ctrl+P / Alt+P | Parent / clear parent (keep world) |

### Pies, panel, bookmarks, sets

| Input | Action |
|---|---|
| Z | Shading pie |
| ` (backtick) | View pie |
| . (period) | Pivot-point pie |
| , (comma) | Orientation pie |
| N | Toggle N-panel |
| Ctrl+Alt+1..9 | Store camera bookmark |
| Alt+1..9 | Recall camera bookmark |
| Ctrl+Shift+1..9 | Store selection set |
| Shift+1..9 | Recall selection set (Emulate-Numpad off) |

---

## Feature walkthrough

**Navigation** — Blender turntable orbit/pan/zoom on the middle mouse button; numpad axis views animate;
`Alt+MMB` snaps to the nearest orthographic axis.

**Transforms + numeric input** — Press G/R/S and the selection follows the mouse. Constrain with
X/Y/Z (press again for local, a third time to clear); hold Shift for precision, Ctrl to snap. Start
typing to enter exact values — each component accepts a small math expression (`1+2`, `pi/4`, `(3*2)`),
Tab advances to the next component. Enter confirms, Esc/RMB reverts exactly.

**Snapping** — Modes: Increment, Grid Absolute, Vertex, Surface. The magnet button makes snapping
always-on (Ctrl temporarily disables it). All of this lives in the toolbar's **snapping popover** (see the
header section), including the **Snap Base** and **Align Rotation to Target** options below.

Blender-exact modifier behavior during G/R/S:

| State | Magnet OFF | Magnet ON | Effect |
|---|---|---|---|
| (no modifier) | free move | **snap** | Magnet-on snaps at the normal increment |
| **Ctrl** | **snap** | free move | Ctrl toggles snapping (on when off, off when on) |
| **Shift** (snap off) | **precision** | — | 10× slower, rebased so there is no jump on press/release |
| **Ctrl+Shift** (snap on) | **fine snap** | — | Snap at 1/10 the increment: move `inc/10`, rotate **1°**, scale **0.01** |

- **Increment** snaps the *movement delta* (exact multiples from the start); **Grid Absolute** snaps the
  *resulting world position* to the grid. **Rotate** snaps to 5° (fine 1°), **Scale** to 0.1 (fine 0.01).
- **Adaptive Increment** (pref, default on) makes the Grab increment scale with zoom, matching the visible
  scene grid; turn it off to use the fixed Move Increment. Rotate/Scale steps stay fixed.
- **Vertex / Surface** snapping jumps the selection straight to the target under the cursor while snapping
  is active (precision never dampens a snapped position); release Ctrl (magnet off) for smooth motion again.
  No snapping while numeric entry is active.
- **Snap Base** (Blender's *Snap With*) picks which point of the moving selection lands on the Vertex/Surface
  target, so the whole selection shifts by that same delta (relative offsets preserved):
  - **Closest** (default) — the point of the selection nearest the target (renderer bounds corners + centers).
  - **Center** — the current transform pivot point (resolved pivot mode).
  - **Median** — the median of the selection's object origins.
  - **Active** — the active object's origin (the pre-existing behavior).
- **Align Rotation to Target** — while a geometry snap with a known normal is active, each object's chosen
  align axis (pref, default +Y) rotates to the target normal — for **Surface** (face normal) *and* **Vertex**
  (mesh vertex normal). It reverts the instant the snap deactivates mid-drag. Works in both the G modal and
  the Blender Move gizmo.
- The HUD shows the effective state, e.g. `(snap 0.25)`, `(snap 0.025 fine)`, `(snap Surface, Closest base)`,
  or `(precision)`.

**Pivot point (Rotate / Scale)** — Median Point, Active Element, Individual Origins, 3D Cursor, Bounding
Box Center (cycle with the `.` pie or the toolbar dropdown). Rotate orbits each object's position about the
pivot and rotates it; Scale spans positions from the pivot; Individual Origins works in place. The active
pivot is drawn as an orange marker and named in the HUD during R/S. (For a single object centred on its
own origin, most modes coincide — that is expected.) Grab ignores the pivot setting entirely.

**Blender-aware gizmo tools** — When Blender Mode is on (and *Override Transform Gizmos*, default on),
selecting Unity's Move / Rotate / Scale tools (W/E/R or the toolbar) swaps in Blender Mode versions of the
gizmos. They draw the handle at the resolved **pivot point** (Median / Active / Individual Origins / 3D
Cursor / Bounding Box) and obey Blender Mode **snapping**: Increment snaps the delta (Ctrl / magnet
inversion, Shift = fine), Grid Absolute snaps world positions, and Vertex / Surface place the selection's
**Snap Base** point on the target under the cursor (others keep their offset; both align to the normal when
*Align Rotation to Target* is on). The three tools carry distinct Blender-style icons and tooltips showing
their keys (Move — G / gizmo W, Rotate — R / gizmo E, Scale — S / gizmo R). Turning the master toggle off
restores Unity's stock tools. Everything is a single undo step.

**Viewport header toolbar** — The Scene view overlay mirrors Blender's header: transform-orientation and
pivot-point dropdowns, the snap magnet + a **snapping popover**, and on the right a
gizmos toggle, an overlays toggle (hides grid + gizmos + the 3D cursor/HUD as a group), an **X-Ray**
(Alt+Z, wireframe) toggle, and four shading pills (Wireframe / Solid / Material Preview / Rendered) with a
shading options menu. The Z shading pie drives the same shared shading utility, so pie and pills agree.
The dropdowns are Blender-style: each row shows a 16px icon and label, the active row is highlighted
Blender-blue, hover highlights, and it closes on click / Esc / click-away.

Clicking the snap button opens the **snapping popover**: a **Snap Base** row (Closest / Center / Median /
Active icon buttons, active one highlighted blue), a **Snap Target** section (Increment / Grid Absolute /
Vertex / Surface), the **Align Rotation to Target** toggle with its align-axis selector, and the Affect
Move / Rotate / Scale toggles. Every toolbar control carries a tooltip that includes its Blender shortcut
where one exists — magnet *Snapping (Ctrl)*, orientation *(,)*, pivot *(.)*, X-Ray *(Alt+Z)*, shading pills
*(Z pie)*, and the three transform tools show their G/R/S keys.

**3D cursor** — A per-scene position/rotation drawn as a red/white dashed circle with RGB axes. Place
it with `Shift+RMB`, reset with `Shift+C`, and use the `Shift+S` snap pie for selection↔cursor↔grid ops.
The cursor is usable as a transform pivot.

**Pies** — Radial menus with the Blender press-drag-release gesture (and click). Z shading, ` view,
`.` pivot point, `,` orientation, `Shift+S` snap.

**N-panel** — Toggle with N. Tabs:
- **Item**: multi-edit position/rotation/scale (math expressions), world dimensions readout.
- **View**: 3D cursor fields, camera bookmark slots (store/recall/rename).
- **Tool**: full snap settings and increments.
- **Sets**: store / recall / add / remove / rename / delete selection sets.
- **Align**: align/distribute/drop tools.

**Camera bookmarks** — Store the current view (pivot/rotation/size/ortho) to a slot with
`Ctrl+Alt+1..9`, recall with an animated transition via `Alt+1..9`. Persisted per scene.

**Selection sets** — Named per-scene sets stored as `GlobalObjectId`s (survive reloads; stale ids
skipped). `Ctrl+Shift+1..9` store, `Shift+1..9` recall, plus full management in the N-panel Sets tab.

**Align tools** — Align min/center/max per world axis, distribute evenly along an axis, Drop to Surface
(raycast down, optional align-to-normal) and Drop to Floor (bounds bottom to y=0). Also under
**Tools ▸ Blender Mode ▸ Align**.

---

## Real Blender icons (optional, personal use)

The toolbar and pies call `IconLibrary.Get(name)`. Drop a PNG named `<name>.png` into
`Editor/Icons/` and it is used instead of the procedural fallback. Recognized names include:
`orientation`, `pivot`, `cursor`, `magnet`, `snap_increment`, `snap_vertex`, `snap_face`, `snap_grid`,
`gizmo`, `overlays`, `xray`, `shade_wire`, `shade_solid`, `shade_material`,
`shade_rendered`, `grid`, `camera`, `eye`, `axisX`, `axisY`, `axisZ`. Dropdown per-value icons:
`orient_global`, `orient_local`, `orient_view`, `orient_normal`, `orient_custom`, `pivot_median`,
`pivot_active`, `pivot_individual`, `pivot_bounds`. Snapping popover + tool icons:
`snap_base_closest`, `snap_base_center`, `snap_base_median`, `snap_base_active`,
`tool_move`, `tool_rotate`, `tool_scale`.
Otherwise anti-aliased procedural fallbacks are drawn to match Blender's header glyphs.
Blender's own icons are GPL — fine for personal use; do not redistribute them with this package.

---

## Preferences overview

**Preferences ▸ Blender Mode**:

- **General** — master toggle, suspend-in-play, emulate numpad.
- **Navigation** — orbit style, orbit-around-selection, zoom-to-mouse, invert zoom, sensitivity, auto-ortho.
- **Modules** — enable/disable Navigation, Transforms, Selection, Pies, Cursor, Overlays, Bookmarks.
- **Transforms** — increments, adaptive increment, precision factor, adjust-last popup, override transform gizmos.
- **Snapping** — default mode, snap base, magnet, grid size, affect toggles, align rotation + align axis.
- **3D Cursor** — orient-to-surface on placement.
- **Pies** — deadzone radius, confirm mode.
- **Selection** — confirm-before-delete, drop-aligns-to-normal.
- **Keymap** — full rebind list (click *Rebind*, then press a key), JSON export/import, reset to defaults.

---

## Known limitations

- **Quad view** is a stub — Unity exposes no public 4-viewport API, so `Ctrl+Alt+Q` only notifies.
- **Trackball orbit** is deferred; turntable is used even when Trackball is selected.
- **World-scale on rotated objects** uses a basis approximation (non-uniform world scale can shear).
- **Vertex/surface snapping** reads readable meshes only; non-readable meshes are skipped.
- Selection changes made by the selection tools are not part of Blender Mode's own undo grouping.

---

## Changes

- Unity's Move / Rotate / Scale gizmo tools are now overridden with Blender-aware versions that honor the
  pivot-point setting and Blender Mode snapping (pref *Override Transform Gizmos*, default on).
- Removed **proportional editing** (the `O` toggle, falloff modes, toolbar/Tool-tab controls, and radius
  wheel resizing) — it is no longer part of Blender Mode.

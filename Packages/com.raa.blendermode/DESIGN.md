# Blender Mode for Unity — Design Document

Personal-use Unity editor addon that makes the Scene view behave like Blender: modal transforms (G/R/S), Blender navigation, numpad views, snapping, 3D cursor, pie menus, and productivity extras. Master toggle on/off. Target: Unity 6000.3.10f1, editor-only, no runtime code.

## 1. Package layout

```
Packages/com.raa.blendermode/
  package.json                 # name com.raa.blendermode, displayName "Blender Mode", version 0.1.0
  DESIGN.md                    # this file
  README.md                    # user manual (written in Phase 3)
  Editor/
    RAA.BlenderMode.Editor.asmdef   # editor-only, no engine asmdef needed
    Core/
      BlenderModeManager.cs    # [InitializeOnLoad] entry point, master toggle, event pump
      InputRouter.cs           # priority dispatch of SceneView events to modules
      Keymap.cs                # action->binding table, JSON import/export
      ModalOperator.cs         # base class for modal ops (grab, box select, pie, ...)
      NumericInput.cs          # Blender-style numeric entry + math expression eval
      BlenderModePrefs.cs      # all preferences (EditorPrefs-backed) + SettingsProvider
      SceneStateStore.cs       # ScriptableSingleton persisted per-project; per-scene data keyed by scene GUID (bookmarks, selection sets, cursor)
      UndoScope.cs             # helpers: one undo group per confirmed operation; cancel = restore cached TRS silently
      Icons.cs                 # IconLibrary: load Editor/Icons/*.png by name, else procedural fallback textures
      Hud.cs                   # viewport header/footer text during modals (Handles.BeginGUI)
    Navigation/
      ViewNavigation.cs        # orbit/pan/zoom on MMB, wheel, alt-snap orbit
      ViewCommands.cs          # numpad views, frame, ortho toggle, isolate, camera view, quad view
    Transform/
      TransformModal.cs        # shared modal state: pivot, orientation, constraints, snapping, numeric
      GrabModal.cs / RotateModal.cs / ScaleModal.cs
      TransformOps.cs          # clear (Alt+G/R/S), duplicate+grab, repeat last, adjust-last popup
      PivotOrientation.cs      # pivot-point + orientation settings incl. custom orientations
    Snapping/
      SnapSettings.cs          # mode, increments, align-rotation-to-normal
      SnapEngine.cs            # increment/grid/vertex/surface queries (HandleUtility.RaySnap + reflection IntersectRayMesh fallback)
    Cursor/
      Cursor3D.cs              # state + drawing (Blender-style red/white dashed circle + axes)
      SnapPie.cs               # Shift+S pie actions
    Selection/
      SelectTools.cs           # A / Alt+A / Ctrl+I / click behavior
      BoxSelectModal.cs / CircleSelectModal.cs / LassoSelectModal.cs
      VisibilityOps.cs         # H / Shift+H / Alt+H / '/' local view via SceneVisibilityManager
      ObjectOps.cs             # delete, duplicate (Shift+D / Alt+D), parent (Ctrl+P / Alt+P)
      SelectionSets.cs         # named sets, per-scene persistence via GlobalObjectId
    Pies/
      PieMenu.cs               # generic radial IMGUI pie (8 slots, gesture select)
      Pies.cs                  # Z shading pie, ` view pie, . pivot pie, , orientation pie
    OverlaysUI/
      BlenderToolbarOverlay.cs # SceneView Overlay: master toggle, magnet+snap mode, pivot, orientation
      NPanelOverlay.cs         # N-key panel: Item / View / Tool / Sets / Align tabs
    Tools/
      AlignTools.cs            # align min/center/max per axis, distribute, drop-to-surface/floor
      CameraBookmarks.cs       # store/recall views
```

## 2. Core architecture

### BlenderModeManager
- `[InitializeOnLoad]` static class. Subscribes `SceneView.duringSceneGui`.
- Master toggle: `BlenderModePrefs.Enabled` (EditorPrefs bool), toggled from menu `Tools/Blender Mode/Enabled` (checked menu item), the toolbar overlay button, and shortcut Ctrl+Alt+B. When off, the addon consumes **nothing** — Unity is 100% stock.
- Suspend conditions (input ignored even when enabled): `EditorApplication.isPlaying` && prefs.SuspendInPlayMode (default true); `EditorGUIUtility.editingTextField`; event not for the focused SceneView; modal-less keyboard events only when mouse is over the SceneView rect.

### InputRouter
- Per SceneView call order: (1) active ModalOperator gets every event exclusively; (2) otherwise Keymap lookup → start operator or run instant command; (3) navigation module (MMB/scroll handling).
- Events consumed via `Event.current.Use()`; during modals also claim `GUIUtility.hotControl` with a passive control id so Unity tools/gizmos don't fight us.
- **Do not use ShortcutManager.** All bindings live in our own Keymap (context-sensitive, modal keys, numpad distinction). Keymap = serializable list of `{actionId, keyCode, ctrl, shift, alt, mouseButton}` with JSON export/import in prefs UI. Defaults below.

### ModalOperator
- `OnStart(ctx)`, `HandleEvent(Event)`, `Confirm()`, `Cancel()`, `DrawHandles()`, `HudText()`.
- One active at a time (stored on manager). Confirm on LMB or Enter/NumpadEnter; cancel on RMB or Esc (full state revert). Repaint all scene views every event while active.
- Undo: cache initial transforms at start; `Undo.RecordObjects` + `Undo.SetCurrentGroupName` and collapse on confirm; on cancel restore cached TRS directly (no undo entry).

### NumericInput
- Activated inside transform modals by typing digits/`-`/`.`/`/`; backspace deletes; supports simple expressions (`+ - * /`, parentheses) evaluated with a tiny recursive-descent parser (no `DataTable.Compute`).
- Multi-component: Tab advances to next axis component (like Blender), `=` optional. Displayed in HUD.

## 3. Navigation

Turntable orbit (trackball = pref option, may ship later; default turntable).

| Input | Action |
|---|---|
| MMB drag | Orbit around pivot (pref: orbit-around-selection → pivot = selection center on press) |
| Shift+MMB drag | Pan |
| Ctrl+MMB drag | Zoom (vertical drag) |
| Wheel | Dolly zoom; pref zoom-to-mouse-position (default on) shifts pivot toward cursor ray |
| Alt+MMB drag | Snap view to nearest orthogonal axis view |

Implementation: consume the mouse events and drive `sceneView.pivot/rotation/size/orthographic` directly. Never let Unity's own MMB-pan fire while enabled. Leave Unity's native Alt+LMB orbit and RMB-fly untouched (bonus, not Blender, but harmless).

### View commands (numpad; pref "Emulate Numpad" maps top-row 1..0 to the same actions)

| Key | View (SceneView rotation, Unity axes) |
|---|---|
| Num1 | Front: Euler(0,0,0) — looking along +Z |
| Ctrl+Num1 | Back: Euler(0,180,0) |
| Num3 | Right: Euler(0,-90,0) — looking along −X |
| Ctrl+Num3 | Left: Euler(0,90,0) |
| Num7 | Top: Euler(90,0,0) |
| Ctrl+Num7 | Bottom: Euler(-90,0,0) |
| Num5 | Toggle ortho/perspective |
| Num9 | Flip view 180° around up |
| Num4 / Num6 | Orbit yaw −/+15° |
| Num8 / Num2 | Orbit pitch +/−15° |
| Ctrl+Num4/6/8/2 | Pan left/right/up/down |
| Num0 | Look through Main Camera (align scene view to it) |
| NumPeriod | Frame selected |
| Home | Frame all |
| `/` (both) | Toggle local view (isolate selection) via `SceneVisibilityManager.instance.Isolate` / `ExitIsolation` |
| Ctrl+Alt+Q | Quad view: toggle a saved 4-SceneView layout (best-effort; single-window restore on toggle off) |

Axis-view transitions animate with `sceneView.LookAt(pivot, rot, size, ortho)` (it lerps). Entering 1/3/7 also switches to ortho (Blender behavior, pref-controlled).

## 4. Transform modals (the heart — get this exact)

**G / R / S** start Grab/Rotate/Scale on current selection (ignore if selection empty or contains only assets). Blender parity:

- Object follows mouse immediately, no gizmo needed.
- **Confirm**: LMB or Enter. **Cancel**: RMB or Esc → exact revert.
- **Axis constraint**: X/Y/Z press = constrain to that axis in current orientation; same key again = local orientation; third press = unconstrained. Shift+X/Y/Z = plane constraint (exclude axis). MMB drag = auto-pick nearest axis to mouse motion.
- **Ctrl held** = snapping active (per SnapSettings; increment mode snaps the delta, surface/vertex modes snap the object under the mouse). **Shift held** = precision (delta ×0.1 while held, Blender-style rebasing so no jump). Ctrl+Shift = fine increment (×0.1 increment).
- **Numeric input**: any digit/`-`/`.` starts typed-value mode; Tab cycles components; HUD shows e.g. `D: 2.5, 0, 0 (2.5)`. Enter confirms.
- **Grab math**: mouse delta mapped through the camera plane at the pivot depth; constrained → project onto axis/plane through pivot.
- **Rotate**: default = around view axis through pivot; angle = angular sweep of mouse around pivot's screen position. Axis-constrained rotates around that axis. Pressing **R again** during rotate = trackball mode (stretch goal, OK to defer).
- **Scale**: ratio = current mouse distance to pivot screen pos ÷ distance at start. Axis/plane constraints scale only those components.
- **Pivot point** (affects R and S; setting cycled via `.` pie): Median Point (default), Active Element, Individual Origins (each object rotates/scales around its own pivot), 3D Cursor, Bounding Box Center.
  - Rotate orbits each object's position about the resolved pivot (`pos' = pivot + R·(pos−pivot)`) **and** rotates its orientation; Scale spans positions from the pivot (`pos' = pivot + s∘(pos−pivot)`) and scales `localScale`. **Individual Origins** operates each object in place (no position change). The resolved pivot(s) are drawn as an orange marker during R/S, and the active pivot mode is shown in the HUD, so mode changes are always visible (for a single object at its mesh centre most modes coincide — that is correct Blender behaviour, not a bug).
  - **Grab** never uses the pivot-point setting for its motion: the drag plane is anchored at the selection **median** depth (`MotionRef`), so choosing a distant 3D-cursor / Active pivot does not distort the mouse→world translation scale. Only R/S consume the pivot.
- **Orientation** (`,` pie): Global (default), Local (active object's), View, Normal (active object's rotation for object mode), Custom (user-saved from active object; stored in SceneStateStore, manageable in prefs/N-panel).
- HUD footer during modal, Blender header style: `Dx: 1.0240 along global X  (snap 0.25)  [numeric: 2.5]`.

### Related ops
- **Alt+G / Alt+R / Alt+S**: clear local position / rotation / scale (undoable).
- **Shift+D**: duplicate (Unity's `Duplicate` command so prefab instances stay instances) then immediately enter Grab. **Alt+D**: same but force-link — if source is a prefab instance, instantiate the same prefab; else plain duplicate.
- **Shift+R**: repeat last confirmed transform (store op type + final delta/orientation; reapply to current selection).
- **F9 / adjust-last**: after confirming, small floating IMGUI window (bottom-left) with the operation's values; editing re-applies from the pre-op state. Dismiss on click-away.

## 5. Snapping

`SnapSettings` (shown in the toolbar snapping popover + N-panel Tool tab + prefs):
- **Modes** (Snap Target): Increment (default; move 0.25u / rotate 5° / scale 0.1 — all pref-editable), Grid Absolute (snap world position to multiples), Vertex, Surface (face). Edge = out of scope.
- **Snap Base** (Blender's *Snap With*, EditorPrefs `Snap.Base`, default **Closest**): which reference point of the moving selection lands on a geometry (Vertex/Surface) target. **Closest** = the candidate point of the selection nearest the target (candidates = each object's `Renderer.bounds` 8 corners + center, or the transform position when it has no renderer; cached once at drag start and shifted by the running snapped delta, then the nearest is chosen per event). **Center** = the resolved transform pivot point. **Median** = the selection origins median. **Active** = the active object's origin. The move delta is `target − basePoint(start)`; every selected object moves by that same delta (relative offsets preserved), and the base point drives the HUD readout. This generalizes the earlier "land the active origin on the target" behavior.
- **Affect**: which of move/rotate/scale snapping applies to (toggle set like Blender).
- **Align Rotation to Target**: while a geometry snap with a known normal is active, rotate each object's chosen axis (pref, default +Y) to the target normal — **Surface** (face normal) *and* **Vertex** (mesh vertex normal). Applied as a delta from the object's drag-start rotation (`FromToRotation(startRot·axis, normal)·startRot`) so it reverts cleanly when the snap deactivates mid-drag. Active in both the Grab modal and the Blender Move gizmo.
- **Snap toggle**: magnet button = snapping always-on (then Ctrl temporarily disables — exact Blender inversion).

`SnapEngine`:
- Surface: `HandleUtility.PlaceObject` (Unity 6 API, handles meshes without colliders); fallback `HandleUtility.RaySnap`. Exclude the objects being transformed (move them to a temp hidden state or filter via `HandleUtility.ignoreRaySnapObjects`).
- Vertex: object under mouse → nearest mesh vertex to cursor within 50px screen radius (read via `MeshFilter.sharedMesh`, cache per-drag; transform to world; screen-distance compare). Also returns the world vertex normal (cached mesh normals) for align-rotation. The chosen **Snap Base** point (default Closest) lands on the target vertex.
- Snap base: `BuildClosestCandidates` (per-drag corner/center cache) + `ResolveGeometryBasePoint` compute the base point for the four modes; shared by the Grab modal (`TransformModal.GeoBasePoint`) and `BlenderMoveTool`.

### Exact modifier semantics (Blender parity — the "feel")

Resolved once per event in `TransformModal` from the live Ctrl/Shift state and the per-op Affect flag:

- **Snap activation** (`SnapActive`): magnet **off** → snapping active only while **Ctrl** is held; magnet **on** → snapping always active, **Ctrl temporarily disables** it (`SnapSettings.IsActive(ctrl) = magnet ? !ctrl : ctrl`). Gated off entirely while numeric input is engaged (numeric wins).
- **Shift = precision, not snap** (`Precise = Shift && !SnapActive`): motion is 10× slower (pref `Precision Factor`, default 0.1), integrated **per mouse-move** (`PrecisionAxis`: `value += rawDelta·(precise?factor:1)`), so pressing/releasing Shift mid-drag **never jumps** — only the incremental motion is ever scaled, never the accumulated total.
- **Ctrl+Shift = fine snap** (`FineSnap = SnapActive && Shift`): snap at 1/10 the increment — move `increment/10`, **rotate 1°** (vs 5°), **scale 0.01** (vs 0.1). Because precision only applies when snapping is *inactive*, a snapped position is never dampened — **snap always wins** (vertex/surface likewise jump straight to the target under the raw mouse).
- **Increment vs Grid Absolute**: Increment snaps the **delta** (movement in exact multiples from the start position, expressed in the constraint basis); Grid Absolute snaps the **resulting world position** to grid multiples per object.
- **Adaptive Increment** (pref, default **on**): the Grab move increment tracks zoom like Blender — `SnapEngine.AdaptiveStep(sceneView.size)` = `pow(10, floor(log10(size·K)))` (K = 0.1, tuned to the visible Unity grid subdivision), clamped to `0.001..1000`. When off, the fixed `IncrementMove` pref is used. Rotate (5°/1°) and Scale (0.1/0.01) use fixed steps.
- The HUD shows the effective state: `(snap 0.25)`, `(snap 0.025 fine)`, or `(precision)`.

## 6. 3D Cursor

- State: world position + rotation, per-scene in SceneStateStore. Drawn always-on-top with Handles: red/white dashed circle + small RGB axes.
- **Shift+RMB click**: place cursor at surface hit under mouse (with normal orientation if pref set), else on the camera-facing plane through the current pivot.
- **Shift+C**: cursor → world origin + frame all.
- **Shift+S pie**: Selection→Cursor, Selection→Grid, Selection→Cursor (Keep Offset), Cursor→Selected, Cursor→World Origin, Cursor→Grid, Cursor→Active.
- Cursor is usable as pivot point and as spawn point: new GameObjects created via our duplicate ops don't use it, but an "Add at Cursor" menu isn't needed — keep scope tight.

## 7. Selection

| Input | Action |
|---|---|
| LMB click | Select (stock Unity click-through preserved); Shift+click toggle-add; click empty = deselect all. Alt+click ignored (Unity orbit). Last clicked = active object |
| A | Select all (scene objects, respects visibility) |
| Alt+A | Deselect all |
| Ctrl+I | Invert selection |
| B | Box select modal: crosshair lines + drag rect; `HandleUtility.PickRectObjects`; Shift during = add, Ctrl = subtract |
| C | Circle select modal: circle at cursor (wheel resizes), LMB drag paints select, MMB drag deselects, RMB/Esc exits. Approximate membership: object screen-space bounds center within circle |
| Ctrl+RMB drag | Lasso select (polygon contains object's screen-space pivot) |
| H | Hide selected (`SceneVisibilityManager.Hide`) |
| Shift+H | Hide unselected |
| Alt+H | Unhide all |
| X or Delete | Delete selected (pref: instant vs. tiny confirm popup, default instant, undoable) |
| Ctrl+P | Parent selected under active (keep world transform), undoable |
| Alt+P | Clear parent, keep world transform |

Active object gets its name shown briefly in the HUD corner (Blender shows it in the header). Don't attempt custom outline colors — Unity's orange outline stays.

## 8. Pie menus

`PieMenu` — generic radial IMGUI menu rendered in an `Overlay`-independent `Handles.BeginGUI` pass (or a borderless popup window if scene-view clipping is a problem; try in-view first):
- 4/8 slots at compass directions, 20px deadzone, item under mouse direction highlights, **release-confirm** (press key, drag, release = pick — Blender gesture) *and* click-confirm both work. Esc/RMB cancels. Each slot: icon + label.

Pies shipped:
- **Z** — shading: Wireframe, Shaded, Shaded Wireframe, Unlit (maps to `sceneView.cameraMode` / `sceneView.sceneLighting` combos), plus Lighting toggle slot.
- **`** (backtick) — view: Front/Back/Left/Right/Top/Bottom/Frame Selected/Camera.
- **.** (keyboard period, NOT numpad) — pivot point pie.
- **,** (keyboard comma) — orientation pie.
- **Shift+S** — snap pie (section 6).

## 9. Overlays (Unity Overlays API)

- **BlenderToolbarOverlay** (`[Overlay(typeof(SceneView), ...)]`, horizontal toolbar, default docked top): master on/off, magnet toggle + snap-mode dropdown, pivot dropdown, orientation dropdown, cursor→origin button. Icons from IconLibrary.
- **NPanelOverlay**: panel overlay toggled by **N** key (show/hide the overlay instance). Tabs:
  - **Item**: active object position/rotation/scale fields (multi-edit aware, math expressions allowed via NumericInput parser), world dimensions readout.
  - **View**: 3D cursor position/rotation fields, view lock-to-object (stretch), FOV.
  - **Tool**: full SnapSettings, increments.
  - **Sets**: selection sets list — store current, recall, add-to, remove, rename, delete.
  - **Align**: align tools (below).

## 10. Productivity extras

- **Camera bookmarks** (`CameraBookmarks`): Ctrl+Alt+1..9 (top row) store current view (pivot/rotation/size/ortho); Alt+1..9 recall (animated LookAt). Per-scene persistence. Listed in View tab with names.
- **Selection sets**: per-scene named sets stored as `GlobalObjectId` lists (survive domain reload/scene reopen; stale ids skipped). Shortcuts: Ctrl+Shift+1..9 store set N, Shift+1..9 recall (only when Emulate Numpad is OFF for 1..9 conflict-free; otherwise use the Sets tab — detect and warn in prefs UI).
- **Align/distribute/drop** (`AlignTools`, Align tab + `Tools/Blender Mode/Align` menu): align min/center/max per world axis; distribute evenly along axis; **Drop to Surface** (raycast down from each object, optional align-to-normal); Drop keeps undo. 
- **Quad view** best-effort as in §3.
- **Repeat + adjust-last** as in §4.

## 11. Preferences (SettingsProvider, "Preferences/Blender Mode")

Groups: General (master, suspend-in-play, emulate numpad), Navigation (orbit style, orbit-around-selection, zoom-to-mouse, invert zoom, orbit sensitivity, auto-ortho on axis views), Transforms (increments, precision factor, adjust-last popup on/off), Snapping (defaults, align axis), Pies (deadzone radius, confirm mode), Selection (delete confirm), Keymap (full rebind list UI + JSON export/import + reset), Modules (per-module enable checkboxes: Navigation, Transforms, Selection, Pies, Cursor, Overlays, Bookmarks). Everything default-on.

## 12. Icons

`IconLibrary.Get(name)`: look for `Editor/Icons/{name}.png` (user drops in Blender icon extractions here — personal use, GPL fine); if missing, generate a procedural fallback `Texture2D` at 32px (drawn pixel-by-pixel: magnet, axis letters, cursor crosshair, pivot dot, grid, camera, eye, etc. — simple flat shapes in Blender's #DDD-on-transparent style), cached. Both tinted for light/dark skin. README documents how to export real Blender icons later.

## 13. Constraints & gotchas for implementers

- IMGUI event handling only in `duringSceneGui`; always `sceneView.Repaint()` while modal.
- Distinguish `KeyCode.Keypad1` vs `Alpha1`, `KeypadPeriod` vs `Period`.
- Never intercept when `Event.current.type == EventType.Used`, when editing text fields, or when master/module disabled.
- All object mutation undoable except modal-cancel restore.
- Multi-scene editing: operate on all loaded scenes' objects; skip non-editable (locked by `hideFlags`, in prefab-stage handled: prefab stage works like a scene, keep it working).
- No runtime assemblies, no dependencies beyond built-in modules. Everything compiles with only `UnityEditor`/`UnityEngine`.
- Performance: no per-frame allocation storms in `duringSceneGui`; cache vertex arrays per drag, not per event.

## 14. Implementation phases

1. **Phase 1 — Foundation + Navigation**: package.json, asmdef, Core/ (manager, router, keymap+defaults, prefs+SettingsProvider skeleton, modal base, HUD, icons, undo scope, scene store), Navigation/ complete (§3), menu items, master toggle. Deliverable: navigation fully Blender-like, toggleable.
2. **Phase 2 — Transforms + Snapping + Cursor**: §4, §5, §6 complete, incl. numeric input, adjust-last, repeat, snap pie (requires PieMenu — build the generic PieMenu class in this phase).
3. **Phase 3 — Selection + Pies + Overlays + Extras + Polish**: §7–§11 complete, remaining pies, N-panel, toolbar overlay, bookmarks, selection sets, align tools, quad view, prefs UI complete with keymap editor, README.md user manual, final compile verification.

Each phase must leave the package compiling. Verify by running Unity batchmode compile if the project isn't open (`Unity.exe -batchmode -quit -projectPath <proj> -logFile <log>`), else by rigorous static review.

## 15. Viewport Header Parity

Replicates Blender's 3D-viewport header bar (`BlenderToolbarOverlay`, rebuilt) plus the view feature it exposes.

### Right-side view toggles (`ShadingModes`, Navigation/)
- **Gizmos** — maps to `sceneView.drawGizmos`.
- **Overlays** — one grouped bool (`BlenderModePrefs.OverlaysVisible`) that hides/shows the scene grid (`showGrid`), gizmos (`drawGizmos`) and Blender Mode's own overlays (3D cursor drawing + modal HUD, which check the pref). Remembers prior grid/gizmo state to restore exactly.
- **X-Ray** (keymap `view.xray`, default **Alt+Z**) — no true x-ray in Unity, so it toggles the scene view into `DrawCameraMode.Wireframe` and back, remembering the prior camera mode.
- **Shading pills** — Wireframe / Solid / Material Preview / Rendered mapped onto camera mode + `sceneLighting` + `sceneViewState` (skybox/fog/flares/post). Active pill highlighted Blender-blue (#4772B3). The **Z** shading pie and the pills both call the shared `ShadingModes` utility so they stay in sync.

### Toolbar layout (`BlenderToolbarOverlay`)
`[BM master] | [orientation ▾][pivot ▾] | [magnet][snap-mode ▾] | spacer | [gizmos][overlays][x-ray] | [wire][solid][material][rendered][▾ shading menu]`. Compact IMGUI row; live-syncs on `BlenderModePrefs.Changed`.

Dropdowns use **`BlenderDropdown`** (a `PopupWindowContent`), not `GenericMenu`, so each row can show a 16px icon: `[icon] [label]`, dark background, active row highlighted Blender-blue (#4772B3), hovered row lightened; closes on click / Esc / click-away. Used for the orientation, pivot-point, snap-mode and shading-options dropdowns. The button *faces* also show the active value's icon. (N-panel Tool-tab enum rows stay standard `EditorGUILayout` popups.)

### Icons (`IconLibrary`)
Real Blender PNGs still win if dropped into `Editor/Icons/` (override by logical name). The procedural fallbacks are 4× supersampled, box-downsampled anti-aliased glyphs matching the header. The name→glyph mapping is documented inline in the `Generate` switch in `Icons.cs`. Names:
- Header: `orientation` globe, `pivot` dot-with-arrows, `cursor` red/white 3D cursor, `magnet`, `snap_increment`/`snap_vertex`/`snap_face`/`snap_grid`, `gizmo` RGB, `overlays` stacked circles, `xray` corner-brackets, `shade_wire`/`shade_solid`/`shade_material`/`shade_rendered`, `grid`, `camera`, `eye`, `axisX/Y/Z`.
- Dropdown per-value rows: orientations `orient_global` (globe) / `orient_local` (box+axes) / `orient_view` (monitor) / `orient_normal` (face+normal) / `orient_custom` (bookmark+plus); pivots `pivot_median` (dot between dots) / `pivot_active` (outlined dot) / `pivot_individual` (multiple dots) / `pivot_bounds` (dashed box+dot) / `cursor` (3D cursor).

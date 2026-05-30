# Blast Frame — Session Handoff

_Last updated: 2026-05-30_

Status snapshot. Read this first when returning. Architecture detail is in `DESIGN.md`; the transient
needle is `LAST_LEFT_OFF.txt`.

---

## ✅ Done this session (2026-05-30)

Design pass — **no game code written** (and none pre-existed; see finding below).

- **Deleted** the misplaced nested Unity project `Blood and Bark/` from the Blast Frame root
  (empty URP template, untracked in git, ~2GB Library cache — no work lost).
- **Created `DESIGN.md`** (repo root) — canonical architecture spec + ordered build checklist (Milestones A–F).
- **Created this `HANDOFF.md`.**
- **Updated `CLAUDE.md`:** added a pointer making DESIGN.md the architecture source of truth, and
  reconciled the decisions below (area structure, save shape, run powerups).

### ⚠️ Key finding — documented code does not exist
CLAUDE.md and `LAST_LEFT_OFF.txt` describe built movement/combat (PlayerMotor, dash, Fixes 011–013,
Level01), but **none of it is present**: `Assets/` has only URP-template files, git is a single initial
commit, and only `SampleScene.unity` exists. Treat the project as **greenfield with a rich written
spec**. The detailed movement design in CLAUDE.md is the canonical "how we have it" to build toward.
> If you believe real code existed elsewhere (another machine/branch), flag it — otherwise greenfield.

### Decisions locked (detail in DESIGN.md)
- **Area structure:** Mega Man **weakness-web** (some areas open at start; beating one unlocks AND
  alters others) — replaces the old linear `unlockedLevelIndex`.
- **Cross-area effects (#7):** declarative effect graph — `AreaDefinitionSO.onComplete → typed
  effects[]`, applied on area enter, derived from a saved **completed-area set**.
- **Variant system (#1):** one unified **seeded weighted-roll** (`VariantSetSO`) for room layout /
  spawn / enemy variant; enemy variant = `VariantProfileSO` applied to one base prefab in `OnSpawn`.
- **Seeding:** single `runSeed` + `SeedService` deterministic derivation (resumable/shareable runs).
- **Weapons (#5):** **cycle-all, UNLIMITED, trade-off-balanced FPS types — NO energy/ammo** (identity
  via fire rate/damage/spread/range/charge); weakness-web = effectiveness, not availability. Abilities
  are separate boss equips, owned forever.
- **Charge-shot:** **discrete 3 tiers** (tap Lv0 + Lv1/2/3), AoE on upper tiers, tick cue per tier.
- **Run powerups (#6):** hybrid — Minor pickups auto-apply, Major powerups are a **1-of-3 draft** at
  room-clear/chest (`PowerupSO.tier`).
- **Movement (locked 2026-05-30, DESIGN.md §4):** single jump base (double-jump = unlockable ability);
  full-hold jump = **4 units** with gravity tuned up; **snappy/near-instant** ground (momentum only via
  dash/air); dash = ground-only, **movement-input direction, horizontal, locked, jump-cancellable**;
  dash-jump **maintains full dash speed, steerable, resumes on land**; wall slide = **press-toward**;
  wall jump = up + **STRONG away** + ~0.45s steer-lock so **chimney-jumping (not single-wall climb)** is
  the vertical mechanic. Open tuning: dash distance ~11m — confirm in prototype.
- **Charge-shot, hub loop, save/AES, pooling, services:** as specified in CLAUDE.md.

---

## 🔧 Manual steps outstanding

- [ ] (None in the editor yet — no code to wire. First build work is Milestone A in DESIGN.md §14.)
- [ ] Decide the open movement tuning question when prototyping: dash distance ~11m at speed 15.4 —
      keep, or drop speed to ~11 m/s to hold ~8m (carried over from LAST_LEFT_OFF.txt).

---

## ▶️ Next up (first code)

Per DESIGN.md §14, **Milestone A — Core Framework**, one item at a time in a test scene
(ServiceLocator → Events → Variable SOs → Pool → EntityRegistry → SeedService/VariantSetSO → state/scene).
Then Milestone B (Player movement) — the system you most want to get feeling right.

# Blast Frame tests — index

All tests are **EditMode** (Test Runner → EditMode tab). None enter play mode: movement tests drive the REAL
PlayerController/PlayerMotor/modules through `MotorSimRig` (Assets/Editor/LevelDesign) with `Physics.Simulate`
inside an empty scene, and restore your open scenes afterwards (save first — the runner prompts).

Run them headless from a terminal (worktree or main project, editor CLOSED on that project):

    Unity.exe -batchmode -nographics -projectPath <project> -runTests -testPlatform EditMode -testResults results.xml [-testCategory "!Report;!Slow"]

| Suite | Owns | Speed |
|---|---|---|
| `Movement.MovementRuleTests` | Glossary movement rulings: held dash, coyote, auto wall-jump, dash wall-jump speed, dash-jump air control, step-up, variable jump, gravity/apex | ~5 s |
| `Movement.MovementReachTests` | Rig sanity + reach budgets (standing jump ≥ 7, dash jump ≥ 10). `Report_FullReachProfile` [Slow] re-measures everything and refreshes the cached profile | 1–2 min |
| `Movement.ReachabilityTests` | Reachability analyzer on synthetic courses (gap classes, ledge height, step-up, markers, hazards) | ~10 s (+ measure if profile stale) |
| `Movement.LevelContentReportTests` [Report] | Lints over REAL content: room-variant data errors, per-scene reachability summaries (logged) | ~15 s |
| `Invariants.PoolTests` | Pool<T>: prewarm, marker, hooks, expansion, double-return guard | <1 s |
| `Invariants.HealthTests` | PlayerHealth: start 5, clamp, single death, i-frames, heal clamp, DamageInfo | <1 s |
| `Invariants.RoomSelectorTests` | Seeded variant pick: deterministic, in range at extreme seeds, null-safe | <1 s |
| `Invariants.ArcTurretTests` | Arc turret: launch angle always 45–90°, shot leaves along the barrel at ProjectileSpeed, no inherited velocity | <1 s |
| `Invariants.ProjectDataTests` | PoolIds ↔ PoolConfig, EntityDefinition ids, weapon layer matrix + prefab layers, projectile rigidbody/trigger, player probe masks, PlayerTestMode warning (Inconclusive, not a failure) | ~3 s |
| `Stress.*` [Slow] | Seed distribution / determinism at scale, arc-solver accuracy fuzz | see suite |

**Default gate after a change:** run the suite(s) that own what you touched (table above). Full run only for shared
machinery (pooling, motor, serialization) or before a build. `Report` tests describe content state and may be red
while content is mid-authoring (e.g. Demo room variants with no prefab) — they are not code regressions.

# Handoffs — plan index

Plans authored by Fable 5 on 2026-07-07 (overnight, Unity closed). Each doc is self-contained: an
implementing model needs the doc + CLAUDE.md, nothing else. Implementation order matters:
SPEC-000 first, then any order.

| Doc | What | Plan | Code |
|---|---|---|---|
| [SPEC-000-damage-pipeline.md](SPEC-000-damage-pipeline.md) | DamageInfo payload, IDamageable overload, player i-frames | ✅ | ✅ implemented + reviewed (compile pending) |
| [PLAN-001-structural-review.md](PLAN-001-structural-review.md) | Prioritized structural findings (assessment — F4/F5/F6 remain open recommendations) | ✅ | n/a (F1→SPEC-000, F2/F3→PLAN-003) |
| [PLAN-002-flying-enemy.md](PLAN-002-flying-enemy.md) | EnemyBehaviorFlyer: approach/orbit/burst/dodge-on-aim/reposition + wizard | ✅ | ✅ implemented + reviewed (compile pending) |
| [PLAN-003-lava-damage-orange-screen.md](PLAN-003-lava-damage-orange-screen.md) | Waterfall DoT volumes, V4 submersion detection, orange overlay, LavaV4Sampler extraction, projectile trigger fix | ✅ | ✅ implemented + reviewed (compile pending) |
| [PLAN-004-directional-damage-indicators.md](PLAN-004-directional-damage-indicators.md) | Ring indicators pointing at damage sources | ✅ | ✅ implemented + reviewed (compile pending) |
| [HANDOFF-005-bug-review-and-verify.md](HANDOFF-005-bug-review-and-verify.md) | Bug/inefficiency review + live verification of SPEC-000/PLAN-002/003/004 | ✅ | ✅ verified live (all 8 acceptance tests pass); F5 pool-leak fix applied; overlay reorder + flyer reposition saved |
| [HANDOFF-006-movement-feel.md](HANDOFF-006-movement-feel.md) | CameraFeel juice: dash FOV kick, landing dip, wall-slide lean, strafe roll | ✅ | ✅ implemented + verified live (all 7 acceptance tests pass); CameraFeel on Core player camera (covers Sandbox + Area1LavaBridge — single persistent player); wizard `Tools/Blast Frame/Player/Add Camera Feel` shipped; no mechanics changed |
| [HANDOFF-007-pathfinding-enemies.md](HANDOFF-007-pathfinding-enemies.md) | NavMesh + 5 enemies: Lobber, Skirmisher, Missiler, Biter, Bat + HomingMissile/DroppedBomb | ✅ | ✅ implemented + verified live (all 6 acceptance tests pass). NavMesh baked in Sandbox; EnemyNavMotor + 5 behaviors + 6 wizards + `Bake NavMesh` menu; HomingMissile pooled + shootable; DroppedBomb & Missiler explosive shot reuse ArcProjectile. Fixed an AoeExplosion static-buffer re-entrancy (HomingMissile pop → pure-visual VfxPop). Not committed. |
| [HANDOFF-008-multiplayer.md](HANDOFF-008-multiplayer.md) | Local split-screen 2P (full) + NGO online foundation | ✅ | ✅ Phase A/B implemented + verified live (single-player regression clean; P2 join/split/independent-input/leave all pass, 0 errors). Phase C (online) BLOCKED: NGO package won't install here — UPM TLS "unable to verify the first certificate" vs packages.unity.com; manifest reverted to keep the tree green; NGO foundation code shipped guarded behind `#if BLASTFRAME_NGO`. Not committed. |

HANDOFF-005→008 authored 2026-07-09 (overnight batch, Fable 5 planning → Opus implementers,
sequential in this order).

Update the Code column (✅ / ⏳ / ❌ + note) as implementation lands. Compile verification and all
`## Manual steps` checklists require the Unity editor — pending the next editor session.

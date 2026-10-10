# Sprint 1 -- 2026-10-12 to 2026-10-23

> **Milestone**: M1 — Core Loop Playable (`production/milestones/m1-core-loop-playable.md`, target 2026-11-20)
> **Manifest Version**: 2026-10-10
> **Review mode**: lean — PR-SPRINT gate skipped
> **QA Plan**: `production/qa/qa-plan-sprint-1.md` (~178 automated tests; 13 flagged gaps G-1…G-13 — resolve each at `/story-readiness` before starting the story)
> **Revised 2026-10-10 after QA plan**: GD-001 moved into Must Have (OP-001/OP-002 compile against `EnemyDataSO`, gap G-5); IM-004 moved to Should Have to hold capacity at 7.75 days. Pool tests construct configs in-memory via `ScriptableObject.CreateInstance` until GD-003/GD-004 land.

## Sprint Goal
Stand up the Unity project and the deterministic runtime spine (clock, frame orchestrator, pool, parry input buffer), and put a throwaway greybox combo + parry prototype in the player's hands so its findings can shape the Core-layer stories in Sprint 2.

## Capacity
- Total days: 10 (solo, 2 weeks)
- Buffer (20%): 2 days reserved for unplanned work (Unity 6.3 setup friction, post-cutoff API verification)
- Available: 8 days

## Tasks

### Must Have (Critical Path)
| ID | Task | Agent/Owner | Est. Days | Dependencies | Acceptance Criteria |
|----|------|-------------|-----------|-------------|-------------------|
| BCF-001 | [Day-0 Unity 6.3 project setup & assembly layout](../epics/bootstrap-combat-frame/story-001-unity-project-setup.md) | **User** + unity-specialist | 1.0 | User: project location decision, `UNITY_LICENSE` secret, OK to move the example test | Project opens clean; URP Forward+; Input System pinned; asmdefs incl. `Dynasty.Input`; CI runs EditMode green |
| BCF-002 | [IGameClock with unified RunTime + test clock](../epics/bootstrap-combat-frame/story-002-game-clock-run-time.md) | gameplay-programmer | 0.5 | BCF-001 | RunTime monotonic + pause-compensated (unit tests) |
| BCF-004 | [Shared [DESIGNER-ERROR] channel](../epics/bootstrap-combat-frame/story-004-designer-error-channel.md) | gameplay-programmer | 0.25 | BCF-001 | Channel assertable by tests |
| BCF-003 | [CombatFrame orchestrator, RunFrame pump & EventRecorder](../epics/bootstrap-combat-frame/story-003-combat-frame-orchestrator.md) | gameplay-programmer | 0.5 | BCF-002 | ADR-0002 order proven by EventRecorder test |
| GD-001 | [EnemyDataSO — per-type/tier enemy data assets](../epics/game-data/story-001-enemy-data-so.md) | gameplay-programmer | 0.5 | BCF-001 | Story ACs; supplies the `EnemyDataSO` type OP-001/OP-002 compile against (QA plan gap G-5) |
| OP-001 | [Pool core: pre-warm, Spawn/ReturnToPool, exhaustion](../epics/object-pool/story-001-pool-core-prewarm-spawn-return.md) | engine-programmer | 0.5 | BCF-001, GD-001 | Story ACs pass; zero Instantiate after pre-warm |
| OP-002 | [Per-spawn AttackerId + cooldown jitter](../epics/object-pool/story-002-attacker-id-spawn-jitter.md) | engine-programmer | 0.5 | OP-001, BCF-002, GD-001 | Unique ids across reuse; seeded jitter distribution |
| IM-001 | [PlayerControls asset, single instance & SetContext](../epics/input-manager/story-001-player-controls-asset-and-input-manager-shell.md) | gameplay-programmer + unity-specialist | 0.5 | BCF-001 | One instance; exactly one map active |
| IM-002 | [Parry ring buffer — liveness, pruning, capacity](../epics/input-manager/story-002-parry-ring-buffer-liveness.md) | gameplay-programmer | 0.5 | IM-001, BCF-002 | AC-IM-01 (incl. inclusive-boundary float case) |
| IM-003 | [TryConsumeParryPress — consume-once, notBefore](../epics/input-manager/story-003-try-consume-parry-press.md) | gameplay-programmer | 0.5 | IM-002 | AC-IM-02..05; `IInputAuthority` fake available |
| PROTO-1 | `/prototype combo-and-parry-system` — throwaway greybox build in `prototypes/` (Guan Yu, Levy + Pikeman + Archer capsules, combo pips, momentum bar, parry window, 50/100/200 spawn toggle) | prototyper | 2.0 | BCF-001 (Unity install only — prototype is isolated from `src/`) | Playable build; standards relaxed per prototype rules |
| PROTO-2 | Play the prototype + `/playtest-report` | **User** + prototyper | 0.5 | PROTO-1 | Report answers: 4 balance concerns (start with Finisher ≥ Levy HP), 4 feel questions, telegraph readability, 50/100/200 frame-time baseline in a player build on min-spec |
| | | | **7.75** | | |

### Should Have
| ID | Task | Agent/Owner | Est. Days | Dependencies | Acceptance Criteria |
|----|------|-------------|-----------|-------------|-------------------|
| OP-003 | [Deferred registration + pre-warm phantom guard](../epics/object-pool/story-003-deferred-registration-prewarm-guard.md) | engine-programmer | 0.5 | OP-001 | Pre-warm stages zero RegisterPending entries (PlayMode test) |
| OP-004 | [Binding ReturnToPool order](../epics/object-pool/story-004-binding-return-order.md) | engine-programmer | 0.5 | OP-001 | Flush → deregister → deactivate → push (spy) |
| IM-004 | [Performed-edge capture, run-clock stamps, inert-until-run-start](../epics/input-manager/story-004-parry-capture-run-clock-timestamps.md) | gameplay-programmer | 0.5 | IM-002, BCF-002 | AC-IM-09/10/11 |
| IM-005 | [Parry buffer freeze outside Gameplay](../epics/input-manager/story-005-context-freeze.md) | gameplay-programmer | 0.25 | IM-004 | AC-IM-08 |
| BCF-005 | [Bootstrap composition root](../epics/bootstrap-combat-frame/story-005-bootstrap-composition-root.md) | unity-specialist | 0.5 | BCF-003 | ADR-0012 boot order; one scene |

### Nice to Have
| ID | Task | Agent/Owner | Est. Days | Dependencies | Acceptance Criteria |
|----|------|-------------|-----------|-------------|-------------------|
| GD-003 | [TuningConfig incl. run_duration_s, arena_bounds, pool sizes](../epics/game-data/story-003-tuning-config.md) | gameplay-programmer | 0.5 | BCF-001; needs arena size + pool capacities decided | Story ACs |
| GD-004 | [TestConfigs.Default() factory](../epics/game-data/story-004-test-configs-factory.md) | gameplay-programmer | 0.5 | GD-003 | Story ACs |
| IM-006 | [Directional recording](../epics/input-manager/story-006-directional-recording.md) | gameplay-programmer | 0.25 | IM-001, IM-004 | AC-IM-06/07 |

## Carryover from Previous Sprint
| Task | Reason | New Estimate |
|------|--------|-------------|
| — | First sprint | — |

## Risks
| Risk | Probability | Impact | Mitigation |
|------|------------|--------|------------|
| Unity 6.3 setup friction (licence activation, package pinning, asmdef layout for `tests/`) eats days | Medium | High — blocks everything | Do BCF-001 on day 1; 2-day buffer; story's option A layout (project at repo root, `src/`+`tests/` as local packages) |
| Post-cutoff Input System APIs differ from the engine reference (`ctx.time`, `InputActionReference.Create`, Input test fixture) | Medium | Medium | Verify each API against the pinned package docs before coding (IM stories flag them) |
| Prototype scope creep (VFX Graph, real art) | Medium | Medium | Greybox only; VFX Graph is off the critical path this sprint (producer gate rec); scale-pop / mesh-flash feedback is enough to answer the feel questions |
| Prototype slips → Sprint 2 Core stories written blind | Low | High | PROTO-1 starts as soon as BCF-001 lands, in parallel with Foundation stories |
| Min-spec hardware not available for benchmarks | Unknown | Medium | Confirm access to a GTX 1650 / Ryzen 5 3600 class machine; otherwise record dev-PC numbers and flag them as non-binding |

## Dependencies on External Factors
- **User**: decide where the Unity project lives (BCF-001 recommends the repo root with `src/` and `tests/` as local packages)
- **User**: add the `UNITY_LICENSE` GitHub secret; check whether game-ci v4 also needs `UNITY_EMAIL`/`UNITY_PASSWORD` for a Personal licence
- **User**: OK to move `tests/unit/damage-health/damage_health_effective_damage_test.cs` out of the compiled tree until `PlayerHealthSystem` exists
- **User**: Unity 6.3 LTS installed via Unity Hub
- **User**: play the prototype (PROTO-2)
- Access to min-spec hardware for the frame-time baseline

## Definition of Done for this Sprint
- [ ] All Must Have tasks completed
- [ ] All tasks pass acceptance criteria
- [ ] QA plan exists (`production/qa/qa-plan-sprint-1.md`)
- [ ] All Logic/Integration stories have passing unit/integration tests
- [ ] Smoke check passed (`/smoke-check sprint`)
- [ ] QA sign-off report: APPROVED or APPROVED WITH CONDITIONS (`/team-qa sprint`) — lightweight checklist form for a solo dev, per the producer's gate recommendation
- [ ] No S1 or S2 bugs in delivered features
- [ ] Design documents updated for any deviations (prototype findings → GDDs)
- [ ] Code reviewed and merged
- [ ] Prototype report written; Sprint 2 Core-layer epics/stories created from its findings

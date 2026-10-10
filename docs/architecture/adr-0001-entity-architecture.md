# ADR-0001: Entity Architecture — MonoBehaviour + Central Manager (DOTS Deferred)

## Status
Accepted
*(Proposed and Accepted 2026-07-04 in the same autonomous session, per user directive to proceed on the ratified architecture.md recommendations. Lean review mode — TD-ADR gate skipped.)*
*(Amended 2026-10-10 by /architecture-review: benchmark conditions pinned (player build, not Editor); a cheaper pre-DOTS escalation step added; budget-nesting with ADR-0007 clarified.)*

## Date
2026-07-04

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS |
| **Domain** | Core |
| **Knowledge Risk** | HIGH for the DOTS alternative (Entities 1.3 API is entirely post-cutoff); LOW for the chosen MonoBehaviour path (stable APIs) |
| **References Consulted** | `docs/engine-reference/unity/VERSION.md`, `breaking-changes.md`, `current-best-practices.md`, `plugins/dots-entities.md` |
| **Post-Cutoff APIs Used** | None on the chosen path. If the DOTS trigger fires, all ECS code must use `ISystem`/`IJobEntity`/`ComponentLookup<T>` (Entities 1.3) — never `ComponentSystem`/`GameObjectEntity` |
| **Verification Required** | Profile frame time at 50/100/200 enemies in the prototype; the 200-enemy tick must fit its share of the 16.6ms budget |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | None (first ADR) |
| **Enables** | ADR-0002 (frame order), ADR-0005 (pooling), ADR-0007 (spatial queries) |
| **Blocks** | All gameplay implementation epics — no combat code before this is Accepted (damage-health Implementation Contract #1) |
| **Ordering Note** | Foundation ADR — author/accept first |

## Context

### Problem Statement
Dynasty Survivors ticks up to 200 concurrent enemies (state machines, steering, telegraphs, cooldowns) plus per-frame player regen/iframe countdowns at 60fps. damage-health.md Contract #1 mandates an accepted ADR choosing MonoBehaviour (simplicity) or DOTS (performance) before any combat code, because the choice shapes every downstream system (Combo, Parry, UI event wiring, test harness).

### Constraints
- 16.6ms frame budget, ≤1500 draw calls, ≤4GB (technical-preferences)
- Solo developer, 3–6 week MVP — architecture must minimise learning-curve risk
- Every GDD implementation contract (EnemySystemManager two-phase tick, deferred registration, pool flush) is already written in MonoBehaviour + manager vocabulary
- DOTS (Entities 1.3) is production-ready in Unity 6.3 but is the project's largest LLM knowledge gap (complete post-cutoff API overhaul)

### Requirements
- Must tick 200 enemies with no per-enemy `Update()` overhead (TR-ED-001)
- Must support deterministic EditMode testing with a manual tick pump (TR-TP-001)
- Must preserve the exact two-phase frame order enemy-definition specifies (TR-ED-001, TR-ED-008)

## Decision

**All entities are plain MonoBehaviours driven by central manager ticks. DOTS is not used in MVP.**

1. Enemies are `EnemyController` MonoBehaviours holding plain C# state; **none implement `Update()`**. A single `EnemySystemManager` (`[DefaultExecutionOrder(-100)]`) iterates all active enemies once per frame in two phases (damage → state-transition) per enemy-definition Contract #1.
2. Player-side systems (CombatCore, PlayerHealthSystem, ComboSystem, ParrySystem, DodgeSystem, ProgressionSystem) are plain C# classes ticked by the `CombatFrame` orchestrator (ADR-0002), not self-updating MonoBehaviours.
3. Per-entity state lives in plain C# fields/structs; per-type configuration lives in `EnemyDataSO` ScriptableObjects (ADR-0006). Hot loops use arrays/lists indexed by the manager, pre-allocated at pool warm-up.
4. **DOTS escalation trigger (amended 2026-10-10)**: if prototype profiling at 200 enemies shows the enemy tick exceeding ~4ms (its share of the 16.6ms budget after rendering/VFX/UI; ADR-0007's ~2ms query+steering budget is a NESTED SUBSET of this 4ms, not additive — and neither covers Animator/rendering cost), escalate in two steps: **(a) first** cache positions in struct arrays and batch the transform writes once per frame via `TransformAccessArray` + `IJobParallelForTransform` + Burst — this stays MonoBehaviour-based and removes the dominant native-boundary cost; **(b) only if (a) still misses budget**, a follow-up ADR migrates the enemy tick (only) to Entities 1.3 `ISystem` + `IJobEntity` + Burst. The manager's phase contract and the `AttackEvent` stream are the stable seam for that migration. **Benchmark conditions are binding**: measure in a player build (record Mono vs IL2CPP) on min-spec hardware — Editor numbers run 2–5× worse and would falsely trigger escalation.

### Architecture Diagram
```
            frame N
InputManager.Tick (-200)
EnemySystemManager.Tick (-100)   ── for enemy in active[]:
                                     phase 1: damage/HP/phase-2/stagger
                                     phase 2: state transitions, steering
CombatFrame.Update (0)           ── Parry → Dodge → CombatCore → Combo
                                     → PlayerHealth → Progression
Presentation (+100)
```

### Key Interfaces
- `EnemySystemManager : MonoBehaviour` — `RegisterPending` staging, `Tick()` (extracted so tests can pump it manually), `IEnemyWorld` queries (architecture.md API Boundaries)
- `EnemyController : MonoBehaviour` — passive data holder + `Initialize(spawnParams)`; no `Update`/`FixedUpdate`/`OnEnable` registration side effects beyond staging

## Alternatives Considered

### Alternative 1: DOTS/ECS (Entities 1.3) from day one
- **Description**: Enemies as entities; `ISystem` + Burst-compiled `IJobEntity` for movement/state; hybrid rendering.
- **Pros**: Headroom far beyond 200 entities; production-ready in 6.3; best-in-class cache behaviour.
- **Cons**: Entire API is post-LLM-cutoff (highest verification cost per line); solo dev on a 3–6 week MVP; all 17 GDD contracts written against the manager pattern; harder EditMode determinism; complicates the struct-event architecture with sync points.
- **Rejection Reason**: 200 simple agents is comfortably within a well-written manager tick; DOTS's payoff doesn't justify its risk at this scale. Kept as a profiled escalation path.

### Alternative 2: Conventional per-enemy `Update()` MonoBehaviours
- **Description**: Each enemy self-ticks; Unity calls 200 `Update()`s.
- **Pros**: Most familiar Unity idiom; zero orchestration code.
- **Cons**: ~200 managed↔native call transitions per frame; no control over intra-frame ordering (breaks the damage-before-state-transition contract and same-frame parry priority); explicitly forbidden by enemy-definition Contract #1.
- **Rejection Reason**: Violates a hard GDD contract and wastes frame budget.

## Consequences

### Positive
- Zero post-cutoff API exposure on the critical path; every GDD implementation contract maps 1:1 onto code
- Deterministic, test-pumpable ticks (`Tick()` methods callable from EditMode tests)
- Single seam (`EnemySystemManager`) if DOTS migration is ever needed

### Negative
- Manual orchestration code the engine would otherwise provide (accepted: it is exactly what the GDD contracts specify anyway)
- A future entity-count increase (post-MVP army scale) likely re-opens this decision

### Risks
- **Manager becomes a god class** → mitigate: phases delegated to small pure-C# functions; enemy behaviour stays data-driven
- **200-enemy tick misses budget** → mitigate: DOTS escalation trigger defined above, benchmark at 50/100/200 in the prototype (AOQ-01)
- **Editor domain-reload interaction with static state** *(engine-specialist finding)*: with "Enter Play Mode Options" disabling domain reload (a common Unity 6.x editor perf setting), statics (the `AttackerId` counter, any manager singletons) survive between editor play sessions. Mitigate: no gameplay singletons (DI throughout); `ResetInstanceIdCounter()` invoked on play-mode entry in editor and in every test `[SetUp]` (ADR-0005)
- **`OnEnable` on `SetActive(true)` re-activation** *(engine-specialist finding)*: pooled re-enables fire `OnEnable` mid-frame; registration must go through the `RegisterPending` staging list only — never direct list mutation from `OnEnable` (already the contract; noted here because this ADR owns the registration model)

## GDD Requirements Addressed

| GDD System | Requirement | How This ADR Addresses It |
|------------|-------------|--------------------------|
| damage-health.md | Contract #1: entity-architecture ADR before implementation | This ADR; MonoBehaviour path chosen explicitly |
| enemy-definition.md | Contract #1: single EnemySystemManager, two-phase tick, `[DefaultExecutionOrder(-100)]` | Adopted verbatim as the core pattern |
| enemy-definition.md | Deferred registration, spawn cooldown jitter | Manager-owned staging list + `Initialize()` params |
| game-concept.md | 60fps at 200 enemies | No per-enemy Update; pre-allocated iteration |

## Performance Implications
- **CPU**: One virtual call into the manager per frame + linear array iteration; ~200 × (state eval + steering) well under 4ms at this complexity
- **Memory**: Pre-allocated enemy list + state arrays at pool warm-up; no per-frame allocation
- **Load Time**: Negligible (pool pre-warm dominates, see ADR-0005)
- **Network**: N/A

## Migration Plan
Greenfield — no existing code. If the DOTS trigger fires: keep `AttackEvent`/`IEnemyWorld` contracts, port the enemy tick to `ISystem`, verify against `plugins/dots-entities.md`, supersede this ADR.

## Validation Criteria
- Prototype perf baseline: frame time at 50/100/200 enemies recorded; enemy tick ≤ ~4ms at 200
- Static check: no `EnemyController` (or combat system) implements `Update()`
- AC-18 (spawn jitter) and the two-phase edge cases (Warlord roar/death ordering) pass in EditMode tests

## Related Decisions
- ADR-0002 (frame execution order), ADR-0005 (pooling), ADR-0007 (spatial queries)
- `docs/architecture/architecture.md` — Module Ownership, Required ADRs

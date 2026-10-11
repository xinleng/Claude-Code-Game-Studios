# ADR-0002: Canonical Frame Execution Order and the CombatFrame Orchestrator

## Status
Accepted
*(Proposed and Accepted 2026-07-04, autonomous session — user directive to proceed; lean review mode.)*
*(Amended 2026-10-10 by /architecture-review: Finisher-replaces-3rd-swing handshake made explicit — sequential order alone could not express the replacement semantics of combat-core Rule 6 / TR-CC-006.)*

## Date
2026-07-04

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS |
| **Domain** | Core |
| **Knowledge Risk** | LOW — `[DefaultExecutionOrder]` and MonoBehaviour lifecycle are stable pre-cutoff APIs |
| **References Consulted** | `docs/engine-reference/unity/VERSION.md`, `breaking-changes.md` |
| **Post-Cutoff APIs Used** | None |
| **Verification Required** | AC-IM-12 (Input before Parry) and AC-DH-06 (same-frame resets) pass under the orchestrator |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | ADR-0001 (manager-tick entity model) |
| **Enables** | ADR-0003 (ordered event dispatch), ADR-0010 (deterministic test pump) |
| **Blocks** | All combat implementation stories |
| **Ordering Note** | The orchestrator is among the first code written; every combat system registers with it |

## Context

### Problem Statement
Three GDD contracts constrain intra-frame ordering: Input Manager must capture before the Parry System queries (input-manager Rule 9); enemies must emit AttackEvents and resolve damage phases before Parry/UI process them (enemy-definition Contract #1); Parry must evaluate before Damage & Health so a same-frame parry suppresses the hit (damage-health Contract #3). Unity's default script ordering is unspecified — relying on it produces heisenbugs; scattering Script Execution Order entries in ProjectSettings hides the dependency.

### Constraints
- Ordering must be visible in code (reviewable), not buried in ProjectSettings
- Must be drivable by a manual test pump without the Unity player loop
- Must preserve exact GDD event-ordering semantics (S6 before momentum reset, DamageTaken before PlayerDead)

### Requirements
- Deterministic order: Input → Enemy tick → Parry → Dodge → CombatCore → Combo → PlayerHealth → Progression → Presentation
- Same-frame parry priority; same-frame combo/momentum resets on DamageTaken

## Decision

**A single `CombatFrame` MonoBehaviour orchestrates the combat tick as explicit sequential calls. Only three `[DefaultExecutionOrder]` anchors exist project-wide.**

```
[-200] InputManager          (capture + prune the parry buffer)
[-100] EnemySystemManager    (two-phase enemy tick; AttackEvent emission)
[   0] CombatFrame.Update():
         parry.Evaluate();       // consume presses vs open windows; suppress hits
         dodge.Tick(dt);         // dash state
         combatCore.Tick(dt);    // locomotion, auto-attack, SwingLanded
         combo.Tick(dt);         // counter timeout, Finisher on 3rd swing
         playerHealth.Apply(dt); // unsuppressed damage → DamageTaken → iframes
         progression.Tick();     // XP thresholds, level-up requests
[+100] Presentation          (UI Toolkit updates, VFX/audio triggers)
```

- Combat subsystems are **plain C# classes** implementing `ITickable { void Tick(float dt); }` (or explicit named methods where clearer); `CombatFrame` holds them in a fixed, code-declared order. No combat system implements its own `Update()`.
- **Finisher handshake (amended 2026-10-10, TR-CC-006)**: `combatCore.Tick` resolves each swing by first reading `combo.ComboCounter`. A primed 3rd landing swing (`ComboCounter == 2`) applies **no** normal swing damage — Combat Core reports the landing to `ComboSystem`, which resolves the Finisher *in its place* within the same tick (combat-core Rule 6: the Finisher "replaces" the swing). The sequence `combatCore.Tick → combo.Tick` cannot express replacement on its own; without this check the swing's damage would already be applied before Combo runs.
- The identical sequence is invokable from tests: `combatFrame.RunFrame(dt)` pumps one deterministic frame with injected time (ADR-0010).
- Wiring: `CombatFrame` receives its subsystems by constructor/initializer injection at scene bootstrap (no singletons, no `FindObjectOfType` — coding standard: DI over singletons). This also breaks the Combo↔Parry↔CombatCore reference cycle: each receives interfaces (`IComboSystem`, `ICombatCore`, …) wired by the bootstrap, and cross-system communication flows through events one way and interface reads the other (architecture.md API Boundaries).

### Key Interfaces
- `CombatFrame.RunFrame(float dt)` — the one true tick, used by both play mode and tests
- `ITickable` registration order is a compile-time constant list — changing order requires a code review, by design

## Alternatives Considered

### Alternative 1: Script Execution Order entries in ProjectSettings for every system
- **Pros**: No orchestrator code.
- **Cons**: Ordering invisible in code review; breaks silently when a script is added; damage-health Contract #3 explicitly disfavours it.
- **Rejection Reason**: Hidden dependency; GDD prefers the orchestrator.

### Alternative 2: Event-chain ordering (each system fires "done" events consumed by the next)
- **Pros**: Loose coupling.
- **Cons**: Control flow becomes a distributed state machine; reentrancy hazards; much harder to test deterministically.
- **Rejection Reason**: Complexity without benefit at this scale.

## Consequences

### Positive
- The whole game's causality is readable in one function; test pump falls out for free
- Same-frame contracts (parry priority, reset ordering) are trivially provable

### Negative
- One more bespoke infrastructure class; systems must not sneak in their own `Update()` (enforced by review + a forbidden pattern in the registry)

### Risks
- **Someone adds a self-updating MonoBehaviour** → mitigate: forbidden pattern `self_updating_combat_system` registered; code review checklist item
- **Pause handling divergence** → `CombatFrame` respects `Time.timeScale` via scaled `dt`; unscaled timers (iframes, input) read `Time.unscaledTime` directly per their GDD contracts

## GDD Requirements Addressed

| GDD System | Requirement | How This ADR Addresses It |
|------------|-------------|--------------------------|
| input-manager.md | Rule 9 / AC-IM-12: capture before Parry | `[-200]` anchor precedes everything |
| enemy-definition.md | Contract #1: manager before Parry/UI | `[-100]` anchor |
| damage-health.md | Contract #3: Parry before Damage & Health (orchestrator preferred) | `CombatFrame` sequential calls, option (b) adopted |
| game-concept.md | Same-frame parry priority edge case | Structural consequence of the order |

## Performance Implications
- **CPU**: Negligible — replaces N engine `Update()` dispatches with direct calls (slightly cheaper)
- **Memory / Load Time / Network**: None

## Migration Plan
Greenfield. If FixedUpdate physics interactions appear later (none in MVP — combat is frame-based by design), a `FixedCombatFrame` variant would be added by a superseding ADR.

## Validation Criteria
- AC-IM-12, AC-DH-06, AC-DH-09 (event order log), AC-PR-01 pass in EditMode using `RunFrame`
- Static check: no combat system class contains a Unity `Update()` method

## Related Decisions
- ADR-0001 (entity model), ADR-0003 (event dispatch inside `PlayerHealth.Apply`), ADR-0010 (test pump)

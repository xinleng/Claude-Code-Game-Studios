# ADR-0010: Deterministic Test Harness

## Status
Accepted
*(Proposed and Accepted 2026-07-04, autonomous session — user directive to proceed; lean review mode.)*
*(Amended 2026-10-10 by /architecture-review: `IGameClock` gains `RunTime` — the unified pause-compensated run clock that resolves blocking conflict C1.)*

## Date
2026-07-04

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS |
| **Domain** | Core / Testing |
| **Knowledge Risk** | LOW — Unity Test Framework (NUnit) stable; `game-ci/unity-test-runner@v4` is the pinned CI action |
| **References Consulted** | `docs/engine-reference/unity/current-best-practices.md` (testing), coding-standards CI rules |
| **Post-Cutoff APIs Used** | None |
| **Verification Required** | CI green on the first scaffolded test run (license activation for game-ci) |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | ADR-0002 (`RunFrame` pump), ADR-0004 (input test double), ADR-0006 (in-memory configs) |
| **Enables** | Every Logic/Integration story's BLOCKING test evidence |
| **Blocks** | First gameplay story (tests-first per coding standards) |
| **Ordering Note** | Run `/test-setup` to scaffold; this ADR fixes the patterns the scaffold implements |

## Context

### Problem Statement
The testing standards demand determinism (no time-dependent assertions, no random seeds), isolation, and DI-based independence — while ~150 GDD acceptance criteria are timing-sensitive (parry windows, iframes, buffers, cooldowns). That is only reconcilable if the combat core runs on an injected clock and a manual tick pump, which must be an architectural property, not a test afterthought.

### Constraints
- Tests must produce identical results every run; no `WaitForSeconds`/`WaitForFixedUpdate` in logic tests
- EditMode preferred (fast, no player loop); PlayMode reserved for scene-integration smoke
- CI: `game-ci/unity-test-runner@v4`, blocking gate on push/PR (coding standards)

### Requirements
- Manual frame pump (`CombatFrame.RunFrame(dt)`) with injected time
- Test seams: `ForceState(HealthState)`, `ResetInstanceIdCounter()`, `IInputAuthority` double, in-memory `GameData`
- Deterministic randomness where logic uses RNG (seeded `System.Random` injected, never `UnityEngine.Random` in testable logic)

## Decision

**All combat/gameplay logic is EditMode-testable through three seams: the injected clock, the manual frame pump, and constructor-injected dependencies. PlayMode tests are limited to scene-wiring smoke checks.**

1. **Clock (amended 2026-10-10)**: gameplay systems never read `Time.time/unscaledTime` directly — they read an injected `IGameClock { float Time; float UnscaledTime; float DeltaTime; float RunTime; }`. **`RunTime` is the unified pause-compensated run-local clock (TR-RS-005): the single domain for ALL combat timestamps** — enemy `AttackStartTime`, parry press stamps (converted from Input System `ctx.time` by InputManager, ADR-0004), `steer_time`, and every parry-window comparison. RSM owns the run origin and the paused-duration accumulator behind it. Mixing `Time.time`- and `Time.unscaledTime`-derived run-local values is forbidden (blocking conflict C1: they diverge by total paused duration after the first level-up pause, which would break all parry matching). `UnscaledTime` remains for the explicitly-unscaled timers (iframes, per damage-health Formula 2). Play mode binds the clock to Unity/Input System time; tests advance it explicitly (`clock.Advance(0.501f)` for the iframe boundary in AC-DH-07).
2. **Pump**: tests drive `combatFrame.RunFrame(dt)` (and `enemyManager.Tick()` / `inputManager.Tick()`) directly — the exact production order (ADR-0002), no engine loop.
3. **Doubles**: `IInputAuthority` fake injects timestamped presses; `IEnemyWorld` stub for player-side logic tests; `GameData` configs constructed in-memory via factory helpers (`TestConfigs.Default()` mirroring GDD defaults — no magic numbers inline except boundary-value tests, per standards).
4. **RNG**: systems needing randomness (spawn jitter, drought draw, spawn angle) receive an injected `System.Random`; tests pass a fixed seed or a scripted sequence. This makes AC-18 (jitter distribution) and AC-PG-04 (drought weighting) exactly assertable.
5. **Seams**: `[Conditional("UNITY_EDITOR")]` on `ResetInstanceIdCounter`; `ForceState` on PlayerHealthSystem; event-recorder helper (ordered subscriber log) for AC-DH-09-style order assertions.
6. **Structure & CI**: `tests/unit/[system]/` + `tests/integration/[system]/` naming per standards (`[system]_[feature]_test.cs`, `test_[scenario]_[expected]` methods); GitHub Actions workflow runs EditMode + PlayMode via `game-ci/unity-test-runner@v4` on every push/PR; failing tests block merge; skipping/disabling tests to pass CI is forbidden.

### Key Interfaces
`IGameClock`, `CombatFrame.RunFrame(dt)`, `TestConfigs.*`, `EventRecorder`.

## Alternatives Considered

### Alternative 1: PlayMode tests with real time (`WaitForSeconds`)
- **Rejection Reason**: Non-deterministic by construction (frame-rate dependent); explicitly banned by the project's testing standards; 100× slower.

### Alternative 2: Static `Time` wrapper with a settable test mode
- **Pros**: Less injection plumbing.
- **Cons**: Global mutable state leaks between tests (violates isolation); hidden dependency vs. the DI standard.
- **Rejection Reason**: Constructor injection is the project standard and keeps tests order-independent.

## Consequences

### Positive
- Every timing-sensitive GDD AC becomes an exact assertion; tests run in milliseconds; CI trustworthy
### Negative
- `IGameClock` plumbing through every system (small, uniform cost)

### Risks
- **Drift between pump order and any future engine-loop behaviour** → the pump *is* production code (same method), so drift is impossible by construction
- **game-ci licensing friction in CI** → resolved once during `/test-setup`; documented in the workflow

## GDD Requirements Addressed

| GDD System | Requirement | How This ADR Addresses It |
|------------|-------------|--------------------------|
| damage-health.md | AC-DH-06/07 deterministic harness, manual tick, time control; Contracts #5/#6 seams | Clock + pump + seams |
| input-manager.md | "All timing tests use a deterministic harness … never WaitForSeconds" | The decision itself |
| enemy-definition.md | AC-18 jitter, AC-20 watchdog — deterministic assertions | Injected RNG + clock |
| coding-standards | Determinism, isolation, no hardcoded fixtures, DI, CI blocking gate | Rules 1–6 |

## Performance Implications
- **CPU/Memory**: Test-only; negligible runtime cost (one interface indirection on time reads)
- **Load Time / Network**: N/A

## Migration Plan
Greenfield — `/test-setup` scaffolds `tests/`, asmdefs, and the CI workflow implementing this ADR.

## Validation Criteria
- The scaffolded example test suite passes locally and in CI
- Spot check: AC-DH-07 implemented with `clock.Advance` and passing identically across 10 consecutive runs

## Related Decisions
- ADR-0002 (pump = production order), ADR-0004 (input double), ADR-0006 (in-memory configs)

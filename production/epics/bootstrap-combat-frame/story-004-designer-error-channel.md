# Story 004: Shared [DESIGNER-ERROR] Logging Channel (Test-Assertable)

> **Epic**: Bootstrap, CombatFrame & Run Clock
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Logic
> **Manifest Version**: 2026-10-10

## Context

**GDD**: `design/gdd/damage-health.md` (AC-DH-10, AC-DH-12 — "assert error log contains designer-error marker"; intake guard; Contract #5 reset guard). Consumers also in `enemy-definition.md`, `upgrade-definition.md`, `progression-system.md`, `dodge-system.md`, `combat-core.md`.
**Requirement**: `TR-TP-002` (shared `[DESIGNER-ERROR]` channel assertable by tests)
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0006: Data-Driven Configuration (Decision 5 — `[DESIGNER-ERROR]`-prefixed logging) + ADR-0010: Deterministic Test Harness (DI, test seams) — both Accepted
**ADR Decision Summary**: Validation and runtime guards log `[DESIGNER-ERROR]`-prefixed messages; out-of-range values clamp and the run continues. Under ADR-0010 every dependency is constructor-injected and tests assert outcomes without global mutable state.

**Engine**: Unity 6.3 LTS | **Risk**: LOW
**Engine Notes**: No post-cutoff APIs. In Unity Test Framework EditMode tests, an unexpected `Debug.LogError`/`LogAssert` message fails the test unless matched with `LogAssert.Expect` — the Unity-backed sink therefore stays assertable through the native path as well as through the injected recording sink.

**Control Manifest Rules (this layer)**:
- Required: Run `GameDataValidator` at Bootstrap: every GDD clamp rule, `[DESIGNER-ERROR]` logging (ADR-0006) — this story provides the channel it logs through
- Required: Wire dependencies by constructor/initializer injection — no singletons (ADR-0002, ADR-0012) → the channel is an injected interface, not a static logger
- Forbidden: Never hardcode a gameplay/tuning value (ADR-0006) — the channel carries messages only, no clamp values
- Guardrail: Bootstrap validation is the only load-time pass (ADR-0006)

---

## Acceptance Criteria

*From TR-TP-002, ADR-0006 Decision 5, damage-health AC-DH-10 / AC-DH-12 pass conditions, scoped to this story:*

- [ ] A single shared designer-error channel interface exists; every message it emits begins with the exact marker `[DESIGNER-ERROR]`
- [ ] Reporting a designer error never throws and returns control to the caller, so the caller can clamp-and-continue ("run continues; no exception" — AC-DH-10)
- [ ] Tests can assert designer errors deterministically through an injected recording sink: count since reset, and that a recorded message contains the designer-error marker and the reporting system's tag (the pass conditions of AC-DH-10 / AC-DH-12: "assert error log contains designer-error marker")
- [ ] Tests can assert that **no** designer error was logged (AC-DH-10 boundary 0.75, AC-UD-06, AC-UD-27 "no designer error logged")
- [ ] The Unity-backed implementation writes the prefixed message to the Unity console, and it is matchable in EditMode tests via `LogAssert.Expect` with a `^\[DESIGNER-ERROR\]` pattern
- [ ] The channel is consumed by constructor injection (no static instance, no global state shared between tests)

---

## Implementation Notes

*Derived from ADR-0006 Decision 5 and ADR-0010 (DI / seams):*

- **Location**: interface + Unity-backed implementation in `Dynasty.Core`; recording sink in `tests/helpers/` (e.g. `RecordingDesignerErrorLog`). Names are proposals (e.g. `IDesignerErrorLog.Report(string system, string message)`).
- **Format**: `[DESIGNER-ERROR] <system>: <message>` — the prefix is fixed by ADR-0006; the system tag lets one test distinguish e.g. a damage-health guard from a validator rule.
- **Unity-backed sink**: logs via `Debug.LogError` with the prefixed message. Use one log type consistently so `LogAssert.Expect(LogType.Error, regex)` works for every reporter.
- **Recording sink**: stores messages in order; exposes `Count`, `Messages`, `Reset()`, and a `Contains(marker)`-style helper. No Unity dependency, so pure EditMode unit tests never touch the console.
- **No policy here**: the channel only reports. The "assert in editor / clamp-and-continue in builds" policy of the bootstrap validation pass (ADR-0006 §5) belongs to `GameDataValidator` (game-data epic); per-guard clamping belongs to each owning system.
- Rare-path code — no allocation constraint, but do not call it per frame from hot paths in normal operation.
- Doc comments on all public members, including the marker constant.

---

## Out of Scope

*Handled by neighbouring stories / epics — do not implement here:*

- Story 005: Bootstrap constructing one channel instance and injecting it
- game-data epic: `GameDataValidator.ValidateAll` and every clamp rule (it reports through this channel; it must not re-implement it)
- damage-health / enemy / upgrade / progression / dodge / combat-core epics: the individual guards that call the channel (AC-DH-10, AC-DH-12, etc.)

---

## QA Test Cases

*Written by qa-lead at story creation (lean mode — QL-STORY-READY skipped).*

- **AC-1**: Every message starts with `[DESIGNER-ERROR]`
  - Given: a recording sink
  - When: `Report("DamageHealth", "raw_damage <= 0 rejected")`
  - Then: `Messages[0]` starts with exactly `[DESIGNER-ERROR]` (ordinal, case-sensitive)
  - Edge cases: empty message string → still prefixed, no exception; null message → no exception, still prefixed
- **AC-2**: Reporting never throws; caller continues
  - Given: both the Unity-backed sink (with `LogAssert.Expect` set) and the recording sink
  - When: `Report` is called, followed by a statement that sets a flag
  - Then: no exception; the flag is set
  - Edge cases: 100 consecutive reports → no exception, count == 100
- **AC-3**: Recorded marker + system tag assertable
  - Given: a recording sink, `Reset()`
  - When: `Report("DamageHealth", "damage_reduction 0.90 clamped to 0.75")`
  - Then: `Count == 1`; the message contains `[DESIGNER-ERROR]` and `DamageHealth`
  - Edge cases: after `Reset()`, `Count == 0` and `Messages` empty
- **AC-4**: "No designer error" assertable
  - Given: a fresh recording sink injected into a consumer stub that does not report
  - When: the consumer runs
  - Then: `Count == 0`
  - Edge cases: none
- **AC-5**: Unity-backed sink matchable via `LogAssert`
  - Given: the Unity-backed sink; `LogAssert.Expect(LogType.Error, new Regex(@"^\[DESIGNER-ERROR\]"))`
  - When: `Report("Validator", "stagger_resist_multiplier out of range")`
  - Then: the test passes (expectation satisfied; no unexpected-log failure)
  - Edge cases: without the `Expect`, the same report fails the test — documents that unexpected designer errors are never silent in tests
- **AC-6**: Injected, not static
  - Given: two recording sinks injected into two consumer stubs in the same test
  - When: only the first consumer reports
  - Then: first sink `Count == 1`, second sink `Count == 0` (no shared global state)
  - Edge cases: none

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: `tests/unit/designer-error/designer_error_channel_test.cs` — must exist and pass (EditMode)
- Helper: `tests/helpers/RecordingDesignerErrorLog.cs` (name proposed)

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001 (project/asmdefs) must be DONE
- Unlocks: Story 005 (Bootstrap injects the channel); game-data epic (`GameDataValidator`), and every guard AC that asserts a designer-error marker (AC-DH-10, AC-DH-12, enemy-definition guards, progression `xp_scalar` guard)

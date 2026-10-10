# Story 003: TryConsumeParryPress — consume-once, notBefore guard, most-recent wins

> **Epic**: Input Manager
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Logic
> **Manifest Version**: 2026-10-10

## Context

**GDD**: `design/gdd/input-manager.md`
**Requirement**: `TR-IM-003`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0004: Single Input Authority and the Parry Input Buffer (with ADR-0010: Deterministic Test Harness for the `IInputAuthority` test double)
**ADR Decision Summary**: `TryConsumeParryPress(notBefore, out pressTime)` returns the most recent live press with `press_time ≥ notBefore` and marks it consumed. The `notBefore` guard stops one press from serving two attack windows. Input Manager never decides whether a parry succeeds. ADR-0010 adds an `IInputAuthority` fake that injects timestamped presses for consumer tests.

**Engine**: Unity 6.3 LTS + Input System package (pinned) | **Risk**: HIGH (epic-level; this story's code is plain C# with no Input System types, so its own risk is LOW)
**Engine Notes** (ADR-0004): keep the consume logic on the engine-free buffer from Story 002. `InputManager.cs` only forwards to it. The fake must not reference `Unity.InputSystem`, because consumer test assemblies (Parry, Dodge) are outside `Dynasty.Input`.

**Control Manifest Rules (Foundation layer)**:
- Required: consumers use `TryConsumeParryPress(notBefore)` / `HasLiveParryPress()`, and one press equals one consumption (ADR-0004).
- Required: one run clock for all combat timestamps, `IGameClock.RunTime`. The parry-window comparison is in this domain (ADR-0010, TR-RS-005).
- Forbidden: never write state owned by another system (ADR-0003). Input Manager owns presses. The Parry System owns windows, and Input Manager never evaluates them.
- Guardrail: O(capacity) scan per query with zero allocation (ADR-0004 Performance).

---

## Acceptance Criteria

*From GDD `design/gdd/input-manager.md` (Core Rules 6–7, Formula 2, Edge Cases), scoped to this story:*

- [ ] **AC-IM-02**: **GIVEN** one live press at `t = 1.000`, **WHEN** `TryConsumeParryPress(notBefore = 0.0)` is called, **THEN** it returns true with `pressTime == 1.000`. **WHEN** called again, **THEN** it returns false (one press, one consumption)
- [ ] **AC-IM-03**: **GIVEN** a live press at `t = 1.000`, **WHEN** `TryConsumeParryPress(notBefore = 1.5)` is called, **THEN** it returns false (press older than the guard). **WHEN** `TryConsumeParryPress(notBefore = 0.9)` is called, **THEN** it returns true
- [ ] **AC-IM-04**: **GIVEN** `parry_input_buffer_ms = 120` and a press at `t = 1.000`, **WHEN** a parry window opens at `t = 1.080` and queries with `notBefore = 1.080 − 0.120 = 0.960`, **THEN** the press is consumable. **GIVEN** the same press but the window opens at `t = 1.200`, **THEN** the press has been pruned and is not consumable (validates Formula 2 front-extension)
- [ ] **AC-IM-05**: **GIVEN** three live presses at `t = 1.00, 1.02, 1.04`, **WHEN** `TryConsumeParryPress(notBefore = 0)` is called, **THEN** it returns `pressTime == 1.04` (most recent)
- [ ] A `HasLiveParryPress()` peek and a `TryConsumeParryPress` consume in the same frame give the same result in either call order (GDD edge case "two consumers touch the buffer in the same frame")
- [ ] An `IInputAuthority` test double (ADR-0010 rule 3) is available to consumer tests. It injects timestamped presses and honours the **same** consume-once / `notBefore` / most-recent-wins contract, which a shared contract test runs against both the real buffer and the fake

---

## Implementation Notes

*Derived from ADR-0004 Decision rule 3, input-manager Core Rules 6–7 and Formula 2, ADR-0010 rule 3:*

- `TryConsumeParryPress(float notBefore, out float pressTime)` scans the ring for the live (Formula 1) press with the **largest** `press_time` that also satisfies `press_time ≥ notBefore`. It sets that entry's `Consumed = true`, writes its time to `pressTime`, and returns `true`. If no entry qualifies, it returns `false` with `pressTime = 0`.
- `current_time` for the liveness check is the run-clock value. Expose the method on `IInputAuthority` with the exact architecture.md signature, which takes no time parameter. InputManager supplies `IGameClock.RunTime` internally, and the engine-free buffer method can take `currentTime` explicitly for testability.
- Formula 2 (front-extension) is **composed by the Parry System**, which owns `[w_open, w_close]` and decides `notBefore`. Input Manager only guarantees that a press is still live (≤ 120 ms old) and ≥ `notBefore`. Do **not** add any window or back-edge logic here (GDD Core Rule 10: no combat semantics).
- Extra presses from mashing are not consumed and simply age out. Mashing cannot bank multiple parries (GDD edge case).
- Test double: implement `FakeInputAuthority : IInputAuthority` in the test-helper location scaffolded by `/test-setup` (ADR-0010). It should either reuse the engine-free buffer or match its semantics, which the shared contract test enforces. Other `IInputAuthority` members on the fake (`CurrentMoveDirection`, `LastSteerDirection`, `SetContext`, …) are simple settable stubs.

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 002: ring storage, liveness predicate, pruning, capacity
- Story 004: producing presses from `Parry.performed` with run-clock stamps
- Parry System epic: opening and closing windows, computing `notBefore`, calling the consume API, the `ParrySucceeded` burst
- Combat Feedback UI epic: the optional "parry attempted" flourish that peeks `HasLiveParryPress()`

---

## QA Test Cases

*Written at story creation (lean review — QL-STORY-READY skipped). The developer implements against these. Do not invent new test cases during implementation.*

- **AC-1**: AC-IM-02 consume once
  - Given: a buffer with one press at `1.000`, current time `1.050`
  - When: `TryConsumeParryPress(0.0)` is called twice
  - Then: the first call returns `true` with `pressTime == 1.000`. The second returns `false`
  - Edge cases: `HasLiveParryPress()` is `false` after the consume, because a consumed press is not live

- **AC-2**: AC-IM-03 notBefore guard
  - Given: a press at `1.000`, current time `1.050`
  - When: `TryConsumeParryPress(1.5)`, then `TryConsumeParryPress(0.9)`
  - Then: `false` then `true` (`pressTime == 1.000`)
  - Edge cases: `notBefore == press_time` exactly (`1.000`) returns `true`, because the guard is `≥`

- **AC-3**: AC-IM-04 front-extension composition
  - Given: a press at `1.000`, window 120 ms
  - When (case 1): current time `1.080`, `Prune(1.080)`, `TryConsumeParryPress(0.960)`
  - Then (case 1): `true`, `pressTime == 1.000`
  - When (case 2): a fresh buffer with the same press, current time `1.200`, `Prune(1.200)`, `TryConsumeParryPress(1.080)`
  - Then (case 2): `false`, because the press is pruned
  - Edge cases: a press **after** `w_close` is a clean miss decided by Parry, not here. There is no back-edge extension in Input Manager, so assert that no API takes a window close

- **AC-4**: AC-IM-05 most-recent wins
  - Given: live presses at `1.00, 1.02, 1.04`, current time `1.05`
  - When: `TryConsumeParryPress(0)`
  - Then: `pressTime == 1.04`. A second call returns `1.02`, and a third returns `1.00`, while all are still live
  - Edge cases: a newer press filtered out by `notBefore` does not fall through to an *older* one. With `notBefore = 1.03`, only `1.04` qualifies. After it is consumed, a call with `notBefore = 1.03` returns `false`

- **AC-5**: Peek/consume order independence
  - Given: two identical buffers, each with one live press
  - When: buffer A runs peek→consume and buffer B runs consume→peek
  - Then: both consumes return `true` with the same `pressTime`. A's peek is `true` (before consume) and B's peek is `false` (after consume). Neither peek changes the result of a consume
  - Edge cases: none

- **AC-6**: The test double honours the contract
  - Given: a parameterised contract fixture run against (a) the real buffer / InputManager forwarding and (b) `FakeInputAuthority`
  - When: AC-1 to AC-4 above are executed against each
  - Then: the results are identical
  - Edge cases: the fake referencing `UnityEngine.InputSystem` is a compile error by design (consumer test asmdefs do not reference it)

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: `tests/unit/input-manager/input_manager_parry_consume_test.cs` — must exist and pass (EditMode, deterministic)

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 002
- Unlocks: Story 004; Parry System epic (consume API + fake); Combat Feedback UI epic (peek)

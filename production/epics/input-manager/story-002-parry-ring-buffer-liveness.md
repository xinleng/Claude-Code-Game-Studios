# Story 002: Parry press ring buffer — liveness, pruning & capacity

> **Epic**: Input Manager
> **Status**: Complete
> **Layer**: Foundation
> **Type**: Logic
> **Manifest Version**: 2026-10-10

## Context

**GDD**: `design/gdd/input-manager.md`
**Requirement**: `TR-IM-003`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0004: Single Input Authority and the Parry Input Buffer
**ADR Decision Summary**: Parry presses go into a preallocated, fixed-size ring buffer (`parry_buffer_capacity` = 4). A press is live while its age is ≤ `parry_input_buffer_ms` (120) and it is unconsumed. Pruning runs at the start of `InputManager.Tick`. All comparisons happen in the single `IGameClock.RunTime` domain.

**Engine**: Unity 6.3 LTS + Input System package (pinned) | **Risk**: HIGH (epic-level; this story's buffer is plain C# with no Input System types, so its own risk is LOW)
**Engine Notes** (ADR-0004): the buffer must not reference any `UnityEngine.InputSystem` type. Under ADR-0004 rule 6 the only files in `Dynasty.Input` that touch InputSystem types are `InputManager.cs` and `UiInputModuleBinder.cs`. Keeping the buffer engine-free is what makes AC-IM-01 runnable in EditMode with injected times. The engine-dependent capture path (`performed` timing relative to the frame edge, Dynamic Update) belongs to Stories 004, 007 and 010.

**Control Manifest Rules (Foundation layer)**:
- Required: parry presses go into the ring buffer (capacity 4, 120 ms life), stamped on `IGameClock.RunTime` (ADR-0004).
- Required: gameplay logic reads time through the injected `IGameClock` (ADR-0010).
- Forbidden: never mix values derived from `Time.time` with values derived from `Time.unscaledTime` (ADR-0010, conflict C1). The buffer receives `current_time` as a run-clock value. It never reads `Time.*`.
- Forbidden: never hardcode a gameplay/tuning value (ADR-0006). Capacity and lifetime come from the injected input tuning view. The only literals allowed are in boundary-value tests.
- Guardrail: fixed ring buffer with zero runtime allocation (ADR-0004 Performance), and an O(capacity) scan per query.

---

## Acceptance Criteria

*From GDD `design/gdd/input-manager.md` (Core Rule 5, Formula 1, Edge Cases), scoped to this story:*

- [ ] **AC-IM-01**: **GIVEN** a press recorded at run-local `t = 1.000` with `parry_input_buffer_ms = 120`, **WHEN** the buffer is queried at `t = 1.100`, **THEN** a live press exists. **WHEN** queried at `t = 1.121`, **THEN** no live press exists (pruned). **Pass:** `HasLiveParryPress() == true` at 1.100 and `== false` at 1.121
- [ ] Formula 1 boundary is **inclusive**: when `current_time − press_time == parry_input_buffer_ms / 1000`, the press is still live on that frame (GDD Edge Cases)
- [ ] Capacity is `parry_buffer_capacity` (default 4). Recording a 5th press while 4 are held **overwrites the oldest** (GDD mash edge case)
- [ ] `Prune(current_time)` removes only presses whose age exceeds the window. Live presses keep their `press_time` and `consumed` state
- [ ] `HasLiveParryPress()` is a **non-consuming** peek. Calling it any number of times does not change buffer state
- [ ] Recording, pruning and peeking allocate nothing (verified with a GC-allocation assertion in the test)

---

## Implementation Notes

*Derived from ADR-0004 Decision rules 1–2 and input-manager Formula 1:*

- Implement the buffer as a plain C# class inside `Dynasty.Input`, for example `ParryPressBuffer`. It holds a preallocated `ParryPress[]` (a struct of `float PressTime; bool Consumed`) sized from `parry_buffer_capacity` at construction, plus a write index and a count.
- The liveness predicate (Formula 1) is `(current_time − press.press_time ≤ parry_input_buffer_ms / 1000) AND (press.consumed == false)`. `current_time` is `IGameClock.RunTime`, which InputManager passes in (Rule 4 revised 2026-10-10). The GDD Formula 1 table still says "run-local unscaled time". The unified run clock supersedes that wording (GDD Rule 4 revision; ADR-0004 amendment (a); ADR-0010 rule 1).
- **Float-precision trap at the inclusive boundary:** in IEEE single precision, `1.12f − 1.0f` (≈ 0.12000000477) is **greater** than `0.12f` (≈ 0.11999999732). A naive `now − press ≤ window` therefore reports the exact-boundary press as dead. Choose the comparison form deliberately, either `press_time + window ≥ current_time` or double-precision ages, and make sure the inclusive boundary test (QA AC-2) passes at the GDD's example values. Record the chosen form in a code comment.
- Pruning runs at the start of `InputManager.Tick` (ADR-0004 rule 2; Story 007 owns the call site). It does not run while context ≠ Gameplay (freeze — Story 005), so `Prune` must be a separate, explicit call. Never prune as a side effect of `HasLiveParryPress`.
- Read the knobs from the injected input tuning view: `parry_input_buffer_ms` (default 120, safe range 60–200) and `parry_buffer_capacity` (default 4, range 2–8). Values come from `TuningConfig` (ADR-0006; game-data epic). Tests build them with the `TestConfigs` factory (ADR-0010 rule 3). Clamping is GameDataValidator's job, not this class's.

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 003: `TryConsumeParryPress(notBefore, out pressTime)`, most-recent-wins, consume-once
- Story 004: turning `Parry.performed` into a recorded press and stamping it on the run clock
- Story 005: freeze (no prune, no add) outside Gameplay
- Story 007: calling `Prune` at the start of `Tick` in the `[-200]` frame slot
- game-data epic: `TuningConfig` input section and validator clamps (60–200 ms, 2–8)

---

## QA Test Cases

*Written at story creation (lean review — QL-STORY-READY skipped). The developer implements against these. Do not invent new test cases during implementation.*

- **AC-1**: AC-IM-01 liveness then prune
  - Given: a buffer built from `TestConfigs` (window 120 ms, capacity 4), and one press recorded at `1.000`
  - When: `Prune(1.100)` then `HasLiveParryPress(1.100)`; then `Prune(1.121)` then `HasLiveParryPress(1.121)`
  - Then: `true` at 1.100, then `false` at 1.121, and the buffer count is 0 after the second prune
  - Edge cases: querying at 1.121 *without* pruning still returns `false`, because liveness is the predicate and does not depend on pruning

- **AC-2**: Inclusive boundary
  - Given: a press at `1.000` with a 120 ms window
  - When: `HasLiveParryPress(1.120)`
  - Then: `true` (boundary-value test, so literals are allowed)
  - Edge cases: repeat at `press = 0.0 / now = 0.120` and at a large run time (`press = 1199.000 / now = 1199.120`, the 20-minute run end) to show the chosen comparison form holds without float drift late in the run

- **AC-3**: Capacity overwrite (mashing)
  - Given: capacity 4 and presses at `1.00, 1.01, 1.02, 1.03`
  - When: a 5th press is recorded at `1.04`
  - Then: the buffer holds `{1.01, 1.02, 1.03, 1.04}`, so the oldest (`1.00`) was overwritten and the count stays 4
  - Edge cases: capacity 2 (lower safe bound) behaves the same way. Overwriting a *consumed* press is fine

- **AC-4**: Prune keeps live state intact
  - Given: presses at `1.000` (consumed) and `1.100` (unconsumed)
  - When: `Prune(1.150)` runs
  - Then: the `1.000` entry is removed (age 0.150 > 0.120), and the `1.100` entry remains with `Consumed == false`
  - Edge cases: an empty buffer prunes without error

- **AC-5**: Peek is non-consuming
  - Given: one live press
  - When: `HasLiveParryPress` is called 10 times
  - Then: it returns `true` every time, and the press's `Consumed` stays `false`
  - Edge cases: a buffer holding only consumed presses peeks `false`

- **AC-6**: Zero allocation
  - Given: a constructed buffer
  - When: record ×8, prune and peek run inside an allocation-measuring scope (`Assert.That(() => …, Is.Not.AllocatingGCMemory())`)
  - Then: no GC allocation
  - Edge cases: none

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: `tests/unit/input-manager/input_manager_parry_buffer_test.cs` — must exist and pass (EditMode, injected times, no `WaitForSeconds`)

**Status**: [x] `tests/unit/input-manager/input_manager_parry_buffer_test.cs` (12 cases), EditMode 151/151

---

## Dependencies

- Depends on: Story 001 (assembly, `IInputAuthority` surface)
- Unlocks: Story 003, Story 004, Story 005, Story 007

---

## Completion Notes
**Completed**: 2026-10-11
**Criteria**: 6/6 passing.
**Deviations**: Advisory —
- `ParryPressBuffer` (public, engine-free, `src/Input/`) holds the ring; InputManager owns one instance built from the new read-only view `IInputTuning` (`Dynasty.GameData`, `src/GameData/Tuning/`; `TuningConfig` implements it in GD-003). `Dynasty.Input` now references `Dynasty.GameData`. `InputManager.Initialize(clock, tuning)`.
- **Inclusive boundary form:** `(float)(press_time + window) >= current_time`. The explicit float cast matters: without it the runtime evaluated the sum at higher precision and the exact-boundary press (1.000 → 1.120) read as expired — caught by the boundary test. Holds at 0.0/0.120 and 1199.000/1199.120.
- Prune compacts in place preserving order (no assumption that stamps arrive sorted). Extra API: `Count`, `Capacity`, `TryGetEntry`, `Clear` (run reset).
**Test Evidence**: Logic — `tests/unit/input-manager/input_manager_parry_buffer_test.cs` (12 cases incl. zero-GC assertion); fixture `tests/helpers/TestInputTuning.cs`. EditMode 151/151.
**Code Review**: Self-review during the autonomous Sprint 1 batch; consolidated independent review at batch end. LP/QL gates skipped (lean).

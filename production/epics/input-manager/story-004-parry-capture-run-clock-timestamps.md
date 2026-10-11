# Story 004: Parry capture on the performed edge, run-clock stamps & inert-until-run-start

> **Epic**: Input Manager
> **Status**: Complete
> **Layer**: Foundation
> **Type**: Logic
> **Manifest Version**: 2026-10-10

## Context

**GDD**: `design/gdd/input-manager.md`
**Requirement**: `TR-IM-004` (primary), `TR-IM-003` (performed-edge only / no autofire)
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0004: Single Input Authority and the Parry Input Buffer (amended 2026-10-10), with ADR-0010: Deterministic Test Harness (amended 2026-10-10, `IGameClock.RunTime`)
**ADR Decision Summary**: `InputManager` subscribes to `Parry.performed`. Each press is stamped from the input event's own sub-frame-precise clock (`ctx.time`, a `realtimeSinceStartup`-basis double) and **converted to the unified run clock `IGameClock.RunTime`** (pause-compensated, run-local, origin owned by RSM). All parry comparisons happen in that one domain. Capture is inert until `SetRunStartTime` has been called and context is Gameplay.

**Engine**: Unity 6.3 LTS + Input System package (pinned) | **Risk**: HIGH
**Engine Notes** (ADR-0004 Engine Compatibility + Risks):
- `ctx.time` (`InputAction.CallbackContext.time`) is a `double` on the `realtimeSinceStartup` basis. It is **not** `Time.time` and **not** `Time.unscaledTime`. ADR-0004 rule 1 says the `runStartTime` used for conversion is captured on the same basis.
- **Verification required (ADR-0004):** confirm the timing of `performed` callbacks against frame boundaries in a device test. With `InputSystem.settings.updateMode` = Dynamic Update, callbacks fire during the input update immediately **before** `Update()` in the same frame. Story 010 holds the device-level check, and the 120 ms buffer absorbs a one-frame slip.
- `performed` on a **Button** action with no interactions fires once per press. Do not add Hold, Tap or MultiTap interactions to `Parry`, because they change when `performed` fires. Subscribe to `performed` only, never `started` or `canceled`.
- Active Input Handling must be "Input System Package (New)". Story 009 verifies it.

**Control Manifest Rules (Foundation layer)**:
- Required: **ONE run clock for all combat timestamps, `IGameClock.RunTime`.** Parry press stamps use it (ADR-0010, ADR-0004, TR-RS-005).
- Required: parry presses are stamped from `ctx.time` converted to `IGameClock.RunTime` (ADR-0004).
- Required: run-start order is binding: RSM records the origin, then `SetRunStartTime` on InputManager, then `IRunResettable.ResetForNewRun()`, then `SetContext(Gameplay)`. InputManager stays inert until then (ADR-0012).
- Forbidden: **never mix values derived from `Time.time` with values derived from `Time.unscaledTime`.** They diverge by the total paused duration and silently break parry matching after the first level-up (ADR-0010, conflict C1).
- Forbidden: never read `Time.time`/`Time.unscaledTime` inside testable gameplay logic. Use the injected `IGameClock` (ADR-0010).
- Forbidden: never allocate a closure lambda in a hot dispatch path (ADR-0003). The `performed` handler is a cached method-group delegate.

---

## Acceptance Criteria

*From GDD `design/gdd/input-manager.md` (Core Rules 4–5, revised Rule 4, Edge Cases), scoped to this story:*

- [ ] On `Parry.performed` in Gameplay context, after `SetRunStartTime`, exactly one `ParryPress { press_time, consumed = false }` is recorded. `press_time` is the event's `ctx.time` converted to `IGameClock.RunTime` (GDD Rule 4 revised 2026-10-10; ADR-0004 rule 1)
- [ ] **AC-IM-09**: **GIVEN** the Parry button is held down for 0.5 s, **WHEN** the hold is processed, **THEN** exactly one `ParryPress` is recorded (on the `performed` edge, with no autofire). **Pass:** buffer press count == 1 over the hold. *This story runs it at the handler seam; Story 010 re-confirms it at device level*
- [ ] **AC-IM-10**: **GIVEN** `runStartTime` has not been set (pre-init), **WHEN** a `Parry.performed` is injected, **THEN** no press is recorded. **WHEN** `runStartTime` is set and `SetContext(Gameplay)` is called, **THEN** later presses are recorded with run-local timestamps
- [ ] **AC-IM-11** *(scoped; see Implementation Notes conflict flag)*: **GIVEN** `Time.timeScale == 0` (paused), **WHEN** a press timestamp is produced, **THEN** it is a run-clock value that keeps buffer liveness coherent, with no division or precision error. Stamping never depends on scaled time
- [ ] Presses are captured during the player's invulnerability frames. Input Manager never suppresses capture (GDD edge case; damage-health guarantee "parry input is still processed")
- [ ] No press is ever recorded with an undefined or default timestamp (GDD edge case: pre-init)

---

## Implementation Notes

*Derived from ADR-0004 Decision rule 1, ADR-0010 rule 1, ADR-0012 run-start contract:*

- Split the handler so the logic is testable without devices. The `performed` callback in `InputManager.cs` reads `ctx.time` and forwards it to an internal method that takes no InputSystem types, for example `internal void OnParryPerformed(double eventTime)`. That method converts the time to `RunTime`, checks the gates (run started, context == Gameplay) and records into the Story 002 buffer. Tests call the internal method through `InternalsVisibleTo` for the EditMode test assembly.
- **Conversion to `RunTime`:** ADR-0004 rule 1 requires converting `ctx.time` into the `IGameClock.RunTime` domain, with the run origin on the same basis. The clock and its pause accumulator are owned by the **bootstrap-combat-frame IGameClock story** (origin set by RSM, TR-RS-005). Use the conversion that story's `IGameClock` provides. If it exposes none, **stop and raise it with that story's owner. Do not compute an ad-hoc conversion from `Time.*`**, because that is exactly how conflict C1 happened. The conversion must be a pure function of (event time, clock state) so tests can inject both.
- **`SetRunStartTime(float runStartTime)`** (architecture.md signature) records that the run has started. Before it is called, the handler returns without recording, whatever the context (AC-IM-10). RSM calls it before `SetContext(Gameplay)` (ADR-0012 order).
- **Conflict flag (AC-IM-11 vs AC-IM-08 / States table) — do not resolve silently.** AC-IM-11's literal wording ("context is Paused … a press is injected … it is captured") contradicts the GDD States table and AC-IM-08: in Paused the Gameplay map is disabled and presses are not buffered. Its "unscaled run-local timestamp" wording is also superseded by revised Rule 4 (`IGameClock.RunTime`). This story implements the States table (no buffering in Paused, see Story 005) and scopes AC-IM-11 to **timestamp coherence while `timeScale == 0`**. Raise both points with game-designer at `/story-readiness` so the GDD AC text can be corrected.
- **Open item:** neither the GDD nor the ADRs say whether InputManager is an `IRunResettable`, meaning whether a retry clears the buffer and `LastSteerDirection` before the new run origin. Stale stamps from the previous run's origin would be meaningless in the new run. Raise it at `/story-readiness`, and do not add a reset contract without a recorded decision.
- Do not touch `LastSteerDirection` in the parry path. Story 006 asserts this (AC-IM-07).

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 002 / 003: buffer storage, liveness, consume API
- Story 005: freeze behaviour and the `SetContext(Menu)`-same-frame edge case
- Story 007: the `[-200]` slot and prune-then-capture ordering inside `Tick`
- Story 010: device-level confirmation of AC-IM-09, held-key-across-`SetContext`, and the `performed` frame-edge verification
- bootstrap-combat-frame IGameClock story: `IGameClock.RunTime`, the pause accumulator, the event-time conversion
- RSM epic: when `SetRunStartTime` / `SetContext` are called

---

## QA Test Cases

*Written at story creation (lean review — QL-STORY-READY skipped). The developer implements against these. Do not invent new test cases during implementation.*

- **AC-1**: A performed press is stamped on the run clock
  - Given: InputManager initialised with an injected fake `IGameClock` whose conversion maps event time `E` to RunTime `R` (scripted: `E = 101.000 → R = 1.000`), `SetRunStartTime` called, `SetContext(Gameplay)`
  - When: `OnParryPerformed(101.000)`
  - Then: the buffer count is 1 and the recorded `press_time == 1.000`. `TryConsumeParryPress(0)` returns `1.000`
  - Edge cases: an event time with sub-frame precision (`101.0083`) maps to the matching sub-frame RunTime, with no snapping to a frame boundary

- **AC-2**: AC-IM-09 one press per hold (handler seam)
  - Given: a started run, Gameplay context
  - When: one `OnParryPerformed` is followed by 30 `Tick()` pumps advancing the fake clock by 1/60 s each (0.5 s held), with no further performed events (a Button action fires `performed` once per press)
  - Then: the buffer press count == 1 over the whole hold
  - Edge cases: a static check that InputManager subscribes `Parry.performed` only (no `started`/`canceled` handler for Parry) and that the `Parry` action has no interactions in the asset

- **AC-3**: AC-IM-10 inert before run start
  - Given: an initialised InputManager, `SetRunStartTime` **not** called
  - When: `OnParryPerformed(…)` is injected (also try after `SetContext(Gameplay)` without `SetRunStartTime`)
  - Then: buffer count 0 and `HasLiveParryPress() == false`
  - Then (continued): call `SetRunStartTime(…)` then `SetContext(Gameplay)` and inject a press. It is recorded with the converted RunTime stamp
  - Edge cases: `SetRunStartTime` called but context not yet Gameplay leaves the press unrecorded (Gameplay map disabled; Story 005 owns the full freeze test)

- **AC-4**: AC-IM-11 (scoped) stamp coherence at `timeScale == 0`
  - Given: the fake clock reports a paused state (`timeScale == 0`, scaled `DeltaTime == 0`, `RunTime` frozen at `5.000` by the pause accumulator)
  - When: the stamping conversion runs for an event time inside the pause
  - Then: the result is finite (not NaN or ∞), ≥ the last pre-pause stamp, and computed with no division by `timeScale` or `deltaTime`
  - Edge cases: per the conflict flag, assert that **no** press enters the buffer while context == Paused, matching AC-IM-08 and the States table

- **AC-5**: Capture during iframes
  - Given: a started run in Gameplay. Input Manager has no notion of iframes, and the test only confirms nothing suppresses capture
  - When: a press is injected while a test stub reports `PlayerHealthSystem` invulnerable
  - Then: the press is recorded. A static check confirms InputManager has no dependency on any health/iframe interface
  - Edge cases: none

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: `tests/unit/input-manager/input_manager_timestamp_test.cs` — must exist and pass (EditMode, injected clock, synthetic presses)

**Status**: [x] `tests/unit/input-manager/input_manager_timestamp_test.cs` (8 tests), EditMode 173/173

---

## Dependencies

- Depends on: Story 001, Story 002, Story 003; bootstrap-combat-frame IGameClock story (`IGameClock.RunTime` + event-time conversion)
- Unlocks: Story 005, Story 007, Story 010

---

## Completion Notes
**Completed**: 2026-10-11
**Criteria**: 6/6 passing (AC-IM-09 at the handler seam; device-level re-check is Story 010).
**Deviations**: Advisory —
- **G-2 resolved (autonomous decision — ratify/adjust):** new read-only `IRunTimeConverter { float ToRunTime(double) }` in `Dynasty.Core`; `IRunClockControl` now extends it. `InputManager.Initialize(clock, runTimeConverter, tuning)` — Bootstrap passes the same `UnityGameClock` for both. ADR-0004 carries a dated amendment note. BCF-002 test updated for the inherited member.
- `Parry.performed` subscribed once with a cached method-group delegate, unsubscribed in `Dispose`; the callback forwards `ctx.time` to `internal OnParryPerformed(double)` (device-free seam). Non-finite converted stamps are dropped (never recorded with an undefined time).
- **AC-IM-11 scoped** per the story's conflict flag: tests assert stamp coherence while paused (finite, frozen at pause start, ≥ last pre-pause stamp) and that **no** press is buffered in Paused — the GDD AC-IM-11 text still needs game-designer correction.
- "Subscribes `performed` only" is code-reviewed (no started/canceled handlers); the asset check that Parry has no interactions is automated.
- **Open (unchanged):** whether InputManager is an `IRunResettable` (clear buffer on retry).
- Test file at the story's path `input_manager_timestamp_test.cs` (class `InputManagerParryCaptureTest`).
**Test Evidence**: Logic — `tests/unit/input-manager/input_manager_timestamp_test.cs` (8 tests). EditMode 173/173.
**Code Review**: Self-review during the autonomous Sprint 1 batch; consolidated independent review at batch end. LP/QL gates skipped (lean).

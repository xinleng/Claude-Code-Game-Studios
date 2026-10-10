# Story 001: Run clock origin, pause accumulator, and countdown

> **Epic**: Run Session Manager
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Logic
> **Manifest Version**: 2026-10-10

## Context

**GDD**: `design/gdd/run-session-manager.md`
**Requirement**: `TR-RS-005`, `TR-RS-001`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0010: Deterministic Test Harness (amended 2026-10-10)
**ADR Decision Summary**: Gameplay reads time only through an injected `IGameClock { Time; UnscaledTime; DeltaTime; RunTime; }`. `RunTime` is the single pause-compensated run-local clock domain for every combat timestamp; RSM owns the run origin and the paused-duration accumulator behind it.
*Secondary*: ADR-0012 Decision 4 — RSM owns the `run_duration_s` countdown (TuningConfig, default 1200s); no other system keeps a run timer.

**Engine**: Unity 6.3 LTS | **Risk**: LOW
**Engine Notes**: No post-cutoff APIs. Testable logic must not read `Time.time`/`Time.unscaledTime` directly — only the injected `IGameClock` (tests advance it with `clock.Advance(...)`).

**Control Manifest Rules (this layer)**:
- Required: ONE run clock for all combat timestamps: `IGameClock.RunTime` (pause-compensated, run-local, origin owned by RSM) — ADR-0010, ADR-0004 (TR-RS-005)
- Required: RSM owns the run countdown (`run_duration_s`, default 1200s from TuningConfig); no other system keeps a run timer — ADR-0012
- Forbidden: Never mix `Time.time`-derived and `Time.unscaledTime`-derived run-local values (conflict C1) — ADR-0010
- Forbidden: Never hardcode a gameplay/tuning value (exception: boundary-value tests) — ADR-0006
- Guardrail: Gameplay logic reads time via injected `IGameClock` — ADR-0010

---

## Acceptance Criteria

*From GDD `design/gdd/run-session-manager.md` (Core Rule 2, Formulas, Tuning Knobs) and TR-RS-005, scoped to this story:*

- [ ] On run start, RSM records the run origin; `run_time` (exposed as `IGameClock.RunTime`) reads exactly 0 on that frame.
- [ ] `time_remaining = max(0, run_duration_s − run_time)`, with `run_duration_s` read from TuningConfig (default 1200) — never a literal in production code.
- [ ] While the run is paused, `RunTime` does not advance; after resume it continues from the value it held at pause start — no jump forward by the paused duration and no drift.
- [ ] Across multiple pause intervals, the paused durations accumulate (`RunTime` = unpaused elapsed time only).
- [ ] RSM is the only writer of the run origin and the paused-duration accumulator; consumers read `RunTime` only.

---

## Implementation Notes

*Derived from ADR-0010 Decision 1, ADR-0004 Decision 1, ADR-0012 Decision 4:*

- `RunTime` is pause-compensated and run-local. Per ADR-0004 rule 1, parry press stamps come from the Input System event clock (`ctx.time`, `realtimeSinceStartup` basis) and are converted to `RunTime`; the run origin must therefore be captured **on the same (unscaled/realtime) basis**, with the paused-duration accumulator subtracting every pause interval. The GDD formula `run_time = Time.time − runStartTime` predates the ADR-0010 amendment — the clock *domain* is now governed by ADR-0010 (unified, pause-compensated), the meaning (seconds of run elapsed) is unchanged.
- RSM drives three operations on the clock: begin-run (set origin, zero accumulator), begin-pause (record pause start), end-pause (add the interval to the accumulator). Pause/resume are invoked by the pause-orchestration stories (004/005) — this story provides the operations and proves the math.
- The `IGameClock` interface and its play-mode binding are delivered by the **bootstrap-combat-frame** epic (IGameClock story). If that story already implements the accumulator arithmetic, this story wires RSM as its sole writer and adds the countdown — do not duplicate the clock implementation.
- Read `run_duration_s` from the TuningConfig view injected at construction (game-data epic). Tests build configs with `TestConfigs.Default()`.
- No `Update()` in RSM logic under test; tests drive time with the fake clock's `Advance(dt)` and the manual pump.
- Doc comments on all public APIs; PascalCase public / `_camelCase` private.

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 002: flow state machine and the win/death resolution decision
- Story 003: distributing the origin via `SetRunStartTime` to InputManager/EnemySystemManager/WaveSpawner (run-start contract)
- Story 004/005: when pauses begin/end (level-up orchestration, `timeScale`, `SetContext`)
- Story 006: scheduled-event triggers derived from the clock
- Story 007: resetting the timer to `run_duration_s` on retry

---

## QA Test Cases

*Written at story creation (lean review — QL-STORY-READY skipped). The developer implements against these — do not invent new test cases during implementation.*

- **AC-1**: Run origin recorded; `run_time` begins at 0
  - Given: a fake `IGameClock` at an arbitrary non-zero unscaled time (e.g. 37.25s since app start) and RSM constructed with `TestConfigs.Default()`
  - When: RSM begins the run
  - Then: `RunTime == 0` on that frame; after `clock.Advance(1.0f)`, `RunTime == 1.0` (within float epsilon)
  - Edge cases: origin captured at app time 0 also yields `RunTime == 0`; begin-run called twice without a reset is rejected/ignored (no silent origin overwrite mid-run)

- **AC-2**: `time_remaining` countdown is data-driven
  - Given: TuningConfig `run_duration_s = 1200`
  - When: run time advances to 0, 600, 1199.9, 1200, 1250
  - Then: `time_remaining` = 1200, 600, 0.1, 0, 0 (clamped at 0, never negative)
  - Edge cases: a config with `run_duration_s = 600` (safe-range minimum) yields `time_remaining == 600` at start — proves no literal 1200 in code

- **AC-3**: `RunTime` frozen during pause, continuous after resume
  - Given: a running run at `RunTime == 100.0`
  - When: begin-pause; `clock.Advance(30.0f)` (unscaled); end-pause; `clock.Advance(0.5f)`
  - Then: `RunTime == 100.0` throughout the pause; `RunTime == 100.5` after resume (no +30 jump)
  - Edge cases: zero-length pause (begin/end in the same frame) leaves `RunTime` unchanged; end-pause without begin-pause is a no-op

- **AC-4**: Multiple pauses accumulate
  - Given: a run started at origin
  - When: run 10s → pause 5s → run 10s → pause 7s → run 10s
  - Then: `RunTime == 30.0` and the accumulator equals 12.0
  - Edge cases: 20 consecutive pause/resume cycles produce identical `RunTime` to an unpaused 30s run (no float drift beyond epsilon)

- **AC-5**: Single writer
  - Given: the RSM public/consumer surface
  - When: inspected (compile-level) and exercised by a consumer double
  - Then: consumers can only read `RunTime`; origin/accumulator mutators are reachable only from RSM (internal or RSM-owned interface)
  - Edge cases: N/A

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: `tests/unit/run-session-manager/run_session_manager_run_clock_test.cs` — must exist and pass

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: bootstrap-combat-frame epic — project setup story (Unity project, asmdefs, Test Framework) and `IGameClock` (unified `RunTime`) story DONE; game-data epic — TuningConfig story (`run_duration_s`) DONE (or `TestConfigs.Default()` factory available)
- Unlocks: Story 002, Story 003, Story 004, Story 006

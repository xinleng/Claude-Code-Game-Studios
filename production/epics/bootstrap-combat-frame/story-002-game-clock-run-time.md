# Story 002: IGameClock with Unified Pause-Compensated RunTime + Test Clock

> **Epic**: Bootstrap, CombatFrame & Run Clock
> **Status**: Complete
> **Layer**: Foundation
> **Type**: Logic
> **Manifest Version**: 2026-10-10

## Context

**GDD**: `design/gdd/run-session-manager.md` (Core Rule 2 — run clock; Formulas `run_time`), `design/gdd/damage-health.md` (Implementation Contract #2 — iframes on unscaled time; AC-DH-07 time control)
**Requirement**: `TR-RS-005` (unified run clock), `TR-TP-001` (injected clock, deterministic tests)
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

> **GDD wording superseded**: run-session-manager.md Rule 2 / Formulas say `runStartTime = Time.time` and `run_time = Time.time − runStartTime`. Per architecture.md v1.1 amendment (1) and TR-RS-005, that wording is superseded — all combat timestamps use the pause-compensated `IGameClock.RunTime`. The amended ADRs win.

**ADR Governing Implementation**: ADR-0010: Deterministic Test Harness (Decision 1 — Clock, amended 2026-10-10); ADR-0004 §1 (press-stamp basis, amended 2026-10-10)
**ADR Decision Summary**: Gameplay never reads `Time.*` directly; it reads an injected `IGameClock { float Time; float UnscaledTime; float DeltaTime; float RunTime; }`. `RunTime` is the single pause-compensated, run-local domain for ALL combat timestamps; RSM owns the run origin and the paused-duration accumulator behind it. Play mode binds the clock to Unity / Input System time; tests advance it explicitly (`clock.Advance(0.501f)`).

**Engine**: Unity 6.3 LTS | **Risk**: LOW
**Engine Notes**: No post-cutoff APIs. Input System `ctx.time` is on a `realtimeSinceStartup` basis (double, sub-frame precise) and the run origin used to convert press stamps must be captured on the **same** basis (ADR-0004 §1). Verify in the Editor that `Time.realtimeSinceStartupAsDouble` and Input System event time share an epoch (in the Editor, realtime may count from editor start rather than play start — this is exactly why the basis must match). Float precision: run-local values stay < ~1300 s, so float exposure is safe at 350 ms windows (TR-ED-010); compute internally in double.

**Control Manifest Rules (this layer)**:
- Required: ONE run clock for all combat timestamps: `IGameClock.RunTime` (pause-compensated, run-local, origin owned by RSM) (ADR-0010, ADR-0004, TR-RS-005)
- Required: Gameplay logic reads time via injected `IGameClock` (ADR-0010)
- Forbidden: Never mix `Time.time`-derived and `Time.unscaledTime`-derived run-local values — they diverge by total paused duration (ADR-0010, conflict C1)
- Forbidden: Never read `Time.time`/`Time.unscaledTime` inside testable gameplay logic (ADR-0010) — the Unity-backed clock binding is the single permitted reader
- Guardrail: one interface indirection per time read; negligible cost (ADR-0010 Performance)

---

## Acceptance Criteria

*From ADR-0010 Decision 1, TR-RS-005, epic DoD, damage-health Contract #2 / AC-DH-07, scoped to this story:*

- [ ] `IGameClock` exposes exactly `Time`, `UnscaledTime`, `DeltaTime`, `RunTime` (float, read-only), with doc comments stating each one's domain and intended consumers
- [ ] Before a run origin is marked, `RunTime` reads 0 (consumers are inert until run start — ADR-0012 §3); after the origin is marked, `RunTime` equals elapsed un-paused seconds since the origin
- [ ] **Pause compensation (epic DoD)**: across a simulated `timeScale = 0` pause interval, `RunTime` does not advance, never decreases, and resumes from exactly the value it held at pause start — i.e. `RunTime == (realtime − origin) − totalPausedDuration`
- [ ] During a pause `UnscaledTime` keeps advancing (iframes run on unscaled time — damage-health Contract #2) and `DeltaTime == 0` (scaled; regen pauses — architecture.md §Data Flow 4)
- [ ] Origin and pause accumulation are controlled only through a run-clock control interface that is separate from `IGameClock` (RSM is its sole intended holder — ADR-0010 "RSM owns the run origin and the paused-duration accumulator"); `IGameClock` consumers cannot mutate the clock
- [ ] Re-marking the origin for a new run (retry, ADR-0012 §6) resets `RunTime` to 0 and clears the paused-duration accumulator
- [ ] A `realtimeSinceStartup`-basis stamp (Input System `ctx.time` basis) converts to `RunTime` on the same domain: a stamp taken at the same instant the clock reports `RunTime = R` converts to `R` (ADR-0004 §1)
- [ ] Test clock: `FakeGameClock.Advance(dt)` advances `UnscaledTime` by exactly `dt`, advances `Time` by `dt × TimeScale`, sets `DeltaTime = dt × TimeScale`, and advances `RunTime` by `dt` only while not paused; identical call sequences produce identical values on every run (ADR-0010 validation: 10 consecutive runs)
- [ ] The Unity-backed play-mode clock is the only class in runtime assemblies that reads `UnityEngine.Time` time values

---

## Implementation Notes

*Derived from ADR-0010 Decision 1 (amended), ADR-0004 §1, architecture.md v1.1 amendment (1):*

- **Location**: `IGameClock` and the play-mode implementation in `Dynasty.Core`; `FakeGameClock` in `tests/helpers/` (the existing scaffold test already expects the name `FakeGameClock`).
- **Interface**: `IGameClock { float Time; float UnscaledTime; float DeltaTime; float RunTime; }` — exactly as ADR-0010 specifies. Do not add setters.
- **Run-clock control (name proposed, e.g. `IRunClockControl`)**: `MarkRunOrigin()`, `BeginPause()`, `EndPause()`, plus the stamp conversion (e.g. `float ToRunTime(double realtimeStamp)`). The play-mode clock implements both interfaces; Bootstrap (Story 005) hands `IGameClock` to everyone and the control interface to RSM only. RSM *calls* these at run start / pause / resume — the call sites are the run-session-manager epic.
- **Formula**: `RunTime = (now_realtime − origin_realtime) − pausedTotal`, where `pausedTotal` includes the in-progress pause (`now − pauseStart`) while paused. Keep `origin`, `pauseStart`, `pausedTotal` as `double`; expose `float`.
- **Basis**: origin is captured on the `realtimeSinceStartup` basis (`Time.realtimeSinceStartupAsDouble`) so InputManager can convert `ctx.time` stamps exactly (ADR-0004 §1). Never derive `RunTime` from `Time.time` (scaled) — that is the C1 mixing hazard.
- **Frame coherence (recommended — verify)**: all consumers in one frame must read the same `RunTime` (e.g. every enemy's `AttackStartTime` in a frame). Recommended approach without adding a new execution anchor: sample realtime lazily on the first read per frame (key on `Time.frameCount`) and cache for the rest of the frame. ADR-0002 allows only the existing anchors — do not add a new `[DefaultExecutionOrder]` for the clock.
- **Pause API robustness**: `BeginPause` while already paused and `EndPause` while not paused are no-ops (never double-count, never produce negative accumulation).
- **Stamp conversion edge**: a stamp earlier than the origin, or falling inside a paused interval, must not produce a negative or NaN value; the press-handling semantics during pause are owned by input-manager AC-IM-11 — this story only guarantees a finite, non-negative, monotonic conversion.
- **FakeGameClock**: implements both interfaces; `TimeScale` settable to emulate RSM's `timeScale = 0`; `Advance(float dt)` is the only way time moves (no real time). No static state — each test constructs its own (isolation).
- Doc comments on every public member (coding standards).

---

## Out of Scope

*Handled by neighbouring stories / epics — do not implement here:*

- Story 003: `CombatFrame.Update()` passing `IGameClock.DeltaTime` into `RunFrame(dt)`
- Story 005: Bootstrap constructing the single clock instance and injecting it (control interface → RSM only)
- Story 006: static check that no other runtime class reads `UnityEngine.Time`
- run-session-manager epic: *when* the origin is marked (run-start contract), pause/resume call sites, `run_duration_s` countdown, `time_remaining`
- input-manager epic: converting `ctx.time` press stamps (calls this story's conversion), AC-IM-11 pause behaviour
- damage-health epic: iframe timer reading `UnscaledTime`

---

## QA Test Cases

*Written by qa-lead at story creation (lean mode — QL-STORY-READY skipped). Values below are boundary/illustrative values where the number IS the point; otherwise use `TestConfigs` factories.*

- **AC-1**: `IGameClock` exposes `Time`, `UnscaledTime`, `DeltaTime`, `RunTime`
  - Given: the `IGameClock` type
  - When: reflected over its public members
  - Then: exactly four read-only `float` properties with those names exist; no setters
  - Edge cases: none
- **AC-2**: `RunTime` is 0 before origin, run-local after
  - Given: a `FakeGameClock` advanced 3.0 s with no origin marked
  - When: `RunTime` is read; then `MarkRunOrigin()` and `Advance(2.0f)`
  - Then: `RunTime == 0` before origin; `RunTime == 2.0` (±1e-5) after
  - Edge cases: read immediately after `MarkRunOrigin()` with no advance → exactly 0
- **AC-3**: Pause compensation — no advance, monotonic, resumes in place
  - Given: origin marked, `Advance(10f)` (`RunTime == 10`)
  - When: `BeginPause()`, `TimeScale = 0`, `Advance(5f)`, `EndPause()`, `TimeScale = 1`, `Advance(1f)`
  - Then: `RunTime == 10` throughout the pause (sample after every advance); `RunTime == 11` after resume; sampled sequence is non-decreasing
  - Edge cases: two consecutive pause intervals (5 s and 3 s) accumulate to 8 s of compensation; pause of 0 s duration changes nothing
- **AC-4**: `UnscaledTime` advances and `DeltaTime == 0` during pause
  - Given: origin marked, paused with `TimeScale = 0`, recorded `UnscaledTime = U0`
  - When: `Advance(0.5f)`
  - Then: `UnscaledTime == U0 + 0.5`; `DeltaTime == 0`; `Time` unchanged
  - Edge cases: `TimeScale = 1` → `DeltaTime == dt` exactly
- **AC-5**: Control is separate from `IGameClock`
  - Given: the `IGameClock` interface and the run-clock control interface
  - When: reflected
  - Then: `IGameClock` declares no mutating members; origin/pause/convert members live only on the control interface
  - Edge cases: none
- **AC-6**: New-run origin resets `RunTime` and the accumulator
  - Given: a run with `RunTime == 30` and 5 s accumulated pause
  - When: `MarkRunOrigin()` again, then `Advance(1f)`
  - Then: `RunTime == 1.0` (accumulator cleared; old pause not subtracted)
  - Edge cases: `MarkRunOrigin()` called while paused → new run starts un-paused, `RunTime == 0`
- **AC-7**: Realtime-basis stamp conversion
  - Given: origin marked at realtime `T0`, then 4 s elapsed including a 1 s pause that has ended
  - When: converting a stamp equal to the current realtime
  - Then: result equals the clock's current `RunTime` (== 3.0)
  - Edge cases: stamp before origin → 0 (not negative); stamp inside the ended pause interval → finite, ≤ current `RunTime`, ≥ 0; no NaN
- **AC-8**: Deterministic test clock
  - Given: a fixed script of `Advance` / `BeginPause` / `EndPause` / `TimeScale` calls (include `Advance(0.501f)` — the AC-DH-07 boundary)
  - When: the script is executed 10 times on fresh `FakeGameClock` instances
  - Then: every run yields bit-identical `Time`, `UnscaledTime`, `DeltaTime`, `RunTime` sequences
  - Edge cases: `Advance(0f)` changes nothing
- **AC-9**: Only the Unity-backed clock reads `UnityEngine.Time`
  - Given: runtime source of this story
  - When: reviewed (automated gate lands in Story 006)
  - Then: `UnityEngine.Time` time reads appear only in the play-mode clock class
  - Edge cases: writing `Time.timeScale` (RSM) is not a read and is out of this rule

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: `tests/unit/game-clock/game_clock_run_time_test.cs` — must exist and pass (EditMode, `FakeGameClock` only, no real time)
- Helper: `tests/helpers/FakeGameClock.cs`

**Status**: [x] Created and passing — `tests/unit/game-clock/game_clock_run_time_test.cs` (37 tests) + `tests/helpers/FakeGameClock.cs`

---

## Dependencies

- Depends on: Story 001 (project + `Dynasty.Core` / test asmdefs) must be DONE
- Unlocks: Story 003 (CombatFrame reads `DeltaTime`), Story 005 (Bootstrap injects the clock); run-session-manager epic (origin/pause call sites), input-manager epic (stamp conversion), every timing-sensitive Logic story

---

## Completion Notes
**Completed**: 2026-10-10
**Criteria**: 9/9 passing — AC-1…AC-8 by automated EditMode tests (suite 38/38 green via `unity test`); AC-9 by code review (only `src/Core/Time/UnityGameClock.cs` reads `UnityEngine.Time`; automated gate = BCF-006)
**Test Evidence**: Logic — `tests/unit/game-clock/game_clock_run_time_test.cs` (37 tests), helper `tests/helpers/FakeGameClock.cs`
**Code Review**: Complete — APPROVED WITH SUGGESTIONS (unity-specialist: 0 blocking; qa-tester: TESTABLE with minor gaps). Applied before closure: `MarkRunOrigin` re-samples realtime (hitched-frame origin bug); NaN/±Infinity stamp test with origin marked; exact post-overflow retained/archived values; re-mark-after-overflow; 1e6-realtime double-precision test; now-before-origin; `FakeGameClock.Advance` rejects negative/non-finite dt; doc notes on realtime basis (focus loss) and same-frame stamp collapse.
**Deviations** (advisory):
- Extra file `src/Core/Time/RunClockState.cs` — shared pure math so the production formula is unit-tested directly (sanctioned by the implementation brief)
- Implemented while dependency BCF-001 awaits AC-10 (CI green) — user-directed
**Follow-ups (owned elsewhere)**:
- run-session-manager epic: pause the run clock on focus loss (`OnApplicationFocus`) or set `runInBackground` — RunTime is realtime-based and would otherwise burn the run countdown while alt-tabbed
- input-manager epic: G-2 — recommended narrow `IRunTimeConverter { float ToRunTime(double) }` handed to InputManager by Bootstrap; run the ctx.time vs `realtimeSinceStartupAsDouble` epoch diagnostic (unity-specialist confirmed shared basis from Input System 1.18.0 source, ~90% confidence)
- BCF-005/006: PlayMode smoke for `UnityGameClock` frame-cache coherence (no automated coverage of the cache today)

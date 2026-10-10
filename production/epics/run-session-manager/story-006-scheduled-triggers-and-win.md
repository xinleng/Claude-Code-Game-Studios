# Story 006: Scheduled-event triggers from the run clock and win resolution

> **Epic**: Run Session Manager
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Integration
> **Manifest Version**: 2026-10-10

## Context

**GDD**: `design/gdd/run-session-manager.md` (also `design/gdd/game-concept.md`, `design/gdd/wave-spawner.md` for the timeline)
**Requirement**: `TR-GC0-002`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0012: Scene Organisation and Boot Flow
**ADR Decision Summary**: RSM owns the `run_duration_s` countdown (TuningConfig, default 1200s) and derives every scheduled event trigger (wave steps 120s, elites 300/600/900s ±5s, boss 1080s, win at 0) from the single run clock it exposes. No other system keeps its own run timer.

**Engine**: Unity 6.3 LTS | **Risk**: LOW
**Engine Notes**: No post-cutoff APIs. Pure logic over `IGameClock.RunTime`.

**Control Manifest Rules (this layer)**:
- Required: RSM owns the run countdown (`run_duration_s`, default 1200s from TuningConfig) and derives every scheduled trigger (wave steps, elites, boss, win) from the run clock; no other system keeps a run timer — ADR-0012
- Required (Feature, consumer side): Spawn scheduling is a pure function of the run clock — ADR-0005, ADR-0012
- Forbidden: Never hardcode a gameplay/tuning value — ADR-0006
- Guardrail: Gameplay logic reads time via injected `IGameClock` — ADR-0010

---

## Acceptance Criteria

*From GDD `design/gdd/run-session-manager.md` (AC-RS-03, Core Rule 6) and TR-GC0-002, scoped to this story:*

- [ ] **AC-RS-03**: GIVEN `run_time ≥ run_duration_s` (1200s) and the player alive, WHEN the check runs, THEN the Win screen fires and spawning halts.
- [ ] RSM publishes a wave-step trigger each time `RunTime` crosses a multiple of the escalation interval (default 120s), a trigger for each elite time (300/600/900s), a boss trigger at 1080s, and the win trigger at `run_duration_s` — every time value read from TuningConfig.
- [ ] Each trigger fires exactly once per run, on the first frame whose `RunTime` is at or past its time — including when a single large frame step crosses several trigger times (all crossed triggers fire, in time order).
- [ ] No trigger fires while the run is Paused (`RunTime` frozen) or after the run is Resolved.
- [ ] RSM publishes triggers only — it does not spawn, choose elite stats, or clear elites (WaveSpawner acts on the triggers).

---

## Implementation Notes

*Derived from ADR-0012 Decision 4 and TR-GC0-002:*

- Keep a per-run "next trigger" cursor over a sorted schedule built at run start from TuningConfig: `escalation_interval` (wave-spawner Tuning Knobs, default 120s), elite times, boss time, `run_duration_s`. If TuningConfig does not yet expose elite/boss times, raise it against the **game-data** epic TuningConfig story — **never** hardcode 300/600/900/1080 in RSM.
- Publish triggers as publisher-owned `Action<T>` events with `readonly struct` payloads (ADR-0003), e.g. a step index or event kind; subscriptions cached at init.
- The ±5s elite tolerance (wave-spawner Rule 7 / Edge Cases) is the spawner's acceptance window: RSM fires on the first frame at/after the scheduled time; the spawner spawns on the first tick within tolerance. RSM does not apply jitter.
- Boss trigger semantics (Warlord spawns, active Iron Captains returned to the pool without death/XP signals) are WaveSpawner's (AC-WS-05) — out of scope here.
- Win: when the win trigger fires and Story 002's resolution decides Resolved-Win (player alive — death-before-win already proven there), RSM calls the WaveSpawner halt surface and requests the Win screen (Run UI presenter `Show`). Killing the boss is **not** a win condition (GDD Edge Cases, OQ-02).
- Doubles: WaveSpawner trigger consumer + halt surface, win-screen presenter, fake clock, `IPlayerHealth` (alive), `TestConfigs.Default()`.

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 002: the win/death resolution decision and death-before-win priority
- Story 007: clearing enemies and resetting the schedule on retry
- Story 008: win-screen stats content (BLOCKED)
- wave-spawner epic: density refill, elite stats, boss spawn + elite clear, ±5s acceptance
- run-ui epic: win screen layout and sting

---

## QA Test Cases

*Written at story creation (lean review — QL-STORY-READY skipped). The developer implements against these — do not invent new test cases during implementation.*

- **AC-1 (AC-RS-03)**: Win fires and spawning halts
  - Given: Running, player alive, `run_duration_s = 1200`, WaveSpawner and win-presenter doubles
  - When: `RunTime` advances from 1199.9 to 1200.0
  - Then: `State == Resolved-Win`; WaveSpawner halt called exactly once; win presenter `Show` called exactly once
  - Edge cases: player dead on that frame → no halt-for-win and no win `Show` (Death path instead)

- **AC-2**: Full schedule fires from config
  - Given: `TestConfigs.Default()` (escalation 120s, elites 300/600/900, boss 1080, duration 1200); an EventRecorder on the trigger event
  - When: the clock is advanced in 1s frames from 0 to 1200
  - Then: recorded triggers = wave steps at 120, 240, …, 1080 (multiples below `run_duration_s` — at 1200 the win resolves the run and spawning halts), elites at 300/600/900, boss at 1080, win at 1200 — each exactly once, in time order
  - Edge cases: a config with escalation 60s and duration 600 produces the scaled schedule (proves no literals)

- **AC-3**: Large frame step fires every crossed trigger once, in order
  - Given: `RunTime = 295`
  - When: one frame advances to 365
  - Then: elite(300) fires once, then wave step(360) once, in that order
  - Edge cases: a step landing exactly on 300.0 fires elite(300) on that frame (≥ comparison)

- **AC-4**: No triggers while Paused or after Resolved
  - Given: Running at `RunTime = 299.5`
  - When: pause; unscaled clock advances 60s; resume; advance 0.5s
  - Then: no trigger during the pause; elite(300) fires on the frame `RunTime` reaches 300.0 after resume
  - Edge cases: after Resolved-Death at 500s, advancing the fake clock past 600 fires nothing

- **AC-5**: Publish-only boundary
  - Given: trigger consumer doubles
  - When: the boss trigger fires
  - Then: RSM made no spawn/pool calls itself (its only outbound call is the trigger publication)
  - Edge cases: N/A

---

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- Integration: `tests/integration/run-session-manager/run_session_manager_scheduled_triggers_test.cs` — must exist and pass

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001, Story 002, Story 003 DONE; game-data epic — TuningConfig story (escalation interval, elite/boss times, `run_duration_s`)
- Unlocks: Story 007 (schedule reset on retry); wave-spawner epic consumes the triggers

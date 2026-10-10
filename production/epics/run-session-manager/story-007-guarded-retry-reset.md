# Story 007: Guarded retry reset

> **Epic**: Run Session Manager
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Integration
> **Manifest Version**: 2026-10-10

## Context

**GDD**: `design/gdd/run-session-manager.md`
**Requirement**: `TR-RS-004`, `TR-GC0-003`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0012: Scene Organisation and Boot Flow
**ADR Decision Summary**: Retry re-runs the run-start reset from a Resolved state only (guarded, TR-RS-004). Single scene, no persistent state to flush; application quit is the only exit.
*Secondary*: `IRunResettable.ResetForNewRun()` is the reset contract, registered with RSM and invoked only from a Resolved state (manifest Foundation rules; ADR-0002 / `docs/registry/architecture.yaml` reset contract). ADR-0005 — enemies are cleared only through `ObjectPool.ReturnToPool`.

**Engine**: Unity 6.3 LTS | **Risk**: LOW
**Engine Notes**: No post-cutoff APIs. No scene reload on retry (ADR-0012).

**Control Manifest Rules (this layer)**:
- Required: `IRunResettable.ResetForNewRun()` is the reset contract; RSM invokes it only from a Resolved state — ADR-0002, ADR-0012
- Required: All enemy/projectile lifecycles go through `ObjectPool`; `ReturnToPool` order is binding (`FlushEventsFor` → deregister → `SetActive(false)` → push) — ADR-0005
- Forbidden: Never call `SetActive(false)` on an enemy (or `Destroy` any pooled object) outside `ObjectPool.ReturnToPool` — ADR-0005
- Forbidden: Never call `SceneManager.LoadScene` in MVP — ADR-0012
- Guardrail: Zero `Instantiate`/`Destroy` during a run (retry included) — ADR-0005

---

## Acceptance Criteria

*From GDD `design/gdd/run-session-manager.md` (AC-RS-06, AC-RS-07, Core Rule 9, Edge Cases) and TR-RS-004 / TR-GC0-003, scoped to this story:*

- [ ] **AC-RS-06**: GIVEN a Resolved state and a retry, WHEN reset fires, THEN RSM calls Damage & Health, Progression, and Upgrade Definition reset APIs, clears all enemies, and resets the timer to 1200s (`run_duration_s`).
- [ ] **AC-RS-07**: GIVEN a live (Running) run, WHEN a retry/reset is requested, THEN it is ignored (no mid-run reset).
- [ ] Retry is accepted from both Resolved-Win and Resolved-Death; it is ignored from Selection, Running and Paused.
- [ ] Retry also halts/resets the WaveSpawner and resets the scheduled-trigger cursor (Story 006), and re-enters Selection (MVP — OQ-01).
- [ ] No scene load occurs on retry.

---

## Implementation Notes

*Derived from ADR-0012 Decision 6, GDD Core Rule 9, architecture.md §3 (reset order), ADR-0005:*

- architecture.md §3: on retry, RSM invokes every `IRunResettable.ResetForNewRun()`, clears the enemy pool, resets the timer, re-enters Selection. Damage & Health (`IPlayerHealth : IRunResettable`), Progression and Upgrade Definition (eligible pool → 24) are reached through the `IRunResettable` registry wired by Bootstrap — RSM must not know their concrete types.
- Enemy clear: return every active enemy through the pool's reset/clear surface, which uses `ReturnToPool` per enemy (object-pool epic). RSM never deactivates enemies itself.
- Timer reset: zero the Story 001 origin/accumulator state so `time_remaining == run_duration_s` (TuningConfig) — not a literal 1200.
- Guard at the RSM entry point: if `State` is not Resolved-Win / Resolved-Death, return without side-effects. (Damage & Health's own `Reset()` guard — valid only from a dead/resolved state — is a second line of defence in that epic.)
- ADR-0012 Decision 6 says retry "re-runs step 3"; since Story 003's run-start contract also calls `ResetForNewRun()` on the next confirm, resets run twice across retry → confirm. `ResetForNewRun` implementations must be idempotent — note this for the consumer epics; do not suppress either call.
- Doubles: `IRunResettable` fakes for D&H/Progression/Upgrade pool (record calls), pool double (records clear), WaveSpawner double (halt/reset), selection presenter double.
- MVP has no persistent upgrades — every run-granted value resets (damage-health retry rule); there is nothing to save.

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 003: the run-start contract that follows the next selection confirm
- Story 008: clearing aggregated run statistics (BLOCKED — reset its counters when that story lands)
- Each consumer epic: the content of its own `ResetForNewRun()`
- object-pool epic: `ReturnToPool` ordering internals

---

## QA Test Cases

*Written at story creation (lean review — QL-STORY-READY skipped). The developer implements against these — do not invent new test cases during implementation.*

- **AC-1 (AC-RS-06)**: Reset from Resolved-Death
  - Given: Resolved-Death at `run_time = 842`; 3 `IRunResettable` fakes (D&H, Progression, Upgrade pool) registered; pool double holding 37 active enemies
  - When: retry is requested
  - Then: each fake's `ResetForNewRun` called exactly once; pool clear called once (0 active after); `time_remaining == run_duration_s`; `State == Selection`
  - Edge cases: zero active enemies still succeeds; registration order of resettables is preserved in the call order

- **AC-2 (AC-RS-06)**: Reset from Resolved-Win
  - Given: Resolved-Win at `run_time = 1200`
  - When: retry
  - Then: same assertions as AC-1; WaveSpawner double received halt/reset; trigger cursor reset so elite(300) fires again in the next run
  - Edge cases: config `run_duration_s = 1800` → `time_remaining == 1800` after reset

- **AC-3 (AC-RS-07)**: Retry ignored while Running
  - Given: Running at `run_time = 400`
  - When: retry is requested
  - Then: no `ResetForNewRun`, no pool clear, no spawner reset; `State == Running`; `RunTime` unchanged
  - Edge cases: retry ignored in Selection and in Paused (pick screen open) with identical no-side-effect assertions

- **AC-4**: Double retry
  - Given: Resolved-Death
  - When: retry is requested twice in the same frame
  - Then: resets run exactly once (second request arrives in Selection and is ignored)
  - Edge cases: N/A

- **AC-5**: No scene load on retry
  - Given: retry from Resolved
  - When: reset completes
  - Then: no scene-load call (covered by the Story 003 static check; assert no scene-load seam invocation here)
  - Edge cases: N/A

---

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- Integration: `tests/integration/run-session-manager/run_session_manager_retry_reset_test.cs` — must exist and pass

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 002, Story 003, Story 006 DONE; object-pool epic — pool clear via `ReturnToPool` available (or its interface for the double)
- Unlocks: None (Story 008 extends the reset with stats counters)

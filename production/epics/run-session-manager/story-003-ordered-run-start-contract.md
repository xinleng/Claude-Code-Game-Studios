# Story 003: Ordered run-start contract and inert-until-init

> **Epic**: Run Session Manager
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Integration
> **Manifest Version**: 2026-10-10

## Context

**GDD**: `design/gdd/run-session-manager.md`
**Requirement**: `TR-RS-006`, `TR-RS-001`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0012: Scene Organisation and Boot Flow
**ADR Decision Summary**: On selection confirm, RSM — in a fixed order, same frame, before any game logic ticks — records the run origin, calls `SetRunStartTime` on InputManager/EnemySystemManager/WaveSpawner, invokes every `IRunResettable.ResetForNewRun()`, then `SetContext(Gameplay)` and unfreezes the spawner. All three consumers stay inert until that call. Single scene, no `SceneManager.LoadScene`.
*Secondary*: ADR-0004 — `SetContext(Gameplay|Menu|Paused)` enables exactly one action map; `SetRunStartTime` is RSM-only.

**Engine**: Unity 6.3 LTS | **Risk**: LOW (ADR-0012); the `SetContext` call crosses into ADR-0004 (HIGH — input package) but is exercised here only through the `IInputAuthority` double
**Engine Notes**: No post-cutoff APIs in RSM. ADR-0012 verification: no `SceneManager.LoadScene` between selection and run; selection root `activeSelf == false` during the run (general-characters AC-07).

**Control Manifest Rules (this layer)**:
- Required: Run-start order is binding (same frame, before any game logic): RSM records the run origin → `SetRunStartTime` on InputManager/EnemySystemManager/WaveSpawner → every `IRunResettable.ResetForNewRun()` → `SetContext(Gameplay)` → spawner unfreezes. All three consumers stay inert until then — ADR-0012
- Required: Wire dependencies by constructor/initializer injection at Bootstrap — interfaces only, no singletons, no `FindObjectOfType`/`FindFirstObjectByType` — ADR-0002, ADR-0012
- Forbidden: Never call `SceneManager.LoadScene` in MVP — ADR-0012
- Forbidden: Never let a gameplay object self-initialise or touch another system in `Awake/Start` — ADR-0012
- Guardrail: Bootstrap validation is the only load-time pass — ADR-0006

---

## Acceptance Criteria

*From GDD `design/gdd/run-session-manager.md` (AC-RS-01, Core Rules 2–4, Edge Cases) and TR-RS-006, scoped to this story:*

- [ ] **AC-RS-01**: GIVEN a run starts, WHEN the first frame runs, THEN `runStartTime` is recorded and passed to Input Manager, Enemy AI (`EnemySystemManager`), and Wave Spawner; `run_time` begins at 0.
- [ ] The run-start calls happen in the exact ADR-0012 order, all in the confirm frame, before any `CombatFrame.RunFrame` / enemy / input tick for the run: origin → `SetRunStartTime` ×3 → every registered `IRunResettable.ResetForNewRun()` → `SetContext(Gameplay)` → spawner unfreeze.
- [ ] Inert-until-init: before the run-start contract executes, a pumped frame produces no input capture, no enemy tick effects and no spawns (consumers have not received `SetRunStartTime`).
- [ ] Single-scene flow: selection confirm hides the selection screen (requests Hide on the selection presenter) and triggers no scene load; zero `SceneManager.LoadScene` references in the codebase (static check).
- [ ] The confirmed `GeneralId` from the selection callback is passed to general initialisation for the run.
- [ ] If general data is unavailable at selection (data-load failure), RSM does not enter Running and raises the critical error state instead of defaulting to a phantom general.

---

## Implementation Notes

*Derived from ADR-0012 Decisions 1–3 and ADR-0004 Decision 4:*

- Bootstrap (bootstrap-combat-frame epic) constructs RSM with injected references: `IInputAuthority`, the EnemySystemManager and WaveSpawner run-start/unfreeze surfaces, the `IRunResettable` registry, the selection presenter, `IGameClock`, TuningConfig. RSM looks nothing up.
- Execute the five steps as explicit sequential calls in one method (no events, no priority lists) so the order is reviewable. Record the order in tests with an `EventRecorder` (ordered subscriber log, ADR-0010 Decision 5).
- The origin value passed to `SetRunStartTime` is the one recorded by Story 001 (same basis as `IGameClock.RunTime`, per ADR-0004 rule 1).
- Use test doubles for all not-yet-built consumers: `IInputAuthority` fake (records `SetRunStartTime`/`SetContext`), fake EnemySystemManager/WaveSpawner start surfaces, `IRunResettable` fakes (record call order), fake `IPlayerHealth` (it is itself `IRunResettable`).
- Inert-until-init is each consumer's own contract (input-manager / enemy-definition / wave-spawner GDDs). RSM's obligation is to never tick a consumer for a run before calling `SetRunStartTime`; the inert behaviour itself is verified in the consumer epics. Here, assert it with the doubles (no tick reaches them before the call).
- `IRunResettable.ResetForNewRun()` is invoked here **and** in the retry path (Story 007, ADR-0012 Decision 6) — resets must therefore be safe to call on a fresh run.
- ADR-0012 step 3 does not specify where general initialisation (GDD Rule 3) sits in the ordered sequence; place it before `SetContext(Gameplay)` and flag the position in `/story-readiness` for confirmation.
- The general data-load failure error state comes from general-characters.md Edge Cases (recorded in TR-RS-006); log on the shared `[DESIGNER-ERROR]` channel (TR-TP-002) and request the error screen — do not pick a default general.
- Static check: add a test (or CI grep) asserting zero `SceneManager.LoadScene` occurrences under `src/` (ADR-0012 Validation Criteria).

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 001: clock math (origin/accumulator/countdown)
- Story 002: the Selection/Running state transitions themselves
- Story 007: retry from Resolved (pool clear, timer reset, Resolved-only guard)
- input-manager epic: the inert/frozen behaviour inside InputManager and the spurious-Parry-on-context-switch AC
- game-data epic: `arena_bounds` in TuningConfig (TR-RS-006 arena-bounds ownership is delivered there per ADR-0012 Decision 5)
- run-ui epic: selection screen layout/rendering

---

## QA Test Cases

*Written at story creation (lean review — QL-STORY-READY skipped). The developer implements against these — do not invent new test cases during implementation.*

- **AC-1 (AC-RS-01)**: Origin recorded and distributed; `run_time` = 0
  - Given: RSM in Selection with doubles for Input/EnemySystemManager/WaveSpawner, fake clock at unscaled 12.5s
  - When: selection is confirmed with a valid `GeneralId`
  - Then: all three doubles received `SetRunStartTime` exactly once with the same origin value RSM recorded; `RunTime == 0` that frame
  - Edge cases: a second confirm in the same frame does not call `SetRunStartTime` again

- **AC-2**: Binding call order
  - Given: an EventRecorder attached to every double and to 3 `IRunResettable` fakes
  - When: selection is confirmed
  - Then: the recorded sequence is exactly: origin recorded → `SetRunStartTime`(Input, EnemySystemManager, WaveSpawner) → `ResetForNewRun` ×3 → `SetContext(Gameplay)` → spawner unfreeze; and the first `RunFrame` of the run is recorded after all of them
  - Edge cases: zero registered `IRunResettable`s still produces the remaining order; registration order of resettables is preserved

- **AC-3**: Inert before init
  - Given: RSM in Selection; doubles that record any Tick/spawn/capture call
  - When: 10 frames are pumped before confirm
  - Then: no consumer received a run tick, spawn or `SetContext(Gameplay)`; the spawner is still frozen
  - Edge cases: pumping frames after confirm does reach the consumers

- **AC-4**: Single-scene flow
  - Given: selection presenter double
  - When: confirm
  - Then: presenter `Hide` was requested; no scene-load call occurred (static test: zero `SceneManager.LoadScene` matches under `src/`)
  - Edge cases: cancel requests the main-menu screen, also without a scene load

- **AC-5**: General initialisation receives the confirmed `GeneralId`
  - Given: selection callback returns `GeneralId` X
  - When: confirm
  - Then: the general-initialisation double received X exactly once, before `SetContext(Gameplay)`
  - Edge cases: each of the three generals is passed through unchanged

- **AC-6**: Data-load failure → critical error, no run
  - Given: the general data source reports no available generals
  - When: confirm is attempted
  - Then: state does not become Running; no `SetRunStartTime` call; one `[DESIGNER-ERROR]`/critical-error entry is emitted and the error screen is requested
  - Edge cases: no phantom/default general is initialised

---

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- Integration: `tests/integration/run-session-manager/run_session_manager_run_start_test.cs` — must exist and pass

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001 and Story 002 DONE; bootstrap-combat-frame epic — Bootstrap composition-root story and CombatFrame `RunFrame` pump story DONE; input-manager epic — `SetContext` / `SetRunStartTime` (context switching + inert-until-run-start) story DONE or its `IInputAuthority` interface published for the double
- Unlocks: Story 004, Story 006, Story 007

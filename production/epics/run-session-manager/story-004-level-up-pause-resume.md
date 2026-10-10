# Story 004: Level-up pause and resume orchestration

> **Epic**: Run Session Manager
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Integration
> **Manifest Version**: 2026-10-10

## Context

**GDD**: `design/gdd/run-session-manager.md`
**Requirement**: `TR-RS-002`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0004: Single Input Authority and the Parry Input Buffer
**ADR Decision Summary**: `SetContext(Gameplay|Menu|Paused)` enables exactly one action map and the parry buffer freezes (no prune/add) outside Gameplay; context switching is owned by RSM, executed by InputManager.
*Secondary*: ADR-0009 rule 4 — RSM calls `Show/Hide` on screen presenters; presenters return the pick via typed callbacks and never touch `Time.timeScale` or input contexts. ADR-0010 — `RunTime` is pause-compensated (Story 001).

**Engine**: Unity 6.3 LTS | **Risk**: HIGH (input package — ADR-0004)
**Engine Notes**: ADR-0004 risk "held key across `SetContext` re-enable": initial-state-check behaviour differs by action type when a map is re-enabled while a control is held — verifying that Parry does not fire spuriously on Menu→Gameplay is an **input-manager epic** AC; this story only issues the calls. `Time.timeScale` is written by RSM (architecture.md: RSM engine APIs `Time.time`, `Time.timeScale`).

**Control Manifest Rules (this layer)**:
- Required: ONE run clock (`IGameClock.RunTime`, pause-compensated, origin owned by RSM) — ADR-0010
- Required: Wire dependencies by constructor injection — interfaces only, no singletons — ADR-0002, ADR-0012
- Forbidden: Never mix `Time.time`- and `Time.unscaledTime`-derived run-local values — ADR-0010
- Forbidden: Never hardcode a gameplay/tuning value (`pause_on_pick` comes from TuningConfig) — ADR-0006
- Guardrail: Gameplay logic reads time via injected `IGameClock` — ADR-0010

---

## Acceptance Criteria

*From GDD `design/gdd/run-session-manager.md` (AC-RS-02, Core Rules 4–5, Tuning Knobs), scoped to this story:*

- [ ] **AC-RS-02**: GIVEN a Progression level-up, WHEN it fires, THEN RSM pauses the run (`Time.timeScale = 0`), switches Input Manager to Menu context, and shows the pick screen; on pick, it resumes Gameplay context and `Time.timeScale = 1`.
- [ ] The run enters the Paused flow state on pause and returns to Running on pick confirmation.
- [ ] `RunTime` is continuous across the pause: it does not advance while paused and does not jump on resume (EPIC DoD: no jump, no drift between consumers).
- [ ] The pick screen is shown with the options Progression drew at level-up time (RSM passes them through; it does not draw).
- [ ] `pause_on_pick` (TuningConfig, default `true`) gates the `Time.timeScale = 0` write.

---

## Implementation Notes

*Derived from ADR-0004 Decision 4, ADR-0009 rule 4, architecture.md §4 (pause path):*

- Pause path (architecture.md §4): `ProgressionSystem.LevelUp` → RSM → `Time.timeScale = 0` → `InputManager.SetContext(Menu)` [parry buffer freezes] → `UpgradeSelectionUI.Show(options)` → pick → Progression routes deltas to the owning system → RSM: `SetContext(Gameplay)`, `timeScale = 1` [buffer pruning resumes].
- Subscribe to Progression's `LevelUp`/`OptionsDrawn` event once at init (cached delegate; ADR-0003). Use a Progression double — ProgressionSystem is a Feature epic not yet built.
- The pick is returned to RSM by the Upgrade Selection UI presenter through a typed callback (ADR-0009); RSM resumes after Progression has applied the pick. RSM never applies deltas.
- Write `Time.timeScale` through an injected seam so EditMode tests can assert the value without the engine loop (ADR-0010 DI rule — no direct static time access in testable logic). Production binding writes `UnityEngine.Time.timeScale`.
- Drive Story 001's begin-pause / end-pause operations at the same points so `RunTime` freezes for exactly the pause interval.
- GDD Rule 4 lists `Menu/Paused` as the pause contexts; the level-up pick uses **Menu** (AC-RS-02). `Paused` is reserved for the future pause menu — not built in this story.
- `pause_on_pick = false`: the GDD only specifies that it controls the `timeScale = 0` write. The context/flow behaviour when it is false is unspecified — implement the gate only and raise the question in `/story-readiness`; do not invent behaviour.

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 005: deferring the pause to the next safe frame after a Finisher; `PlayerDead` during an open pick
- input-manager epic: buffer freeze behaviour inside InputManager; spurious Parry on context re-enable
- upgrade-selection-ui / progression epics: pick screen layout, option drawing, delta routing
- Future pause menu (`SetContext(Paused)`) — not in MVP scope of this epic

---

## QA Test Cases

*Written at story creation (lean review — QL-STORY-READY skipped). The developer implements against these — do not invent new test cases during implementation.*

- **AC-1 (AC-RS-02)**: Pause on level-up, resume on pick
  - Given: Running at `RunTime = 250`, no Finisher in progress; doubles for Progression, `IInputAuthority`, Upgrade Selection presenter, timeScale seam
  - When: the Progression double raises a level-up with 3 option IDs; later the presenter double returns a pick
  - Then: on level-up — timeScale seam == 0, `SetContext(Menu)` called once, presenter `Show(options)` called once (in that order per architecture §4); on pick — `SetContext(Gameplay)` called once and timeScale seam == 1
  - Edge cases: a pick callback arriving while not Paused is ignored (no `SetContext`/timeScale writes)

- **AC-2**: Flow state follows the pause
  - Given: as AC-1
  - When: level-up then pick
  - Then: `State` goes Running → Paused → Running
  - Edge cases: a second level-up while already Paused does not re-issue `SetContext(Menu)` or a second `Show` (state unchanged)

- **AC-3**: `RunTime` continuity across the pause
  - Given: Running at `RunTime = 250.0`
  - When: level-up; unscaled clock advances 45s while paused; pick; clock advances 0.25s
  - Then: `RunTime == 250.0` during the pause and `== 250.25` after resume; the value RSM reports equals the value read by every consumer double (no drift)
  - Edge cases: pause and pick in the same frame leaves `RunTime` unchanged

- **AC-4**: Options passed through unchanged
  - Given: level-up with option IDs [A, B, C]
  - When: the pick screen is shown
  - Then: the presenter double received exactly [A, B, C] in that order
  - Edge cases: RSM performs no draw (Progression double's draw method is never called by RSM)

- **AC-5**: `pause_on_pick` gate
  - Given: TuningConfig with `pause_on_pick = true` (default) / `false`
  - When: level-up fires
  - Then: `true` → timeScale seam == 0; `false` → timeScale seam is not written
  - Edge cases: the default config factory yields `true`

---

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- Integration: `tests/integration/run-session-manager/run_session_manager_pause_test.cs` — must exist and pass

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001, Story 002, Story 003 DONE; input-manager epic — `SetContext` context-switching story DONE (or `IInputAuthority` published for the double); game-data epic — TuningConfig story (`pause_on_pick`)
- Unlocks: Story 005

# Story 002: Run flow state machine and death-before-win resolution

> **Epic**: Run Session Manager
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Logic
> **Manifest Version**: 2026-10-10

## Context

**GDD**: `design/gdd/run-session-manager.md`
**Requirement**: `TR-RS-001`, `TR-RS-002`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0012: Scene Organisation and Boot Flow
**ADR Decision Summary**: One scene; menu/selection/end screens are UI Toolkit screen states driven by RSM's flow state machine — there are no scene transitions after application start. RSM owns the run clock and the `run_duration_s` countdown that defines the win condition.
*Secondary*: ADR-0003 — `PlayerDead` is a publisher-owned `Action<PlayerDeadData>` (readonly struct) fired once, after `DamageTaken`, in the same frame HP hits 0.

**Engine**: Unity 6.3 LTS | **Risk**: LOW
**Engine Notes**: No post-cutoff APIs. Plain C# state machine; no `UnityEvent`, no `SceneManager.LoadScene`.

**Control Manifest Rules (this layer)**:
- Required: Menu/select/end screens are UI Toolkit states, not scenes — ADR-0012
- Required: Events are publisher-owned `System.Action<T>` with `readonly struct` payloads, subscribed once at init with cached delegates — ADR-0003
- Forbidden: Never call `SceneManager.LoadScene` in MVP — ADR-0012
- Forbidden: Never use `UnityEvent`/`SendMessage` for combat events — ADR-0003
- Guardrail: Zero GC allocation per event dispatch (no capturing lambdas in the subscription) — ADR-0003

---

## Acceptance Criteria

*From GDD `design/gdd/run-session-manager.md` (States and Transitions, Core Rules 1/6/7, Formulas, Edge Cases), scoped to this story:*

- [ ] RSM exposes exactly the flow states **Selection, Running, Paused, Resolved-Win, Resolved-Death**, starting in Selection when Bootstrap hands control to it.
- [ ] Legal transitions only: Selection → Running (confirm) / main menu (cancel); Running → Paused / Resolved-Win / Resolved-Death; Paused → Running (resume); Resolved-* → Selection (retry) / main menu. Any other requested transition is rejected and the state is unchanged.
- [ ] **Win**: while Running, when `run_time ≥ run_duration_s` AND the player is alive, the run resolves to Resolved-Win.
- [ ] **Death**: on `PlayerDead`, the run resolves to Resolved-Death, and the `PlayerDeadData` payload is retained for the end screen.
- [ ] **AC-RS-05**: GIVEN the player dies on the same frame `run_time` reaches `run_duration_s`, THEN the run resolves to Death (not Win) — alive-at-20:00 is required for Win.
- [ ] Once resolved, the state is terminal for that run: a later `PlayerDead` or win check does not change Resolved-Win ↔ Resolved-Death.

---

## Implementation Notes

*Derived from ADR-0012 Decisions 1/4 and ADR-0003 Decisions 1/3/5:*

- Selection, Paused and the end screens are flow states inside `MainArena.unity` — they request UI presenters to `Show/Hide` (ADR-0009 rule 4); this story does not render anything. "Main menu" on cancel is a UI screen request, not a scene load.
- Subscribe to `IPlayerHealth.PlayerDead` once, at RSM construction/init, with a cached method-group delegate (no per-frame lambda). Use an `IPlayerHealth` test double (fake raising `PlayerDead`) — the real PlayerHealthSystem is a Core epic not yet built.
- **Death-before-win ordering**: per ADR-0003 `PlayerDead` is dispatched inside the PlayerHealth step of `CombatFrame.RunFrame`. RSM's win check for a frame must run **after** that frame's PlayerHealth step (e.g. after `RunFrame` returns), and must test "player alive" using the latched death flag. ADR-0002 does not fix RSM's per-frame slot — place the check after the CombatFrame call and confirm the slot with the bootstrap-combat-frame CombatFrame story; the test must prove the same-frame case either way.
- `run_duration_s` comes from TuningConfig (Story 001 countdown) — no literal 1200.
- Death during an open pick screen (Paused → Resolved-Death) is handled in Story 005; side-effects of Win (spawner halt, win screen) are Story 006; retry is Story 007.

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 003: what happens inside the Selection → Running transition (ordered run-start contract)
- Story 004/005: entering/leaving Paused (timeScale, `SetContext`), deferred pick, death-during-pick
- Story 006: Win side-effects (spawner halt, win-screen request) and scheduled triggers
- Story 007: retry reset and the Resolved-only guard
- Story 008: cause-of-death summary and aggregated stats (BLOCKED)

---

## QA Test Cases

*Written at story creation (lean review — QL-STORY-READY skipped). The developer implements against these — do not invent new test cases during implementation.*

- **AC-1**: Initial state and state set
  - Given: RSM constructed with doubles (fake clock, fake `IPlayerHealth`, `TestConfigs.Default()`)
  - When: Bootstrap hands control to RSM
  - Then: `State == Selection`; the state enum contains exactly the five flow states
  - Edge cases: no clock/countdown activity while in Selection (`time_remaining == run_duration_s`)

- **AC-2**: Legal and illegal transitions
  - Given: RSM in each state in turn (driven to it via the public API)
  - When: every transition request is issued (confirm, cancel, pause, resume, win, death, retry)
  - Then: only the transitions in the GDD table change state; all others leave the state unchanged (table-driven test)
  - Edge cases: resume while Running is a no-op; pause while in Selection is a no-op; confirm while Running is a no-op

- **AC-3**: Win at the duration boundary with player alive
  - Given: Running, player alive, `run_duration_s = 1200`
  - When: the clock advances to `run_time = 1199.99` and the frame runs; then to exactly `1200.0`
  - Then: still Running at 1199.99; Resolved-Win at 1200.0
  - Edge cases: a large frame step from 1199.0 to 1201.5 resolves to Win once (not twice); config `run_duration_s = 600` wins at 600

- **AC-4**: Death on `PlayerDead`
  - Given: Running at `run_time = 842`
  - When: the fake `IPlayerHealth` raises `PlayerDead` with a populated `PlayerDeadData`
  - Then: `State == Resolved-Death`; the retained payload equals the raised payload field-for-field
  - Edge cases: `PlayerDead` while in Selection is ignored (no run to resolve)

- **AC-5 (AC-RS-05)**: Same-frame death beats win
  - Given: Running at `run_time = 1199.99`, `run_duration_s = 1200`
  - When: within one pumped frame the clock reaches 1200.0 AND the fake `IPlayerHealth` raises `PlayerDead` during the combat step
  - Then: `State == Resolved-Death`, never Resolved-Win (assert the state history recorded by an EventRecorder never contains Resolved-Win)
  - Edge cases: `PlayerDead` raised on the frame *after* the win frame leaves Resolved-Win unchanged

- **AC-6**: Resolved states are terminal for the run
  - Given: Resolved-Win
  - When: `PlayerDead` is raised and further frames run
  - Then: state stays Resolved-Win; and symmetric — Resolved-Death stays Death when the clock passes `run_duration_s`
  - Edge cases: a second `PlayerDead` while Resolved-Death does not overwrite the retained payload

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: `tests/unit/run-session-manager/run_session_manager_flow_state_test.cs` — must exist and pass

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001 DONE; bootstrap-combat-frame epic — CombatFrame `RunFrame` pump story DONE (for the same-frame ordering test) and EventRecorder test helper available
- Unlocks: Story 003, Story 004, Story 006, Story 007

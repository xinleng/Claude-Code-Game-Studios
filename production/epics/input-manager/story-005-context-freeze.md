# Story 005: Parry buffer freeze outside Gameplay

> **Epic**: Input Manager
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Logic
> **Manifest Version**: 2026-10-10

## Context

**GDD**: `design/gdd/input-manager.md`
**Requirement**: `TR-IM-004` (primary), `TR-IM-002` (RSM-driven context)
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0004: Single Input Authority and the Parry Input Buffer
**ADR Decision Summary**: `SetContext(Gameplay|Menu|Paused)` enables exactly one action map. The parry buffer **freezes outside Gameplay**: no prune and no add. A press made just before a screen opens therefore survives, and pruning resumes on the first Gameplay tick in the unified `IGameClock.RunTime` domain.

**Engine**: Unity 6.3 LTS + Input System package (pinned) | **Risk**: HIGH
**Engine Notes** (ADR-0004 Risks):
- When the Gameplay map is disabled, `Parry.performed` does not fire. That is the mechanism that stops presses arriving in Menu/Paused, and it relies on Story 001's exactly-one-map rule.
- **Held key across a `SetContext` re-enable** (audit 2026-10-10): the initial-state check on re-enable behaves differently depending on action type. Story 010 owns that device-level check. This story covers the buffer semantics only.

**Control Manifest Rules (Foundation layer)**:
- Required: ONE run clock (`IGameClock.RunTime`) for all combat timestamps (ADR-0010, TR-RS-005).
- Required: RSM decides **when** to switch context. InputManager only executes the switch (ADR-0004; RSM GDD Rule 4).
- Forbidden: never mix values derived from `Time.time` with values derived from `Time.unscaledTime` (ADR-0010, conflict C1).

---

## Acceptance Criteria

*From GDD `design/gdd/input-manager.md` (States and Transitions, Edge Cases), scoped to this story:*

- [ ] **AC-IM-08**: **GIVEN** context is Gameplay, **WHEN** `SetContext(Menu)` is called and a `Parry.performed` is injected afterward, **THEN** no new press is buffered (Gameplay map disabled) **AND** presses recorded before the switch remain in the buffer (frozen, not pruned). **WHEN** `SetContext(Gameplay)` is called, **THEN** capture resumes
- [ ] `Paused` behaves the same as `Menu` for input purposes: no add, no prune (GDD States table)
- [ ] If `SetContext(Menu)` is called in the same frame as a Parry press, presses already recorded before the switch stay in the frozen buffer, and a press arriving *after* the switch is not buffered (GDD edge case)
- [ ] On return to Gameplay, the first `Tick` prunes, in the `RunTime` domain, any frozen press whose age now exceeds `parry_input_buffer_ms` (GDD States table; ADR-0004 rule 4)
- [ ] While frozen, `HasLiveParryPress()` and `TryConsumeParryPress()` stay callable and evaluate liveness against `RunTime` as normal. This is derived: the GDD and ADR-0004 rule 4 define freeze as "no prune, no add" only, and nothing hides or locks the buffer

---

## Implementation Notes

*Derived from ADR-0004 Decision rule 4 and the GDD States table:*

- Hold the current `InputContext` in InputManager. In `Tick` (Story 007), skip `Prune` when context ≠ Gameplay. In the parry handler (Story 004), drop the event when context ≠ Gameplay. This is defensive, because the map is already disabled.
- Freezing is **not** clearing. Never empty the buffer on `SetContext(Menu|Paused)`.
- **Clock-semantics flag (raise with game-designer; ties to OQ-02):** the States table says "presses that aged out during the menu are pruned on the first Gameplay tick (the run-local unscaled clock kept advancing)". Revised Rule 4 and ADR-0004/0010 replace that clock with `IGameClock.RunTime`, which is **pause-compensated**: it does not advance while the run is paused for a level-up pick (`timeScale = 0` + `SetContext(Menu)`). A press frozen just before a level-up pause therefore does **not** age during the pick and is still live on resume. That is the case OQ-02 worries about ("a press from before a 5 s menu shouldn't auto-fire on resume"). Implement per the ADR (RunTime) and write the tests in RunTime terms. Record this behaviour change in the story's completion notes and raise it at `/story-readiness`. Changing it needs a GDD/ADR decision, not a code-level workaround.
- Because nothing consumes a frozen press unless a parry window is open and `notBefore` permits it, the Parry System's `notBefore` guard and window logic still govern any resumed consumption. Input Manager adds no special case.

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 001: map enable/disable mechanics
- Story 004: capture gating on run start, stamping
- Story 007: where `Prune` sits inside `Tick`
- Story 010: held Parry across Menu→Gameplay (no spurious press) at device level
- RSM epic: pause orchestration (`timeScale`, when `SetContext` is called)

---

## QA Test Cases

*Written at story creation (lean review — QL-STORY-READY skipped). The developer implements against these. Do not invent new test cases during implementation.*

- **AC-1**: AC-IM-08 freeze then resume
  - Given: a started run, Gameplay, fake clock RunTime `1.000`, one press recorded at `1.000`
  - When: `SetContext(Menu)`, then `OnParryPerformed(…)` injected (event mapping to RunTime `1.010`), then `Tick()` at RunTime `1.010`
  - Then: buffer count == 1 (only the `1.000` press). The `1.000` press is still present and unconsumed (not pruned)
  - When (continued): `SetContext(Gameplay)`, then a press injected at RunTime `1.020`
  - Then: buffer count == 2 (capture resumed)
  - Edge cases: a `Tick()` while in Menu with RunTime advanced well beyond the window (fake clock forced to `2.000`, for example) still prunes nothing (count unchanged)

- **AC-2**: Paused ≡ Menu
  - Given: as AC-1, but switching with `SetContext(Paused)`
  - When: the same sequence runs
  - Then: identical results
  - Edge cases: `Menu → Paused → Gameplay` without an intermediate Gameplay tick keeps the buffer intact until the first Gameplay `Tick`

- **AC-3**: Same-frame switch
  - Given: Gameplay. A press is recorded in this frame's earlier capture (handler called before the switch)
  - When: `SetContext(Menu)` is called in the same frame, and a second press arrives after the switch
  - Then: the first press stays buffered and the second is not buffered
  - Edge cases: none

- **AC-4**: Prune on the first Gameplay tick (RunTime domain)
  - Given: a press at RunTime `1.000`, then `SetContext(Menu)`. The fake clock is scripted so that RunTime is `1.200` when Gameplay resumes (unpaused time advanced, for example a non-paused Menu context)
  - When: `SetContext(Gameplay)`, then `Tick()` at RunTime `1.200`
  - Then: the press is pruned (age 0.200 > 0.120) and `HasLiveParryPress() == false`
  - Edge cases (documents the clock flag): with a pause-compensated script where RunTime stays at `1.050` across the menu, the press is **still live** after resume (age 0.050). Assert this explicitly so the behaviour is visible and reviewable against OQ-02

- **AC-5**: Queries work while frozen
  - Given: Menu context, a frozen live press at `1.000`, RunTime `1.050`
  - When: `HasLiveParryPress()`, then `TryConsumeParryPress(0)`
  - Then: `true`, then `true` with `pressTime == 1.000`
  - Edge cases: none

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: `tests/unit/input-manager/input_manager_context_freeze_test.cs` — must exist and pass (EditMode, injected clock)

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001, Story 004; bootstrap-combat-frame IGameClock story
- Unlocks: Story 007, Story 010

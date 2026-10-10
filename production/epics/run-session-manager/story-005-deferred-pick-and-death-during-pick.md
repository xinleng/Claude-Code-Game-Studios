# Story 005: Deferred pick after Finisher and death during pick

> **Epic**: Run Session Manager
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Logic
> **Manifest Version**: 2026-10-10

## Context

**GDD**: `design/gdd/run-session-manager.md`
**Requirement**: `TR-RS-002`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0004: Single Input Authority and the Parry Input Buffer
**ADR Decision Summary**: RSM owns when the input context changes; `SetContext` enables exactly one action map and the parry buffer freezes outside Gameplay.
*Secondary*: ADR-0003 — `PlayerDead` fires once, same frame, after `DamageTaken`; ADR-0012 — flow states are UI screen states driven by RSM.

**Engine**: Unity 6.3 LTS | **Risk**: HIGH (input package — ADR-0004; exercised here only through the `IInputAuthority` double)
**Engine Notes**: No post-cutoff APIs in RSM. `Time.timeScale` writes go through the injected seam introduced in Story 004.

**Control Manifest Rules (this layer)**:
- Required: Events are publisher-owned `System.Action<T>` with `readonly struct` payloads, subscribed once at init with cached delegates — ADR-0003
- Required: Wire dependencies by constructor injection — interfaces only — ADR-0002, ADR-0012
- Forbidden: Never write state owned by another system (RSM does not alter drawn options or combo/finisher state) — ADR-0003
- Guardrail: Gameplay logic reads time via injected `IGameClock` — ADR-0010

---

## Acceptance Criteria

*From GDD `design/gdd/run-session-manager.md` (Edge Cases) and TR-RS-002, scoped to this story:*

- [ ] If a level-up fires during a Finisher animation, RSM queues the pick screen until the animation completes; the run pauses on the next safe frame (not mid-Finisher).
- [ ] The options shown for a deferred pick are the ones drawn at level-up time (locked by Progression) — RSM shows them unchanged.
- [ ] If `PlayerDead` fires while a pick screen is open, the pick is discarded and RSM transitions to Resolved-Death; a dead player does not pick an upgrade (a later pick callback is ignored; no resume to Gameplay/timeScale 1 occurs from the pick path).
- [ ] If `PlayerDead` fires while a pick is still queued (deferred, not yet shown), the queued pick is discarded and the run resolves to Death.

---

## Implementation Notes

*Derived from TR-RS-002, architecture.md §4 ("queue until Finisher animation completes"), ADR-0003:*

- Implement a pause-request queue in RSM: a level-up while a Finisher is in progress enqueues the request with its locked options; at the start of each subsequent frame where no Finisher is in progress (the "next safe frame"), RSM dequeues and runs the Story 004 pause path.
- **Finisher-in-progress signal**: the architecture does not define the exact observable (Combo/CombatCore epics are held until the combo-and-parry prototype reports). Implement against a minimal injected read-only seam (e.g. "is a Finisher in progress" query) with a test double, and flag the concrete source (ComboSystem / CombatCore) in `/story-readiness` so the Combo epic adopts it. Do not invent Finisher timing inside RSM.
- Death-during-pick: on `PlayerDead` while Paused or with a queued request, clear the queue, request the pick screen `Hide`, and transition Paused → Resolved-Death (Story 002's death path). Retain the `PlayerDeadData` payload as usual.
- After death-during-pick, the input context and `timeScale` are left for the Resolved-Death screen flow (end-screen context is Menu per GDD Rule 4); do **not** run the pick-resume path (`SetContext(Gameplay)`, `timeScale = 1`). Whether `timeScale` is restored on entering Resolved is not specified by the GDD — flag for confirmation with the run-ui epic rather than inventing.
- Use doubles: Progression (level-up + options), `IPlayerHealth` (raises `PlayerDead`), Finisher-in-progress seam, Upgrade Selection presenter, `IInputAuthority`, timeScale seam.

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 004: the basic pause/resume path and `RunTime` continuity
- Story 002: the general death/win resolution rules (incl. death-before-win)
- Story 008: cause-of-death summary content (BLOCKED)
- Combo/CombatCore epics: the authoritative Finisher-in-progress signal

---

## QA Test Cases

*Written at story creation (lean review — QL-STORY-READY skipped). The developer implements against these — do not invent new test cases during implementation.*

- **AC-1**: Level-up during a Finisher is deferred to the next safe frame
  - Given: Running; Finisher seam reports in-progress for frames N..N+3
  - When: Progression double raises level-up on frame N; frames N+1..N+4 are pumped
  - Then: no timeScale write, no `SetContext(Menu)`, no `Show` on frames N..N+3; on frame N+4 (first frame with no Finisher) the full Story 004 pause path runs exactly once
  - Edge cases: Finisher ends on the same frame as the level-up → pause on the next frame, not mid-frame; a Finisher that never ends keeps the request queued (no pause, no crash)

- **AC-2**: Deferred pick shows the options locked at level-up
  - Given: level-up during a Finisher with options [A, B, C]
  - When: the pick screen is eventually shown
  - Then: the presenter receives [A, B, C]
  - Edge cases: Progression double changes its "current" options after the level-up — presenter still receives [A, B, C]

- **AC-3**: Death while the pick screen is open discards the pick
  - Given: Paused with the pick screen shown
  - When: `IPlayerHealth` double raises `PlayerDead`; then the presenter double returns a pick
  - Then: `State == Resolved-Death`; pick screen `Hide` requested; the late pick callback causes no `SetContext(Gameplay)` and no `timeScale = 1` write; Progression double receives no pick application from RSM
  - Edge cases: `PlayerDead` and pick callback in the same frame → Death wins, pick discarded

- **AC-4**: Death while a pick is queued discards the queued request
  - Given: a level-up queued behind an in-progress Finisher
  - When: `PlayerDead` is raised before the Finisher ends; the Finisher then ends
  - Then: `State == Resolved-Death`; the queue is empty; the pick screen is never shown and no timeScale/`SetContext(Menu)` write occurs from the pause path
  - Edge cases: two queued level-ups are both discarded

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: `tests/unit/run-session-manager/run_session_manager_deferred_pick_test.cs` — must exist and pass

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 004 DONE (and transitively 001–003)
- Unlocks: None within the epic (Story 008 consumes the same death path)

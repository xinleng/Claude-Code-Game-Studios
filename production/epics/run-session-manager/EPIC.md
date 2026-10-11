# Epic: Run Session Manager

> **Layer**: Foundation (architecture layer; systems-index lists it as Feature by design order)
> **GDD**: design/gdd/run-session-manager.md
> **Architecture Module**: RunSessionManager
> **Status**: Ready
> **Manifest Version**: 2026-10-10
> **Stories**: 8 created 2026-10-10 (1 Blocked)
> **Sequencing**: Clock-origin + run-start-contract stories are Sprint 1–2 (other systems need `SetRunStartTime`); stats aggregation waits on Combo/Parry signals (post-prototype).

## Overview

Owns the run: the run-clock origin and the `run_duration_s` countdown (single source for every
scheduled event and the win condition), the flow state machine (Selection / Running / Paused /
Resolved-Win / Resolved-Death), the ordered run-start contract that brings Input, enemies and the
spawner out of their inert state, pause orchestration for level-up picks (`timeScale = 0` +
`SetContext(Menu)`, deferred to the next safe frame after a Finisher, death-during-pick handling,
death-before-win priority), stats aggregation from published signals (Damage & Health stays a
ledger), and guarded retry reset via `IRunResettable`.

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-0012: Scene/boot flow | Single scene; run-start order; RSM owns run clock + arena bounds | LOW |
| ADR-0010: Test harness | RSM owns the `IGameClock.RunTime` origin/pause accumulator | LOW |
| ADR-0004: Input authority | RSM drives `SetContext` | HIGH (input package) |
| ADR-0003: Events | Consumes `PlayerDead`, Finisher/parry stat signals | LOW |

## GDD Requirements

| TR-ID | Requirement | ADR Coverage |
|-------|-------------|--------------|
| TR-RS-001 | runStartTime owner; data-driven duration; flow FSM | ADR-0010 + ADR-0012 ✅ |
| TR-RS-002 | Pause orchestration; deferred pick; death priority | ADR-0004/0009 ✅ |
| TR-RS-003 | Stats aggregation from signals | ADR-0003 ✅ |
| TR-RS-004 | Guarded retry reset orchestration | ADR-0012 ✅ |
| TR-RS-005 | Unified run clock origin + pause accumulator | ADR-0010 ✅ |
| TR-RS-006 | Init order; inert-until-init; single-scene flow; arena bounds | ADR-0012 ✅ |
| TR-GC0-002 | 20-min timer drives win + scheduled events | ADR-0012 ✅ |
| TR-GC0-003 | Full state reset on retry | ADR-0012 ✅ |

## Stories

| # | Story | Type | Status | ADR |
|---|-------|------|--------|-----|
| 001 | [Run clock origin, pause accumulator, and countdown](story-001-run-clock-origin-countdown.md) | Logic | Ready | ADR-0010 |
| 002 | [Run flow state machine and death-before-win resolution](story-002-flow-state-machine.md) | Logic | Ready | ADR-0012 |
| 003 | [Ordered run-start contract and inert-until-init](story-003-ordered-run-start-contract.md) | Integration | Ready | ADR-0012 |
| 004 | [Level-up pause and resume orchestration](story-004-level-up-pause-resume.md) | Integration | Ready | ADR-0004 |
| 005 | [Deferred pick after Finisher and death during pick](story-005-deferred-pick-and-death-during-pick.md) | Logic | Ready | ADR-0004 |
| 006 | [Scheduled-event triggers from the run clock and win resolution](story-006-scheduled-triggers-and-win.md) | Integration | Ready | ADR-0012 |
| 007 | [Guarded retry reset](story-007-guarded-retry-reset.md) | Integration | Ready | ADR-0012 |
| 008 | [Run statistics aggregation and cause-of-death summary](story-008-run-stats-aggregation.md) | Integration | Blocked | ADR-0003 |

## Definition of Done

This epic is complete when:
- All stories are implemented, reviewed, and closed via `/story-done`
- AC-RS-01…07 pass via the `RunFrame` pump (incl. death-before-win on the final frame, retry ignored outside Resolved)
- `RunTime` continuity verified across a level-up pause (no jump, no drift between consumers)
- All Logic and Integration stories have passing test files in `tests/`

## Next Step

Run `/create-stories run-session-manager`.

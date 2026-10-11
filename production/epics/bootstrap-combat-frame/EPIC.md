# Epic: Bootstrap, CombatFrame & Run Clock

> **Layer**: Foundation
> **GDD**: design/gdd/damage-health.md (Contract #3 orchestrator), design/gdd/input-manager.md (Rule 9 order), design/gdd/run-session-manager.md (Rule 2 init order) — infrastructure implied by contracts, no dedicated GDD
> **Architecture Module**: CombatFrame + Bootstrap (composition root) + IGameClock
> **Status**: Ready
> **Manifest Version**: 2026-10-10
> **Stories**: 6 created 2026-10-10
> **Sequencing**: **Sprint 1, first epic.** Includes Day-0 Unity 6.3 project creation (URP Forward+, Input System pinned, VFX Graph, UI Toolkit, Test Framework, asmdefs per ADR-0004 gate, UNITY_LICENSE CI secret) — nothing else can be tested until this lands.

## Overview

Creates the runtime spine every other system plugs into: the Unity project and assembly layout,
the single-scene `Bootstrap` composition root that constructs and injects all systems (no
singletons, no lookups), the `CombatFrame` orchestrator that runs the canonical combat sub-order
as explicit sequential calls (`RunFrame(dt)` — identical in play mode and tests), and the injected
`IGameClock` whose `RunTime` is the single pause-compensated domain for every combat timestamp
(the fix for the 2026-10-10 clock-domain conflict). This epic is what makes every later Logic
story deterministically testable.

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-0002: Frame execution order + CombatFrame | Three `[DefaultExecutionOrder]` anchors; combat sub-order as sequential calls; Finisher handshake | LOW |
| ADR-0010: Deterministic test harness | `IGameClock` (incl. unified `RunTime`), `RunFrame` pump, injected RNG, test seams | LOW |
| ADR-0012: Scene organisation & boot flow | One scene; Bootstrap boot order; forbidden LoadScene | LOW |
| ADR-0004 (assembly gate portion) | `Dynasty.Input` asmdef is the only InputSystem reference | HIGH (package pinning) |

## GDD Requirements

| TR-ID | Requirement | ADR Coverage |
|-------|-------------|--------------|
| TR-DH-004 | Same-frame parry priority; CombatFrame orchestrator over Script Execution Order | ADR-0002 ✅ |
| TR-IM-005 | Input → Parry → Combat/D&H frame order | ADR-0002 ✅ |
| TR-RS-005 | Unified run clock (`IGameClock.RunTime`) for all combat timestamps | ADR-0010 ✅ |
| TR-TP-001 | Data-driven, DI over singletons, deterministic tests (manual pump, injected clock/RNG) | ADR-0010 ✅ |
| TR-TP-002 | Shared `[DESIGNER-ERROR]` channel assertable by tests | ADR-0010 ✅ |
| TR-DH-002 | Entity architecture decided before combat code | ADR-0001 ✅ |

## Stories

| # | Story | Type | Status | ADR |
|---|-------|------|--------|-----|
| 001 | [Day-0 Unity 6.3 LTS project setup & assembly layout](story-001-unity-project-setup.md) | Config/Data | Ready | ADR-0004, ADR-0010, ADR-0012 |
| 002 | [IGameClock with unified pause-compensated RunTime + test clock](story-002-game-clock-run-time.md) | Logic | Ready | ADR-0010, ADR-0004 |
| 003 | [CombatFrame orchestrator, ITickable order, RunFrame pump & EventRecorder](story-003-combat-frame-orchestrator.md) | Logic | Ready | ADR-0002, ADR-0010 |
| 004 | [Shared [DESIGNER-ERROR] logging channel](story-004-designer-error-channel.md) | Logic | Ready | ADR-0006, ADR-0010 |
| 005 | [Bootstrap composition root — boot order & DI wiring](story-005-bootstrap-composition-root.md) | Integration | Ready | ADR-0012, ADR-0002, ADR-0010 |
| 006 | [Forbidden-pattern static checks](story-006-forbidden-pattern-static-checks.md) | Integration | Ready | ADR-0012, ADR-0004, ADR-0002, ADR-0010 |

Order: 001 → (002, 004 in parallel) → 003 → 005 → 006. TR-DH-002 needs no story — satisfied by ADR-0001 being Accepted.

## Definition of Done

This epic is complete when:
- All stories are implemented, reviewed, and closed via `/story-done`
- Unity project opens clean; CI runs the EditMode suite green on push (UNITY_LICENSE set)
- `CombatFrame.RunFrame(dt)` drives a scripted fake-subsystem sequence in the exact ADR-0002 order (EventRecorder test)
- `IGameClock.RunTime` stays monotonic and pause-compensated across a simulated `timeScale = 0` interval (unit test)
- Static checks pass: no `SceneManager.LoadScene`; no InputSystem reference outside `Dynasty.Input`
- All Logic and Integration stories have passing test files in `tests/`

## Next Step

Run `/create-stories bootstrap-combat-frame`.

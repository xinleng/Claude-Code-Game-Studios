# Epic: Input Manager

> **Layer**: Foundation
> **GDD**: design/gdd/input-manager.md
> **Architecture Module**: InputManager (`Dynasty.Input` assembly)
> **Status**: Ready
> **Manifest Version**: 2026-10-10
> **Stories**: 11 created 2026-10-10
> **Sequencing**: Sprint 1 — the prototype needs the parry buffer to answer the parry-feel questions honestly.

## Overview

The single input authority. Wraps the Input System package entirely inside `Dynasty.Input`: one
`PlayerControls` instance (Gameplay + UI maps, UI map as the module-compatible superset), context
switching on RSM's command, directional recording (`CurrentMoveDirection`, `LastSteerDirection`),
and the parry press ring buffer — sub-frame-precise `ctx.time` stamps converted to the unified run
clock, 120 ms front-extension liveness, consume-once with the `notBefore` guard. Also owns
`UiInputModuleBinder`, which hands the same action instance to the UI Toolkit input module. This
is the parry-fairness guarantee the whole skill ceiling rests on.

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-0004: Single input authority + parry buffer | Input System only; ring buffer; single instance; asmdef gate; superset UI map | HIGH |
| ADR-0002: Frame order | `[-200]` anchor; prune-then-capture | LOW |
| ADR-0010: Test harness | `IInputAuthority` test double; injected clock | LOW |

## GDD Requirements

| TR-ID | Requirement | ADR Coverage |
|-------|-------------|--------------|
| TR-IM-001 | Single input authority (asmdef gate + binder exemption) | ADR-0004 ✅ |
| TR-IM-002 | One asset, two maps, RSM-driven context; KB/M primary, gamepad best-effort | ADR-0004 ✅ |
| TR-IM-003 | Parry ring buffer (4 / 120 ms), consumer-pull API, consume-once, performed-edge only | ADR-0004 ✅ |
| TR-IM-004 | Run-clock stamps; buffer frozen outside Gameplay; inert until run start | ADR-0004/0010 ✅ |
| TR-IM-005 | Frame order Input → Parry → Combat/D&H | ADR-0002 ✅ |
| TR-CB-002 | `LastSteerDirection` raw recording (Combo applies 300 ms staleness) | ADR-0004 ✅ |

## Stories

| # | Story | Type | Status | ADR |
|---|-------|------|--------|-----|
| 001 | [PlayerControls asset, single instance & SetContext map switching](story-001-player-controls-asset-and-input-manager-shell.md) | Logic | Ready | ADR-0004 |
| 002 | [Parry press ring buffer — liveness, pruning & capacity](story-002-parry-ring-buffer-liveness.md) | Logic | Ready | ADR-0004 |
| 003 | [TryConsumeParryPress — consume-once, notBefore guard, most-recent wins](story-003-try-consume-parry-press.md) | Logic | Ready | ADR-0004, ADR-0010 |
| 004 | [Parry capture on the performed edge, run-clock stamps & inert-until-run-start](story-004-parry-capture-run-clock-timestamps.md) | Logic | Ready | ADR-0004, ADR-0010 |
| 005 | [Parry buffer freeze outside Gameplay](story-005-context-freeze.md) | Logic | Ready | ADR-0004 |
| 006 | [Directional recording — CurrentMoveDirection & LastSteerDirection](story-006-directional-recording.md) | Logic | Ready | ADR-0004 |
| 007 | [Frame-order anchor — [-200] Tick, prune → capture → record](story-007-frame-order-tick-anchor.md) | Logic | Ready | ADR-0002 |
| 008 | [UiInputModuleBinder — UI module bound to the shared instance](story-008-ui-input-module-binder.md) | Integration | Ready | ADR-0004, ADR-0009 |
| 009 | [Single-authority gate — asmdef compile gate + advisory scan](story-009-input-authority-compile-gate.md) | Config/Data | Ready | ADR-0004 |
| 010 | [Held Parry across SetContext — no spurious press](story-010-held-key-across-set-context.md) | Integration | Ready | ADR-0004 |
| 011 | [Active control scheme tracking & GetActiveBindingDisplay](story-011-control-scheme-and-binding-display.md) | Logic | Ready | ADR-0004 |

## Definition of Done

This epic is complete when:
- All stories are implemented, reviewed, and closed via `/story-done`
- AC-IM-01…13 pass in EditMode with an injected clock and synthetic presses
- New AC (TD-PHASE-GATE): holding Parry across a `SetContext` Menu→Gameplay switch does not produce a spurious press
- Compile gate verified: a deliberate InputSystem reference in a gameplay assembly fails to build
- UI module verified bound to the shared instance (navigation disabled during Gameplay context)
- All Logic and Integration stories have passing test files in `tests/`

## Next Step

Run `/create-stories input-manager`.

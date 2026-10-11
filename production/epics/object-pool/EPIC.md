# Epic: Object Pool & Enemy Lifecycle

> **Layer**: Foundation
> **GDD**: design/gdd/enemy-definition.md (Implementation Contracts #1–#2), design/gdd/wave-spawner.md (Rule 5)
> **Architecture Module**: ObjectPool
> **Status**: Ready
> **Manifest Version**: 2026-10-10
> **Stories**: 6 created 2026-10-10
> **Sequencing**: Sprint 1, immediately after project setup — "the very first piece of code" (systems-index high-risk mitigation).

## Overview

A bespoke pool that owns the complete enemy/projectile lifecycle, because the GDDs attach
correctness contracts to it, not just performance: pre-warm at load under an inactive container
(no phantom registrations), `Spawn` assigns a fresh per-spawn `AttackerId` from the pool's
instance counter and spawn-time cooldown jitter from an injected RNG, and `ReturnToPool` is the
only legal deactivation site with the binding order flush-parry-events → deregister →
`SetActive(false)` → push. Eliminates the ghost-parry and ghost-projectile bug classes
structurally and guarantees zero mid-run `Instantiate`/`Destroy`.

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-0005: Object pooling and enemy lifecycle | Bespoke pool; ordered return path; instance-field AttackerId; pre-warm guard; injected jitter RNG | LOW |
| ADR-0001: Entity architecture | Deferred registration via `RegisterPending` | LOW |

## GDD Requirements

| TR-ID | Requirement | ADR Coverage |
|-------|-------------|--------------|
| TR-ED-003 | AttackerId unique per spawn; `ResetInstanceIdCounter()` seam | ADR-0005 ✅ |
| TR-ED-004 | Flush-before-deactivate; `SetActive(false)` only in pool; AttackerId-keyed flush | ADR-0005 ✅ |
| TR-ED-008 | Deferred registration staging | ADR-0001/0005 ✅ |
| TR-ED-009 | Spawn-time cooldown jitter | ADR-0005 ✅ |
| TR-WS-001 | Pre-warmed pools sized to max_enemy_count; no Instantiate/Destroy per enemy | ADR-0005 ✅ |
| TR-PR-005 | Projectile pool keyed by AttackerId (cancel linkage detail at story time) | ADR-0005 ⚠️ partial |

## Stories

| # | Story | Type | Status | ADR |
|---|-------|------|--------|-----|
| 001 | [Pool core: pre-warm, Spawn/ReturnToPool, exhaustion policy](story-001-pool-core-prewarm-spawn-return.md) | Logic | Ready | ADR-0005 |
| 002 | [Per-spawn AttackerId and spawn-time cooldown jitter](story-002-attacker-id-spawn-jitter.md) | Logic | Ready | ADR-0005 |
| 003 | [Deferred registration staging and pre-warm phantom guard](story-003-deferred-registration-prewarm-guard.md) | Logic | Ready | ADR-0001, ADR-0005 |
| 004 | [Binding ReturnToPool order (flush → deregister → deactivate → push)](story-004-binding-return-order.md) | Logic | Ready | ADR-0005 |
| 005 | [Archer projectile pool keyed by AttackerId with cancel-all linkage](story-005-projectile-pool-attacker-cancel.md) | Logic | Ready | ADR-0005 |
| 006 | [Zero-Instantiate churn validation and player-build Profiler capture](story-006-zero-instantiate-churn-validation.md) | Integration | Ready | ADR-0005 |

## Definition of Done

This epic is complete when:
- All stories are implemented, reviewed, and closed via `/story-done`
- EditMode tests: unique AttackerIds across reuse; flush called before deactivation (spy); jitter distribution with seeded RNG (AC-18); pre-warm produces zero RegisterPending entries
- Profiler capture (player build): zero `Instantiate` calls and zero spawn-churn GC over a 2-minute stress run
- All Logic and Integration stories have passing test files in `tests/`

## Next Step

Run `/create-stories object-pool`.

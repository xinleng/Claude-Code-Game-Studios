# Epic: GameData Configuration & Validation

> **Layer**: Foundation
> **GDD**: design/gdd/enemy-definition.md, design/gdd/general-characters.md, design/gdd/upgrade-definition.md (data definitions); validation rules from damage-health, dodge-system, combat-core, progression-system
> **Architecture Module**: GameData (ScriptableObject configs)
> **Status**: Ready (flag #4 resolved 2026-10-10 — delta-multiplier model; story 008 unblocked)
> **Manifest Version**: 2026-10-10
> **Stories**: 8 created 2026-10-10 (all Ready)

## Overview

The three pure data-definition GDD systems become ScriptableObject assets rather than runtime
modules: `EnemyDataSO` (per type/tier, Warlord P1 + P2 both serialized), `GeneralData` (nullable
geometry fields behind accessor guards, never 0-sentinels), `UpgradeCatalogueSO` (24 entries +
draw-rule constants), and `TuningConfig` (every other knob, incl. `run_duration_s` and
`arena_bounds`). A `GameDataValidator` pass at Bootstrap applies every GDD clamp rule with
`[DESIGNER-ERROR]` logging, and an editor test cross-checks SO values against
`design/registry/entities.yaml` so design truth and code truth can't drift silently.
Configs are injected (no static `Config.Instance`) and constructible in-memory for tests.

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-0006: Data-driven configuration via ScriptableObjects | Four SO families; direct refs; validator; registry cross-check; no Addressables | LOW |
| ADR-0012: Scene/boot flow | Validation runs first in boot; `arena_bounds` + `run_duration_s` in TuningConfig | LOW |
| ADR-0010: Test harness | `TestConfigs.Default()` factories | LOW |

## GDD Requirements

| TR-ID | Requirement | ADR Coverage |
|-------|-------------|--------------|
| TR-ED-007 | Warlord P1/P2 serialized SO refs, atomic swap | ADR-0006 ✅ (swap point: GDD flag #3) |
| TR-ED-013 | Load/receipt-time validation + clamps + designer errors | ADR-0006 ✅ |
| TR-GC-001 | GeneralData assets; nullable accessor guards; final stored values | ADR-0006 ✅ |
| TR-UD-001 | UpgradeCatalogue data asset (24 entries + draw rules) | ADR-0006 ✅ (delta model, flag #4 resolved) |
| TR-CC-005 | Upgrade deltas delivered as resolved values | ADR-0006 ✅ |
| TR-TP-001 | Gameplay values data-driven, never hardcoded | ADR-0006 ✅ |
| TR-TP-002 | `[DESIGNER-ERROR]` channel | ADR-0006/0010 ✅ |

## Stories

| # | Story | Type | Status | ADR |
|---|-------|------|--------|-----|
| 001 | [EnemyDataSO — per-type/tier enemy data assets (incl. Warlord P1 + P2)](story-001-enemy-data-so.md) | Logic | Ready | ADR-0006 |
| 002 | [GeneralData — general profiles with nullable accessor guards](story-002-general-data.md) | Logic | Ready | ADR-0006 |
| 003 | [TuningConfig — grouped tuning asset with run_duration_s, arena_bounds, pool sizes](story-003-tuning-config.md) | Logic | Ready | ADR-0006, ADR-0012 |
| 004 | [TestConfigs.Default() — in-memory config factory mirroring GDD defaults](story-004-test-configs-factory.md) | Logic | Ready | ADR-0010 |
| 005 | [GameDataValidator core + enemy-definition clamp rules](story-005-validator-core-enemy-rules.md) | Logic | Ready | ADR-0006, ADR-0012 |
| 006 | [GameDataValidator cross-system rules (caps, dodge, A6, XP, arena, generals)](story-006-validator-cross-system-rules.md) | Logic | Ready | ADR-0006, ADR-0012 |
| 007 | [Registry cross-check — SO values == entities.yaml (CI)](story-007-registry-crosscheck.md) | Integration | Ready | ADR-0006 |
| 008 | [UpgradeCatalogueSO — 24-entry catalogue + draw-rule constants](story-008-upgrade-catalogue-so.md) | Logic | Ready | ADR-0006 |

## Definition of Done

This epic is complete when:
- All stories are implemented, reviewed, and closed via `/story-done`
- Every GDD clamp rule has a red/green validator test (AC-DH-10, enemy-definition guards, dodge Formula 2 — both inequalities, A6 floor, `xp_scalar > 0`, spawn ring fits arena bounds)
- CI cross-check: SO values == entities.yaml values
- All Logic and Integration stories have passing test files in `tests/`

## Next Step

Run `/create-stories game-data` (resolve GDD flag #4 before the UpgradeCatalogue story).

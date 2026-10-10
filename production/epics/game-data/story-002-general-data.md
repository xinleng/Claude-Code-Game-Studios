# Story 002: GeneralData — general profiles with nullable accessor guards

> **Epic**: GameData Configuration & Validation
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Logic
> **Manifest Version**: 2026-10-10

## Context

**GDD**: `design/gdd/general-characters.md` (General Interface data contract; Formulas; Tuning Knobs; AC-06)
**Requirement**: `TR-GC-001`, `TR-TP-001`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0006: Data-Driven Configuration via ScriptableObjects
**ADR Decision Summary**: `GeneralData` is one asset per general holding weapon identity, Finisher geometry (shape enum, radius, nullable angle, direction mode), pre-computed FinisherDamage/Radius and nullable knockback fields. Nullable semantics are enforced by accessor guards — never 0-sentinels.

**Engine**: Unity 6.3 LTS | **Risk**: LOW
**Engine Notes**: Unity cannot serialize `Nullable<T>` (`float?`) — use a serialized has-value bool + value field pair per nullable field. No post-cutoff APIs.

**Control Manifest Rules (this layer)**:
- Required: All gameplay values live in `GameData` ScriptableObjects, direct serialized references — ADR-0006
- Required: Systems receive their SO (or a typed view) via Bootstrap injection — no static `Config.Instance` — ADR-0006, ADR-0012
- Forbidden: Never hardcode a gameplay/tuning value; never use `Resources.Load`/Addressables — ADR-0006
- Guardrail: configs resident, zero runtime cost after Bootstrap validation — ADR-0006

---

## Acceptance Criteria

*From GDD `design/gdd/general-characters.md`, scoped to this story:*

- [ ] `GeneralData` exposes every General Interface field: `GeneralId` (GuanYu, ZhaoYun, LuBu), `WeaponName`, `PlaystyleSummary`, `FinisherShape` (Ring, Cone, PointBurst), `FinisherRadius`, `FinisherAngle` (nullable), `FinisherDirectionMode` (Omnidirectional, ForwardFacing), `FinisherDamage`, `BaseAttack`, `KnockbackForce` (nullable), `KnockbackRadius` (nullable)
- [ ] `FinisherAngle` is readable only when `FinisherShape == Cone`; `KnockbackForce`/`KnockbackRadius` only when `FinisherShape == PointBurst` — accessor guards, never a 0.0 sentinel (TR-GC-001)
- [ ] Three assets authored with GDD values: Guan Yu (Ring, Omnidirectional, radius 3.0, damage 25), Zhao Yun (Cone, ForwardFacing, half-angle 45°, length 3.0, damage 25), Lu Bu (PointBurst, Omnidirectional, radius 3.0, damage 25, knockback force 5.0, knockback radius 3.0); all with `BaseAttack = 10`; weapon names and playstyle summaries per the GDD
- [ ] `FinisherDamage` and `FinisherRadius` are stored final pre-computed values (`base_attack × 2.5`, `base_radius × 2.0`) and are never mutated at runtime — no setter reachable from gameplay code (TR-GC-001; GDD "Key design principle")
- [ ] AC-06 (data portion): a `GeneralData` profile initialized directly in a test — no Combo System — yields `FinisherDamage == 25` and `FinisherRadius == 3.0` (±0.001), so `effective = stored × (1 + 0.0)` returns 25 / 3.0 with zero deltas

---

## Implementation Notes

*Derived from ADR-0006 Decision §2 and general-characters.md General Interface:*

- `GeneralData : ScriptableObject` in the `GameData` module; private serialized `_camelCase` fields, doc-commented PascalCase get-only accessors.
- Nullable pattern (Unity cannot serialize `float?`): `[SerializeField] bool _hasFinisherAngle; [SerializeField] float _finisherAngle;` exposed only as `bool TryGetFinisherAngle(out float angle)` (same for `KnockbackForce`, `KnockbackRadius`). The `TryGet` returns `false` whenever the has-value flag is clear — callers can never read a meaningless 0.0.
- Store final values only. The formulas (`finisher_damage = base_attack × finisher_damage_multiplier`, `finisher_radius = max(base_radius × finisher_radius_multiplier, base_radius + 0.5u)`) are applied when the designer authors the asset; runtime code never evaluates them. `base_radius` (1.5u) is stored alongside for the floor-clamp validation rule (Story 006).
- Canonical screen-forward fallback `Vector3(0,0,1)` (TR-GC-003) is a Combo/Combat Core concern — not a GeneralData field.
- EditMode test construction path as in Story 001 (`CreateInstance` + test-assembly-only initializer).
- **Flag #4 caveat:** the runtime effective-value formula (`FinisherDamage × (1 + damage_delta_multiplier)`) belongs to the Combo System and is subject to GDD revision flag #4 (C1/C2 static override vs delta-multiplier). This story only guarantees the stored, never-mutated base values that both models read.
- Flag #9 note: damage-health Rule 4 references per-general `combo_reset_on_damage` / `momentum_reset_multiplier` fields that the General Interface does not list — do not add them here until the GDD revision lands.

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 006: validator rules for this data (FinisherRadius ≥ base_radius + 0.5u floor; has-value flags consistent with shape; shape ↔ direction-mode mapping)
- Story 007: CI cross-check against `entities.yaml` (guan_yu / zhao_yun / lu_bu entries)
- Combo System / Combat Core epics: Finisher geometry resolution, knockback application, upgrade deltas
- Run UI / RSM epics: selection screen, data-load-failure critical error state

---

## QA Test Cases

*Written by qa-lead at story creation (lean mode). The developer implements against these — do not invent new test cases during implementation.*

- **AC-1**: General Interface fields present and typed
  - Given: an in-memory Guan Yu `GeneralData` built from the test fixture
  - When: all accessors are read
  - Then: `GeneralId == GuanYu`, `FinisherShape == Ring`, `FinisherDirectionMode == Omnidirectional`, `BaseAttack == 10`, `WeaponName`/`PlaystyleSummary` equal fixture strings
  - Edge cases: enum fields default must not silently equal a valid general — uninitialized test profile fails a sanity assertion

- **AC-2**: Nullable accessor guards
  - Given: Guan Yu (Ring) and Lu Bu (PointBurst) and Zhao Yun (Cone) profiles
  - When: `TryGetFinisherAngle`, `TryGetKnockbackForce`, `TryGetKnockbackRadius` are called on each
  - Then: Zhao Yun angle → `true`, 45.0; Guan Yu and Lu Bu angle → `false`; Lu Bu knockback force → `true`, 5.0 and radius → `true`, 3.0; Guan Yu and Zhao Yun knockback → `false`
  - Edge cases: a Cone profile whose has-value flag is set with angle 0.0 still returns `true`, 0.0 (the guard is the flag, not the value — Story 006 validator catches the bad angle)

- **AC-3**: Three authored assets match the GDD
  - Given: the three `.asset` files loaded via `AssetDatabase` in an EditMode test
  - When: compared against the expected-values fixture
  - Then: all fields match (radius 3.0, damage 25 for all; Zhao Yun half-angle 45; Lu Bu knockback 5.0 / 3.0); exactly 3 `GeneralData` assets exist
  - Edge cases: tolerance 0.001 on floats

- **AC-4**: Stored values never mutated
  - Given: the compiled `GeneralData` type
  - When: public members are reflected
  - Then: `FinisherDamage` and `FinisherRadius` have no public or internal-to-runtime setter; no public field
  - Edge cases: reading `FinisherDamage` 1,000 times returns the same value (no lazy recompute)

- **AC-5**: AC-06 data portion — values readable without Combo System
  - Given: a profile initialized directly with base values and test-fixture deltas `damage_delta_multiplier = 0.0`, `radius_delta_multiplier = 0.0`
  - When: `stored × (1 + delta)` is evaluated in the test
  - Then: damage == 25 and radius == 3.0 (±0.001)
  - Edge cases: test assembly must not reference the Combo System assembly

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: `tests/unit/game-data/game_data_general_data_test.cs` — must exist and pass

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: bootstrap-combat-frame Story 001 (Unity 6.3 project setup, asmdefs, Test Framework, CI) must be DONE
- Unlocks: Story 004 (TestConfigs factory), Story 006 (GeneralData validation rules), Story 007 (registry cross-check)

# Story 006: GameDataValidator cross-system rules (caps, dodge, A6, XP, arena, generals)

> **Epic**: GameData Configuration & Validation
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Logic
> **Manifest Version**: 2026-10-10

## Context

**GDD**: `design/gdd/damage-health.md` (Formula 1 guard, `regen_rate_max`, AC-DH-10); `design/gdd/upgrade-definition.md` (Formulas 1–2 guards); `design/gdd/dodge-system.md` (Formula 2, Edge Cases); `design/gdd/combat-core.md` (A6 floor edge case); `design/gdd/progression-system.md` (`xp_scalar > 0` guard); `design/gdd/general-characters.md` (finisher_radius floor clamp, General Interface nullable rules); `design/gdd/wave-spawner.md` (spawn ring + arena rejection)
**Requirement**: `TR-DH-008`, `TR-DG-001`, `TR-PG-001`, `TR-RS-006`, `TR-GC-001`, `TR-TP-002`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0006: Data-Driven Configuration via ScriptableObjects (Decision §5, Requirements); ADR-0012 (Decision 5 — spawn ring must fit inside arena bounds)
**ADR Decision Summary**: `GameDataValidator` applies every GDD clamp rule at bootstrap with `[DESIGNER-ERROR]` logging (assert in editor, clamp-and-continue in builds). ADR-0006 enumerates the cross-system checks: `regen_rate_max`, exact-binary-fraction reductions, dodge-vs-slam constraint, A6 floor vs interval, `xp_scalar > 0`; ADR-0012 adds the spawn-ring-fits-bounds rule.

**Engine**: Unity 6.3 LTS | **Risk**: LOW
**Engine Notes**: Pure C# rule functions; no engine API beyond `Vector3`/value types. No post-cutoff APIs.

**Control Manifest Rules (this layer)**:
- Required: Run `GameDataValidator` at Bootstrap: every GDD clamp rule, `[DESIGNER-ERROR]` logging, assert in editor / clamp-and-continue in builds — ADR-0006
- Required: Arena bounds live in `TuningConfig.arena_bounds`, exposed read-only to WaveSpawner and DodgeSystem — ADR-0012
- Forbidden: Never hardcode a gameplay/tuning value (boundary-value tests excepted) — ADR-0006
- Guardrail: Bootstrap validation is the only load-time pass — ADR-0006

---

## Acceptance Criteria

*From the listed GDDs, scoped to this story:*

- [ ] **AC-DH-10 (cap rule)**: a damage-reduction value of 0.90 against `damage_reduction_cap == 0.75` is clamped to 0.75 and a designer error is logged; run continues, no exception. Boundary: 0.75 applies without an error. Delivered as a reusable pure rule the Damage & Health epic calls at upgrade-apply time and the validator uses at load.
- [ ] **Regen cap** (upgrade-definition Formula 2 guard): a cumulative `regen_rate` above `regen_rate_max` (1.5 HP/s) is clamped to 1.5 with a designer error; exactly 1.5 is accepted silently
- [ ] **Dodge Formula 2, inequality A**: designer error at load if `dodge_distance ≤ slam_radius` (slam radius read from the Warlord-P2 `EnemyDataSO`)
- [ ] **Dodge Formula 2, inequality B**: designer error at load if `dodge_invuln_ms < dodge_duration × 1000 + reaction_margin`
- [ ] **A6 floor**: designer error at load if `attack_cooldown_minimum ≥ base_attack_interval` (combat-core edge case: A6 reset would have no effect)
- [ ] **`xp_scalar > 0`**: designer error if `xp_scalar ≤ 0` (progression guard — surfaced, never silent)
- [ ] **Spawn ring vs arena**: designer error if the `spawn_ring_radius` ring does not fit inside `arena_bounds`
- [ ] **GeneralData finisher_radius floor**: a stored `FinisherRadius < BaseRadius + 0.5u` is clamped up to `BaseRadius + 0.5u` with a designer error
- [ ] **GeneralData nullable/shape consistency**: designer error if `HasFinisherAngle` is set on a non-Cone or missing on a Cone; if knockback values are set on a non-PointBurst or missing on a PointBurst; or if direction mode breaks Ring→Omnidirectional, Cone→ForwardFacing, PointBurst→Omnidirectional
- [ ] Every rule has a red and a green test; `ValidateAll(TestConfigs.Default())` remains clean

---

## Implementation Notes

*Derived from ADR-0006 Decision §5 / Requirements and ADR-0012 Decision 5:*

- Add each rule as a pure function registered in `ValidateAll` (Story 005 framework). Same report shape, same channel, same mode semantics.
- Rules with a GDD-specified correction clamp (cap, regen cap, finisher_radius floor); rules the GDDs describe as "designer-warn" (dodge A/B, A6 floor) or "surfaced, not silent" (`xp_scalar`) and the consistency rules report only — no invented correction value. In fail-loud (editor) mode every violation still asserts.
- AC-DH-10 and the regen cap are enforced at *upgrade-apply time* by Damage & Health (damage-health Formula 1 Guard). This story ships the shared pure rule and its red/green tests; wiring it into `PlayerHealthSystem` is the Damage & Health epic. The *exact-binary-fraction* rule on S2/S3/S6 deltas depends on the catalogue descriptor shape and lives in Story 008 (Blocked).
- `xp_scalar` is required "at run start" by progression-system; the validator rule runs at bootstrap and the RSM epic may re-invoke the same rule at run start.
- **Spawn-ring inequality — derived, confirm at `/story-readiness`:** neither ADR-0012 nor wave-spawner.md gives the exact inequality. Proposed: `spawn_ring_radius < min(arena_half_width, arena_half_depth)` (a ring centred on the arena centre lies entirely inside). Also report zero/negative-area bounds.
- `reaction_margin` and `arena_bounds` have no GDD default (Story 003 open items); green tests for those two rules use the designer-confirmed values via `TestConfigs`.

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 005: validator framework + enemy-definition rules
- Story 008 (Blocked): binary-fraction rule for upgrade damage-reduction deltas; catalogue counts
- Damage & Health epic: calling the cap rules at upgrade-apply time; `raw_damage ≤ 0` intake guard (AC-DH-12)
- Dodge / WaveSpawner epics: runtime dash clamp and spawn-point rejection

---

## QA Test Cases

*Written by qa-lead at story creation (lean mode). The developer implements against these — do not invent new test cases during implementation.*

- **AC-1**: AC-DH-10 damage-reduction cap
  - Given: `damage_reduction_cap = 0.75` from `TestConfigs.DefaultTuning()`
  - When: the cap rule is applied to 0.90, 0.75, 0.50
  - Then: 0.90 → 0.75 + one `[DESIGNER-ERROR]` line, no exception; 0.75 → 0.75, no error; 0.50 → 0.50, no error
  - Edge cases: 0.7500001 → 0.75 + error (float just above cap)

- **AC-2**: Regen cap
  - Given: `regen_rate_max = 1.5`
  - When: rule applied to 2.0, 1.5, 1.0
  - Then: 2.0 → 1.5 + error; 1.5 → 1.5 no error; 1.0 → 1.0 no error
  - Edge cases: cumulative input 1.0 + 1.0 (two sources summed by caller) → 1.5 + error

- **AC-3**: Dodge inequality A (distance vs slam radius)
  - Given: default Warlord-P2 slam radius 2.0
  - When: `dodge_distance` = 3.0, 2.0, 1.9
  - Then: 3.0 → no error; 2.0 → error (≤ fails strict >); 1.9 → error
  - Edge cases: slam radius overridden to 3.5 with default distance 3.0 → error (re-validation when slam radius changes)

- **AC-4**: Dodge inequality B (invuln vs duration + margin)
  - Given: `dodge_duration = 0.25`, `reaction_margin = m` (designer value from TestConfigs)
  - When: `dodge_invuln_ms` = 250 + m, 250 + m − 1, 400
  - Then: 250 + m → no error (equality satisfies ≥); 250 + m − 1 → error; 400 → no error if 400 ≥ 250 + m
  - Edge cases: `dodge_duration = 0.4` with default 400ms invuln → error whenever m > 0

- **AC-5**: A6 floor
  - Given: `base_attack_interval = 1.0`
  - When: `attack_cooldown_minimum` = 0.2, 1.0, 1.2
  - Then: 0.2 → no error; 1.0 → error; 1.2 → error
  - Edge cases: interval overridden to 0.5 with minimum 0.4 → no error

- **AC-6**: `xp_scalar > 0`
  - When: `xp_scalar` = 1.0, 0.0001, 0.0, −1.0
  - Then: 1.0 and 0.0001 → no error; 0.0 → error; −1.0 → error; values are not altered
  - Edge cases: none

- **AC-7**: Spawn ring vs arena (derived inequality, after confirmation)
  - Given: square bounds of half-extent h
  - When: `spawn_ring_radius` = h − 1, h, h + 1
  - Then: h − 1 → no error; h → error; h + 1 → error
  - Edge cases: zero-area bounds → error regardless of radius

- **AC-8**: GeneralData finisher_radius floor
  - Given: a general with `BaseRadius = 1.5`
  - When: stored `FinisherRadius` = 3.0, 2.0, 1.9
  - Then: 3.0 → unchanged, no error; 2.0 → unchanged, no error (boundary = base + 0.5); 1.9 → 2.0 + error
  - Edge cases: `BaseRadius = 0.8` with `FinisherRadius = 1.2` → 1.3 + error

- **AC-9**: GeneralData nullable/shape consistency
  - Given: default generals with one field mutated per case
  - When: (a) Ring with `HasFinisherAngle = true`; (b) Cone with `HasFinisherAngle = false`; (c) Cone with knockback set; (d) PointBurst missing knockback radius; (e) Cone with direction mode Omnidirectional
  - Then: each case → exactly one error naming the general and field; the three unmodified defaults → no error
  - Edge cases: Cone with angle 0.0 and flag set → error (zero-width cone, general-characters FinisherAngle note)

- **AC-10**: Defaults remain clean
  - Given: `TestConfigs.Default()`
  - When: `ValidateAll` runs in fail-loud mode
  - Then: empty report, no assertion
  - Edge cases: none

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: `tests/unit/game-data/game_data_validator_cross_system_rules_test.cs` — must exist and pass

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 005 (validator framework) must be DONE; Story 003 open items (`arena_bounds`, `reaction_margin`) resolved
- Unlocks: Damage & Health epic (cap rules at upgrade-apply time); Dodge, WaveSpawner, Progression, Combat Core epics (load-time guarantees)

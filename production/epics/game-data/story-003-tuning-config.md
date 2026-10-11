# Story 003: TuningConfig — grouped tuning asset with run_duration_s, arena_bounds, pool sizes

> **Epic**: GameData Configuration & Validation
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Logic
> **Manifest Version**: 2026-10-10

## Context

**GDD**: Tuning Knobs sections of `design/gdd/damage-health.md`, `dodge-system.md`, `combat-core.md`, `progression-system.md`, `enemy-definition.md`, `wave-spawner.md`; `run-session-manager.md` (run duration, arena-bounds ownership)
**Requirement**: `TR-TP-001`, `TR-RS-001`, `TR-RS-006`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0006: Data-Driven Configuration via ScriptableObjects; ADR-0012: Scene Organisation and Boot Flow
**ADR Decision Summary**: ADR-0006 — `TuningConfig` holds every knob not owned by EnemyDataSO/GeneralData/UpgradeCatalogueSO, grouped by system in one asset and exposed to systems as read-only typed views (one owner comment per knob naming its source GDD). ADR-0012 — `run_duration_s` (default 1200s) and the rectangular `arena_bounds` live in `TuningConfig`; `GameData` exposes a read-only `ArenaBounds` value consumed by WaveSpawner and DodgeSystem.

**Engine**: Unity 6.3 LTS | **Risk**: LOW
**Engine Notes**: Nested `[Serializable]` section classes/structs serialize fine in a ScriptableObject. No post-cutoff APIs.

**Control Manifest Rules (this layer)**:
- Required: Arena bounds live in `TuningConfig.arena_bounds` (rectangle), exposed read-only via GameData to WaveSpawner and DodgeSystem — ADR-0012
- Required: RSM owns the run countdown (`run_duration_s`, default 1200s from TuningConfig) — ADR-0012
- Required: Systems receive their SO or a typed view via Bootstrap injection — no static `Config.Instance` — ADR-0006
- Forbidden: Never hardcode a gameplay/tuning value — ADR-0006
- Guardrail: TuningConfig must not become a dumping ground — grouped sections + one owner comment per knob (ADR-0006 Risks)

---

## Acceptance Criteria

*From the GDD Tuning Knobs tables and ADR-0012, scoped to this story:*

- [ ] One `TuningConfig` ScriptableObject asset with system-grouped sections; every knob carries a source-GDD owner comment
- [ ] `RunSession` section: `run_duration_s` = 1200 (data-driven, never hardcoded — TR-RS-001)
- [ ] `Arena` section: `arena_bounds` rectangle; `GameData` exposes a read-only `ArenaBounds` value (TR-RS-006)
- [ ] `Pools` section: `max_enemy_count` = 200 plus pool-capacity fields for per-type enemy pools and the archer projectile pool (ADR-0005 "sizes in TuningConfig")
- [ ] Sections required by the validator rules exist with GDD defaults: DamageHealth (`base_max_hp` 100, `invulnerability_duration_ms` 500, `damage_reduction_cap` 0.75, `reset_damage_threshold` 3, `regen_rate_max` 1.5); Dodge (`dodge_distance` 3.0, `dodge_duration` 0.25, `dodge_invuln_ms` 400, `dodge_cooldown` 1.0, `reaction_margin`); CombatCore (`base_attack_interval` 1.0, `base_move_speed` 5.0, `attack_cooldown_minimum` 0.2); Progression (`xp_scalar` 1.0, `xp_base` 10, `xp_growth` 1.22); EnemyShared (`stagger_resist_multiplier` 0.5, `phase_2_threshold_ratio` 0.5, `roar_duration_ms` 1200, `separation_radius` 0.5, `archer_chase_fallback_s` 5, `archer_projectile_speed` 8.0, `archer_projectile_count` 1); Spawner (`spawn_ring_radius` 14)
- [ ] Systems consume per-section read-only typed views (e.g. `IDodgeTuning`) injected at Bootstrap; no static accessor exists anywhere in the module

---

## Implementation Notes

*Derived from ADR-0006 Decision §4 + Risks, ADR-0012 Decisions 4–5:*

- `TuningConfig : ScriptableObject` with one `[Serializable]` section type per system. Each section is surfaced through a read-only interface/view so a consumer sees only its own knobs (e.g. DodgeSystem gets the Dodge view + `ArenaBounds`).
- `ArenaBounds` is an immutable value (readonly struct: XZ min/max of the square/rectangular arena). Consumers own the clamp/rejection logic (Dodge clamp — TR-DG-001; spawn rejection — TR-WS-003).
- Remaining sections (Combo, Parry, Input, UI, VFX/Audio, full Wave escalation) are added by the first story in each consuming epic — ADR-0006 Ordering Note: "GameData assets are authored alongside the first system that consumes them." This story establishes the pattern and the Foundation/validator-relevant sections.
- Values listed in the AC are GDD defaults; cite the source GDD in each field's owner comment (e.g. `// owner: dodge-system.md Tuning Knobs`).
- EditMode-test construction path as in Story 001.

**Open items to confirm at `/story-readiness` (no GDD default exists — do not invent):**
- `arena_bounds` dimensions — ADR-0012 cites the art bible's square-arena rule but neither the art bible nor any GDD gives a size.
- `reaction_margin` (dodge Formula 2) — named, no value given.
- Per-type enemy pool and archer projectile pool capacities — wave-spawner says "sized to `max_enemy_count` (200) per type as needed"; ObjectPool epic owns final sizing.

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 004: `TestConfigs.Default()` in-memory factory mirroring these defaults
- Stories 005–006: clamp/validation rules over these knobs
- RunSessionManager epic: the run clock and countdown behaviour; WaveSpawner/Dodge epics: bounds rejection and dash clamp
- ObjectPool epic: pre-warm using the pool sizes

---

## QA Test Cases

*Written by qa-lead at story creation (lean mode). The developer implements against these — do not invent new test cases during implementation.*

- **AC-1**: Grouped sections with owner comments
  - Given: the `TuningConfig` type
  - When: reflected in an EditMode test
  - Then: the sections RunSession, Arena, Pools, DamageHealth, Dodge, CombatCore, Progression, EnemyShared, Spawner exist as distinct section members
  - Edge cases: owner-comment presence is verified in code review (not automatable)

- **AC-2**: `run_duration_s` default
  - Given: the authored `TuningConfig.asset`
  - When: `RunSession.RunDurationS` is read
  - Then: value == 1200 (from fixture file of GDD defaults)
  - Edge cases: a test config with `run_duration_s = 600` is honoured by the view (no hidden constant)

- **AC-3**: `ArenaBounds` exposed read-only
  - Given: an in-memory config with bounds min (−a, −b) and max (a, b) from the fixture
  - When: `GameData.ArenaBounds` is read twice and the returned value is modified locally
  - Then: the second read is unchanged (value semantics / no setter)
  - Edge cases: zero-area bounds are representable (validator Story 006 rejects them, not this type)

- **AC-4**: Pool section
  - Given: the authored asset
  - When: `Pools.MaxEnemyCount` is read
  - Then: value == 200; per-type and projectile capacity fields exist and are readable
  - Edge cases: none

- **AC-5**: Validator-relevant section defaults
  - Given: the authored asset and the GDD-defaults fixture
  - When: every listed knob is compared
  - Then: all match the AC values (floats ±0.001)
  - Edge cases: `reaction_margin` and `arena_bounds` compare against the designer-supplied fixture values once confirmed

- **AC-6**: Injection only — no static access
  - Given: the GameData assembly
  - When: scanned via reflection
  - Then: no public static property/field returns `TuningConfig` or any section view
  - Edge cases: a consumer constructed with a section view interface compiles without referencing `TuningConfig` directly

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: `tests/unit/game-data/game_data_tuning_config_test.cs` — must exist and pass

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: bootstrap-combat-frame Story 001 (Unity 6.3 project setup, asmdefs, Test Framework, CI) must be DONE
- Unlocks: Story 004 (TestConfigs factory), Stories 005–006 (validator rules), Story 007 (registry cross-check of constants)

# Story 007: Registry cross-check — SO values == design/registry/entities.yaml (CI)

> **Epic**: GameData Configuration & Validation
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Integration
> **Manifest Version**: 2026-10-10

## Context

**GDD**: `design/registry/entities.yaml` (design-side source of truth; entities + constants sections) — values owned by `enemy-definition.md`, `general-characters.md`, `damage-health.md`
**Requirement**: `TR-TP-001`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0006: Data-Driven Configuration via ScriptableObjects (Decision §6, Validation Criteria)
**ADR Decision Summary**: `design/registry/entities.yaml` remains design truth; a lightweight editor test cross-checks SO values against the registry so drift between design truth and code truth is caught in CI, not in playtest. Validation criterion: "CI cross-check: SO values == entities.yaml values."

**Engine**: Unity 6.3 LTS | **Risk**: LOW
**Engine Notes**: Editor-only EditMode test (`AssetDatabase` to load the authored SOs; `System.IO` to read the yaml from the repo root). Runs in CI via `game-ci/unity-test-runner@v4` (ADR-0010). No post-cutoff APIs.

**Control Manifest Rules (this layer)**:
- Required: All gameplay values live in `GameData` ScriptableObjects — ADR-0006
- Required: CI runs EditMode + PlayMode via `game-ci/unity-test-runner@v4` on every push/PR; failing tests block merge — ADR-0010
- Forbidden: Never disable or skip a failing test to make CI pass — coding standards
- Guardrail: No third-party libraries approved yet — additions require a technical-preferences update (manifest Approved Libraries)

---

## Acceptance Criteria

*From ADR-0006 Decision §6 / Validation Criteria and epic Definition of Done:*

- [ ] An editor test reads `design/registry/entities.yaml` and, for every `status: active` entry that maps to an authored GameData field, asserts the SO value equals the registry value
- [ ] Coverage at minimum: entities `guan_yu`, `zhao_yun`, `lu_bu` (base_attack, base_radius, finisher_shape, finisher_direction, finisher_radius, finisher_damage, cone_half_angle_deg, knockback_force/radius); `levy_soldier`, `halberd_pikeman`, `fire_arrow_archer`, `iron_captain`, `warlord` (hp, move speeds, attack damages, ranges, xp_weight, telegraph durations, stagger duration, `phase_2_trigger_hp` via Formula 3); constants `base_attack`, `base_radius`, `zhao_yun_cone_half_angle_deg`, `base_max_hp`, `invulnerability_duration_ms`, `reset_damage_threshold`, `regen_rate_max`, `damage_reduction_cap`, `stagger_resist_multiplier`, `phase_2_threshold_ratio`
- [ ] `status: deprecated` entries (e.g. `pity_threshold`) are ignored
- [ ] A mismatch fails the test with a message naming registry entry, attribute, registry value and SO value
- [ ] Registry attributes with no GameData counterpart yet (e.g. `zhao_yun_input_direction_window_ms`, `parry_input_buffer_ms` — owned by Combo/Input sections added later) are listed in an explicit, reviewed skip-map in the test, never silently dropped
- [ ] The test runs in the CI EditMode suite and blocks merge on failure

---

## Implementation Notes

*Derived from ADR-0006 Decision §6 and Consequences ("two sources that can drift → mitigated by the CI cross-check test; yaml stays authoritative"):*

- Keep yaml authoritative: on mismatch, the fix is to update the SO (or, if design changed, update the yaml via `/consistency-check` first) — the test never rewrites either file.
- **YAML parsing:** no YAML library is approved (manifest). Either (a) a minimal reader limited to the registry's documented flat format (`entities:` / `constants:` lists of `- name:` blocks with `key: value` lines; strip `# …` comments), or (b) request library approval via a technical-preferences update. Choose at `/story-readiness`; (a) needs no approval.
- Mapping lives in one table in the test (registry name/attribute → SO asset + accessor), so adding a field is a one-line change.
- Iron Captain tier HP 375/450 appear only in a yaml comment (`hp: 300  # 5-min; 10-min = 375; 15-min = 450`); only the machine-readable 5-min value is cross-checked — record the 10/15-min tiers in the skip-map with that reason.
- Enum attributes compare by name mapping (`Ring`/`Cone`/`PointBurst`, `Omnidirectional`/`ForwardFacing`); telegraph-shape strings (`horizontal_ring`, `arc_270`, …) map to the `TelegraphShape` enum.

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Stories 001–003: authoring the assets being checked
- Story 008 (Blocked): catalogue-related registry references (A1/A3/S1 overrides noted in `referenced_by` comments)
- `/consistency-check`: GDD ↔ registry drift (design-side); this story is registry ↔ code only

---

## QA Test Cases

*Written by qa-lead at story creation (lean mode). The developer implements against these — do not invent new test cases during implementation.*

- **AC-1**: Green on current assets
  - Given: the authored assets from Stories 001–003 and the committed `entities.yaml`
  - When: the cross-check test runs
  - Then: it passes, and the number of compared (entry, attribute) pairs equals the mapping-table size
  - Edge cases: test fails if the mapping table compares zero pairs (guards against a broken parser passing vacuously)

- **AC-2**: Mismatch detection (red)
  - Given: a temporary in-memory copy of the Levy data with `MaxHp = 31` injected into the comparer (no asset written to disk)
  - When: the comparison runs against registry `levy_soldier.hp: 30`
  - Then: a failure is reported naming `levy_soldier.hp`, registry 30, SO 31
  - Edge cases: float attribute (`warlord.move_speed_p2: 2.0` vs 2.05) also reported; tolerance 0.001

- **AC-3**: Deprecated entries ignored
  - Given: the registry containing `pity_threshold` with `status: deprecated`
  - When: the test runs
  - Then: no comparison is attempted for it and no failure results
  - Edge cases: an entry flipped to deprecated in a test fixture yaml is skipped

- **AC-4**: Explicit skip-map
  - Given: registry attributes with no SO counterpart
  - When: the test runs
  - Then: each is present in the skip-map with a reason; an active attribute that is neither mapped nor skip-listed fails the test ("unmapped registry attribute")
  - Edge cases: comment-only data (Iron Captain 10/15-min HP) is skip-listed, not parsed

- **AC-5**: Derived values
  - Given: Warlord P1 asset (`MaxHp` 800) and `phase_2_threshold_ratio` 0.5 from TuningConfig
  - When: compared with registry `warlord.phase_2_trigger_hp: 400`
  - Then: Formula 3 result equals 400
  - Edge cases: none

- **AC-6**: CI gate
  - Given: the GitHub Actions workflow from `/test-setup`
  - When: a push includes a deliberately mismatched SO on a scratch branch
  - Then: the EditMode job fails and the PR check is red
  - Edge cases: verified once and recorded in the story's completion notes

---

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- Integration: `tests/integration/game-data/game_data_registry_crosscheck_test.cs` — must exist and pass in CI

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001, Story 002, Story 003 must be DONE (assets authored); bootstrap-combat-frame Story 001 (CI workflow live)
- Unlocks: Epic Definition of Done item "CI cross-check: SO values == entities.yaml values"

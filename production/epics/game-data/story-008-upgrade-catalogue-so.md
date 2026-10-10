# Story 008: UpgradeCatalogueSO — 24-entry catalogue + draw-rule constants

> **Epic**: GameData Configuration & Validation
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Logic
> **Manifest Version**: 2026-10-10

> **UNBLOCKED 2026-10-10:** GDD flag #4 resolved by the user — **delta-multiplier model**. C1 descriptor = `damage_delta_multiplier += 0.6` (`finisher_damage_delta_c1`), C2 = `radius_delta_multiplier += 0.5` (`finisher_radius_delta_c2`); no static overrides anywhere. Combo's `finisher_damage_delta_cap` raised to 0.6. See upgrade-definition.md C1/C2 rows + Tuning Knobs.

## Context

**GDD**: `design/gdd/upgrade-definition.md` (Core Rules §1–2; Upgrade Catalogue table; Formulas 1–2; Tuning Knobs; AC-UD-01 catalogue portion)
**Requirement**: `TR-UD-001`, `TR-CC-005`, `TR-DH-008`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0006: Data-Driven Configuration via ScriptableObjects (Decision §3)
**ADR Decision Summary**: `UpgradeCatalogueSO` holds the 24 entries (id, name, one-line description, category, typed delta descriptor, draw-condition flag for C3) plus draw-rule constants (drought threshold/multiplier, modifier cap). The validator enforces exact-binary-fraction damage reductions.

**Engine**: Unity 6.3 LTS | **Risk**: LOW
**Engine Notes**: A polymorphic "typed delta descriptor" in a ScriptableObject may need `[SerializeReference]` or a tagged-union struct — choose only after flag #4 fixes the descriptor shape. No reflection/string lookups for effect routing (TR-UD-003).

**Control Manifest Rules (this layer)**:
- Required: All gameplay values live in `GameData` ScriptableObjects (`UpgradeCatalogueSO` named explicitly) — ADR-0006
- Required: Draw logic (drought, C3 condition, Modifier cap) uses the injected seedable RNG — ADR-0010 (consumer: Progression epic)
- Forbidden: Never hardcode a gameplay/tuning value; never `Resources.Load`/Addressables — ADR-0006
- Guardrail: Bootstrap validation is the only load-time pass — ADR-0006

---

## Acceptance Criteria

*From GDD `design/gdd/upgrade-definition.md`, scoped to this story:*

- [ ] AC-UD-01 (catalogue portion): the total catalogue contains exactly 24 entries — 6 Combo, 6 Parry, 6 Auto-attack, 6 Modifier — with C3 present in the catalogue
- [ ] Each entry carries: `id` (C1–C6, P1–P6, A1–A6, S1–S6), `Name`, one-line `Description`, `Category`, typed stat-effect descriptor, `eligible_generals` (default `all`), and a `draw_condition` flag (set only on C3) — TR-UD-001
- [ ] Draw-rule constants with GDD defaults: `modifier_pick_cap` 3, `drought_weighting_threshold` 4, `drought_weighting_multiplier` 2.0
- [ ] Per-entry tuning values from the GDD Tuning Knobs table are data, not code (e.g. `finisher_damage_delta_c1` 0.6, `finisher_radius_delta_c2` 0.5, `base_attack_a1` 14, `base_radius_a3` 2.25, `max_targets_a4` 3, `max_hp_bonus_s1` 40, `damage_reduction_delta_s2/s3` 0.25, `regen_rate_delta_s4` 1.0, `damage_reduction_at_half_momentum_s6` 0.10, …) — descriptor field names per the flag #4 resolution
- [ ] Deltas are delivered to receivers as resolved values at pick time; Combat Core never reads the catalogue (TR-CC-005)
- [ ] Validator rule (damage-health Rounding contract / TR-DH-008): every damage-reduction delta (S2, S3) must be an exact binary fraction (0.0, 0.25, 0.5, 0.75) — designer error otherwise; red/green tested
- [ ] `TestConfigs.DefaultCatalogue()` factory added, mirroring GDD defaults

---

## Implementation Notes

*Derived from ADR-0006 Decision §3; do not start until flag #4 is resolved:*

- The unresolved question: C1/C2 are written as **static overrides** (`finisher_damage: 25 → 40`, `finisher_radius: 3.0u → 4.5u`) in upgrade-definition.md, while general-characters.md mandates a **delta-multiplier** model (`effective = stored × (1 + delta)`, stored values never mutated). The descriptor shape (override value vs multiplier delta) — and therefore every Combo-lane entry's fields — follows from that decision.
- Once unblocked: one `UpgradeCatalogueSO` asset; entries as serialized structs; category enum {Combo, Parry, AutoAttack, Modifier}; descriptor routing is typed (C→Combo, A→CombatCore, P/S6→Parry, S1–S5→PlayerHealthSystem per TR-UD-003) — no reflection or string lookups.
- The catalogue is immutable content. The mutable `eligible_pool` (starts at 23 with C3 held out) is Progression run state (TR-UD-002) — not part of this SO.
- S6 (0.10) is a conditional reduction evaluated by Parry/D&H at DamageTaken time; the binary-fraction rounding contract in damage-health names S2/S3 deltas — confirm with game-designer whether S6's 0.10 is in scope of that rule before writing the S6 red/green case.
- Register the binary-fraction rule in `GameDataValidator.ValidateAll` (Story 005 framework).

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Progression epic: eligible-pool state, drought weighting, C3 condition evaluation, Modifier cap enforcement, category exhaustion (AC-UD-01 pool portion, AC-UD-02/04/25/26)
- Damage & Health / Combo / Parry / Combat Core epics: applying deltas and enforcing caps at pick time
- Upgrade Selection UI epic: Name/Description display
- Story 006: damage-reduction cap and regen cap rules (already delivered)

---

## QA Test Cases

*Written by qa-lead at story creation (lean mode). Descriptor-field assertions are finalised when flag #4 resolves; the structural cases below are stable.*

- **AC-1**: Catalogue counts (AC-UD-01 catalogue portion)
  - Given: `TestConfigs.DefaultCatalogue()`
  - When: entries are counted by category
  - Then: total == 24; Combo == 6; Parry == 6; Auto-attack == 6; Modifier == 6; an entry with id C3 exists
  - Edge cases: duplicate id in a fixture → validator designer error

- **AC-2**: Entry fields
  - Given: the default catalogue
  - When: each entry is inspected
  - Then: id, Name, Description, Category, descriptor present; `eligible_generals == all` for all 24; `draw_condition` set on C3 only
  - Edge cases: Description containing a newline → designer error (one-line display constraint)

- **AC-3**: Draw-rule constants
  - Given: the default catalogue
  - When: constants are read
  - Then: `modifier_pick_cap == 3`, `drought_weighting_threshold == 4`, `drought_weighting_multiplier == 2.0`
  - Edge cases: none

- **AC-4**: Binary-fraction rule — red/green
  - Given: the S2/S3 descriptors
  - When: delta set to 0.25, 0.5, 0.0, 0.75 (green) and 0.3, 0.1 (red)
  - Then: green values → no error; red values → one `[DESIGNER-ERROR]` each
  - Edge cases: 0.25000001 → error (not exactly representable as the authored binary fraction)

- **AC-5**: Resolved-value delivery (TR-CC-005)
  - Given: the A1 entry
  - When: its descriptor is resolved for delivery
  - Then: the receiver gets the resolved value (`base_attack` 14 under the override model, or the agreed equivalent under the delta model) without referencing `UpgradeCatalogueSO`
  - Edge cases: finalise after flag #4

- **AC-6**: Defaults clean under validation
  - Given: `TestConfigs.Default()` + `DefaultCatalogue()`
  - When: `ValidateAll` runs in fail-loud mode
  - Then: empty report
  - Edge cases: none

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: `tests/unit/game-data/game_data_upgrade_catalogue_test.cs` — must exist and pass

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: **GDD revision flag #4 resolved** (game-designer / creative-director decision; update upgrade-definition.md and/or general-characters.md); Story 004 (TestConfigs) and Story 005 (validator framework) DONE
- Unlocks: Progression epic draw/pool stories; Combo, Parry, Combat Core and Damage & Health upgrade-application stories

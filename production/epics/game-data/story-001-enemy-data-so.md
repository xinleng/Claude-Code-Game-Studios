# Story 001: EnemyDataSO — per-type/tier enemy data assets (incl. Warlord P1 + P2)

> **Epic**: GameData Configuration & Validation
> **Status**: Complete
> **Layer**: Foundation
> **Type**: Logic
> **Manifest Version**: 2026-10-10

## Context

**GDD**: `design/gdd/enemy-definition.md` (Core Rules stat + telegraph tables; Formula 1 xp_weight table; Formula 3; Formula 4 cooldown table; Tuning Knobs; Impl Contract #1 "Phase 2 data load hook")
**Requirement**: `TR-ED-007`, `TR-TP-001`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0006: Data-Driven Configuration via ScriptableObjects
**ADR Decision Summary**: Four ScriptableObject families under `GameData`, referenced directly by serialized fields (no Addressables, no `Resources.Load`). `EnemyDataSO` is one asset per enemy type/tier — Levy, Pikeman, Archer, IronCaptain-5/10/15, Warlord-P1, Warlord-P2 — holding stats, telegraph shape/duration, cooldowns, aggro range, xp_weight and spawn weight; Warlord P1 + P2 are both serialized on the prefab.

**Engine**: Unity 6.3 LTS | **Risk**: LOW
**Engine Notes**: ScriptableObjects are stable (no post-cutoff APIs). Unity cannot serialize `Nullable<T>` — elite/boss-only fields use explicit has-value flags, never 0-sentinels. Do not use C# `record`/`init` in serialized types without the project `IsExternalInit` shim (control manifest, engine-reference correction 2026-10-10).

**Control Manifest Rules (this layer)**:
- Required: All gameplay values live in `GameData` ScriptableObjects (`EnemyDataSO`, `GeneralData`, `UpgradeCatalogueSO`, `TuningConfig`), direct serialized references — ADR-0006
- Required: Wire dependencies by constructor/initializer injection at Bootstrap — no singletons, no `FindObjectOfType`/`FindFirstObjectByType` — ADR-0002, ADR-0012
- Forbidden: Never use `Resources.Load` or Addressables in MVP; never hardcode a gameplay/tuning value (exception: boundary-value tests) — ADR-0006
- Guardrail: Bootstrap validation is the only load-time pass; configs resident (~KBs), zero runtime cost — ADR-0006

---

## Acceptance Criteria

*From GDD `design/gdd/enemy-definition.md`, scoped to this story:*

- [ ] `EnemyDataSO` type exists with typed, inspector-editable fields covering every per-type column in the GDD stat, telegraph, cooldown, aggro and xp_weight tables (HP as `int`, AttackDamage as `int` — GDD Core Rules type contract)
- [ ] Eight assets authored with the GDD default values: Levy Soldier, Halberd Pikeman, Fire Arrow Archer, Iron Captain 5/10/15-min tiers, Warlord Phase 1, Warlord Phase 2 (values table in Implementation Notes)
- [ ] Elite/boss-only fields (counter-hit damage/range/telegraph, stagger duration, slam damage/radius/telegraph) are guarded by has-value flags; reading an absent field through its accessor is impossible without checking (no 0-sentinel)
- [ ] Warlord Phase 1 and Phase 2 are two distinct assets that can both be held as serialized `EnemyDataSO` references at the same time (TR-ED-007 / Impl Contract #1 "both available at spawn"); the Phase 2 asset carries `warlord_p2_move_speed` (2.0) and `warlord_p2_attack_cooldown_ms` (1800) — the data portion of AC-13
- [ ] Formula 3 derived accessor: `PhaseTwoTriggerHp = warlord_max_hp × phase_2_threshold_ratio` (800 × 0.5 = 400 at defaults), computed from config, never hardcoded
- [ ] Assets are read-only to gameplay code (public get-only accessors; no runtime setters)

---

## Implementation Notes

*Derived from ADR-0006 Decision §1 + Key Interfaces, enemy-definition.md Impl Contract #1:*

- One class `EnemyDataSO : ScriptableObject` in the `GameData` module. Private serialized fields (`_camelCase`) behind PascalCase get-only properties, all doc-commented (coding standards).
- Fields (from GDD tables): `EnemyType` enum (Levy, Pikeman, Archer, IronCaptain, Warlord), `MaxHp:int`, `MoveSpeed:float`, `AttackDamage:int`, `AttackRange:float`, `Parriable:bool`, `StaggerRule` enum (AnyFinisher, FullComboOnly), `TelegraphShape` enum, `TelegraphDurationMs:int`, `AttackCooldownMs:int`, `AggroRange:float`, `XpWeight:int`, `SpawnWeight`.
- Guarded optional groups (has-value flag + `TryGet…(out …)` accessor): CounterHit {damage, range, telegraph ms, telegraph shape}; Stagger {duration ms}; Slam {damage, radius, telegraph ms, telegraph shape}.
- Telegraph shapes from the GDD telegraph table: ring (Levy), directional line (Pikeman), arrowhead-above (Archer), thick ring (Iron Captain / Warlord standard), 270° arc (counter-hit), filled circle (Warlord P2 slam).
- `phase_2_threshold_ratio` is the shared knob in `TuningConfig` (Story 003); `PhaseTwoTriggerHp` takes the ratio as an argument (or is computed by a pure static helper) so the SO never caches a derived constant.
- Consumers receive assets by injection; Warlord prefab holds `[SerializeField] EnemyDataSO phase1Data` and `phase2Data` (Impl Contract #1). The atomic `activeData` swap itself is Enemy AI scope — swap point is pending GDD revision flag #3 (registry follows Impl Contract #1 wording).
- Provide an EditMode-test construction path (`ScriptableObject.CreateInstance<EnemyDataSO>()` + an initializer reachable only from the test assembly, e.g. `internal` + `InternalsVisibleTo`) so tests never load assets (ADR-0006 Requirements; ADR-0010 Decision §3).

**Default values to author (enemy-definition.md, mirrored in `design/registry/entities.yaml`):**

| Asset | HP | Speed | Dmg (std / counter / slam) | Range (std / counter / slam) | Telegraph ms (std / counter / slam) | Cooldown ms | Aggro | xp_weight | Stagger |
|---|---|---|---|---|---|---|---|---|---|
| Levy | 30 | 1.5 | 5 | 0.8 | 400 | 2000 | 5 | 1 | AnyFinisher |
| Pikeman | 40 | 2.0 | 8 | 1.8 | 350 | 1800 | 5 | 2 | AnyFinisher |
| Archer | 25 | 0.8 | 10 | 6.0 | 600 | 2500 | 8 | 2 | AnyFinisher |
| IronCaptain-5 | 300 | 1.6 | 12 / 25 | 1.0 / 1.5 | 500 / 600 | 3000 | 6 | 15 | FullComboOnly, 800ms |
| IronCaptain-10 | 375 | 1.6 | 12 / 25 | 1.0 / 1.5 | 500 / 550 | 3000 | 6 | 15 | FullComboOnly, 800ms |
| IronCaptain-15 | 450 | 1.6 | 12 / 25 | 1.0 / 1.5 | 500 / 450 | 3000 | 6 | 15 | FullComboOnly, 800ms |
| Warlord-P1 | 800 | 1.2 | 20 / 30 | 1.0 / 1.5 | 500 / 450 | 2500 | 10 | 50 | FullComboOnly, 1000ms |
| Warlord-P2 | 800 | 2.0 | 20 / 30 / 40 (slam, Parriable=false) | 1.0 / 1.5 / 2.0 | see open item / 800 slam | 1800 | 10 | 50 | FullComboOnly, 1000ms |

**Open items to confirm at `/story-readiness` (do not invent):**
- `SpawnWeight` — ADR-0006 lists it on EnemyDataSO and enemy-definition says Wave Spawner reads spawn weights, but no GDD gives values. Field exists; values must be supplied by the designer.
- Warlord P2 standard/counter-hit telegraph durations and standard/counter damage are not separately tabled for Phase 2 (prose says "increased speed, damage, slam radius"). Until the designer specifies, the P2 asset repeats the P1 standard/counter values; flag to game-designer.

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 003: `stagger_resist_multiplier`, `phase_2_threshold_ratio` and other shared enemy knobs (`roar_duration_ms`, `separation_radius`, archer fallback/projectile knobs) live in `TuningConfig`
- Story 005: clamping/designer-error validation of enemy knobs (Formula 2/3 guards)
- Story 007: CI cross-check of these asset values against `entities.yaml`
- Enemy AI / EnemySystemManager epics: `activeData` swap timing, state machine, runtime HP ledger

---

## QA Test Cases

*Written by qa-lead at story creation (lean mode — QL-STORY-READY gate skipped). The developer implements against these — do not invent new test cases during implementation.*

- **AC-1**: EnemyDataSO exposes every GDD per-type field with the GDD types
  - Given: an in-memory `EnemyDataSO` built via the test construction path with Levy values from the test fixture
  - When: each accessor is read
  - Then: `MaxHp` and `AttackDamage` are `int`; values round-trip exactly (`MaxHp == 30`, `AttackDamage == 5`, `TelegraphDurationMs == 400`, `AttackCooldownMs == 2000`, `XpWeight == 1`)
  - Edge cases: float fields compared within 0.001 tolerance

- **AC-2**: Eight assets authored with GDD defaults
  - Given: the eight authored `.asset` files loaded in an EditMode test via `AssetDatabase`
  - When: each asset's fields are compared with the expected table (fixture file, not inline literals)
  - Then: every field matches; exactly 8 assets of type `EnemyDataSO` exist under the GameData folder
  - Edge cases: Iron Captain tiers differ only in `MaxHp` (300/375/450) and counter telegraph (600/550/450)

- **AC-3**: Optional elite/boss fields are has-value guarded
  - Given: the Levy asset (no counter-hit, no stagger, no slam)
  - When: `TryGetCounterHit`, `TryGetStagger`, `TryGetSlam` are called
  - Then: all return `false`; for IronCaptain-5 `TryGetCounterHit` returns `true` with damage 25 and telegraph 600, `TryGetSlam` returns `false`
  - Edge cases: Warlord-P2 is the only asset whose `TryGetSlam` returns `true` (damage 40, radius 2.0, telegraph 800) and whose `Parriable` for the slam is `false`

- **AC-4**: Warlord P1 and P2 coexist as distinct assets
  - Given: Warlord-P1 and Warlord-P2 assets
  - When: both are assigned to two serialized fields on a test holder object
  - Then: references are non-null and not equal; `P1.MoveSpeed == 1.2`, `P2.MoveSpeed == 2.0`; `P1.AttackCooldownMs == 2500`, `P2.AttackCooldownMs == 1800` (values read from the fixture, not hardcoded in the assertion)
  - Edge cases: none

- **AC-5**: Formula 3 derived trigger HP
  - Given: `warlord_max_hp = 800`, `phase_2_threshold_ratio = 0.5`
  - When: `PhaseTwoTriggerHp` is computed
  - Then: result == 400
  - Edge cases: ratio 0.3 → 240; ratio 0.7 → 560 (boundary-value tests — literals allowed)

- **AC-6**: Assets are read-only to gameplay code
  - Given: the compiled `EnemyDataSO` type
  - When: its public members are reflected in a test
  - Then: no public property has a public setter and no public field is exposed
  - Edge cases: test-only initializer is not `public`

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: `tests/unit/game-data/game_data_enemy_data_test.cs` — must exist and pass

**Status**: [x] `tests/unit/game-data/game_data_enemy_data_test.cs` (13 tests), EditMode 84/84

---

## Dependencies

- Depends on: bootstrap-combat-frame Story 001 (Unity 6.3 project setup, asmdefs, Test Framework, CI) must be DONE
- Unlocks: Story 004 (TestConfigs factory), Story 005 (enemy validation rules), Story 006 (dodge rule reads slam radius), Story 007 (registry cross-check)

---

## Completion Notes
**Completed**: 2026-10-11
**Criteria**: 6/6 passing.
**Deviations**: Advisory —
- New assembly `Dynasty.GameData` (`src/GameData/`, no references) with `InternalsVisibleTo` for both test assemblies; both test asmdefs now reference it.
- Assets authored at `Assets/GameData/Enemies/{Levy,Pikeman,Archer,IronCaptain5,IronCaptain10,IronCaptain15,WarlordP1,WarlordP2}.asset` by a one-shot batchmode script (deleted after use).
- `SpawnWeight` is guarded like the elite groups (`TryGetSpawnWeight`) and **unset on all 8 assets** — no GDD value exists (open item stands: designer to supply).
- Warlord-P2 standard/counter damage + telegraph **repeat Phase 1** (20/30 dmg, 500/450 ms) until the designer tables Phase 2 separately (open item → game-designer).
- `PhaseTwoTriggerHp` returns `float` (GDD variable table types it float) and computes in double (800 × 0.7 → 560).
- Test construction path: internal `EnemyDataSpec` + `EnemyDataSO.CreateForTests`; fixtures in `tests/helpers/EnemyDataFixtures.cs`.
**Test Evidence**: Logic — `tests/unit/game-data/game_data_enemy_data_test.cs` (13 tests). EditMode 84/84.
**Code Review**: Self-review during the autonomous Sprint 1 batch; consolidated independent review at batch end. LP/QL gates skipped (lean).

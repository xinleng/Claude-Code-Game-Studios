# Story 004: TestConfigs.Default() — in-memory config factory mirroring GDD defaults

> **Epic**: GameData Configuration & Validation
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Logic
> **Manifest Version**: 2026-10-10

## Context

**GDD**: Default values from `design/gdd/enemy-definition.md`, `general-characters.md`, and the Tuning Knobs tables used in Story 003; testing standards in `.claude/docs/coding-standards.md`
**Requirement**: `TR-TP-001`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0010: Deterministic Test Harness
**ADR Decision Summary**: All gameplay logic is EditMode-testable through injected clock, manual frame pump and constructor-injected dependencies; `GameData` configs are constructed in-memory via factory helpers (`TestConfigs.Default()` mirroring GDD defaults — no inline magic numbers except boundary-value tests). ADR-0006 requires that deterministic tests can construct configs in memory with no asset loading.

**Engine**: Unity 6.3 LTS | **Risk**: LOW
**Engine Notes**: `ScriptableObject.CreateInstance<T>()` works in EditMode tests without assets; destroy created instances in `[TearDown]` (`Object.DestroyImmediate`) to keep tests isolated.

**Control Manifest Rules (this layer)**:
- Required: Gameplay logic reads time via injected `IGameClock` and randomness via injected `System.Random` — ADR-0010
- Required: Tests first; every Logic AC is a deterministic EditMode test on the manual pump — coding standards, ADR-0010
- Forbidden: Never hardcode a gameplay/tuning value (exception: boundary-value tests) — ADR-0006
- Guardrail: Tests must not perform file I/O or asset loading in unit tests (coding standards: Independence)

---

## Acceptance Criteria

*From ADR-0010 Decision §3, ADR-0006 Requirements, and coding-standards Automated Test Rules:*

- [ ] `TestConfigs.Default()` returns a complete in-memory GameData set — 8 `EnemyDataSO`, 3 `GeneralData`, 1 `TuningConfig` — whose values equal the GDD defaults authored in Stories 001–003, with no `AssetDatabase`/`Resources`/file access
- [ ] Per-family factories exist (`TestConfigs.DefaultEnemies()`, `DefaultGenerals()`, `DefaultTuning()` or equivalent) plus a mutation hook so a test can override one knob without restating the rest (boundary-value tests)
- [ ] Every call returns fresh, independent instances — mutating one test's config never affects another (isolation, order-independence)
- [ ] The factory is the single home of default literals for tests; test files reference factory values, not inline numbers
- [ ] The `UpgradeCatalogueSO` factory is NOT part of this story (Story 008, Blocked)

---

## Implementation Notes

*Derived from ADR-0010 Decision §3 and §6, ADR-0006 Requirements:*

- Lives in the shared test-helpers assembly scaffolded by `/test-setup` (referenced by `tests/unit/**` and `tests/integration/**`); not shipped in player builds.
- Build instances with `ScriptableObject.CreateInstance<T>()` + the test-only initializers added in Stories 001–003 (`InternalsVisibleTo` the helpers assembly).
- Default literals are written once, here, with a comment citing the source GDD table; they must stay equal to the authored assets — Story 007's cross-check compares assets with `entities.yaml`, and this story's test compares the factory with the authored assets so all three agree.
- Override pattern example: `TestConfigs.DefaultTuning(t => t.Dodge.DodgeDistance = 2.0f)` — exact shape is the implementer's choice, but overrides must not leak across calls.
- Values without a GDD default (`arena_bounds`, `reaction_margin`, `SpawnWeight`, pool capacities — see Stories 001/003 open items) use the designer-confirmed values once supplied; until then the factory must not invent them. This story cannot be marked Done until the Story 001/003 open items are resolved (never ship a disabled or skipped test to get around a missing value — coding standards CI rules).

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 008 (Blocked): `TestConfigs.DefaultCatalogue()`
- bootstrap-combat-frame epic: `IGameClock` fake, `EventRecorder`, seeded RNG helpers
- Stories 005–006: asserting `ValidateAll(TestConfigs.Default())` yields zero designer errors (added there once the validator exists)

---

## QA Test Cases

*Written by qa-lead at story creation (lean mode). The developer implements against these — do not invent new test cases during implementation.*

- **AC-1**: Complete default set, no asset loading
  - Given: an EditMode test with no assets loaded
  - When: `TestConfigs.Default()` is called
  - Then: 8 enemy configs, 3 general configs and 1 tuning config are returned, all non-null
  - Edge cases: test passes with the GameData asset folder absent from the test assembly's dependencies

- **AC-2**: Factory equals authored assets
  - Given: the authored `.asset` files (loaded in a separate editor-only comparison test) and `TestConfigs.Default()`
  - When: each field is compared pairwise
  - Then: every field matches (floats ±0.001)
  - Edge cases: this comparison test lives under `tests/unit/game-data/` but is marked editor-only so it may use `AssetDatabase`

- **AC-3**: Single-knob override
  - Given: `DefaultTuning` with an override setting `dodge_distance = 2.0`
  - When: the config is read
  - Then: `dodge_distance == 2.0` and every other Dodge knob still equals its default
  - Edge cases: overriding an enemy field affects only that enemy instance

- **AC-4**: Fresh independent instances
  - Given: two consecutive `TestConfigs.Default()` calls
  - When: the first result's Levy HP is overridden to 99
  - Then: the second result's Levy HP still equals the default; `ReferenceEquals` between the two Levy instances is false
  - Edge cases: run the suite in randomized order twice — identical results

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: `tests/unit/game-data/game_data_test_configs_test.cs` — must exist and pass

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001, Story 002, Story 003 must be DONE
- Unlocks: Story 005, Story 006 (validator tests build on the factory); every downstream epic's Logic tests

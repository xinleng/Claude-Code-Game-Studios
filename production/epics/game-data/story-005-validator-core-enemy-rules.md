# Story 005: GameDataValidator core + enemy-definition clamp rules

> **Epic**: GameData Configuration & Validation
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Logic
> **Manifest Version**: 2026-10-10

## Context

**GDD**: `design/gdd/enemy-definition.md` (Formula 2 runtime + input lower-bound guards; Formula 3 runtime guard; Formula 5 burst guard; Tuning Knobs `stagger_resist_multiplier`, `phase_2_threshold_ratio`)
**Requirement**: `TR-ED-013`, `TR-TP-002`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0006: Data-Driven Configuration via ScriptableObjects (Decision §5); ADR-0012 (boot step (a))
**ADR Decision Summary**: A `GameDataValidator` pass runs at bootstrap before `runStartTime` is recorded, applies every GDD clamp rule, logs `[DESIGNER-ERROR]`-prefixed messages, and fails loudly in editor (assert) while clamping-and-continuing in builds. Key interface: `GameDataValidator.ValidateAll(gameData) : ValidationReport`, also invoked by an EditMode test. ADR-0012: validation is boot step (a), before system construction.

**Engine**: Unity 6.3 LTS | **Risk**: LOW
**Engine Notes**: Use Unity Test Framework `LogAssert.Expect` (or the shared channel's test sink) to assert designer-error log lines; an unexpected `LogType.Error`/`Assert` fails the test by default, so the editor-assert mode must be switchable for tests.

**Control Manifest Rules (this layer)**:
- Required: Run `GameDataValidator` at Bootstrap — every GDD clamp rule, `[DESIGNER-ERROR]` logging, assert in editor / clamp-and-continue in builds — ADR-0006
- Required: Boot order: GameData validation → construct + inject all systems → ObjectPool pre-warm → RSM `Selection` — ADR-0012
- Forbidden: Never hardcode a gameplay/tuning value (boundary-value tests excepted) — ADR-0006
- Guardrail: Bootstrap validation is the only load-time pass — ADR-0006

---

## Acceptance Criteria

*From GDD `design/gdd/enemy-definition.md` Formulas 2, 3, 5 and ADR-0006 §5, scoped to this story:*

- [ ] `GameDataValidator.ValidateAll(gameData)` returns a `ValidationReport` listing every rule violation (rule id, field, original value, applied value) and logs each through the shared `[DESIGNER-ERROR]` channel
- [ ] Mode: editor/fail-loud mode raises an assert after reporting; build/clamp-and-continue mode applies clamps and returns normally; tests select the mode explicitly
- [ ] Formula 2 runtime guard: `stagger_resist_multiplier` is clamped to [0.1, 0.8]; a designer error is logged whenever the authored value is outside [0.2, 0.8]
- [ ] Formula 3 runtime guard: `phase_2_threshold_ratio` must lie in (0.0, 1.0) exclusive; a designer error is logged whenever the value is outside [0.3, 0.7]
- [ ] Formula 2 / Formula 5 input lower-bound guards: a reusable pure guard clamps a received `finisher_damage` or `burst_damage` below 1.0 up to 1.0 and logs a designer error (receipt-time; runtime call sites belong to the Enemy AI epic)
- [ ] Validation runs on the default config with zero designer errors (`ValidateAll(TestConfigs.Default())` is clean)
- [ ] Every rule above has a red (violating) and green (boundary-valid) test

---

## Implementation Notes

*Derived from ADR-0006 Decision §5 + Key Interfaces, ADR-0012 Decision 2(a):*

- `GameDataValidator` is a plain C# class (no MonoBehaviour), constructed with its log sink (the shared `[DESIGNER-ERROR]` channel from bootstrap-combat-frame) and a mode flag — DI, no statics.
- Rules are small pure functions (`(value, report) → appliedValue`) so the same function serves load-time validation and receipt-time guards (TR-ED-013: "load-time/receipt-time validation").
- Clamp-and-continue writes the clamped value into the *runtime view* handed to systems; it must not dirty the authored `.asset` on disk (no `EditorUtility.SetDirty` from the validator).
- `stagger_resist_multiplier`: two thresholds by design — clamp band [0.1, 0.8] (authoritative, Tuning Knobs single source of truth) and error band outside [0.2, 0.8]. So 0.15 is kept but reported; 0.05 → 0.1 + error; 0.9 → 0.8 + error.
- `phase_2_threshold_ratio`: the GDD mandates validation in (0,1) and an error outside [0.3, 0.7] but does not name a clamp target for invalid 0/1 values. **Derived (confirm at `/story-readiness` with game-designer):** clamp an out-of-(0,1) value to the nearest safe-range bound (0.3 or 0.7); values inside (0,1) but outside [0.3, 0.7] are reported, not clamped.
- Bootstrap's call into `ValidateAll` is wired in the bootstrap-combat-frame epic (ADR-0012 boot step (a)); this story delivers the API and rules.

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 006: cross-system rules (damage-reduction/regen caps, dodge Formula 2, A6 floor, `xp_scalar > 0`, spawn ring vs arena bounds, GeneralData rules)
- Story 008 (Blocked): exact-binary-fraction rule on upgrade damage-reduction deltas
- bootstrap-combat-frame epic: the `[DESIGNER-ERROR]` channel itself and the Bootstrap call site
- Enemy AI epic: calling the receipt-time damage guards when a Finisher/burst resolves

---

## QA Test Cases

*Written by qa-lead at story creation (lean mode). The developer implements against these — do not invent new test cases during implementation.*

- **AC-1**: Report + channel logging
  - Given: `TestConfigs.Default()` with `stagger_resist_multiplier` overridden to 0.9, clamp-and-continue mode, a recording log sink
  - When: `ValidateAll` runs
  - Then: report contains exactly one entry (rule = stagger_resist range, original 0.9, applied 0.8); sink received one line starting with `[DESIGNER-ERROR]`
  - Edge cases: two violations in one config → two entries, two log lines, order stable

- **AC-2**: Mode behaviour
  - Given: the same violating config
  - When: run in fail-loud mode
  - Then: an assertion failure is raised after the report is built (test expects it via `LogAssert`/`Assert.Throws` per chosen mechanism); in clamp-and-continue mode no exception and the runtime view holds 0.8
  - Edge cases: a clean config raises nothing in either mode

- **AC-3**: Formula 2 `stagger_resist_multiplier` — red/green
  - Given: default config with the knob overridden
  - When: `ValidateAll` runs (clamp mode)
  - Then: 0.5 → 0.5, no error; 0.2 → 0.2, no error (boundary); 0.8 → 0.8, no error (boundary); 0.15 → 0.15 kept + 1 error; 0.1 → 0.1 kept + 1 error; 0.05 → 0.1 + 1 error; 0.9 → 0.8 + 1 error
  - Edge cases: negative value (−1.0) → 0.1 + 1 error

- **AC-4**: Formula 3 `phase_2_threshold_ratio` — red/green
  - Given: default config with the ratio overridden
  - When: `ValidateAll` runs (clamp mode)
  - Then: 0.5 → no error; 0.3 and 0.7 → no error (boundaries); 0.25 → kept + 1 error; 0.75 → kept + 1 error; 0.0 → error + clamped to 0.3 (derived rule); 1.0 → error + clamped to 0.7 (derived rule)
  - Edge cases: derived clamp targets are asserted only after game-designer confirmation recorded on this story

- **AC-5**: Finisher/burst input lower-bound guards — red/green
  - Given: the pure guard function
  - When: called with 25.0, 1.0, 0.99, 0.0, −5.0
  - Then: 25.0 → 25.0 no error; 1.0 → 1.0 no error (boundary); 0.99 → 1.0 + error; 0.0 → 1.0 + error; −5.0 → 1.0 + error
  - Edge cases: separate tests for the finisher and burst call labels so the logged message names the source

- **AC-6**: Defaults are clean
  - Given: `TestConfigs.Default()`
  - When: `ValidateAll` runs in fail-loud mode
  - Then: report is empty and no assertion is raised
  - Edge cases: none

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: `tests/unit/game-data/game_data_validator_enemy_rules_test.cs` — must exist and pass

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 004 (TestConfigs factory) must be DONE; bootstrap-combat-frame `[DESIGNER-ERROR]` channel story (TR-TP-002) must be DONE
- Unlocks: Story 006 (cross-system rules), Story 008 (catalogue rules, when unblocked)

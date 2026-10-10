# ADR-0006: Data-Driven Configuration via ScriptableObjects (No Addressables in MVP)

## Status
Accepted
*(Proposed and Accepted 2026-07-04, autonomous session — user directive to proceed; lean review mode.)*

## Date
2026-07-04

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS |
| **Domain** | Core / Asset Management |
| **Knowledge Risk** | LOW for ScriptableObjects (stable). MEDIUM noted for the rejected Addressables path (6.2+ throws on load failure) |
| **References Consulted** | `docs/engine-reference/unity/current-best-practices.md`, `deprecated-apis.md`, `breaking-changes.md`, `plugins/addressables.md` |
| **Post-Cutoff APIs Used** | None |
| **Verification Required** | Load-time validation pass runs before the first frame of a run and logs all clamp violations |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | None |
| **Enables** | ADR-0005 (pool sizes from TuningConfig); all Feature implementation (every system reads its knobs here) |
| **Blocks** | Any story containing a gameplay value — no hardcoded numbers from the first commit |
| **Ordering Note** | `GameData` assets are authored alongside the first system that consumes them |

## Context

### Problem Statement
The coding standard mandates "gameplay values data-driven, never hardcoded." The 17 GDDs define ~90 tuning knobs, 5 enemy stat profiles (7 with Warlord P1/P2), 3 general profiles, and a 24-entry upgrade catalogue — plus load-time validation rules (clamps + designer-error logging) attached to several of them. The project needs one authoritative shape for all of this, and a decision on whether Addressables is involved.

### Constraints
- `design/registry/entities.yaml` is the design-side source of truth — code-side values must mirror it
- MVP content: 1 map, 5 enemies, 3 generals, 24 upgrades — trivially small asset volume
- `Resources.Load` is deprecated practice (deprecated-apis.md); enemy-definition already mandates direct serialized SO refs for Warlord phase data

### Requirements
- Typed, inspector-editable configs per data family; balance changes without code changes
- Load-time validation: clamp + designer-error for out-of-range knobs (`stagger_resist_multiplier` [0.1,0.8], `phase_2_threshold_ratio` (0,1), `regen_rate_max`, exact-binary-fraction damage reductions, dodge-vs-slam constraint, A6 floor vs interval, `xp_scalar > 0`)
- Deterministic tests can construct configs in memory (no asset loading required in EditMode logic tests)

## Decision

**Four ScriptableObject families under `GameData`, all referenced directly (serialized fields) — no Addressables, no `Resources.Load` in MVP.**

1. **`EnemyDataSO`** — one asset per enemy type/tier (Levy, Pikeman, Archer, IronCaptain-5/10/15, Warlord-P1, Warlord-P2): stats, telegraph shape/duration, cooldowns, aggro range, xp_weight, spawn weight. Warlord P1+P2 both serialized on the prefab (enemy-definition contract).
2. **`GeneralData`** — one per general: weapon identity, Finisher geometry (shape enum, radius, nullable angle, direction mode), pre-computed FinisherDamage/Radius, nullable knockback fields. Nullable semantics enforced by accessor guards (never 0-sentinels).
3. **`UpgradeCatalogueSO`** — the 24 entries (id, name, one-line description, category, typed delta descriptor, draw-condition flag for C3) + draw-rule constants (drought threshold/multiplier, modifier cap).
4. **`TuningConfig`** — everything else: combat intervals, buffer sizes, pool sizes, wave escalation, XP curve, UI timings. Grouped by system in one asset (small project; one place to look), exposed to systems as read-only typed views.
5. **Validation**: a `GameDataValidator` pass runs at bootstrap (before `runStartTime` is recorded): applies every GDD clamp rule, logs `[DESIGNER-ERROR]`-prefixed messages, and fails loudly in editor (assert) while clamping-and-continuing in builds.
6. **Registry mirror**: `design/registry/entities.yaml` remains design truth. A lightweight editor test cross-checks SO values against the registry export so drift is caught in CI, not in playtest.
7. **Addressables: rejected for MVP.** All references are direct serialized fields; everything ships in the build. Re-evaluate post-MVP only if content volume (multiple maps/DLC) or memory ceilings demand streaming — that ADR must handle the 6.2+ throw-on-failure behaviour.

### Key Interfaces
- Systems receive their SO (or a typed view) via bootstrap injection — no static `Config.Instance`
- `GameDataValidator.ValidateAll(gameData) : ValidationReport` — also invoked by an EditMode test

## Alternatives Considered

### Alternative 1: Addressables for all game data
- **Pros**: Async loading, remote content, memory control at scale.
- **Cons**: Setup + catalog overhead for 30-odd tiny assets; new failure modes (6.2+ exceptions); zero MVP benefit (everything is always loaded).
- **Rejection Reason**: Complexity with no payoff at this content volume.

### Alternative 2: JSON/YAML files parsed at runtime
- **Pros**: Diff-friendly; could read entities.yaml directly.
- **Cons**: No inspector editing, no type safety until runtime, parsing/validation code to maintain; Unity-idiomatic tooling lost.
- **Rejection Reason**: SOs give typed authoring + inspector for free; the registry cross-check test recovers the single-source-of-truth benefit.

### Alternative 3: Constants in code
- **Rejection Reason**: Violates the coding standard outright; every balance change becomes a code change.

## Consequences

### Positive
- Balance iteration without recompiles; validation rules from five GDDs enforced in one place; tests construct configs freely
### Negative
- Two sources (yaml + SOs) that can drift → mitigated by the CI cross-check test; yaml stays authoritative

### Risks
- **Someone hardcodes a value under deadline** → forbidden pattern `hardcoded_gameplay_value` registered; review checklist
- **TuningConfig becomes a dumping ground** → grouped sections + one owner comment per knob (source GDD)

## GDD Requirements Addressed

| GDD System | Requirement | How This ADR Addresses It |
|------------|-------------|--------------------------|
| enemy-definition.md | SO phase data at spawn; load-time clamps + designer errors | EnemyDataSO + GameDataValidator |
| general-characters.md | Data profiles, nullable field semantics, pre-computed values | GeneralData accessor guards |
| upgrade-definition.md | 24-entry catalogue + draw-rule constants; exact binary fractions | UpgradeCatalogueSO + validator rule |
| damage-health.md / dodge-system.md / combat-core.md / progression-system.md | Caps and constraint checks (regen max, dodge>slam radius, A6 floor, xp_scalar>0) | GameDataValidator rules |
| coding-standards | Gameplay values external, never hardcoded | The module's existence |

## Performance Implications
- **CPU**: One validation pass at bootstrap; zero runtime cost thereafter
- **Memory**: All configs resident (~KBs)
- **Load Time**: Negligible
- **Network**: N/A

## Migration Plan
Greenfield. Post-MVP streaming (if ever) = a superseding Addressables ADR; the injection seam means consumers don't change.

## Validation Criteria
- EditMode test suite: every GDD clamp rule has a red/green validator test (AC-DH-10, enemy-definition guards, dodge Formula 2 constraint)
- CI cross-check: SO values == entities.yaml values

## Related Decisions
- ADR-0005 (pool sizes), ADR-0010 (validator tests), design/registry/entities.yaml

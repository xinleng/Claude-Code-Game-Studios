# ADR-0012: Scene Organisation and Boot Flow

## Status
Accepted
*(Proposed and Accepted 2026-10-10, autonomous session — authored by /gate-check follow-through
under the user's standing proceed directive, to close the last two Foundation-layer traceability
gaps (TR-GC0-002, TR-RS-006) found by /architecture-review 2026-10-10. Lean review mode.)*

## Date
2026-10-10

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS |
| **Domain** | Core / Scene Management |
| **Knowledge Risk** | LOW — scene management and bootstrap APIs stable since 2022 LTS |
| **References Consulted** | `docs/engine-reference/unity/VERSION.md`, `current-best-practices.md`, `deprecated-apis.md` |
| **Post-Cutoff APIs Used** | None |
| **Verification Required** | AC-07 (general-characters): no `SceneManager.LoadScene` call between selection and run; selection root `activeSelf == false` during the run |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | ADR-0002 (frame order — bootstrap builds the CombatFrame), ADR-0006 (GameData validation runs at boot) |
| **Enables** | Run Session Manager implementation; retry flow stories |
| **Blocks** | RSM stories; anything reading `runStartTime` or arena bounds |
| **Ordering Note** | The bootstrap scene is the composition root — first code after the pool |

## Context

### Problem Statement
TR-GC0-002 (the 20-minute run timer drives the win condition and every scheduled event) and
TR-RS-006 (initialisation-order contract; no-loading-screen flow; arena-bounds ownership) had no
ADR. The GDDs already constrain the answer: general-characters AC-07 asserts the run starts in
the same scene with no `SceneManager.LoadScene`; run-session-manager.md Rule 2 demands
`runStartTime` is recorded on the first frame before any game logic, with Input/Enemy/Spawner
inert until it is set; dodge-system and wave-spawner both consume arena bounds nobody owned.

### Constraints
- MVP content is one arena; menu → run must be seamless (no loading screen)
- Deterministic init order (TR-RS-006); pooling pre-warm happens before any run starts
- `run_duration_s` is data-driven (default 1200s) — never hardcoded

## Decision

**One gameplay scene (`MainArena.unity`) for the whole MVP. A single `Bootstrap` object is the
composition root. Menu/selection/end screens are UI Toolkit screen states inside that scene,
driven by RSM — there are no scene transitions after application start.**

1. **Scene set**: exactly one scene in the build for MVP. The former "MainMenu" is RSM's
   `Selection` flow state rendering a UI screen (RunUI), not a scene. `SceneManager.LoadScene`
   is a forbidden pattern project-wide for MVP (AC-07 enforces it for selection → run).
2. **Boot sequence** (synchronous, one-time, in `Bootstrap.Awake/Start`):
   (a) construct `GameData` views and run `GameDataValidator.ValidateAll` (ADR-0006);
   (b) construct all systems with constructor injection and wire interfaces (ADR-0002's
   DI wiring, including `UiInputModuleBinder.Bind` per ADR-0004); (c) `ObjectPool` pre-warm
   (ADR-0005); (d) hand control to RSM in `Selection` state. No gameplay object self-initialises.
3. **Run start contract (TR-RS-006)**: on selection confirm, RSM — in this order, same frame,
   before any game logic ticks — records the run origin on the unified run clock (TR-RS-005),
   calls `SetRunStartTime` on InputManager/EnemySystemManager/WaveSpawner, invokes every
   `IRunResettable.ResetForNewRun()`, then `SetContext(Gameplay)` and unfreezes the spawner.
   All three consumers stay inert until that call (their GDD contracts).
4. **Run clock ownership (TR-GC0-002)**: RSM owns the `run_duration_s` countdown (TuningConfig,
   default 1200s) and derives every scheduled event trigger (wave steps 120s, elites
   300/600/900s ±5s, boss 1080s, win at 0) from the single run clock it exposes. No other
   system keeps its own run timer.
5. **Arena bounds ownership (TR-RS-006)**: the arena footprint (a rectangle, per the art bible's
   square-arena rule) lives in `TuningConfig` as `arena_bounds`; `GameData` exposes a read-only
   `ArenaBounds` value consumed by WaveSpawner (spawn rejection) and DodgeSystem (dash clamp).
   Validation: spawn ring radius must fit inside bounds (GameDataValidator rule).
6. **Retry**: RSM re-runs step 3 from a Resolved state only (guarded, TR-RS-004). Application
   quit is the only exit; there is no persistent state to flush (no save/load in MVP).

## Alternatives Considered

### Alternative 1: Separate Menu scene + additive arena scene
- **Pros**: conventional; isolates menu assets.
- **Cons**: introduces load boundaries the GDDs forbid (AC-07, "no loading screen"); async
  load adds failure modes for zero MVP benefit (one arena, tiny asset volume).
- **Rejection Reason**: contradicts an approved GDD acceptance criterion.

### Alternative 2: Scene-per-flow-state (menu / run / results)
- **Rejection Reason**: same as Alternative 1, worse — results screens need the final
  battlefield visible behind them (run-ui/vfx-audio defeat treatment).

## Consequences

### Positive
- No load boundaries → the ink-wash defeat/victory treatments can keep the battlefield visible;
  retry is instant; AC-07 passes structurally
### Negative
- One scene means scene-file merge conflicts if a team ever grows (acceptable solo; revisit
  with additive scenes post-MVP if maps multiply)

### Risks
- **Bootstrap God-object creep** → Bootstrap only constructs and wires; it owns no gameplay
  state and ticks nothing (CombatFrame does)
- **Hidden self-initialisation** → forbidden pattern: no gameplay `Awake/Start` may touch
  another system; review checklist item

## GDD Requirements Addressed

| GDD System | Requirement | How This ADR Addresses It |
|------------|-------------|--------------------------|
| game-concept.md | TR-GC0-002: 20-min timer drives win + scheduled events | Decision 4 — single RSM-owned run clock |
| run-session-manager.md | TR-RS-006: init order, inert-until-init, no-loading-screen flow | Decisions 2–3 |
| general-characters.md | AC-07: same-scene run start, selection root deactivated | Decision 1 |
| dodge-system.md / wave-spawner.md | Arena-bounds consumer contracts | Decision 5 |

## Performance Implications
- **Load Time**: one synchronous boot (validation + pool pre-warm) before the menu; measured in ADR-0005
- **CPU / Memory / Network**: none beyond boot

## Migration Plan
Greenfield. Post-MVP multi-map = additive arena scenes behind the same RSM flow (superseding ADR).

## Validation Criteria
- AC-07 (general-characters) passes; AC-RS-01/06/07 pass via `RunFrame` pump
- Static check: zero `SceneManager.LoadScene` references outside Bootstrap (and none at all in MVP)

## Related Decisions
- ADR-0002 (wiring), ADR-0005 (pre-warm), ADR-0006 (validation, TuningConfig), ADR-0010 (clock)

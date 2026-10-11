# ADR-0004: Single Input Authority and the Parry Input Buffer

## Status
Accepted
*(Proposed and Accepted 2026-07-04, autonomous session — user directive to proceed; lean review mode.)*
*(Amended 2026-10-11, input-manager Story 004, autonomous Sprint 1 batch — ratify/adjust: the `ctx.time` → RunTime conversion reaches InputManager through a new read-only `IRunTimeConverter { float ToRunTime(double) }` seam in `Dynasty.Core`, which `IRunClockControl` now extends. InputManager receives the converter, never the RSM-only control interface — resolves QA gap G-2. Bootstrap passes the same `UnityGameClock` instance for both.)*

*(Amended 2026-10-10 by /architecture-review: (a) press timestamps move to the unified run clock on the Input System event-time basis — resolves blocking clock-domain conflict C1; (b) single `PlayerControls` instance shared with the UI input module via `UiInputModuleBinder` — resolves blocking finding N2; (c) authority invariant becomes an assembly-scoped compile-time gate; (d) UI action map widened to a superset. Findings from the independent engine-specialist audit.)*

## Date
2026-07-04

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS |
| **Domain** | Input |
| **Knowledge Risk** | HIGH — legacy `Input` class deprecated in Unity 6; Input System package is the only supported path |
| **References Consulted** | `docs/engine-reference/unity/modules/input.md`, `breaking-changes.md`, `deprecated-apis.md` |
| **Post-Cutoff APIs Used** | Input System package patterns as verified in `modules/input.md` (Input Actions asset, generated C# wrapper, action maps, `performed` callbacks) — package API is current in 6.3 |
| **Verification Required** | Confirm `performed` callback timing vs frame boundaries in a device test (timestamping must not miss the frame edge); gamepad scheme is best-effort untested (input-manager OQ-03) |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | ADR-0002 (the `[-200]` anchor), ADR-0003 (no UnityEvent) |
| **Enables** | Parry System implementation; Dodge System implementation |
| **Blocks** | Input Manager stories; anything reading player input |
| **Ordering Note** | `Input.*` legacy calls are forbidden from the first line of code |

## Context

### Problem Statement
The game's skill ceiling rests on the promise that a well-timed parry press always lands (input-manager.md). That requires: one authoritative capture point, timestamped buffered presses, and a consume-once contract — and it must be built on the Input System package because the legacy `Input` class is deprecated in Unity 6.

### Constraints
- KB/M primary; gamepad best-effort (technical-preferences)
- Buffer logic must run before Parry evaluation each frame (ADR-0002)
- Timestamps run-local + unscaled (survives pause; no long-session float drift)

### Requirements
- `Parry.performed` → timestamped `ParryPress` into a fixed ring buffer (capacity 4, 120ms life)
- `TryConsumeParryPress(notBefore)` / `HasLiveParryPress()` consumer-pull API; one press, one consumption
- Two action maps (Gameplay/UI), exactly one active; context switching owned by RSM
- No other class may touch devices or `InputAction`s

## Decision

**The Input System package is the sole input source, wrapped entirely inside `InputManager`. One Input Actions asset ("PlayerControls") with Gameplay (Move/Parry/Dodge) and UI (Navigate/Confirm/Cancel) maps and a generated C# wrapper class. Every gameplay/UI consumer reads through the `IInputAuthority` interface (architecture.md) — never devices.**

1. `InputManager` subscribes to `performed` callbacks; presses are stamped from the input event's own clock (`ctx.time` — a `realtimeSinceStartup`-basis double, sub-frame precise) and converted to the **unified run clock** (`IGameClock.RunTime`, pause-compensated run-local; ADR-0010 / TR-RS-005). `runStartTime` for this conversion is captured on the same basis. Stored in a preallocated ring buffer (`parry_buffer_capacity` = 4). **All parry window comparisons happen in this one clock domain.** Mixing `Time.time`-derived and `Time.unscaledTime`-derived run-local values is forbidden — they diverge by the total paused duration, which would make every parry press appear seconds in the future after the first level-up pause (blocking conflict C1, /architecture-review 2026-10-10).
2. Liveness/pruning per input-manager Formula 1 (`parry_input_buffer_ms` = 120, front-extension only); pruning runs at the start of `InputManager.Tick` (`[-200]`).
3. `TryConsumeParryPress(notBefore, out pressTime)` returns the most recent live press ≥ `notBefore` and marks it consumed — the `notBefore` guard prevents one press serving two attack windows.
4. Context switching: `SetContext(Gameplay|Menu|Paused)` enables exactly one action map; the parry buffer freezes (no prune/add) outside Gameplay.
5. Directional recording: `CurrentMoveDirection` every frame; `LastSteerDirection {dir, steer_time}` updated only when magnitude > `directional_deadzone` (0.2); parry presses never update it. Staleness windows are applied by consumers (Combo's 300ms), not here.
6. **Enforcement (amended)**: the invariant is now compile-time, assembly-scoped: only the `Dynasty.Input` asmdef references `Unity.InputSystem`. Any use of InputSystem types in gameplay/UI/presentation assemblies fails to compile. The assembly contains exactly two files that touch InputSystem types: `InputManager.cs` and `UiInputModuleBinder.cs` (rule 7). A widened advisory grep remains as a second check: `\bInput\.(Get|mouse|anyKey|touch)|Pointer\.current|Touchscreen\.current|Joystick\.current|Pen\.current|\bInputSystem\.` (the old `UnityEngine.Input\b` pattern missed unqualified `Input.GetKeyDown` after `using UnityEngine;`).
7. **Single action-asset instance**: `InputManager` creates the one `PlayerControls` instance. The UI Toolkit input module (`InputSystemUIInputModule`) is bound to *that same instance's* actions by `UiInputModuleBinder` (`InputActionReference.Create` on the shared instance) — never its own clone. The project-wide default actions asset is removed/unassigned. Without this, the module runs a second cloned action set: `SetContext` cannot actually disable UI navigation during Gameplay, and binding overrides never reach the module (blocking finding N2).
8. **UI action map is a superset**: `Navigate`, `Submit`, `Confirm`→Submit mapping, `Cancel`, `Point`, `Click`, `ScrollWheel` — the module needs the pointer actions to serve mouse on the selection/pick screens (KB/M is the primary scheme). UI navigation is consumed through the module only (ADR-0009 rule 3); `IInputAuthority` is not a navigation route.

### Key Interfaces
`IInputAuthority` as specified in architecture.md (TryConsumeParryPress / HasLiveParryPress / CurrentMoveDirection / LastSteerDirection / SetContext / SetRunStartTime / GetActiveBindingDisplay).

## Alternatives Considered

### Alternative 1: Each system polls devices directly (`Keyboard.current...`)
- **Pros**: No wrapper.
- **Cons**: Parry fairness re-implemented per consumer; no buffering; violates input-manager Core Rule 1; untestable (device access everywhere).
- **Rejection Reason**: Hard GDD prohibition; destroys the fairness guarantee.

### Alternative 2: `PlayerInput` component with SendMessage/UnityEvents
- **Pros**: Inspector-visible wiring, quick setup.
- **Cons**: SendMessage is string-based reflection; UnityEvents forbidden (ADR-0003); still no buffer.
- **Rejection Reason**: Conflicts with the event architecture and the buffering requirement.

### Alternative 3: Legacy Input Manager (`Input.GetKeyDown`)
- **Rejection Reason**: Deprecated in Unity 6 (breaking-changes.md). Not viable.

## Consequences

### Positive
- Input fairness solved once; deterministic tests inject presses via an `IInputAuthority` test double — no device needed
- Rebinding post-MVP is pure data (bindings live in the asset)

### Negative
- One indirection layer for every input read (trivial cost, large testability win)

### Risks
- **Callback timing vs frame edge** *(refined by engine-specialist review)*: with `InputSystem.settings.updateMode = Dynamic Update` (Unity 6.3 default), `performed` callbacks fire during the input system update immediately **before** `Update()` in the same frame — so a press is buffered before `InputManager.Tick` consumers run. Pin `updateMode` to Dynamic Update in project settings and assert it in the validation pass; the 120ms buffer additionally makes any one-frame slip harmless
- **`Active Input Handling = Both` loophole** *(engine-specialist finding)*: if Player Settings is left at "Both", legacy `Input.*` calls silently work and bypass the authority invariant. Pin Active Input Handling to "Input System Package (New)" and widen the review grep to include `UnityEngine.Input\.`
- **Gamepad untested for MVP** → accepted (input-manager OQ-03); KB/M is the supported path
- **Package default drift** *(audit 2026-10-10)*: "Dynamic Update is the default" is a *package* default, not an engine one — pin `com.unity.inputsystem` to an exact version in `Packages/manifest.json` and assert `updateMode` at bootstrap
- **Held key across `SetContext` re-enable** *(audit 2026-10-10)*: initial-state-check behaviour differs by action type when a map is re-enabled while a control is held — verify Parry does not fire spuriously on context switch back to Gameplay (add an AC to the Input Manager story)

## GDD Requirements Addressed

| GDD System | Requirement | How This ADR Addresses It |
|------------|-------------|--------------------------|
| input-manager.md | Core Rules 1–10 (authority, maps, buffer, timestamps, order) | Adopted in full as stated |
| parry-system.md | Buffered press consumption with `notBefore` reuse guard | `TryConsumeParryPress` contract |
| general-characters.md | DirectionalInputBuffer raw recording half | `LastSteerDirection` recording; consumer-applied staleness |
| dodge-system.md | Dodge button + dash direction sources | Gameplay map `Dodge` action + direction fallback chain inputs |

## Performance Implications
- **CPU**: O(4) buffer scan per query; negligible
- **Memory**: Fixed ring buffer, zero runtime allocation
- **Load Time / Network**: N/A

## Migration Plan
Greenfield. Post-MVP rebinding UI = `PerformInteractiveRebinding` + JSON binding overrides (verified pattern in `modules/input.md`) — no architectural change.

## Validation Criteria
- AC-IM-01…13 pass in EditMode with an injected clock and synthetic presses
- Grep gate: raw device API matches only in `InputManager.cs`

## Related Decisions
- ADR-0002 (tick anchor), ADR-0010 (test doubles for input)

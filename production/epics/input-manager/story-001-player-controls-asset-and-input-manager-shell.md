# Story 001: PlayerControls asset, single instance & SetContext map switching

> **Epic**: Input Manager
> **Status**: Complete
> **Layer**: Foundation
> **Type**: Logic
> **Manifest Version**: 2026-10-10

## Context

**GDD**: `design/gdd/input-manager.md`
**Requirement**: `TR-IM-002` (primary), `TR-IM-001` (single-instance portion)
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0004: Single Input Authority and the Parry Input Buffer
**ADR Decision Summary**: The Input System package is the sole input source, wrapped entirely inside `InputManager` in the `Dynasty.Input` assembly. One Input Actions asset (`PlayerControls`) with a generated C# wrapper, a Gameplay map and a superset UI map. `InputManager` creates the one and only `PlayerControls` instance, and `SetContext` enables exactly one map.

**Engine**: Unity 6.3 LTS + Input System package (exact version pinned in `Packages/manifest.json`) | **Risk**: HIGH
**Engine Notes** (ADR-0004 Engine Compatibility + Risks):
- The legacy `UnityEngine.Input` class is deprecated in Unity 6. The Input System package is the only supported path. The LLM's training data predates Unity 6, so check every Input System API against `docs/engine-reference/unity/modules/input.md`, which confirms the Input Actions asset, the "Generate C# Class" wrapper, action maps and `performed` callbacks as current.
- **Do not copy the `modules/input.md` `PlayerController` example as-is.** It calls `new PlayerControls()` in each component's `Awake`, which creates one instance per component and breaks ADR-0004 rule 7. It also subscribes closure lambdas. Here, only `InputManager` constructs the wrapper, and it does so once.
- Dynamic Update is a *package* default for `InputSystem.settings.updateMode`, not an engine default. Pin the package version and **assert `updateMode` == Dynamic Update when InputManager initialises**.
- Active Input Handling must be "Input System Package (New)". If it is left at "Both", legacy `Input.*` calls work silently and bypass the authority invariant. Bootstrap Story 001 pins this setting. This story's init assertion does not cover it; Story 009 verifies it.
- Gamepad support is best-effort and untested for MVP (input-manager OQ-03).

**Control Manifest Rules (Foundation layer)**:
- Required: InputManager creates the single `PlayerControls` instance. `UiInputModuleBinder` hands that same instance's action references to `InputSystemUIInputModule`. The project-wide default actions asset is removed/unassigned (ADR-0004).
- Required: the UI action map is a superset: Navigate, Submit, Cancel, Point, Click, ScrollWheel (ADR-0004).
- Required: pin Active Input Handling = "Input System Package (New)", `updateMode` = Dynamic Update, and an exact `com.unity.inputsystem` version. Assert them at bootstrap (ADR-0004).
- Required: wire dependencies by constructor/initializer injection at Bootstrap. Use interfaces only: no singletons and no `FindObjectOfType`/`FindFirstObjectByType` (ADR-0002, ADR-0012).
- Forbidden: never reference `Unity.InputSystem` outside the `Dynasty.Input` assembly (ADR-0004).
- Forbidden: never let a gameplay object self-initialise or touch another system in `Awake`/`Start`. Bootstrap constructs and wires everything (ADR-0012).
- Guardrail: zero `Instantiate`/`Destroy` during a run (ADR-0005). Input wiring happens once at boot.

---

## Acceptance Criteria

*From GDD `design/gdd/input-manager.md` (Core Rules 2–3, States and Transitions) and ADR-0004 rules 7–8, scoped to this story:*

- [ ] One Input Actions asset named `PlayerControls`, with "Generate C# Class" enabled, defines a **Gameplay** map (`Move` Value/Vector2, `Parry` Button, `Dodge` Button) and a **UI** map that is the ADR-0004 superset (`Navigate`, `Submit` [carries the GDD's `Confirm` bindings — ADR-0004 rule 8 "Confirm→Submit mapping"], `Cancel`, `Point`, `Click`, `ScrollWheel`)
- [ ] Two control schemes exist: **Keyboard&Mouse** (primary) and **Gamepad** (best-effort). The provisional default bindings from GDD Rule 3 are present: Move = WASD / left stick; Parry = Spacebar / right trigger; Dodge = Left Shift / a face button; Submit(Confirm) = Enter or Space / South button; Cancel = Esc / East button
- [ ] `InputManager` constructs **exactly one** `PlayerControls` instance during injection-time initialisation (not in `Awake`). No other class constructs one. The instance is reachable only inside `Dynasty.Input`, for `UiInputModuleBinder` (Story 008)
- [ ] `IInputAuthority`, `InputContext` (Gameplay | Menu | Paused), `SteerSample` and `GameAction` are declared exactly as specified in `architecture.md` § API Boundaries → InputManager. No Input System type appears anywhere in the public `IInputAuthority` surface
- [ ] `SetContext(Gameplay)` enables the Gameplay map and disables the UI map. `SetContext(Menu)` and `SetContext(Paused)` enable the UI map and disable the Gameplay map. After any `SetContext` call, **exactly one** map is enabled
- [ ] On initialisation, InputManager asserts `InputSystem.settings.updateMode` is Dynamic Update (logged assertion in the Editor; see ADR-0004 Risks)

---

## Implementation Notes

*Derived from ADR-0004 Decision rules 1, 4, 6, 7, 8 and architecture.md § InputManager:*

- Create `PlayerControls.inputactions` inside the `Dynasty.Input` assembly folder and tick "Generate C# Class". The generated wrapper is the typed access path. Never look actions up by string at runtime.
- `InputManager` is the `[-200]` anchor MonoBehaviour. Story 007 adds the attribute and the `Tick()` pump. Bootstrap wires its dependencies through an initializer, `Initialize(IGameClock clock, <input tuning view>, …)`. ADR-0002 and ADR-0012 forbid self-initialisation in `Awake`/`Start`. Construct `PlayerControls` inside that initializer.
- Subscribe to action callbacks **once** at initialisation using method-group delegates cached in fields, and unsubscribe on teardown. Do not use per-press closure lambdas (ADR-0003 pattern; Core-layer forbidden list).
- `SetContext` performs the map enable/disable only. It does **not** decide *when* to switch, because RSM owns that (GDD States table; RSM GDD Rule 4).
- The public `IInputAuthority` surface uses only `UnityEngine.Vector2` and project types (`SteerSample`, `InputContext`, `GameAction`). Consumers can then compile without referencing `Unity.InputSystem`, which is the compile gate verified in Story 009.
- Initial map state before the first `SetContext` call is **not specified** by the GDD or ADR. Constraint: the Gameplay map must not capture parry presses before run start (AC-IM-10, Story 004). Whether Bootstrap/RSM put the UI map in force for the Selection screen (by calling `SetContext(Menu)`) is an RSM/Bootstrap call. Raise it at `/story-readiness` and do not invent a default.
- **Open item (flag, do not resolve here):** GDD Rule 2 binds `Dodge` and `architecture.md` says DodgeSystem consumes "Dodge events" from InputManager, but the `IInputAuthority` interface in `architecture.md` has no Dodge accessor. Bind the action in the asset (this story). The consumer API has to be specified before the Dodge epic, so raise it with lead-programmer.
- **Open item:** GDD Rule 3 says Dodge's gamepad binding is "face button" without naming one. Pick a provisional face button and record it as provisional, since the GDD marks all bindings "validate in prototype".

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 002 / 003: the parry ring buffer and the consume API
- Story 004: `Parry.performed` capture, run-clock stamping, `SetRunStartTime`
- Story 005: buffer freeze semantics on context change
- Story 006: `CurrentMoveDirection` / `LastSteerDirection`
- Story 007: `[DefaultExecutionOrder(-200)]` and `Tick()` ordering
- Story 008: `UiInputModuleBinder` (binding the UI module to this instance)
- Story 009: asmdef compile gate and advisory grep verification
- Story 011: active control scheme tracking and `GetActiveBindingDisplay`
- Bootstrap-combat-frame Story 001: creating the Unity project, installing and pinning the package, creating the `Dynasty.Input` asmdef, Player Settings

---

## QA Test Cases

*Written at story creation (lean review — QL-STORY-READY skipped). The developer implements against these. Do not invent new test cases during implementation.*

- **AC-1**: The PlayerControls asset has the required maps and actions
  - Given: the `PlayerControls` wrapper constructed by InputManager
  - When: the test enumerates the Gameplay and UI maps (inside a `Dynasty.Input` test seam that reports action names/types as strings, so the test assembly needs no InputSystem types)
  - Then: Gameplay = {Move (Value, Vector2), Parry (Button), Dodge (Button)}; UI ⊇ {Navigate, Submit, Cancel, Point, Click, ScrollWheel}
  - Edge cases: no extra Gameplay actions. `Confirm` exists as Submit's bindings, not as a separate action (ADR-0004 rule 8)

- **AC-2**: Control schemes and provisional bindings
  - Given: the asset
  - When: schemes and Parry/Move/Submit/Cancel bindings are inspected through the same seam
  - Then: schemes {Keyboard&Mouse, Gamepad} exist. The Keyboard&Mouse Parry binding path is the space key, and Move includes WASD composite bindings
  - Edge cases: Space is bound to both Parry (Gameplay) and Submit (UI). This is legal because only one map is ever enabled (AC-5). Story 010 covers the held-Space hazard

- **AC-3**: Exactly one PlayerControls instance
  - Given: Bootstrap-style initialisation of one InputManager
  - When: initialisation runs, then `SetContext` is called several times
  - Then: the instance reference InputManager exposes to `Dynasty.Input` is the same object before and after. A static scan of `Assets/` finds `new PlayerControls(` only in `InputManager.cs`
  - Edge cases: calling initialise twice must not create a second instance. Assert or log a designer error. Do not silently replace the instance

- **AC-4**: The public interface leaks no InputSystem types
  - Given: the compiled `IInputAuthority`
  - When: reflection enumerates every member's parameter and return types
  - Then: none belongs to the `UnityEngine.InputSystem` namespace
  - Edge cases: `out float pressTime` and `Vector2` are allowed

- **AC-5**: SetContext enables exactly one map
  - Given: an initialised InputManager
  - When: `SetContext(Gameplay)` → `SetContext(Menu)` → `SetContext(Paused)` → `SetContext(Gameplay)` is applied in sequence
  - Then: after each call, (GameplayEnabled, UiEnabled) is (true,false), (false,true), (false,true), (true,false)
  - Edge cases: calling `SetContext` with the current context is idempotent and keeps exactly one map enabled. `Menu`→`Paused` keeps UI enabled with no flicker to zero maps

- **AC-6**: updateMode assertion
  - Given: `InputSystem.settings.updateMode` set to something other than Dynamic Update in a test setup (restored in teardown)
  - When: InputManager initialises
  - Then: an assertion or error log naming `updateMode` fires (`LogAssert.Expect`)
  - Edge cases: with Dynamic Update, no assertion is logged

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: `tests/unit/input-manager/input_manager_context_test.cs` — must exist and pass (EditMode, deterministic)

**Status**: [x] `tests/unit/input-manager/input_manager_context_test.cs` (14 tests), EditMode 121/121

---

## Dependencies

- Depends on: bootstrap-combat-frame Story 001 (Unity 6.3 project, pinned `com.unity.inputsystem`, `Dynasty.Input` asmdef, Active Input Handling = New)
- Unlocks: Stories 002–011

---

## Completion Notes
**Completed**: 2026-10-11
**Criteria**: 6/6 passing.
**Deviations**: Advisory —
- **Wrapper generation:** the importer's built-in "Generate C# Class" cannot be used for an asset inside the local `file:../src` package (it resolves `Packages/com.dynasty.game/…`, which does not exist on disk, and would create a stray folder). The asset keeps importer codegen off; `src/Input/Editor/PlayerControlsCodegen.cs` (new Editor-only asmdef `Dynasty.Input.Editor`) regenerates `src/Input/PlayerControls.cs` with the package's own `InputActionCodeGenerator` (menu *Tools ▸ Dynasty ▸ Regenerate PlayerControls Wrapper*). An EditMode test fails if the wrapper drifts from the asset.
- `IInputAuthority`, `InputContext`, `SteerSample`, `GameAction` live in **`Dynasty.Core`** (`src/Core/Input/`) so consumers never reference `Dynasty.Input`/`Unity.InputSystem`. `GameAction` = Move, Parry, Dodge, Submit, Cancel.
- AC-3 "exactly one instance" is proven at runtime (live `InputActionAsset` count delta == 1, identity stable across `SetContext`) instead of a source-file scan (file I/O in unit tests — gap G-8; static gate is BCF-006/IM-009). Second `Initialize` throws.
- Initial map state: **no map enabled** until the first `SetContext` (not invented — RSM/Bootstrap decide the boot context; keeps capture inert, AC-IM-10). `SetContext` disables before enabling so the two maps are never both on (Space is bound in both).
- Edit-mode disposal destroys the wrapper asset with `DestroyImmediate` (generated `Dispose()` uses `Object.Destroy`).
- Provisional gamepad Dodge = `buttonWest` (GDD: unnamed face button). **Open:** `IInputAuthority` still has no Dodge accessor — raise with lead-programmer before the Dodge epic.
- Members owned by later stories return neutral values for now (buffer/consume 002–003, capture 004, move/steer 006, control card 011).
**Test Evidence**: Logic — `tests/unit/input-manager/input_manager_context_test.cs` (14 tests) via `InputManagerTestSeam` (test assemblies reference `Dynasty.Input` but not `Unity.InputSystem`). EditMode 121/121.
**Code Review**: Self-review during the autonomous Sprint 1 batch; consolidated independent review (incl. unity-specialist for this HIGH-risk story) at batch end. LP/QL gates skipped (lean).

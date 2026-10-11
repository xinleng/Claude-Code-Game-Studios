# Story 008: UiInputModuleBinder — UI module bound to the shared PlayerControls instance

> **Epic**: Input Manager
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Integration
> **Manifest Version**: 2026-10-10

## Context

**GDD**: `design/gdd/input-manager.md`
**Requirement**: `TR-IM-001` (single authority, with the binder as the named exemption), `TR-IM-002` (context switching governs UI too)
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0004: Single Input Authority and the Parry Input Buffer (rules 7–8, amended 2026-10-10 to resolve blocking finding N2), with ADR-0009 rule 3 and ADR-0012 boot order
**ADR Decision Summary**: The UI Toolkit input module (`InputSystemUIInputModule`) is bound to the **same** `PlayerControls` instance InputManager created, using `InputActionReference.Create` on that instance's UI-map actions. It is never bound to its own clone. The project-wide default actions asset is removed or unassigned. Without this binding the module runs a second, always-enabled action set, `SetContext` cannot disable UI navigation during Gameplay, and binding overrides never reach the module.

**Engine**: Unity 6.3 LTS + Input System package (pinned) + UI Toolkit | **Risk**: HIGH
**Engine Notes** (ADR-0004 rules 7–8; ADR-0009 Risks):
- `InputActionReference.Create(InputAction)` and the `InputSystemUIInputModule` action-reference properties (for Navigate/move, Submit, Cancel, Point, Click/leftClick, ScrollWheel) are Input System package APIs that `docs/engine-reference/unity/modules/input.md` does **not** document. **Verify the exact property names and signatures against the pinned `com.unity.inputsystem` version before coding.** Use the version's own docs and not memory, because the LLM knowledge predates Unity 6.
- ADR-0009 Risk (engine-specialist finding): `UIDocument` panels have their own event and navigation handling and can consume raw device input in parallel unless the panel's input source is the Input System UI module bound to the `PlayerControls` UI map. **Verify in 6.3 that the runtime panels actually route through the bound module**, for example by checking that keyboard navigation stops while only the Gameplay map is enabled (QA AC-3). If they do not, focus navigation double-handles input outside the authority invariant.
- The project-wide default actions asset (Project Settings → Input System Package) must be removed or unassigned (ADR-0004 rule 7). If it stays, the module can fall back to it.
- All UIDocuments share one `PanelSettings` asset, which gives one navigation scope (ADR-0009 rule 3; manifest).

**Control Manifest Rules (Foundation + Presentation layers)**:
- Required: InputManager creates the single `PlayerControls` instance. **`UiInputModuleBinder` hands that same instance's action references to `InputSystemUIInputModule`.** The project-wide default actions asset is removed/unassigned (ADR-0004).
- Required: Bootstrap is the composition root. Boot order is GameData validation → construct + inject all systems (**including `UiInputModuleBinder.Bind`**) → ObjectPool pre-warm → RSM `Selection` (ADR-0012).
- Required (Presentation): UI navigation arrives through `InputSystemUIInputModule` bound to InputManager's instance. Presenters handle `Navigation*Event`. **`IInputAuthority` is NOT a navigation route**. One shared `PanelSettings` (ADR-0009, ADR-0004).
- Forbidden: never reference `Unity.InputSystem` outside `Dynasty.Input` (`InputManager.cs` + `UiInputModuleBinder.cs` only) (ADR-0004).
- Forbidden: no `FindObjectOfType`/`FindFirstObjectByType` lookups for wiring. Bootstrap passes in the module and the InputManager (ADR-0002, ADR-0012).

---

## Acceptance Criteria

*From ADR-0004 rules 7–8, ADR-0009 rule 3, and the epic's Definition of Done, scoped to this story:*

- [ ] `UiInputModuleBinder` (in `Dynasty.Input`, file `UiInputModuleBinder.cs`) exposes a `Bind(...)` that Bootstrap calls during the construct-and-inject phase (ADR-0012). It assigns `InputActionReference`s created from **InputManager's single `PlayerControls` instance** to every UI-map action the module uses: Navigate, Submit, Cancel, Point, Click, ScrollWheel
- [ ] After `Bind`, each module action reference resolves to the **same `InputAction` object** owned by InputManager's instance, never a clone (ADR-0004 rule 7)
- [ ] The project-wide default actions asset is unassigned in Project Settings, and the module's own `actionsAsset` does not point to a separate copy (ADR-0004 rule 7)
- [ ] **UI navigation is disabled during Gameplay context** (epic DoD): with `SetContext(Gameplay)`, keyboard or gamepad Navigate/Submit/Cancel produce no UI Toolkit navigation events. With `SetContext(Menu)` or `SetContext(Paused)` they do
- [ ] `IInputAuthority` exposes no navigation API, and presenters receive navigation only through the module (ADR-0004 rule 8; ADR-0009 rule 3)

---

## Implementation Notes

*Derived from ADR-0004 Decision rules 6–8, ADR-0009 rule 3, ADR-0012 boot step (b):*

- InputManager exposes the `PlayerControls` instance (or its UI map) to `UiInputModuleBinder` through an `internal` member. Both live in `Dynasty.Input`, so no InputSystem type leaks into the public surface (Story 001 AC-4).
- `Bind(InputManager inputManager, InputSystemUIInputModule module)`, or the equivalent signature agreed with the Bootstrap story. For each UI action, call `InputActionReference.Create(controls.UI.<Action>)` and assign it to the matching module property. Do **not** call `module.actionsAsset = …` with a new asset instance, and do not instantiate the asset again.
- The binder adds nothing to map enable/disable. Story 001's `SetContext` enabling and disabling the UI map **is** what turns module navigation on and off, because the module now reads the same actions.
- The `Confirm` bindings live on the `Submit` action (ADR-0004 rule 8, Story 001).
- The binder is the **single named exemption** in TR-IM-001 ("bootstrap PanelSettings wiring"). Per the ADR-0004 amendment, that wiring is implemented here and invoked by Bootstrap.

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 001: the `PlayerControls` instance, UI superset map, `SetContext`
- Story 009: compile-gate and grep verification that only `InputManager.cs` + `UiInputModuleBinder.cs` touch InputSystem
- bootstrap-combat-frame epic: the Bootstrap composition root that calls `Bind`, plus scene setup of the EventSystem/module and the shared `PanelSettings`
- Run UI / Upgrade Selection UI epics: presenter focus handling (`Navigation*Event`, initial focus on `Show()`)

---

## QA Test Cases

*Written at story creation (lean review — QL-STORY-READY skipped). The developer implements against these. Do not invent new test cases during implementation.*

- **AC-1**: The module actions are the shared instance's actions
  - Given: a test scene or fixture with one InputManager (initialised) and one `InputSystemUIInputModule`
  - When: `UiInputModuleBinder.Bind(...)` runs
  - Then: for each of Navigate/move, Submit, Cancel, Point, Click/leftClick and ScrollWheel, `module.<ref>.action` is reference-equal (`Assert.AreSame`) to InputManager's `controls.UI.<Action>`
  - Edge cases: calling `Bind` twice leaves the references the same, with no duplicate instance. Calling `Bind` before InputManager is initialised fails loudly (assert or error log) and does not bind null

- **AC-2**: No default or cloned actions
  - Given: the project settings and the module after `Bind`
  - When: the project-wide actions setting and `module.actionsAsset` are inspected
  - Then: the project-wide actions asset is unassigned, and the module's actions are not a separate `PlayerControls`/`InputActionAsset` copy (compare the asset of `module.<ref>.action.actionMap` with InputManager's instance asset)
  - Edge cases: none

- **AC-3**: UI navigation is gated by SetContext (integration)
  - Given: the bound module, a UIDocument with two focusable elements (first focused), and InputManager context Gameplay
  - When: a keyboard Down-arrow / `S` Navigate input is simulated (Input System test fixture; verify its availability in the pinned package, otherwise use the manual check below)
  - Then: focus does **not** move. Then `SetContext(Menu)` and the same input **does** move focus to the second element
  - Edge cases: `SetContext(Paused)` behaves like Menu. Going back to Gameplay stops navigation again
  - **Manual fallback** (if the fixture cannot drive UI Toolkit in EditMode/PlayMode for the pinned version):
    - Setup: enter Play mode in `MainArena`, start a run (Gameplay context), and with the debugger force the upgrade pick screen visible without changing context
    - Verify: press arrow keys/WASD and Enter. Then let RSM switch to Menu normally and press the same keys
    - Pass condition: no focus movement or Submit while the context is Gameplay. Normal navigation once the context is Menu

- **AC-4**: No navigation route through IInputAuthority
  - Given: the compiled `IInputAuthority`
  - When: reflection lists its members
  - Then: none is a Navigate, Submit or Cancel accessor (members match architecture.md exactly)
  - Edge cases: none

---

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- Integration: `tests/integration/input-manager/input_manager_ui_binder_test.cs` — must exist and pass. AC-3 may instead be covered by a documented playtest at `production/qa/evidence/input-manager-ui-binder-evidence.md` if the Input System test fixture cannot drive UI Toolkit navigation on the pinned version, and the evidence must say why

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001 (single instance + UI superset map + `SetContext`); bootstrap-combat-frame Story 001 (project setup, package pin, project-wide actions unassigned)
- Unlocks: Story 009; Run UI / Upgrade Selection UI epics (keyboard navigation)

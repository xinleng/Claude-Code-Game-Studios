# Story 011: Active control scheme tracking & GetActiveBindingDisplay (control card query)

> **Epic**: Input Manager
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Logic
> **Manifest Version**: 2026-10-10

## Context

**GDD**: `design/gdd/input-manager.md`
**Requirement**: `TR-IM-002` (KB/M primary, gamepad best-effort with scheme auto-switch)
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0004: Single Input Authority and the Parry Input Buffer
**ADR Decision Summary**: All bindings live in the one `PlayerControls` asset, so rebinding is pure data later. `IInputAuthority` exposes `GetActiveBindingDisplay(GameAction action)` for the run-start control card (architecture.md § InputManager). KB/M is primary and gamepad is best-effort.

**Engine**: Unity 6.3 LTS + Input System package (pinned) | **Risk**: HIGH
**Engine Notes** (ADR-0004; `modules/input.md`):
- `modules/input.md` documents control schemes (`Keyboard&Mouse`, `Gamepad`) and device detection through `ctx.control.device is Keyboard/Gamepad`, and those patterns are verified current. Turning a binding into a **display string** (the Input System's binding-display-string API) and **device add/remove notifications** are **not** in the engine reference. Verify both on the pinned package version before coding, and do not rely on memory.
- Gamepad is best-effort and untested for MVP (OQ-03). Gamepad paths only need to not break, and KB/M is the asserted path.
- All of this code touches InputSystem types, so it lives in `InputManager.cs` (ADR-0004 rule 6). Keep the decision logic (which scheme is active, fallback on disconnect) in an engine-free helper so it is unit-testable.

**Control Manifest Rules (Foundation layer)**:
- Forbidden: never reference `Unity.InputSystem` outside `Dynasty.Input` (ADR-0004). `GetActiveBindingDisplay` returns a plain `string`, so no InputSystem types reach Run UI.
- Required (Presentation, consumer side): presentation never mutates gameplay state, and `IInputAuthority` is not a navigation route (ADR-0009). Run UI only *reads* the display string.
- Forbidden: never hardcode a gameplay/tuning value (ADR-0006). The display string comes from the asset binding and is never a literal like `"Space"` in code.

---

## Acceptance Criteria

*From GDD `design/gdd/input-manager.md` (Core Rule 3, UI Requirements, Edge Cases) and architecture.md `IInputAuthority`, scoped to this story:*

- [ ] `GetActiveBindingDisplay(GameAction.Parry)` returns the human-readable display string of Parry's binding **for the active control scheme**, read from the `PlayerControls` asset. With default bindings and Keyboard&Mouse active, that is the Spacebar binding's display string (GDD UI Requirements: "Run UI … reads the active binding from Input Manager for display")
- [ ] The active control scheme **auto-switches on device activity**: the last-active device sets the scheme. It is cosmetic only in MVP, so no on-screen prompt switching beyond what the display query returns (GDD Rule 3; edge case "both devices feed input in the same frame")
- [ ] If both devices feed input in the same frame (for example stick to move and key to parry), **both inputs are accepted**, because actions bind to all devices. Only the cosmetic scheme changes (GDD edge case)
- [ ] **Gamepad disconnects mid-run:** the active scheme falls back to Keyboard&Mouse, the parry buffer and recorded directional input are **untouched**, and a designer-info log fires. There is no pause and no prompt (GDD edge case)
- [ ] No runtime rebinding UI or API in MVP (GDD Rule 3; UI Requirements)

---

## Implementation Notes

*Derived from ADR-0004 (Decision, Key Interfaces, Migration Plan) and input-manager Core Rule 3:*

- `GameAction` is the project enum declared in Story 001 per architecture.md. This story needs at least `Parry`, which the control card uses. architecture.md does not list the enum's members. Include the Gameplay actions (`Move`, `Parry`, `Dodge`) and confirm the list at `/story-readiness`.
- Scheme tracking: when an action fires, InputManager reports the device kind (keyboard/mouse vs gamepad, using the `ctx.control.device is …` pattern from `modules/input.md`) to an engine-free `ActiveSchemeTracker`. The tracker holds the current scheme and decides fallback. `GetActiveBindingDisplay` uses the tracker's scheme to pick the binding, then asks the Input System for that binding's display string (API to be verified, see Engine Notes).
- Disconnect: subscribe once, at init, to the Input System's device-change notification (verify the API on the pinned version), using a cached method-group delegate. On gamepad removal, call `tracker.OnGamepadRemoved()`, which falls back to KB/M, and log at designer-info level. Use the shared logging channel. TR-TP-002 `[DESIGNER-ERROR]` is for errors, so confirm the info-level prefix with the bootstrap-combat-frame owner. **Never** clear the buffer or the directional state there.
- `GetActiveBindingDisplay` is a query for Run UI and should be called on demand (at run start for the control card), not every frame. It is allowed to allocate its string. If Run UI calls it more often, cache the string per (action, scheme).
- Rebinding (`PerformInteractiveRebinding`, JSON overrides) is explicitly post-MVP (ADR-0004 Migration Plan). Do not add it.

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Run UI epic: the control card element, its layout, auto-fade (`control_card_autofade_s`) and dismissal
- Story 001: the schemes and bindings in the asset
- Post-MVP: rebinding screen, binding persistence

---

## QA Test Cases

*Written at story creation (lean review — QL-STORY-READY skipped). The developer implements against these. Do not invent new test cases during implementation.*

- **AC-1**: Parry display string for KB/M
  - Given: InputManager initialised with the default `PlayerControls` asset, active scheme Keyboard&Mouse
  - When: `GetActiveBindingDisplay(GameAction.Parry)` is called
  - Then: it returns a non-empty string equal to the Input System display string of the Parry Keyboard&Mouse binding. The test reads the expected value through the same API from the asset seam, not from a literal
  - Edge cases: with a binding override applied to Parry in a test (simulating future rebinding data), the returned string reflects the override, which proves the value is data-driven

- **AC-2**: Scheme auto-switch (cosmetic)
  - Given: an `ActiveSchemeTracker` starting at Keyboard&Mouse
  - When: `ReportActivity(Gamepad)`, then `ReportActivity(KeyboardMouse)`
  - Then: the scheme is Gamepad, then Keyboard&Mouse. `GetActiveBindingDisplay(Parry)` follows the current scheme
  - Edge cases: a Gamepad scheme with no Parry binding found returns a non-empty fallback (the KB/M string) rather than empty or null. Confirm this fallback choice at `/story-readiness`, because the GDD does not specify it

- **AC-3**: Both devices in one frame
  - Given: Gameplay, run started
  - When: in one frame, gamepad stick `Move = (1,0)` and keyboard Space Parry both arrive (Input System test fixture, or both handler seams called)
  - Then: `CurrentMoveDirection == (1,0)` and one press is buffered. The cosmetic scheme equals whichever device was reported last
  - Edge cases: none

- **AC-4**: Gamepad disconnect fallback
  - Given: tracker scheme = Gamepad, buffer holding one live press, `LastSteerDirection = ((0,1), 3.000)`
  - When: `OnGamepadRemoved()` (through the tracker seam or a simulated device removal)
  - Then: the scheme is Keyboard&Mouse, the buffer is unchanged (same press, unconsumed), `LastSteerDirection` is unchanged, and one designer-info log entry is emitted (`LogAssert.Expect`)
  - Edge cases: removal while the scheme is already KB/M still logs, and the scheme stays KB/M

- **AC-5**: No rebinding surface
  - Given: the compiled `IInputAuthority` and the public members of InputManager
  - When: reflection lists them
  - Then: there is no rebind/override/save-binding method
  - Edge cases: none

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: `tests/unit/input-manager/input_manager_binding_display_test.cs` — must exist and pass (EditMode)

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001 (asset, schemes, `GameAction`); Story 006 (directional state, asserted untouched)
- Unlocks: Run UI epic (control card)

# Story 009: Single-authority gate — asmdef compile gate + advisory scan

> **Epic**: Input Manager
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Config/Data
> **Manifest Version**: 2026-10-10

## Context

**GDD**: `design/gdd/input-manager.md`
**Requirement**: `TR-IM-001`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0004: Single Input Authority and the Parry Input Buffer (rule 6, amended 2026-10-10)
**ADR Decision Summary**: The single-authority invariant is enforced at **compile time and scoped by assembly**: only the `Dynasty.Input` asmdef references `Unity.InputSystem`, so any use of InputSystem types in a gameplay, UI or presentation assembly fails to compile. Inside the assembly, exactly two files touch InputSystem types: `InputManager.cs` and `UiInputModuleBinder.cs`. A widened advisory grep is the second check.

**Engine**: Unity 6.3 LTS + Input System package (pinned) | **Risk**: HIGH
**Engine Notes** (ADR-0004 Risks):
- **The `Active Input Handling = Both` loophole:** if Player Settings is left at "Both", legacy `Input.*` calls compile and work silently, bypassing the asmdef gate, because the legacy `UnityEngine.Input` lives in UnityEngine and not in `Unity.InputSystem`. Active Input Handling must be "Input System Package (New)". This story verifies it, and bootstrap-combat-frame Story 001 sets it.
- The old grep pattern `UnityEngine.Input\b` missed an unqualified `Input.GetKeyDown` written after `using UnityEngine;`. Use the widened pattern exactly as the ADR gives it.
- Unity asmdef references are per-assembly. Verify on the pinned editor that a gameplay asmdef **not** listing `Unity.InputSystem` really fails to compile an `InputSystem` reference. Do not assume it.

**Control Manifest Rules (Foundation layer)**:
- Forbidden: **never reference `Unity.InputSystem` outside the `Dynasty.Input` assembly** (`InputManager.cs` + `UiInputModuleBinder.cs` only). Compile-time asmdef gate. Advisory grep: `\bInput\.(Get|mouse|anyKey|touch)|Pointer\.current|Touchscreen\.current|Joystick\.current|Pen\.current|\bInputSystem\.` (ADR-0004).
- Required: pin Active Input Handling = "Input System Package (New)", `updateMode` = Dynamic Update, and an exact `com.unity.inputsystem` version (ADR-0004).
- Forbidden API: `UnityEngine.Input.*` (legacy input) → Input System package, inside `Dynasty.Input` only (manifest Forbidden APIs).

---

## Acceptance Criteria

*From GDD `design/gdd/input-manager.md` (Core Rule 1, AC-IM-13), ADR-0004 rule 6 + Validation Criteria, and the epic's Definition of Done:*

- [ ] **AC-IM-13**: **GIVEN** no gameplay system reads `Keyboard/Mouse/Gamepad.current` or an `InputAction` directly (only Input Manager does), **THEN** the single-authority invariant holds. **Pass:** a code-review / static-scan assertion that raw device access appears only inside Input Manager (ADVISORY gate per the GDD)
- [ ] **Compile gate verified** (epic DoD): adding a deliberate `using UnityEngine.InputSystem;` plus a `Keyboard.current` reference to a file in a gameplay assembly **fails to build**. The probe is reverted after verification and never committed
- [ ] Only `Dynasty.Input.asmdef` lists `Unity.InputSystem` among its references, and no other runtime asmdef under `Assets/` does
- [ ] The advisory grep (pattern above) over `Assets/` returns matches only in `InputManager.cs` and `UiInputModuleBinder.cs` (see the generated-wrapper flag below)
- [ ] Player Settings → Active Input Handling = "Input System Package (New)". `Packages/manifest.json` pins `com.unity.inputsystem` to an exact version, not a range

---

## Implementation Notes

*Derived from ADR-0004 Decision rule 6, Risks, and Validation Criteria:*

- This is a verification story with no new runtime code. Run the four checks and record the results in the smoke-check document.
- **Compile probe:** in a scratch branch or local change, add the probe to a file in a gameplay assembly (for example the first combat assembly that exists, or a throwaway asmdef with the same reference set). Confirm the compiler error names the missing InputSystem namespace, capture the console text, and revert.
- **asmdef scan:** read every `*.asmdef` under `Assets/`. Only `Dynasty.Input` may list `Unity.InputSystem` (by name or GUID reference). Test-only assemblies are not gameplay/UI/presentation assemblies. If a test asmdef needs InputSystem for the Input System test fixture (Stories 008/010), record it as a **test-only exception** in the smoke doc and confirm with lead-programmer.
- **Generated-wrapper flag (raise at `/story-readiness`, do not resolve silently):** ADR-0004 rule 6 says "exactly two files" in the assembly touch InputSystem types, but the generated `PlayerControls.cs` wrapper (Story 001, from "Generate C# Class") necessarily references `UnityEngine.InputSystem`. Treat it as a generated-code exemption in the grep allow-list and note it in the smoke doc. Ask technical-director to confirm, and to update the ADR wording if needed.
- TR-IM-001's registry text names "bootstrap PanelSettings wiring" as the single exemption. Per the ADR-0004 amendment, that wiring is `UiInputModuleBinder` (Story 008), which lives inside `Dynasty.Input`, so the gate holds.
- Optional, recommended: turn the asmdef scan into an EditMode test (`tests/unit/input-manager/input_manager_authority_gate_test.cs`) that parses `*.asmdef` JSON and fails if any asmdef other than `Dynasty.Input` (and any recorded test-only exceptions) references `Unity.InputSystem`. The CI gate then catches regressions automatically. Under the Config/Data rules this is extra and not required evidence.

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- bootstrap-combat-frame Story 001: creating the asmdefs, setting Active Input Handling, pinning the package
- Story 001: `updateMode` assertion at init
- Story 008: the binder itself

---

## QA Test Cases

*Written at story creation (lean review — QL-STORY-READY skipped). Config/Data story: these are manual verification steps.*

- **AC-1**: AC-IM-13 static scan
  - Setup: a clean checkout at the story's commit
  - Verify: run the ADR-0004 advisory grep over `Assets/` (`rg -n '\bInput\.(Get|mouse|anyKey|touch)|Pointer\.current|Touchscreen\.current|Joystick\.current|Pen\.current|\bInputSystem\.' Assets/`), plus a scan for `Keyboard.current|Mouse.current|Gamepad.current`
  - Pass condition: every match is in `InputManager.cs`, `UiInputModuleBinder.cs`, or the flagged generated `PlayerControls.cs`. Zero matches elsewhere

- **AC-2**: Compile gate
  - Setup: an editor open on the project, plus one gameplay assembly that does not reference `Unity.InputSystem`
  - Verify: add `using UnityEngine.InputSystem;` and `var k = Keyboard.current;` to a file in that assembly, then let it compile
  - Pass condition: compilation fails with a missing namespace/type error for InputSystem. After reverting, compilation is clean again

- **AC-3**: asmdef references
  - Setup: the list of all `*.asmdef` under `Assets/`
  - Verify: each one's `references` array
  - Pass condition: `Unity.InputSystem` (or its GUID) appears only in `Dynasty.Input.asmdef`, apart from test-only exceptions recorded in the smoke doc

- **AC-4**: Player Settings and package pin
  - Setup: `ProjectSettings/ProjectSettings.asset` and `Packages/manifest.json`
  - Verify: Active Input Handling, and the `com.unity.inputsystem` entry
  - Pass condition: Active Input Handling is "Input System Package (New)" (not "Both", not legacy), and the package version is an exact version string

---

## Test Evidence

**Story Type**: Config/Data
**Required evidence**:
- Config/Data: smoke check pass — `production/qa/smoke-[date].md` with an "Input authority gate" section recording AC-1 to AC-4 results (grep output, compiler error text, asmdef list, settings values, and the generated-wrapper and any test-only exceptions)

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001, Story 008 (both InputSystem-touching files exist); bootstrap-combat-frame Story 001
- Unlocks: Epic DoD "compile gate verified"; ADR-0004 Validation Criteria

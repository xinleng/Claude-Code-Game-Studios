# Story 010: Held Parry across SetContext — no spurious press (device-level verification)

> **Epic**: Input Manager
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Integration
> **Manifest Version**: 2026-10-10

## Context

**GDD**: `design/gdd/input-manager.md`
**Requirement**: `TR-IM-003` (performed-edge only, no autofire), `TR-IM-002` (context switching)
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0004: Single Input Authority and the Parry Input Buffer (Risks "Held key across `SetContext` re-enable" and "Callback timing vs frame edge"; Engine Compatibility "Verification Required")
**ADR Decision Summary**: When an action map is re-enabled while a control is held, the initial-state check behaves differently by action type. ADR-0004 requires verifying that Parry does not fire spuriously when the context switches back to Gameplay, and requires this AC in the Input Manager stories (audit 2026-10-10; TD-PHASE-GATE). ADR-0004 also requires a device test confirming `performed` callback timing at the frame edge.

**Engine**: Unity 6.3 LTS + Input System package (pinned) | **Risk**: HIGH
**Engine Notes** (ADR-0004):
- **Initial-state check on map re-enable** differs between Value, Button and Pass-Through action types (ADR-0004 Risks). The engine reference (`modules/input.md`) does not document this behaviour, so it has to be **observed on the pinned package version**, not assumed. `Parry` is a Button and `Move` is a Value (Story 001).
- **Callback timing:** with Dynamic Update, `performed` fires during the input update immediately before `Update()` (ADR-0004 Risks). Confirm at device level that a press made in frame N is in the buffer when `InputManager.Tick` and the `[0]` consumers run in frame N.
- The Input System's test fixture (`InputTestFixture`, from the package's test-framework assembly) can simulate devices and held controls, but it is **not covered by the engine-reference docs**. Verify that it is available and how to use it on the pinned version. A test asmdef that references InputSystem for it is a test-only exception recorded in Story 009.
- Gamepad is best-effort (OQ-03). Keyboard is the required path for this story.

**Control Manifest Rules (Foundation layer)**:
- Required: parry presses go into the ring buffer, and one press is one consumption (ADR-0004).
- Required: pin `updateMode` = Dynamic Update and an exact package version (ADR-0004). This story's results are only valid for that pinned configuration.
- Forbidden: never reference `Unity.InputSystem` outside `Dynasty.Input` (test-only exception per Story 009).

---

## Acceptance Criteria

*From the epic's Definition of Done (TD-PHASE-GATE), ADR-0004 Risks + Verification Required, and GDD AC-IM-09:*

- [ ] **(New AC — TD-PHASE-GATE / ADR-0004 Risks)**: **GIVEN** the Parry control is held down while context is Menu, **WHEN** `SetContext(Gameplay)` re-enables the Gameplay map with the control still held, **THEN** no `ParryPress` is recorded on re-enable (buffer count unchanged across the switch and the following frames until release). A fresh press after release **is** recorded
- [ ] Same check for `Paused → Gameplay`
- [ ] Canonical repro: **Space** is bound to both UI `Submit` (Confirm) and Gameplay `Parry` (GDD Rule 3). The player presses Space to confirm the upgrade pick, RSM switches to Gameplay, and Space is still down. This produces **no** parry press
- [ ] **AC-IM-09 (device-level re-confirmation)**: holding the Parry key for 0.5 s in Gameplay records exactly **one** `ParryPress`
- [ ] **Frame-edge timing (ADR-0004 Verification Required)**: a Parry press delivered in frame N is in the buffer before `InputManager.Tick` returns in frame N, so a consumer at `[0]` in frame N can consume it

---

## Implementation Notes

*Derived from ADR-0004 Risks and Engine Compatibility:*

- This is primarily a **verification** story. Write the integration test with simulated keyboard input through the Input System test fixture on the pinned version. If the fixture cannot run in this project's test setup, fall back to the documented playtest (see Test Evidence).
- Fixture sequence for the new AC: `SetContext(Menu)` → press and hold `Space` → run input updates and `Tick` → `SetContext(Gameplay)` → run several input updates and `Tick` with Space still held → assert the buffer count is unchanged → release, then press again → assert count + 1.
- **If the check fails** (Parry fires on re-enable), ADR-0004 does **not** prescribe a remedy. Do not hand-roll a suppression hack, such as ignoring presses for N frames after a switch. Escalate to unity-specialist / technical-director with the observed behaviour. A fix needs a recorded decision as an ADR-0004 amendment, because it touches parry fairness.
- Record the pinned package version and the observed initial-state behaviour for `Parry` (Button) and `Move` (Value) in the test's doc comment or the evidence doc, so a later package upgrade can be re-checked. How `Move` behaves on re-enable is recorded for information only, because no GDD criterion governs it.

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 004: handler-seam AC-IM-09 and stamping
- Story 005: buffer freeze semantics
- Story 001: map switching and the `updateMode` assertion
- RSM epic: when context switches occur

---

## QA Test Cases

*Written at story creation (lean review — QL-STORY-READY skipped). The developer implements against these. Do not invent new test cases during implementation.*

- **AC-1**: Held Parry across Menu→Gameplay
  - Given: InputManager initialised (run started), a simulated keyboard, context Menu, buffer empty
  - When: Space is pressed and held, three input updates and `Tick` run, then `SetContext(Gameplay)`, then five more input updates and `Tick` with Space still held
  - Then: buffer count == 0 throughout
  - When (continued): Space is released, one update runs, then Space is pressed again
  - Then: buffer count == 1
  - Edge cases: the hold starts in Gameplay, then Menu, then back to Gameplay while still held. The single original press is counted once (recorded before Menu) and the re-enable adds nothing

- **AC-2**: Held Parry across Paused→Gameplay
  - Given/When/Then: as AC-1, with `SetContext(Paused)`
  - Edge cases: none

- **AC-3**: Space-as-Submit canonical repro
  - Given: context Menu (UI map enabled; Space = Submit)
  - When: Space goes down (a Submit performed fires on the UI map), `SetContext(Gameplay)` is called in the same frame in response, and Space stays held
  - Then: no parry press is recorded, either during the hold or on later frames until release
  - Edge cases: none

- **AC-4**: AC-IM-09 device level
  - Given: Gameplay, run started
  - When: Space is held for 0.5 s of simulated input updates (30 updates at 60 Hz), with `Tick` each update
  - Then: buffer press count == 1
  - Edge cases: rapid tap ×3 within 0.1 s records 3 presses (mash, capacity permitting)

- **AC-5**: Frame-edge timing
  - Given: Gameplay, a fake `[0]` consumer stub
  - When: Space is pressed in the input update for frame N, then `inputManager.Tick()` and the stub consumer run in frame N
  - Then: the stub's `TryConsumeParryPress(0)` returns `true` in frame N, not N+1
  - Edge cases: none
  - **Manual fallback** (playtest, if the fixture is unavailable):
    - Setup: a development build or Play mode on the pinned version with a debug overlay showing the buffer count and the frame index of each press
    - Verify: (1) hold Space on the upgrade pick screen, let it confirm, keep holding into Gameplay; (2) hold Space in Gameplay for 1 s; (3) tap Space and compare the press frame with the consume frame from a debug consumer
    - Pass condition: (1) no press appears on resume; (2) exactly one press per hold; (3) pressed and consumed in the same frame

---

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- Integration: `tests/integration/input-manager/input_manager_held_key_context_test.cs` — must exist and pass. **OR** a documented playtest at `production/qa/evidence/input-manager-held-key-context-evidence.md` (pinned package version, steps, observed results, sign-off) if the Input System test fixture is unavailable on the pinned version

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001, Story 004, Story 005, Story 007; bootstrap-combat-frame Story 001 (pinned package / `updateMode`)
- Unlocks: Epic DoD "held Parry across SetContext produces no spurious press"; ADR-0004 Verification Required items

# Story 007: Frame-order anchor — [-200] Tick, prune → capture → record

> **Epic**: Input Manager
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Logic
> **Manifest Version**: 2026-10-10

## Context

**GDD**: `design/gdd/input-manager.md`
**Requirement**: `TR-IM-005`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0002: Canonical Frame Execution Order and the CombatFrame Orchestrator (with ADR-0004 rule 2 and ADR-0010 rule 2)
**ADR Decision Summary**: Only three `[DefaultExecutionOrder]` anchors exist project-wide: `[-200] InputManager` (capture + prune the parry buffer), `[-100] EnemySystemManager`, and `[0] CombatFrame`, which calls `parry.Evaluate()` first. Pruning runs at the start of `InputManager.Tick`. Tests pump `inputManager.Tick()` directly in production order.

**Engine**: Unity 6.3 LTS | **Risk**: LOW for this story (ADR-0002: `[DefaultExecutionOrder]` and the MonoBehaviour lifecycle are stable pre-cutoff APIs). The epic is HIGH because of the Input System package.
**Engine Notes** (ADR-0002 + ADR-0004 Risks):
- With `InputSystem.settings.updateMode` = Dynamic Update (asserted in Story 001), `performed` callbacks fire during the input system update **immediately before** `Update()` in the same frame. A press is therefore already captured before `InputManager.Tick` runs at `[-200]`, and before every consumer. ADR-0004 lists confirming this timing at device level as **Verification Required** (Story 010).
- `[DefaultExecutionOrder]` orders `Update()` across MonoBehaviours. Parry is not a MonoBehaviour. It is a plain C# subsystem called by `CombatFrame.RunFrame(dt)` at `[0]`, so AC-IM-12 is satisfied by the anchor values plus the orchestrator call order (AC-IM-12 explicitly allows this).

**Control Manifest Rules (Foundation layer)**:
- Required: **frame order is fixed**: `[-200] InputManager → [-100] EnemySystemManager → [0] CombatFrame (Parry → Dodge → CombatCore → Combo → PlayerHealth → Progression) → [+100] Presentation` (ADR-0002).
- Required: wire by initializer injection at Bootstrap, with no lookups (ADR-0002, ADR-0012).
- Forbidden: no extra `[DefaultExecutionOrder]` anchors. Do not use Script Execution Order entries in ProjectSettings (ADR-0002 Alternative 1 rejected).
- Forbidden: never implement `Update()` in a *combat* system (ADR-0002). InputManager is the `[-200]` anchor and is allowed one `Update()` whose only body is `Tick()`.
- Guardrail: `Tick` is O(capacity) plus one `ReadValue` and allocates nothing.

---

## Acceptance Criteria

*From GDD `design/gdd/input-manager.md` (Core Rule 9) and ADR-0002, scoped to this story:*

- [ ] **AC-IM-12**: **GIVEN** the Input Manager and Parry System both run in a frame, **THEN** Input Manager's capture/prune executes before the Parry System's query. **Pass:** Input Manager's `[DefaultExecutionOrder]` value is strictly less than the Parry System's, or, because the CombatFrame orchestrator is used, it calls input capture before parry evaluation
- [ ] `InputManager` carries `[DefaultExecutionOrder(-200)]`, which is less than `EnemySystemManager` (`-100`) and `CombatFrame` (`0`). Its `Update()` does nothing but call `Tick()`
- [ ] `Tick()` runs in this fixed order: **(1) prune stale presses** (only when context == Gameplay, per Story 005), **(2) capture new input**, **(3) update `CurrentMoveDirection` / `LastSteerDirection`** (architecture.md § Frame update path; GDD Rule 9 "pruning runs at the very start … before new presses are recorded")
- [ ] `Tick()` is public and drivable from EditMode tests with an injected clock and no engine loop (ADR-0010 rule 2). The production `Update()` and the test pump run the same method

---

## Implementation Notes

*Derived from ADR-0002 Decision, ADR-0004 rule 2, architecture.md § Frame update path:*

- `[DefaultExecutionOrder(-200)] public sealed class InputManager : MonoBehaviour, IInputAuthority`. `Update() => Tick();`. No other lifecycle method holds logic, and Bootstrap initialises it (ADR-0012).
- **Prune then capture.** `performed` callbacks may already have written this frame's presses into the buffer before `Tick` (Engine Notes). That is compatible with the contract because a fresh press is never stale and so is never pruned. Two shapes are acceptable:
  (a) callbacks record directly, and `Tick` prunes and then records direction, or
  (b) callbacks stage stamped events into a small preallocated queue, and `Tick` prunes, drains the queue, then records direction, which matches the GDD wording "prune → capture" literally.
  Choose one at `/dev-story` and record the choice in the class doc comment. Either way, the observable contract is the same: **after `Tick` returns, the buffer holds every press performed up to this frame and no stale press**, before any consumer at `[-100]`/`[0]` runs.
- Freeze (Story 005): skip step (1) when context ≠ Gameplay.
- Do not call any consumer from `Tick`. Consumers pull (GDD Rule 6).
- AC-IM-12 cross-check: the CombatFrame orchestrator and its fixed sub-order come from the bootstrap-combat-frame epic. If CombatFrame does not exist yet when this story is implemented, the attribute-value assertion runs against a stub orchestrator declaring `[DefaultExecutionOrder(0)]`. Re-point the assertion to the real `CombatFrame` type once it lands.

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Stories 002, 004, 005, 006: the bodies of prune, capture, freeze and directional recording
- bootstrap-combat-frame epic: `CombatFrame.RunFrame(dt)`, the `[0]` anchor, `ParrySystem.Evaluate()` placement
- Story 010: device-level verification of `performed` timing relative to the frame edge

---

## QA Test Cases

*Written at story creation (lean review — QL-STORY-READY skipped). The developer implements against these. Do not invent new test cases during implementation.*

- **AC-1**: AC-IM-12 anchor values
  - Given: compiled assemblies
  - When: reflection reads `DefaultExecutionOrderAttribute.order` on `InputManager`, `EnemySystemManager` (if present) and `CombatFrame` (or the stub at `0`)
  - Then: `InputManager == -200`, and `-200 < -100 < 0`
  - Edge cases: a reflection scan of all project assemblies finds no `[DefaultExecutionOrder]` other than the three anchors (plus a `+100` presentation anchor if one has been declared by then, per ADR-0002's diagram)

- **AC-2**: AC-IM-12 behavioural order (pump)
  - Given: InputManager (run started, Gameplay) and a fake Parry consumer that calls `TryConsumeParryPress(notBefore)` from a stub `RunFrame`
  - When: a press is injected for this frame's RunTime, then `inputManager.Tick()` and then the stub `RunFrame(dt)` run, in production order
  - Then: the consumer receives the press in the **same** frame (consume returns `true`)
  - Edge cases: a press stale at the start of the frame (age > window) is pruned in step (1) before the consumer runs, and the consumer gets `false`

- **AC-3**: Tick sub-order prune → capture → record
  - Given: a buffer with a stale press (age 0.130) and a fresh staged or recorded press, plus scripted `Move = (1,0)`
  - When: `Tick()` runs with an event recorder hooked into the three internal steps (test seam)
  - Then: the recorded order is `[Prune, Capture, RecordDirection]`. After `Tick`, the stale press is gone, the fresh press is present, and `LastSteerDirection` is updated
  - Edge cases: in Menu context, the recorded order omits `Prune` (freeze)

- **AC-4**: Same method in play mode and tests
  - Given: the InputManager source
  - When: it is statically inspected (or a reflection test checks that `Update` has a single call to `Tick`)
  - Then: `Update()` contains only the `Tick()` call, and `Tick` is `public`
  - Edge cases: none

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: `tests/unit/input-manager/input_manager_frame_order_test.cs` — must exist and pass (EditMode, manual pump)

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 002, Story 004, Story 005, Story 006; bootstrap-combat-frame IGameClock story (soft: bootstrap-combat-frame CombatFrame story for the real `[0]` anchor, with a stub acceptable meanwhile)
- Unlocks: Parry System epic (same-frame consume guarantee)

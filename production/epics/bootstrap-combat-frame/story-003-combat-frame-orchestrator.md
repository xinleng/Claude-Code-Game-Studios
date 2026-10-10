# Story 003: CombatFrame Orchestrator, ITickable Order, RunFrame Pump & EventRecorder

> **Epic**: Bootstrap, CombatFrame & Run Clock
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Logic
> **Manifest Version**: 2026-10-10

## Context

**GDD**: `design/gdd/damage-health.md` (Implementation Contract #3 — option (b) CombatFrame orchestrator preferred; AC-DH-09 event-recorder pattern), `design/gdd/input-manager.md` (Core Rule 9 execution order; AC-IM-12)
**Requirement**: `TR-DH-004` (same-frame parry priority via CombatFrame), `TR-IM-005` (Input → Parry → Combat/D&H order), `TR-TP-001` (manual tick pump)
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*
*Precondition `TR-DH-002` (entity architecture decided before combat code) is satisfied: ADR-0001 Accepted 2026-07-04.*

**ADR Governing Implementation**: ADR-0002: Canonical Frame Execution Order and the CombatFrame Orchestrator (amended 2026-10-10); ADR-0010: Deterministic Test Harness (Decision 2 — Pump; Decision 5 — event-recorder seam)
**ADR Decision Summary**: A single `CombatFrame` MonoBehaviour at `[DefaultExecutionOrder(0)]` runs the combat sub-order as explicit sequential calls — Parry → Dodge → CombatCore → Combo → PlayerHealth → Progression — held in a fixed, code-declared order. `CombatFrame.RunFrame(dt)` is the one true tick used by both play mode and tests. Only the `[-200]` InputManager / `[-100]` EnemySystemManager / `[0]` CombatFrame anchors (plus `[+100]` presentation) exist.

**Engine**: Unity 6.3 LTS | **Risk**: LOW
**Engine Notes**: `[DefaultExecutionOrder]` and the MonoBehaviour lifecycle are stable pre-cutoff APIs; no post-cutoff APIs. ADR-0002 Verification Required: AC-IM-12 and AC-DH-06 pass under the orchestrator (completed by the input-manager / damage-health epics using this story's pump).

**Control Manifest Rules (this layer)**:
- Required: Frame order is fixed: `[-200] InputManager → [-100] EnemySystemManager → [0] CombatFrame (Parry → Dodge → CombatCore → Combo → PlayerHealth → Progression) → [+100] Presentation`; combat sub-order is explicit sequential calls in `CombatFrame.RunFrame(dt)` (ADR-0002)
- Required: Wire dependencies by constructor/initializer injection at Bootstrap — interfaces only, no singletons, no `FindObjectOfType`/`FindFirstObjectByType` (ADR-0002, ADR-0012)
- Forbidden: Never implement `Update()`/`FixedUpdate()` in a combat system (ADR-0002; registry pattern `self_updating_combat_system`)
- Forbidden: Never read `Time.time`/`Time.unscaledTime` inside testable logic — `CombatFrame` takes `dt` from the injected `IGameClock` (ADR-0010)
- Guardrail: Negligible CPU — direct calls replace N engine `Update()` dispatches (ADR-0002 Performance)

---

## Acceptance Criteria

*From ADR-0002 Decision, epic DoD, input-manager AC-IM-12 (orchestrator variant), damage-health AC-DH-09 (recorder pattern), scoped to this story:*

- [ ] **(epic DoD)** `CombatFrame.RunFrame(dt)` drives six scripted fake subsystems in exactly this order, each exactly once per call: `parry.Evaluate()` → `dodge.Tick(dt)` → `combatCore.Tick(dt)` → `combo.Tick(dt)` → `playerHealth.Apply(dt)` → `progression.Tick()` — asserted with the `EventRecorder`
- [ ] The `dt` passed to `RunFrame` reaches every dt-taking step unchanged; `RunFrame(0)` still invokes every step in order (pause frames tick with zero scaled dt)
- [ ] `CombatFrame.Update()` contains no logic other than `RunFrame(_clock.DeltaTime)` — play mode and tests execute the identical method (ADR-0010: "the pump *is* production code")
- [ ] The step order is a compile-time constant: `CombatFrame` exposes no API to add, remove or reorder steps after construction
- [ ] Execution-order anchors are defined once as named constants (InputManager −200, EnemySystemManager −100, CombatFrame 0, Presentation +100); `CombatFrame` carries `[DefaultExecutionOrder]` using the CombatFrame constant; a test asserts Input < Enemy < CombatFrame < Presentation (the orchestrator half of AC-IM-12)
- [ ] `CombatFrame` receives its clock and six step slots by initializer injection; it performs no lookups (`Find*`, singletons, `GetComponent` on other systems) and owns no gameplay state
- [ ] `RunFrame` before initialization does nothing and does not throw (boot-order safety)
- [ ] `EventRecorder` test helper logs entries in emission order (`List<string>`), supports per-name counts and `Reset()`, so AC-DH-09-style assertions (`log[0] == "DamageTaken"`, `log[1] == "PlayerDead"`, count == 1) can be written directly

---

## Implementation Notes

*Derived from ADR-0002 Decision + Key Interfaces (amended 2026-10-10) and ADR-0010 Decisions 2 & 5:*

- **Shape**: `CombatFrame : MonoBehaviour`, `[DefaultExecutionOrder(ExecutionOrder.CombatFrame)]`, in `Dynasty.Core`. `public void RunFrame(float dt)` is the one true tick:
  ```
  parry.Evaluate();       // consume presses vs open windows; suppress hits
  dodge.Tick(dt);         // dash state
  combatCore.Tick(dt);    // locomotion, auto-attack, SwingLanded
  combo.Tick(dt);         // counter timeout, Finisher on 3rd swing
  playerHealth.Apply(dt); // unsuppressed damage → DamageTaken → iframes
  progression.Tick();     // XP thresholds, level-up requests
  ```
- **Step interfaces**: combat subsystems are plain C# classes implementing `ITickable { void Tick(float dt); }` "or explicit named methods where clearer" (ADR-0002). The real systems' interfaces (`IParrySystem`, `IComboSystem`, `ICombatCore`, `IPlayerHealth` — architecture.md API Boundaries) do not declare their frame-step methods, so define narrow frame-step seams here (e.g. `ITickable` for dodge/combatCore/combo; small named-step interfaces for `Evaluate()`, `Apply(dt)`, `Tick()`), which the real systems will implement in their own epics. Keep these seams in `Dynasty.Core`.
- **Injection**: `Initialize(IGameClock clock, <six step seams>)` with named parameters in the fixed order (MonoBehaviours cannot take constructor args — initializer injection is allowed by ADR-0002). Store in `readonly`-intent private fields (`_parry`, `_dodge`, …). No list, no registration API — the order is the code (changing it requires code review, by design).
- **Unbuilt systems**: until each system's epic lands, Bootstrap (Story 005) wires a no-op step in that slot; the slot itself never moves.
- **Anchors**: one `ExecutionOrder` static class of `const int`s (constants are allowed — they are structural, not gameplay tuning). Later epics apply `ExecutionOrder.Input` / `ExecutionOrder.EnemyManager` to their MonoBehaviours.
- **Pause**: `CombatFrame` respects `Time.timeScale` via the scaled `dt` from `IGameClock.DeltaTime`; unscaled timers (iframes, input) read `IGameClock.UnscaledTime`/`RunTime` inside their own systems — not CombatFrame's concern (ADR-0002 Risks).
- **Finisher handshake** (ADR-0002 amendment, TR-CC-006): ordering alone cannot express "Finisher replaces the 3rd swing" — that is enforced *inside* `combatCore.Tick` (reads `combo.ComboCounter` before applying swing damage). CombatFrame must not attempt to special-case it.
- **EventRecorder** (`tests/helpers/EventRecorder.cs`): `Record(string name)`, `IReadOnlyList<string> Log`, `int Count(string name)`, `Reset()`. Subscribers append in callback order. Also usable as a "call log" for the fake steps in this story. Avoid closure allocation concerns — test-only helper.
- Doc comments on all public members.

---

## Out of Scope

*Handled by neighbouring stories / epics — do not implement here:*

- Story 002: `IGameClock` itself
- Story 005: Bootstrap constructing systems and calling `CombatFrame.Initialize(...)`, MainArena wiring
- Story 006: static check that no other class declares `Update()`/`FixedUpdate()` without an anchor
- input-manager epic: `InputManager` at `[-200]` and the InputManager half of AC-IM-12
- object-pool / enemy epics: `EnemySystemManager` at `[-100]`
- Core / Feature epics: real Parry/Dodge/CombatCore/Combo/PlayerHealth/Progression step implementations; AC-DH-06, AC-DH-09, AC-PR-01 themselves; the Finisher handshake (TR-CC-006)

---

## QA Test Cases

*Written by qa-lead at story creation (lean mode — QL-STORY-READY skipped).*

- **AC-1**: `RunFrame(dt)` calls the six steps in canonical order, once each
  - Given: `CombatFrame` initialized with a `FakeGameClock` and six fake steps that each `Record("<name>")` into one shared `EventRecorder`
  - When: `RunFrame(0.016f)` is called once
  - Then: `recorder.Log` equals `["Parry.Evaluate", "Dodge.Tick", "CombatCore.Tick", "Combo.Tick", "PlayerHealth.Apply", "Progression.Tick"]` exactly (length 6)
  - Edge cases: three consecutive `RunFrame` calls → log of 18 entries, the 6-sequence repeated 3× with no interleaving
- **AC-2**: `dt` passthrough and zero-dt frames
  - Given: fake steps that capture the `dt` they receive
  - When: `RunFrame(0.25f)`; then `RunFrame(0f)`
  - Then: Dodge, CombatCore, Combo, PlayerHealth each received exactly `0.25f`, then exactly `0f`; all six steps still recorded on the zero-dt frame
  - Edge cases: none
- **AC-3**: `Update()` is the same path as `RunFrame`
  - Given: a `FakeGameClock` with `DeltaTime == 0.02f`, CombatFrame initialized with recording fakes
  - When: the Unity message `Update` is invoked (directly, e.g. via a test-visible hook or reflection) once
  - Then: the log is identical to one `RunFrame(0.02f)` call and dt-taking steps received `0.02f`
  - Edge cases: clock paused (`DeltaTime == 0`) → steps receive 0
- **AC-4**: Step order is not mutable at runtime
  - Given: the `CombatFrame` type
  - When: reflected over public/internal members
  - Then: no member adds, removes, replaces or reorders steps other than the single `Initialize(...)`; a second `Initialize` call does not change the order (either rejected or ignored — document which)
  - Edge cases: none
- **AC-5**: Execution-order anchors (orchestrator half of AC-IM-12)
  - Given: the `ExecutionOrder` constants and the `CombatFrame` type
  - When: reading the constants and `CombatFrame`'s `DefaultExecutionOrder` attribute via reflection
  - Then: `Input (-200) < EnemyManager (-100) < CombatFrame (0) < Presentation (100)`; `CombatFrame`'s attribute order == `ExecutionOrder.CombatFrame`
  - Edge cases: boundary values are the point here — literal −200/−100/0/100 assertions are allowed
- **AC-6**: No lookups / no owned gameplay state
  - Given: CombatFrame source
  - When: code-reviewed (automated `Find*` gate in Story 006)
  - Then: no `Find*`, singleton access, or `GetComponent` on another system; fields are only the clock and the six step references
  - Edge cases: none
- **AC-7**: `RunFrame` before `Initialize` is safe
  - Given: a freshly added, un-initialized `CombatFrame`
  - When: `RunFrame(0.016f)` and `Update()` are invoked
  - Then: no exception; nothing recorded
  - Edge cases: none
- **AC-8**: `EventRecorder` preserves order and counts
  - Given: an `EventRecorder` subscribed (by two test publishers) to events "DamageTaken" and "PlayerDead"
  - When: publisher fires DamageTaken, then PlayerDead, then `Reset()`, then DamageTaken
  - Then: before reset `Log[0] == "DamageTaken"`, `Log[1] == "PlayerDead"`, `Count("PlayerDead") == 1`; after reset `Log.Count == 1` and `Count("PlayerDead") == 0`
  - Edge cases: `Count` of a never-recorded name == 0; empty log after construction

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: `tests/unit/combat-frame/combat_frame_order_test.cs` (AC-1…AC-7) and `tests/unit/combat-frame/combat_frame_event_recorder_test.cs` (AC-8) — must exist and pass in EditMode
- Helper: `tests/helpers/EventRecorder.cs`

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001 (project/asmdefs) and Story 002 (`IGameClock`, `FakeGameClock`) must be DONE
- Unlocks: Story 005 (Bootstrap wires CombatFrame), Story 006 (anchor constants used by the Update() gate); input-manager epic (AC-IM-12), and every Core/Feature combat story (they test through `RunFrame`)

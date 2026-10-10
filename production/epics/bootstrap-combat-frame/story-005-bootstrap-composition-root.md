# Story 005: Bootstrap Composition Root — Boot Order & DI Wiring

> **Epic**: Bootstrap, CombatFrame & Run Clock
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Integration
> **Manifest Version**: 2026-10-10

## Context

**GDD**: `design/gdd/run-session-manager.md` (Core Rule 2 — run clock before any game logic; Edge Cases — inert until `runStartTime`), `design/gdd/damage-health.md` (Implementation Contract #3 — orchestrator wiring)
**Requirement**: `TR-RS-006` (initialisation-order contract — boot-phase portion), `TR-TP-001` (DI over singletons), `TR-DH-004` (CombatFrame wired as the orchestrator)
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0012: Scene Organisation and Boot Flow (Decisions 1–2); ADR-0002: Frame Execution Order (DI wiring); ADR-0010 (single injected clock)
**ADR Decision Summary**: One scene (`MainArena.unity`); a single `Bootstrap` object is the composition root. Boot is synchronous and one-time in `Bootstrap.Awake/Start`: (a) construct GameData views + `GameDataValidator.ValidateAll`; (b) construct all systems with constructor injection and wire interfaces (incl. `UiInputModuleBinder.Bind`); (c) `ObjectPool` pre-warm; (d) hand control to RSM in `Selection`. No gameplay object self-initialises. Bootstrap only constructs and wires — it owns no gameplay state and ticks nothing.

**Engine**: Unity 6.3 LTS | **Risk**: LOW
**Engine Notes**: Scene/bootstrap APIs stable since 2022 LTS; no post-cutoff APIs. `FindObjectOfType` is superseded by `FindFirstObjectByType` in Unity 6 — both are forbidden for wiring here. Serialized scene references (Inspector fields on `Bootstrap`) are the permitted way to reach scene objects (`CombatFrame`, UI documents).

**Control Manifest Rules (this layer)**:
- Required: `Bootstrap` is the composition root — boot order: GameData validation → construct + inject all systems (incl. `UiInputModuleBinder.Bind`) → ObjectPool pre-warm → RSM `Selection` (ADR-0012)
- Required: Wire dependencies by constructor/initializer injection at Bootstrap — interfaces only, no singletons, no `FindObjectOfType`/`FindFirstObjectByType` (ADR-0002, ADR-0012)
- Required: Gameplay logic reads time via injected `IGameClock` (ADR-0010)
- Forbidden: Never let a gameplay object self-initialise or touch another system in `Awake/Start` (ADR-0012)
- Forbidden: Never call `SceneManager.LoadScene` in MVP (ADR-0012)
- Guardrail: Bootstrap validation is the only load-time pass (ADR-0006); one synchronous boot before the menu (ADR-0012 Performance)

---

## Acceptance Criteria

*From ADR-0012 Decisions 1–2 + Risks, ADR-0002 wiring, ADR-0010 clock, scoped to this story:*

- [ ] The boot sequence executes its phases in exactly this order, each once: (a) GameData validation → (b) system construction + interface wiring, ending with the UI input-module bind → (c) ObjectPool pre-warm → (d) RSM entered in `Selection` — asserted by an `EventRecorder` integration test against fake phase participants
- [ ] Boot is synchronous and one-time: a second boot invocation performs no phase again (no double construction, no double pre-warm)
- [ ] If validation reports designer errors, boot still completes phases (b)–(d) (clamp-and-continue — ADR-0006 §5); the errors are observable through the injected designer-error channel
- [ ] Exactly one `IGameClock` instance is constructed and the **same instance** is injected into `CombatFrame` and every constructed system that takes a clock; the run-clock control interface is handed to RSM only (ADR-0010: RSM owns origin + accumulator)
- [ ] Exactly one designer-error channel instance is constructed and injected into every consumer that takes one (TR-TP-002)
- [ ] `CombatFrame.Initialize(...)` is called by Bootstrap with the clock and all six step slots in canonical order; slots whose system has not been implemented yet receive a no-op step (the slot order never changes)
- [ ] Bootstrap uses no `Find*` lookups or singletons; scene objects are reached via serialized references only
- [ ] Bootstrap owns no gameplay state and ticks nothing: it declares no `Update`/`FixedUpdate`/`LateUpdate`
- [ ] **PlayMode smoke (ADR-0010 PlayMode scope)**: `MainArena.unity` contains exactly one `Bootstrap`; loading the scene completes boot and one frame runs with zero exceptions/errors in the log

---

## Implementation Notes

*Derived from ADR-0012 Decision 2 + Risks, ADR-0002 Decision (wiring), ADR-0010 Decision 1:*

- **Split for testability**: `Bootstrap : MonoBehaviour` (in `Dynasty.Bootstrap`) holds only serialized scene references and calls a plain-C# boot sequence (e.g. `BootSequence.Run(...)`) from `Awake`. The boot sequence takes its phase participants as interfaces, so the EditMode integration test drives it with recording fakes — no scene needed for the order test.
- **Phase seams** (names proposed): a validation participant (implemented by `GameDataValidator`, game-data epic), the UI input-module binder (`UiInputModuleBinder.Bind`, input-manager epic), a pool pre-warm participant (`IObjectPool`, object-pool epic), and an RSM entry point (`EnterSelection`, run-session-manager epic). Until each epic lands, Bootstrap wires a no-op participant for that phase and the test still proves the order. Keep seams in `Dynasty.Core` so later epics implement them without referencing Bootstrap.
- **Construction (phase b)**: build in dependency order — clock (Story 002) and designer-error channel (Story 004) first, then systems receiving them via constructors, then `CombatFrame.Initialize(clock, parry, dodge, combatCore, combo, playerHealth, progression)`. Interfaces only; this is also how the Combo↔Parry↔CombatCore reference cycle is broken (ADR-0002).
- **Bootstrap must not reference `Unity.InputSystem`** — it constructs `InputManager` (from `Dynasty.Input`) through its InputSystem-free public surface. The `updateMode == Dynamic Update` bootstrap assertion required by ADR-0004 lives inside `Dynasty.Input` and is invoked here once the input-manager epic provides it.
- **No gameplay `Awake/Start` side effects**: other MonoBehaviours in the scene may not touch other systems in `Awake/Start`; Bootstrap is the only object that wires. Because Unity's `Awake` order across objects is unspecified, systems must be passive until Bootstrap initializes them (e.g. `CombatFrame.RunFrame` is a no-op until `Initialize` — Story 003).
- **Scene**: add the `Bootstrap` GameObject and the `CombatFrame` GameObject to `MainArena.unity`; assign the CombatFrame reference on Bootstrap via Inspector.
- **PlayMode smoke** lives in `tests/PlayMode/` per ADR-0010 — scene-wiring only, no gameplay assertions, no `WaitForSeconds` (yield one frame via `yield return null`).
- Doc comments on all public members.

---

## Out of Scope

*Handled by neighbouring stories / epics — do not implement here:*

- Story 006: automated static checks (LoadScene, InputSystem gate, `Update()` ban, `Find*` ban, `UnityEngine.Time` reads)
- game-data epic: `GameData` SO views, `GameDataValidator.ValidateAll` rules, `arena_bounds`, `run_duration_s`
- input-manager epic: `InputManager` construction details, `UiInputModuleBinder.Bind` body, `updateMode` assertion
- object-pool epic: pre-warm implementation and sizes
- run-session-manager epic: `Selection` state, the run-start contract (origin → `SetRunStartTime` → `IRunResettable.ResetForNewRun()` → `SetContext(Gameplay)` → spawner unfreeze — ADR-0012 §3), retry, AC-RS-01/06/07
- UI epics: selection screen rendering, shared `PanelSettings`

---

## QA Test Cases

*Written by qa-lead at story creation (lean mode — QL-STORY-READY skipped).*

- **AC-1**: Boot phases run in ADR-0012 order, once each
  - Given: the boot sequence constructed with recording fakes for validation, construction/wiring, UI bind, pool pre-warm and RSM entry, all writing to one `EventRecorder`
  - When: the boot sequence runs
  - Then: `Log == ["Validate", "Construct", "BindUiInput", "PreWarm", "EnterSelection"]` (construction entries may be finer-grained but must all precede "BindUiInput", and "BindUiInput" must be the last entry of phase b)
  - Edge cases: a participant that throws during phase (a) — document and assert the chosen behaviour (boot surfaces the exception; no later phase runs)
- **AC-2**: Boot is one-time
  - Given: a boot sequence that has completed
  - When: run is invoked a second time
  - Then: the recorder log is unchanged (no phase repeated); pre-warm fake call count == 1
  - Edge cases: none
- **AC-3**: Validation errors do not halt boot
  - Given: a validation fake that reports two designer errors through the injected recording designer-error sink
  - When: the boot sequence runs
  - Then: all five phases are recorded in order; the sink `Count == 2`
  - Edge cases: zero errors → identical phase log, sink `Count == 0`
- **AC-4**: One clock instance everywhere; control to RSM only
  - Given: boot with fake systems that capture the `IGameClock` they receive, and a fake RSM that captures the run-clock control interface
  - When: the boot sequence runs
  - Then: every captured clock is reference-equal to the CombatFrame's clock; only the RSM fake received the control interface; no other fake's constructor accepts it
  - Edge cases: none
- **AC-5**: One designer-error channel instance everywhere
  - Given: fakes capturing the channel they receive
  - When: boot runs
  - Then: all captured channels are reference-equal
  - Edge cases: none
- **AC-6**: CombatFrame wired with canonical slots
  - Given: boot with a real `CombatFrame` and recording step fakes in some slots, no-op steps in the rest
  - When: boot runs, then `combatFrame.RunFrame(0.016f)`
  - Then: the recorded step names appear in canonical relative order (Parry before Dodge before … Progression); no exception from no-op slots
  - Edge cases: all six slots no-op → `RunFrame` completes with an empty log
- **AC-7**: No lookups / singletons
  - Given: Bootstrap and boot-sequence source
  - When: reviewed (automated gate in Story 006)
  - Then: zero `Find*`/`FindObjectOfType`/`FindFirstObjectByType`/`FindAnyObjectByType` calls and no static instance fields
  - Edge cases: none
- **AC-8**: Bootstrap ticks nothing
  - Given: the `Bootstrap` type
  - When: reflected for `Update`, `FixedUpdate`, `LateUpdate` methods (any visibility)
  - Then: none declared
  - Edge cases: none
- **AC-9 (PlayMode smoke)**: Scene boots clean
  - Given: `MainArena.unity` loaded in a PlayMode test
  - When: one frame elapses (`yield return null`)
  - Then: exactly one `Bootstrap` component exists in the scene; boot reported complete; no error/exception log entries (UTF fails on unexpected errors)
  - Edge cases: none

---

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- Integration: `tests/integration/bootstrap/bootstrap_boot_order_test.cs` (AC-1…AC-8, EditMode) — must exist and pass
- PlayMode smoke: `tests/PlayMode/bootstrap/bootstrap_scene_smoke_test.cs` (AC-9) — must pass in CI's PlayMode step

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 002 (clock), Story 003 (CombatFrame + step seams), Story 004 (designer-error channel) must be DONE
- Unlocks: Story 006 (static checks run against the wired tree); run-session-manager epic (Selection entry + run-start contract), input-manager epic (`UiInputModuleBinder.Bind` call site), object-pool epic (pre-warm phase), game-data epic (validation phase)

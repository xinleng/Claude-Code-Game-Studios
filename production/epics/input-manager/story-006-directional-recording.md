# Story 006: Directional recording — CurrentMoveDirection & LastSteerDirection

> **Epic**: Input Manager
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Logic
> **Manifest Version**: 2026-10-10

## Context

**GDD**: `design/gdd/input-manager.md`
**Requirement**: `TR-CB-002` (raw `LastSteerDirection` recording half), `TR-IM-004` (`steer_time` on the unified run clock)
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0004: Single Input Authority and the Parry Input Buffer
**ADR Decision Summary**: `CurrentMoveDirection` is updated every frame. `LastSteerDirection { direction, steer_time }` is updated only when the `Move` magnitude exceeds `directional_deadzone` (0.2). Parry presses never update it. Staleness windows (Combo's 300 ms) are applied by consumers, not by Input Manager. `steer_time` is in the `IGameClock.RunTime` domain (ADR-0010 rule 1).

**Engine**: Unity 6.3 LTS + Input System package (pinned) | **Risk**: HIGH
**Engine Notes** (ADR-0004; `modules/input.md`):
- `Move` is a **Value / Vector2** action read with `ReadValue<Vector2>()` (pattern verified current in `modules/input.md`). Read it once per `Tick` inside `InputManager.cs` and pass the `Vector2` to engine-free recording logic.
- Do **not** put the deadzone into the Input Actions asset as a Stick Deadzone processor in place of the GDD knob. `directional_deadzone` is a data-driven tuning knob (TuningConfig) applied in code. A processor would add a second, hidden deadzone. If a stick processor is added later for hardware drift, document it, because it changes the effective magnitude this knob sees.
- Gamepad is best-effort (OQ-03). A left-stick `Move` composes with WASD through the action's bindings.

**Control Manifest Rules (Foundation layer)**:
- Required: ONE run clock: `steer_time` uses `IGameClock.RunTime` (ADR-0010, ADR-0004, TR-RS-005).
- Required: gameplay logic reads time through the injected `IGameClock` (ADR-0010).
- Forbidden: never hardcode a gameplay/tuning value. `directional_deadzone` comes from the injected input tuning view (ADR-0006).
- Forbidden: never mix values derived from `Time.time` with values derived from `Time.unscaledTime` (ADR-0010, conflict C1).

---

## Acceptance Criteria

*From GDD `design/gdd/input-manager.md` (Core Rule 8, Formula 3, Tuning Knobs), scoped to this story:*

- [ ] **AC-IM-06**: **GIVEN** a `Move` input of `(1,0)` with magnitude > `directional_deadzone`, **WHEN** captured at `t = 2.000`, **THEN** `LastSteerDirection.direction == (1,0)` (normalized) and `steer_time == 2.000`. **GIVEN** a `Move` of `(0.1,0)` (below deadzone 0.2), **THEN** `LastSteerDirection` is unchanged
- [ ] **AC-IM-07**: **GIVEN** a recorded `LastSteerDirection` at `t = 2.000`, **WHEN** a `Parry.performed` fires at `t = 2.010`, **THEN** `LastSteerDirection` is unchanged (a parry is not a directional input)
- [ ] `CurrentMoveDirection` is updated every Gameplay `Tick` with the raw `Move` value and **may be zero** (Rule 8; architecture.md `IInputAuthority`)
- [ ] `LastSteerDirection` updates only when `|Move| > directional_deadzone` (strictly greater, "exceeds" in Rule 8). The stored direction is normalized
- [ ] Input Manager applies **no** staleness or freshness window. `zhao_yun_input_direction_window_ms` (300) belongs to General Characters and is applied by the Combo System (Formula 3; Tuning Knobs "referenced, not owned")
- [ ] No `LastSteerDirection` is recorded before `SetRunStartTime` (GDD edge case: nothing is recorded with an undefined timestamp)

---

## Implementation Notes

*Derived from ADR-0004 Decision rule 5 and input-manager Core Rule 8:*

- In `InputManager.Tick` (Story 007 places it after prune and capture), read `Move` through the generated wrapper, then call an engine-free recorder, for example `DirectionalRecorder.Record(Vector2 move, float runTime)`. The recorder sets `CurrentMoveDirection = move`. If `move.magnitude > deadzone`, it also sets `LastSteerDirection = new SteerSample(move.normalized, runTime)`.
- `steer_time` is the frame's `IGameClock.RunTime` at `Tick`. `Move` is polled per frame, unlike Parry, which is callback-stamped from `ctx.time`.
- `SteerSample` is the `readonly struct` in architecture.md (`Direction`, `SteerTime`). Expose it through `IInputAuthority.LastSteerDirection` exactly as declared.
- The parry path (Story 004) must not reference the recorder. AC-IM-07 is structural, and the test proves it.
- **Open item (flag):** the GDD and architecture.md do not say what `LastSteerDirection` holds before the first steer of a run. A `default(SteerSample)` (zero direction, `SteerTime = 0`) would look "fresh" to Combo's 300 ms check in the first 300 ms of RunTime, but with a zero direction. Raise it with the Combo System owner at `/story-readiness`, since the consumer may need to treat a zero direction as "no steer". Do not invent a sentinel here.
- Dodge also reads `CurrentMoveDirection` / `LastSteerDirection` for dash direction (GDD Dependencies). No extra API is needed.

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 007: placing the recording step in the `Tick` sequence
- Combo System epic: 300 ms freshness and the Zhao Yun cone fallback chain (`TR-CB-002` consumer half)
- Dodge System epic: dash direction resolution
- Combat Core epic: locomotion from `CurrentMoveDirection`

---

## QA Test Cases

*Written at story creation (lean review — QL-STORY-READY skipped). The developer implements against these. Do not invent new test cases during implementation.*

- **AC-1**: AC-IM-06 above-deadzone records
  - Given: a recorder with deadzone 0.2 (from `TestConfigs`), run started
  - When: `Record((1,0), 2.000)`
  - Then: `LastSteerDirection.Direction == (1,0)`, `SteerTime == 2.000`, `CurrentMoveDirection == (1,0)`
  - Edge cases: `Record((0.3,0.4), …)` (magnitude 0.5) stores the normalized `(0.6,0.8)` (tolerance 1e-5). Diagonal keyboard input `(1,1)` normalizes to `(0.7071,0.7071)`

- **AC-2**: AC-IM-06 below-deadzone leaves the steer unchanged
  - Given: a prior steer `((1,0), 2.000)`
  - When: `Record((0.1,0), 2.050)`
  - Then: `LastSteerDirection` is still `((1,0), 2.000)`, and `CurrentMoveDirection == (0.1,0)`
  - Edge cases: boundary `|Move| == 0.2` exactly does **not** update (strict `>`), using a value exactly representable at the boundary such as `(0.2f, 0)` and asserting no update. `(0,0)` leaves the steer unchanged and sets `CurrentMoveDirection` to zero

- **AC-3**: AC-IM-07 parry does not steer
  - Given: a steer recorded at `2.000`, run started, Gameplay
  - When: `OnParryPerformed` is injected for RunTime `2.010`, and then `Tick` runs with `Move = (0,0)`
  - Then: `LastSteerDirection == ((1,0), 2.000)`
  - Edge cases: none

- **AC-4**: No staleness applied here
  - Given: a steer at `2.000`
  - When: `Tick` runs 60 times over 1 s with `Move = (0,0)`
  - Then: `LastSteerDirection` is still `((1,0), 2.000)`. Input Manager never expires it. A static check finds no reference to a 300 ms or `zhao_yun_input_direction_window_ms` value in `Dynasty.Input`
  - Edge cases: none

- **AC-5**: Inert before run start
  - Given: InputManager initialised, `SetRunStartTime` not called
  - When: `Tick` runs with a scripted `Move = (1,0)` (through the recorder seam gated on run start)
  - Then: `LastSteerDirection` is still the initial value (see the open item) and no `SteerTime` is set
  - Edge cases: none

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: `tests/unit/input-manager/input_manager_directional_test.cs` — must exist and pass (EditMode, injected clock)

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001, Story 004 (parry handler, for AC-IM-07); bootstrap-combat-frame IGameClock story
- Unlocks: Story 007; Combo System, Dodge System and Combat Core epics (read directions)

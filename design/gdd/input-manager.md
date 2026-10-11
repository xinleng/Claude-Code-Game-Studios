# Input Manager

> **Status**: Approved (self-review 2026-06-19)
> **Author**: xinleng + Claude Code agents
> **Last Updated**: 2026-06-18
> **Implements Pillar**: Pillar 1 (Readable Chaos)

## Overview

Input Manager is the single authority for capturing player intent and presenting it to gameplay systems as clean, timestamped, buffered input events. It owns the Unity Input Actions asset (the Gameplay action map — Move, Parry, and post-MVP Dodge — plus menu navigation), the active control scheme (Keyboard&Mouse primary; Gamepad best-effort for MVP), and one piece of gameplay-critical logic: a **generous input buffer** that lets a parry press registered slightly before its window still count. No combat rules live here — Input Manager does not decide whether a parry *succeeds* (that is the Parry System's window) or what a movement vector *means* (that is Combat Core / Combo System). It decides only *what the player pressed, when, and whether a press should be held briefly so a downstream system can consume it on the right frame.*

The player never sees Input Manager, but they feel it on every parry. Dynasty Survivors stakes its entire skill ceiling on the promise that a well-timed press always lands — at 200 enemies, with the screen full of telegraphs, a parry that "didn't register" is the single most run-ruining unfairness the game can commit. Input Manager exists to make that failure impossible: it captures the press the instant it happens, stamps it with run-local time, and holds it in a short buffer so a press a few frames early is honoured rather than dropped. Every other system reads input *through* this layer, never from the raw device — so input fairness is solved once, here, instead of re-litigated in every consumer.

## Player Fantasy

Input Manager's fantasy is **trust**. Not a power fantasy — a *reliability* fantasy. The feeling it protects is the quiet confidence that your hands and the game agree: when you read a telegraph and press parry, the parry happens. Every time. The general fantasy at the heart of Dynasty Survivors — "I am unstoppable because I am *skilled*" — only holds if skill is actually rewarded. The moment a player thinks *"I hit it, the game ate my input,"* the fantasy shatters and the run feels stolen rather than lost.

So the emotion Input Manager serves is the *absence* of a bad feeling. A great parry feels like the player's triumph; a dropped input feels like the game's betrayal. This system makes sure failure is always the player's — you mistimed, you misread, you panicked — and never the engine's. The reference is fighting games and *Dead Cells*: defensive timing feels outstanding precisely because the window is honest and the buffer is forgiving. When input is right, the player never thinks about it at all. That invisibility *is* the win condition. The player's takeaway after a hard-won parry chain is "I did that" — and Input Manager's job is to make sure nothing ever undercuts that sentence.

## Detailed Design

### Core Rules

1. **Single input authority.** All gameplay systems read player input *exclusively* through Input Manager. No system calls `Keyboard.current` / `Mouse.current` / `Gamepad.current` or reads an `InputAction` directly. Input Manager is the only subscriber to raw device callbacks. *(Architectural invariant — enforce via ADR.)*

2. **Input Actions asset / action maps.** One Input Actions asset, two maps:
   - **Gameplay** (active during a run): `Move` (Value, Vector2), `Parry` (Button), `Dodge` (Button — consumed by the Dodge System, which is in MVP scope).
   - **UI** (active during selection / pause / upgrade screens): `Navigate` (Value, Vector2), `Confirm` (Button), `Cancel` (Button).
   Exactly one map is enabled at a time; switching is Input Manager's responsibility (see States and Transitions).

3. **Control schemes.** Two schemes: **Keyboard&Mouse** (primary, fully supported) and **Gamepad** (best-effort, MVP-untested). Provisional default bindings (validate in prototype): Move = WASD / left-stick; Parry = Spacebar / right-trigger; Dodge = Left-Shift / face button; UI Confirm = Enter or Space / South button; UI Cancel = Esc / East button. The active scheme auto-switches on device activity. No runtime rebinding UI in MVP (settings deferred), but all bindings live in the asset so rebinding is a pure-data add post-MVP.

4. **Run-local timestamping.** Every captured input event is stamped on the **unified run clock** (`IGameClock.RunTime` — pause-compensated run-local; ADR-0010/TR-RS-005), converted from the input event's own sub-frame-precise clock (`ctx.time`). The run-clock origin is the same value RunSessionManager records and shares with EnemySystemManager, so press stamps and `AttackStartTime` are directly comparable in one domain. *(Revised 2026-10-10 by /architecture-review: the previous wording, `Time.unscaledTime − runStartTime`, conflicted with enemy-definition.md's scaled `Time.time − runStartTime` — the two diverge by total paused duration after every level-up pause, breaking all parry matching. Blocking conflict C1.)* A press the instant before a pause is still captured (the buffer freezes rather than clears outside Gameplay); run-local float avoids the long-session float-precision drift documented in enemy-definition.md.

5. **Parry input buffer (the fairness mechanism).** On `Parry.performed`, Input Manager records a `ParryPress { press_time, consumed = false }` into a fixed-size ring buffer (capacity `parry_buffer_capacity`, default 4). A press is **live** while `current_time − press_time ≤ parry_input_buffer_ms / 1000` AND `consumed == false`. Presses older than the buffer window are pruned at the start of each tick.

6. **Buffer query/consume contract (consumer-pull).** Input Manager exposes:
   - `bool TryConsumeParryPress(float notBefore, out float pressTime)` — returns the most recent live press whose `press_time ≥ notBefore`, marks it `consumed`, returns its timestamp; false if none.
   - `bool HasLiveParryPress()` — non-consuming peek (for UI/feedback).
   The Parry System calls `TryConsumeParryPress` when its window is open; the `notBefore` guard prevents one press satisfying two separate attack windows. **Input Manager never decides parry success — it only reports and consumes presses.**

7. **One press, one consumption.** A consumed press cannot be consumed again — a single tap cannot parry two simultaneous attacks (consistent with the "a single parry resolves against the most recently received unresolved event only" rule in enemy-definition.md / damage-health.md).

8. **Directional input recording.** Each frame Input Manager updates `CurrentMoveDirection` (Vector2, may be zero) and, when `Move` magnitude exceeds `directional_deadzone` (default 0.2), updates `LastSteerDirection { direction (normalized Vector2), steer_time }`. Input Manager owns this *raw recording only*; the staleness window (`zhao_yun_input_direction_window_ms = 300`, owned by General Characters) is applied by the consumer (Combo System / DirectionalInputBuffer), which reads `LastSteerDirection` and decides if it is fresh enough. A parry press is NOT a directional input and never updates `LastSteerDirection` (matches the general-characters.md edge case).

9. **Execution order.** Input Manager captures and buffers input early in the frame, before any consumer evaluates — `[DefaultExecutionOrder]` set lower (earlier) than the Parry System and Combat Core. Combined with damage-health.md Implementation Contract #3 (Parry evaluates before Damage & Health), the per-frame order is: **Input Manager (capture/buffer) → Parry System (query/consume) → Combat Core / Damage & Health.** Stale-buffer pruning runs at the very start of Input Manager's tick, before new presses are recorded.

10. **No combat semantics.** Input Manager does not know what a parry window is, what an attack is, what momentum is, or what a Finisher is. It captures presses and movement; consumers interpret. This boundary is absolute.

### States and Transitions

Input Manager has an **input context** state that determines which action map is enabled:

| State | Active Map | Behaviour | Exits To |
|---|---|---|---|
| **Gameplay** | Gameplay | Move/Parry/Dodge captured and buffered; UI actions ignored | Menu (a screen opens) / Paused (pause toggled) |
| **Menu** | UI | Navigate/Confirm/Cancel captured; gameplay actions ignored and NOT buffered; parry buffer **frozen** (not pruned, not added to) so a press made the instant before the screen opened survives | Gameplay (screen closes / run resumes) |
| **Paused** | UI | Same as Menu for input purposes; `Time.timeScale == 0`; input timestamps use unscaled time so buffer logic stays coherent | Gameplay (unpause) |

- **Context switches are explicit and owned by Run Session Manager**, which calls `InputManager.SetContext(Gameplay | Menu | Paused)`. Input Manager performs the action-map enable/disable; it does not decide *when* to switch.
- **Buffer freeze on leaving Gameplay:** the parry buffer is preserved as-is (frozen) rather than cleared, so a legitimately buffered press is not silently dropped if a level-up screen opens one frame later. On return to Gameplay, pruning resumes on the same run-local clock; presses that aged out during the menu are pruned on the first Gameplay tick. *(Whether a press should survive a long upgrade-screen pause is flagged in Open Questions.)*
- There is no rebinding state in MVP.

### Interactions with Other Systems

| System | Data In from Input Manager | Data Out to Input Manager | Interface Owner |
|---|---|---|---|
| **Parry System** | `TryConsumeParryPress(notBefore)` consumes a live buffered press; `HasLiveParryPress()` peek | None — Parry owns the window, queries the buffer | Input Manager owns the buffer + query API; Parry owns the window logic |
| **Combat Core** | `CurrentMoveDirection` (Vector2) for player movement; reads input through Input Manager, never raw devices | None | Input Manager owns input capture |
| **Combo System** | `LastSteerDirection { direction, steer_time }` for Zhao Yun cone orientation (applies its own 300ms staleness window) | None | Input Manager owns raw recording; staleness window owned by General Characters |
| **Dodge System** *(post-MVP)* | `Dodge` button events (bound but inert in MVP) | None | Input Manager owns the binding |
| **Run Session Manager** | None | `SetContext(Gameplay/Menu/Paused)`; provides `runStartTime` at run init | Run Session Manager drives context; Input Manager executes the map switch |
| **Combat Feedback UI** | `HasLiveParryPress()` (optional, for a "parry attempted" flourish) | None | Input Manager owns the peek API |

## Formulas

Input Manager is logic-heavy rather than math-heavy. These predicates define the buffer's behaviour and how it composes with the Parry System's window.

### 1. Parry Press Liveness

A buffered press is consumable only while live:

`is_live(press) = (current_time − press.press_time ≤ parry_input_buffer_ms / 1000) AND (press.consumed == false)`

| Variable | Type | Range | Description |
|---|---|---|---|
| `current_time` | float | ≥ 0 | Run-local unscaled time (seconds) |
| `press.press_time` | float | ≥ 0 | Run-local stamp when the press was captured |
| `parry_input_buffer_ms` | int | 60–200 | Buffer lifetime (tuning knob; default 120) |
| `press.consumed` | bool | — | Set true once consumed by a consumer |

**Output:** boolean. **Example:** press at `t = 1.000`, buffer 120ms → live until `t = 1.120` unless consumed first.

### 2. Effective Parry Acceptance Window (composition with Parry System)

Input Manager's buffer does not move the Parry System's window — it extends *when a press is accepted* by `parry_input_buffer_ms` on the **front** (early-press forgiveness). For an attack whose Parry window is `[w_open, w_close]`, a press at `press_time` is accepted iff:

`(w_open − parry_input_buffer_ms / 1000) ≤ press_time ≤ w_close` AND `press_time ≥ notBefore`

| Variable | Type | Range | Description |
|---|---|---|---|
| `w_open`, `w_close` | float | ≥ 0 | Parry window bounds = `[AttackStartTime, AttackStartTime + TelegraphDurationMs/1000]` — **owned by Parry System / enemy-definition.md** |
| `parry_input_buffer_ms` | int | 60–200 | Front extension only (this GDD) |
| `notBefore` | float | ≥ 0 | Reuse guard passed by the Parry System (prevents one press serving two windows) |

**Total forgiveness span** = `parry_input_buffer_ms + TelegraphDurationMs`. The back edge is never extended — a press *after* the telegraph ends is a clean miss (no penalty, per game-concept.md). **Example:** Levy telegraph 400ms, buffer 120ms → a press from 120ms *before* the telegraph opens through its 400ms duration counts: a 520ms effective acceptance span.

### 3. Directional Input Freshness *(consumer-applied — documented for completeness)*

Input Manager records `steer_time`; the freshness test is applied by the Combo System using General Characters' window:

`is_fresh(steer) = (current_time − steer.steer_time ≤ zhao_yun_input_direction_window_ms / 1000)`

`zhao_yun_input_direction_window_ms = 300` is **owned by General Characters** — Input Manager does not define or clamp it; it only supplies `steer_time`.

## Edge Cases

- **If the player mashes Parry (multiple presses in quick succession):** Each press is a new `ParryPress` in the ring buffer (capacity 4, oldest overwritten). `TryConsumeParryPress` returns the **most recent** live press; extra presses age out unconsumed. Mashing cannot bank multiple parries — one consumption per window (Core Rule 7).
- **If Parry is pressed with no incoming attack:** The press is buffered and ages out unconsumed after `parry_input_buffer_ms`. No penalty (game-concept.md: a missed parry costs only opportunity), and no combat effect — e.g., upgrade P3 "Deflect and Strike" requires a press that *blocked an attack*, so an unconsumed press never advances the combo.
- **If Parry is pressed during the player's invincibility frames:** Input Manager still captures and buffers it — iframes are a Damage & Health concept, unknown here. damage-health.md guarantees "parry input is still processed" during iframes; Input Manager honours that by never suppressing capture.
- **If a press lands exactly on the buffer boundary (`current_time − press_time == parry_input_buffer_ms / 1000`):** Inclusive — the `≤` predicate means the press is still live on that frame.
- **If the gamepad disconnects mid-run:** The active scheme falls back to Keyboard&Mouse; the buffer and recorded directional input are untouched; a designer-info log fires. Best-effort — no pause, no prompt in MVP.
- **If both devices feed input in the same frame (e.g., stick to move, key to parry):** Both are accepted — actions bind to all devices; the "active scheme" is cosmetic in MVP (no on-screen prompt switching). The last-active device sets the cosmetic scheme only.
- **If `SetContext(Menu)` is called the same frame as a Parry press:** Presses already recorded by Input Manager's earlier-in-frame tick remain in the frozen buffer. A press arriving *after* the switch routes to the UI map and is not buffered (Gameplay map disabled).
- **If a buffered press ages out while the game is paused on an upgrade screen:** On return to Gameplay, the first tick prunes any press now older than the buffer window (the run-local unscaled clock kept advancing). Whether a press *should* survive a multi-second pause is an Open Question (OQ-02).
- **If the Parry button is held rather than tapped:** Input Manager records one press on the `performed` edge only. Holding produces no repeated presses (no parry autofire).
- **If input is captured before `runStartTime` is set (first frames before run init):** Capture and buffering are inert until Run Session Manager provides `runStartTime` and calls `SetContext(Gameplay)`. No press is recorded with an undefined timestamp.
- **If two consumers touch the buffer in the same frame:** Only the Parry System *consumes* parry presses in MVP. `HasLiveParryPress()` is non-consuming, so a Combat Feedback UI peek and a Parry consume are order-independent.

## Dependencies

| System | Direction | Nature | Interface |
|---|---|---|---|
| **Run Session Manager** | Run Session Manager → Input Manager | Hard | Provides `runStartTime` at run init; calls `SetContext(Gameplay/Menu/Paused)` to switch the active action map. Input Manager executes the switch and timestamps against `runStartTime`; it does not decide *when* to switch. *(When authored, RSM must list Input Manager as a dependent.)* |
| **Parry System** | Parry System → Input Manager | Hard | Parry calls `TryConsumeParryPress(notBefore)` when its window is open and `HasLiveParryPress()` for feedback. Input Manager owns the buffer + query API; Parry owns the window `[AttackStartTime, AttackStartTime + TelegraphDurationMs/1000]`. The front-extension forgiveness (Formula 2) is Input Manager's; the window is Parry's. *(Parry GDD must document the consume call + `notBefore` reuse guard.)* |
| **Combat Core** | Combat Core → Input Manager | Hard | Reads `CurrentMoveDirection` for player movement. Must read through Input Manager, never raw devices (Core Rule 1). *(Combat Core GDD must list Input Manager as a dependency.)* |
| **Combo System** | Combo System → Input Manager | Hard | Reads `LastSteerDirection { direction, steer_time }` for Zhao Yun cone orientation and applies the `zhao_yun_input_direction_window_ms = 300` staleness window (owned by General Characters). This **resolves the `DirectionalInputBuffer` forward-dependency** general-characters.md left open ("defined in Combo System GDD or a dedicated ADR"): Input Manager owns the raw recording; the Combo System owns the freshness check. *(Combo GDD must cite this split.)* |
| **Dodge System** | Dodge System → Input Manager | Hard (MVP) | The Dodge System consumes `Dodge` button events and reads `CurrentMoveDirection` / `LastSteerDirection` for the dash direction. (Dodge is an MVP system — see dodge-system.md.) |
| **Combat Feedback UI** | Combat Feedback UI → Input Manager | Soft | Optional `HasLiveParryPress()` peek for a "parry attempted" flourish. Non-consuming; purely cosmetic. |

**Note — General Characters bidirectionality:** general-characters.md declares a hard dependency on a `DirectionalInputBuffer` subsystem. That contract is now satisfied jointly by Input Manager (raw `LastSteerDirection` recording) and the future Combo System GDD (applies the 300ms window). When the Combo System GDD is authored, it should reference both.

## Tuning Knobs

| Knob | Default | Safe Range | Affects |
|---|---|---|---|
| `parry_input_buffer_ms` | 120 | 60–200 | Front-extension forgiveness for parry presses. Below 60ms barely helps an early press and the "didn't register" feeling returns; above 200ms lets a careless early mash count as a read, eroding the skill expression the parry is built on. The single most feel-critical knob in this GDD — tune first in prototype. |
| `parry_buffer_capacity` | 4 | 2–8 | Ring-buffer size for recent presses. 4 covers realistic mash bursts within one buffer window; 2 risks dropping a legitimate press during a double-tap; above 8 wastes memory with no feel benefit (only the most-recent live press is ever consumed). |
| `directional_deadzone` | 0.2 | 0.1–0.4 | Minimum `Move` magnitude before `LastSteerDirection` updates (filters stick drift / tiny key-feathering). Below 0.1 records noise as intent (Zhao Yun cone jitters); above 0.4 makes gentle steering fail to register a direction. |

**Referenced, not owned here** (do not duplicate — point to source):
- `zhao_yun_input_direction_window_ms` = 300 — owned by **General Characters**; Input Manager only supplies `steer_time`.
- Parry window / `TelegraphDurationMs` — owned by **enemy-definition.md / Parry System**; Input Manager's buffer composes with it (Formula 2) but does not define it.

## Visual/Audio Requirements

Input Manager is a pure input-capture/event system — it owns no VFX, animation, or audio. Any "parry attempted" flourish (e.g., a brief flash when `HasLiveParryPress()` is true but no attack was present) is owned by **Combat Feedback UI**, which may subscribe to the non-consuming peek. No asset spec is generated for this system.

## UI Requirements

No UI in MVP. Runtime key-rebinding and a controls/settings screen are explicitly deferred (game-concept.md: settings & controller support are post-MVP). Bindings live in the Input Actions asset so a rebinding screen is a pure-data post-MVP add. The one-frame control card shown at run start ("Attack: auto | Parry: [KEY]") is owned by **Run UI**, which reads the active binding from Input Manager for display.

## Acceptance Criteria

All timing tests use a deterministic harness (manual `Tick()` pump + injected unscaled time), never `WaitForSeconds`.

| # | Criterion | Story Type | Gate |
|---|---|---|---|
| AC-IM-01 | **GIVEN** a `Parry.performed` event at run-local `t = 1.000` with `parry_input_buffer_ms = 120`, **WHEN** the buffer is queried at `t = 1.100`, **THEN** a live press exists; **WHEN** queried at `t = 1.121`, **THEN** no live press exists (pruned). **Pass:** assert `HasLiveParryPress()==true` at 1.100 and `==false` at 1.121. | Logic | BLOCKING |
| AC-IM-02 | **GIVEN** one live press at `t = 1.000`, **WHEN** `TryConsumeParryPress(notBefore=0.0)` is called, **THEN** it returns true with `pressTime==1.000`; **WHEN** called again, **THEN** it returns false (one press, one consumption). | Logic | BLOCKING |
| AC-IM-03 | **GIVEN** a live press at `t = 1.000`, **WHEN** `TryConsumeParryPress(notBefore=1.5)` is called, **THEN** it returns false (press older than guard); **WHEN** `TryConsumeParryPress(notBefore=0.9)` is called, **THEN** it returns true. | Logic | BLOCKING |
| AC-IM-04 | **GIVEN** `parry_input_buffer_ms = 120` and a press at `t = 1.000`, **WHEN** a parry window opens at `t = 1.080` (within buffer) and queries with `notBefore = 1.080 − 0.120 = 0.960`, **THEN** the press is consumable. **GIVEN** the same press but the window opens at `t = 1.200` (beyond buffer), **THEN** the press has been pruned and is not consumable. **Pass:** assert consume succeeds in case 1, fails in case 2 (validates Formula 2 front-extension). | Logic | BLOCKING |
| AC-IM-05 | **GIVEN** three presses at `t = 1.00, 1.02, 1.04` all live, **WHEN** `TryConsumeParryPress(notBefore=0)` is called, **THEN** it returns `pressTime==1.04` (most recent). | Logic | BLOCKING |
| AC-IM-06 | **GIVEN** a `Move` input of `(1,0)` with magnitude > `directional_deadzone`, **WHEN** captured at `t = 2.000`, **THEN** `LastSteerDirection.direction == (1,0)` (normalized) and `steer_time == 2.000`. **GIVEN** a `Move` of `(0.1,0)` (below deadzone 0.2), **THEN** `LastSteerDirection` is unchanged. | Logic | BLOCKING |
| AC-IM-07 | **GIVEN** a recorded `LastSteerDirection` at `t = 2.000`, **WHEN** a `Parry.performed` fires at `t = 2.010`, **THEN** `LastSteerDirection` is unchanged (a parry is not a directional input). | Logic | BLOCKING |
| AC-IM-08 | **GIVEN** context is Gameplay, **WHEN** `SetContext(Menu)` is called and a `Parry.performed` is injected afterward, **THEN** no new press is buffered (Gameplay map disabled) AND presses recorded before the switch remain in the buffer (frozen, not pruned). **WHEN** `SetContext(Gameplay)` is called, **THEN** capture resumes. | Logic | BLOCKING |
| AC-IM-09 | **GIVEN** the Parry button is held down for 0.5s, **WHEN** the hold is processed, **THEN** exactly one `ParryPress` is recorded (on the `performed` edge; no autofire). **Pass:** assert buffer press count == 1 over the hold. | Logic | BLOCKING |
| AC-IM-10 | **GIVEN** `runStartTime` has not been set (pre-init), **WHEN** a `Parry.performed` is injected, **THEN** no press is recorded. **WHEN** `runStartTime` is set and `SetContext(Gameplay)` is called, **THEN** subsequent presses are recorded with run-local timestamps. | Logic | BLOCKING |
| AC-IM-11 | **GIVEN** `Time.timeScale == 0` (paused) and context is Paused, **WHEN** a press is injected, **THEN** it is captured with an unscaled run-local timestamp that keeps buffer liveness coherent (no division/precision error). | Logic | BLOCKING |
| AC-IM-12 | **GIVEN** the Input Manager and Parry System both run in a frame, **THEN** Input Manager's capture/prune executes before the Parry System's query. **Pass:** assert Input Manager's `[DefaultExecutionOrder]` value is strictly less than the Parry System's (or, if a CombatFrame orchestrator is used, that it calls input capture before parry evaluation). | Logic | BLOCKING |
| AC-IM-13 | **GIVEN** no gameplay system reads `Keyboard/Mouse/Gamepad.current` or an `InputAction` directly (only Input Manager does), **THEN** the single-authority invariant holds. **Pass:** code-review / static-scan assertion that raw device access appears only inside Input Manager. | Config/Data | ADVISORY |

## Open Questions

| # | Question | Owner | Resolution Path |
|---|---|---|---|
| OQ-01 | Is `parry_input_buffer_ms = 120` the right default? It must compose with the (unauthored) Parry System window so total forgiveness feels fair but not trivializing. | Prototype | Tune against the Parry System window once that GDD exists; validate in the combat prototype alongside `parry_window_ms`. |
| OQ-02 | Should a buffered parry press survive a multi-second upgrade-screen pause? Current rule prunes it on return if aged out (likely correct — a press from before a 5s menu shouldn't auto-fire on resume), but confirm no edge feels bad. | Playtest | Observe in playtest; if a resumed press ever feels stolen or unfairly fired, revisit the freeze/prune rule. |
| OQ-03 | Gamepad Parry binding (right-trigger vs a face button) is provisional and untested in MVP. | Prototype (best-effort) | Validate feel only if gamepad is exercised; KB/M is the supported path. |
| OQ-04 | Should mouse buttons be a valid Parry binding (some players expect LMB/RMB)? MVP uses Spacebar. | Post-MVP / rebinding | Revisit when the rebinding screen is added post-MVP. |

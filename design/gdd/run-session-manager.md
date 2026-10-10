# Run Session Manager

> **Status**: Approved (self-review 2026-06-19)
> **Author**: xinleng + Claude Code agents
> **Last Updated**: 2026-06-19
> **Implements Pillar**: Pillar 4 (Every Run Teaches You Something)

## Overview

The Run Session Manager (RSM) is the lifecycle authority for a single run: it drives the flow from general selection → active run → win/death → retry, owns the **run clock** (`runStartTime` and the 20-minute countdown), orchestrates pauses (the upgrade pick screen, future pause menu) by switching Input Manager's context, aggregates run statistics for the death/win screen, and executes the full state reset on retry by calling every system's reset API. It is the single source of `runStartTime` — the run-local clock that Input Manager, Enemy AI, and the Wave Spawner all timestamp against. RSM consumes `PlayerDead` (with its kill-context fields) to build a death screen that answers "what cost you the run?", fulfilling Pillar 4. It owns the run's *flow and bookkeeping*; it owns no gameplay rules.

The player experiences RSM as the frame around every run: the general-select screen, the ticking timer, the moment of victory or the death screen that tells them what to try next.

## Player Fantasy

The Run Session Manager is the feeling of a run as a complete story with a clean beginning and a meaningful end. Pick your general, the timer starts, and for 20 minutes the legend is written — and then it ends in a way that *means* something. Survive and the win screen is earned triumph; fall and the death screen is a lesson, not a slap: it tells you the Iron Captain counter-hit that ended a 2-chain at minute 14, so the next run is a sharper version of the same gamble. The fantasy RSM protects is **the loop that pulls you back** — every ending, win or lose, hands you the reason to press retry.

## Detailed Design

### Core Rules

1. **Run lifecycle.** RSM sequences: **Selection** (general-select screen) → **Running** (20-minute run) → **Resolved** (Win or Death screen) → (retry → Selection / Running). It drives the screens via the UI systems but owns the flow.

2. **Run clock.** On the first frame of a run (before any game logic), RSM records `runStartTime = Time.time` and passes it to Input Manager, Enemy AI (`EnemySystemManager`), and the Wave Spawner. All run-local timestamps (`AttackStartTime`, input timestamps, wave step times) derive from it (matches enemy-definition / input-manager contracts). A 20-minute (1200s) countdown runs from `runStartTime`.

3. **General selection.** RSM presents the selection screen (Run UI renders it), initialises the chosen general (General Characters), then starts the run immediately (no loading screen). Cancel returns to the main menu (general-characters Selection Screen rules).

4. **Context switching.** RSM owns when the input context changes: it calls `InputManager.SetContext(Gameplay / Menu / Paused)`. It sets **Menu/Paused** when the upgrade pick screen opens (Progression level-up) — pausing the run (`Time.timeScale = 0`) — and **Gameplay** when the run resumes. Input Manager executes the switch; RSM decides when.

5. **Upgrade pick pause.** On a Progression level-up, RSM pauses the run, switches to Menu context, and shows the pick screen (Upgrade Selection UI). On pick confirmation, it applies the resume: Gameplay context, `Time.timeScale = 1`. The parry buffer is frozen during the pause (Input Manager), so a press the instant before the pause is not lost.

6. **Win condition.** At `run_time ≥ 1200s` with the player alive, RSM fires the **Win** screen (stats displayed) and halts spawning (Wave Spawner). The fixed timer is the natural stopping point.

7. **Death condition.** RSM subscribes to `PlayerDead` (Damage & Health). On it, RSM transitions to the **Death** screen and builds the cause-of-death summary from the event's kill-context fields (`KillerDamageType`, `ComboCounterAtDeath`, `MomentumAtDeath`, `TimeOfDeath`, `KillerAttackerId`).

8. **Run statistics (historian role).** Damage & Health is a ledger, not a historian (damage-health.md). RSM aggregates the run stats for the end screen: **total finishers landed, best parry chain, wave/step reached, time survived**, plus the cause-of-death. RSM subscribes to the relevant signals (Finisher resolved, parry chain length, current step, run time) to maintain these counters.

9. **Retry / reset.** On retry, RSM executes a full reset by calling each system's reset API: Damage & Health (`Reset()` → HP/caps/regen/state), Progression (XP/level/pool/streaks/Modifier count), Upgrade Definition (eligible pool → 24), and clears all enemies via the pool. It resets the timer to 1200s and re-enters Selection (or directly Running with the last general, post-MVP). MVP has no persistent upgrades — all run-granted values reset (damage-health retry rule).

10. **No gameplay rules.** RSM owns flow, the clock, pauses, stats aggregation, and resets. It does not compute damage, draw upgrades, or drive enemies.

### States and Transitions

| State | Behaviour | Exits To |
|---|---|---|
| **Selection** | General-select screen; awaits confirm/cancel | Running (confirm) / MainMenu (cancel) |
| **Running** | Run clock advances; spawning active; Gameplay context | Paused (level-up/pause) / Resolved-Win (t≥1200s alive) / Resolved-Death (`PlayerDead`) |
| **Paused** | `Time.timeScale = 0`; Menu context; pick screen or pause menu | Running (resume) |
| **Resolved-Win** | Win screen + stats; spawning halted | Selection (retry) / MainMenu |
| **Resolved-Death** | Death screen + cause-of-death + stats | Selection (retry) / MainMenu |

### Interactions with Other Systems

| System | Data In | Data Out | Interface Owner |
|---|---|---|---|
| **Input Manager** | — | `SetContext(...)`; provides `runStartTime` at init | RSM drives context |
| **Damage & Health** | `PlayerDead` (+ kill-context fields) | `Reset()` on retry | Damage & Health owns the event + reset API |
| **Progression System** | level-up (pause request) | pause/resume; `Reset()` on retry | Progression owns draw; RSM owns flow |
| **Wave Spawner** | boss-spawn / run-end timeline events | `runStartTime` + run clock; halt/reset | RSM owns the clock |
| **General Characters** | selected `GeneralId` | initialise general at run start | General Characters owns data |
| **Combo / Parry** | Finisher-landed, parry-chain signals (for stats) | — | RSM aggregates stats |
| **Run UI / Upgrade Selection UI** | — | drives selection, pause/pick, win/death screens | UI GDDs own layout |

## Formulas

RSM owns no balance formulas. Its one computed value is the **run-local clock**:

`run_time = Time.time − runStartTime` (seconds since run start); `time_remaining = max(0, 1200 − run_time)`.

| Variable | Type | Range | Description |
|---|---|---|---|
| `runStartTime` | float | ≥ 0 | `Time.time` on the first run frame; shared with Input Manager / Enemy AI / Wave Spawner |
| `run_time` | float | 0–1200+ | Seconds elapsed in the run |
| `time_remaining` | float | 0–1200 | Countdown to the win condition |

**Win:** `run_time ≥ 1200 AND player alive`. **Death:** on `PlayerDead`.

## Edge Cases

- **If the player dies on the exact frame the timer reaches 1200s:** Death takes priority — `PlayerDead` resolves before the win check (player must be alive at 20:00). Resolved-Death, not Win.
- **If a level-up fires during a Finisher animation:** RSM queues the pick screen until the animation completes (the run pauses on the next safe frame); the drawn options are locked at level-up time (Progression / game-concept).
- **If `PlayerDead` fires while a pick screen is open:** The pick is discarded; RSM transitions to Resolved-Death. A dead player does not pick an upgrade.
- **If retry is requested while not in a Resolved state:** RSM ignores it / guards the reset API (damage-health Reset guard — only valid from a dead/resolved state); a live run is not reset mid-play.
- **If the boss is killed before 18:00 is reached... (impossible — boss spawns at 18:00):** N/A; the boss cannot die before it spawns. If the boss is killed *after* spawning but before 20:00, the run continues until the timer (killing the boss is not a win condition in MVP — surviving to 20:00 is).
- **If `runStartTime` is read before it is set (first frame ordering):** Systems treat input/spawn as inert until RSM provides `runStartTime` (input-manager / enemy-definition contracts).

## Dependencies

| System | Direction | Nature | Interface |
|---|---|---|---|
| **Input Manager** | RSM → Input Manager | Hard | Provides `runStartTime`; calls `SetContext(Gameplay/Menu/Paused)`. |
| **Damage & Health** | Bidirectional | Hard | Subscribes to `PlayerDead` (+ kill-context); calls `Reset()` on retry. |
| **Progression System** | Bidirectional | Hard | Receives level-up pause requests; calls `Reset()` on retry. |
| **Wave Spawner** | Bidirectional | Hard | Provides the run clock + `runStartTime`; receives boss-spawn/run-end events; halts/resets. |
| **General Characters** | General Characters → RSM | Hard | Reads `GeneralId` to initialise the run. |
| **Combo / Parry** | systems → RSM | Soft | Subscribes to Finisher/parry-chain signals for stats aggregation. |
| **Run UI / Upgrade Selection UI** | RSM → UI | Hard | Drives selection, pause/pick, and win/death screens. |

## Tuning Knobs

| Knob | Default | Safe Range | Affects |
|---|---|---|---|
| `run_duration_s` | 1200 (20 min) | 600–1800 | Total run length / win timer |
| `pause_on_pick` | true | true/false | Whether the run pauses (`Time.timeScale = 0`) for the upgrade pick |

*Referenced, not owned: `runStartTime` consumers (Input Manager, Enemy AI, Wave Spawner); `PlayerDead` + reset APIs (each owning system); elite/boss timeline (Wave Spawner / Enemy Definition).*

## Visual/Audio Requirements

RSM mandates the end-screen content, not its layout (Run UI): the **death screen** must display the cause-of-death sentence (e.g., "Killed by Iron Captain counter-hit while holding a 2-chain at 14:02") plus total finishers, best parry chain, wave reached, and time survived. The **win screen** shows the same stats framed as triumph. A clear audio/visual sting distinguishes win from death. The 20:00 approach may have a rising-tension audio cue (final 5 minutes are high-intensity per game-concept).

## UI Requirements

RSM triggers the **selection screen**, **upgrade pick screen** (pause), **win screen**, and **death screen** — all rendered by Run UI / Upgrade Selection UI. This GDD mandates the flow and the data each screen displays; layout is owned by the UI GDDs.

> **📌 UX Flag — Run Session Manager**: The selection, win, and death screens have UI requirements. Run `/ux-design` for these screens before writing their stories.

## Acceptance Criteria

| # | Criterion | Story Type | Gate |
|---|---|---|---|
| AC-RS-01 | **GIVEN** a run starts, **WHEN** the first frame runs, **THEN** `runStartTime` is recorded and passed to Input Manager, Enemy AI, and Wave Spawner; `run_time` begins at 0. | Integration | BLOCKING |
| AC-RS-02 | **GIVEN** a Progression level-up, **WHEN** it fires, **THEN** RSM pauses the run (`Time.timeScale = 0`), switches Input Manager to Menu context, and shows the pick screen; on pick, it resumes Gameplay context and `Time.timeScale = 1`. | Integration | BLOCKING |
| AC-RS-03 | **GIVEN** `run_time ≥ 1200s` and the player alive, **WHEN** the check runs, **THEN** the Win screen fires and spawning halts. | Integration | BLOCKING |
| AC-RS-04 | **GIVEN** a `PlayerDead` event with `KillerDamageType = CounterHit`, `ComboCounterAtDeath = 2`, `TimeOfDeath = 842`, **WHEN** RSM handles it, **THEN** the Death screen shows a cause-of-death summary built from those fields plus the aggregated run stats. | Integration | BLOCKING |
| AC-RS-05 | **GIVEN** the player dies on the same frame `run_time` reaches 1200s, **THEN** the run resolves to Death (not Win) — alive-at-20:00 is required for Win. | Logic | BLOCKING |
| AC-RS-06 | **GIVEN** a Resolved state and a retry, **WHEN** reset fires, **THEN** RSM calls Damage & Health, Progression, and Upgrade Definition reset APIs, clears all enemies, and resets the timer to 1200s. | Integration | BLOCKING |
| AC-RS-07 | **GIVEN** a live (Running) run, **WHEN** a retry/reset is requested, **THEN** it is ignored (no mid-run reset). | Logic | BLOCKING |

## Open Questions

| # | Question | Owner | Resolution Path |
|---|---|---|---|
| OQ-01 | On retry, should RSM return to the general-select screen or restart immediately with the last general? MVP = back to selection. | UX/Playtest | Validate which keeps the retry loop snappy (Pillar 4 hook). |
| OQ-02 | Is surviving to 20:00 the right win condition, or should killing the Warlord be the win? MVP = survive the timer. | Design/Playtest | Confirm; the boss is a capstone, not currently a win trigger. |
| OQ-03 | Which exact stats most help the player learn (Pillar 4)? Current: finishers, best parry chain, wave, time, cause-of-death. | Playtest | Validate the death screen genuinely answers "what to try next." |

# Run UI

> **Status**: Approved (self-review 2026-06-19)
> **Author**: xinleng + Claude Code agents
> **Last Updated**: 2026-06-19
> **Implements Pillar**: Pillar 1 (Readable Chaos), Pillar 4 (Every Run Teaches You Something)

## Overview

Run UI is the persistent run-frame interface: the always-on HUD elements that frame a run (HP bar, XP bar, the 20-minute timer), the **general selection screen** (three general cards with Finisher-geometry previews), the run-start control card, and the **win / death screens** with their run stats. Where Combat Feedback UI owns the *combat-state* read-outs (combo counter, momentum, hit feedback), Run UI owns the *run-state* read-outs (health, progress, time) and the bracketing screens (select, win, death). It subscribes to Damage & Health (HP), Progression (XP/level), Run Session Manager (timer + win/death flow + stats), and General Characters (the roster + geometry previews), rendering all of it through Unity UI Toolkit. It owns presentation only.

The player experiences it as the run's dashboard and its bookends — how much life is left, how close the next level-up is, how long remains, and at the end, what the run amounted to.

## Player Fantasy

Run UI is the feeling of a run you can read at a glance and an ending that means something. The HP bar is the stakes, the XP bar is the climb, the timer is the horizon — three honest gauges that let the player feel exactly where they stand without ever looking away from the fight for long. And at the selection screen, the three generals are a real choice made vivid: not stat sheets, but three shapes — ring, cone, burst — previewed so the player picks the *verb* that fits their hands. At the end, the win or death screen closes the story and hands over the hook for the next run (Pillar 4). Run UI's fantasy is **orientation**: always knowing where you are in the run, and always knowing what the run was.

## Detailed Design

### Core Rules

1. **HP bar.** Displays `current_hp` / `max_hp` (Damage & Health), updating on every `DamageTaken` and on regen/heal. Must be readable at 200 enemies (Pillar 1). Visibly grows when `max_hp_bonus` (S1) increases mid-run (the bar ceiling expands; current HP does not jump — damage-health edge case).

2. **XP bar.** Displays current XP toward `xp_to_next(level)` (Progression), filling on kills, resetting (with overflow carry) on level-up, plus the current level number.

3. **Run timer.** Displays `time_remaining` (Run Session Manager run clock) counting down from 20:00. The final 5 minutes may shift to a higher-intensity visual state (game-concept: final 5 min are high-intensity).

4. **General selection screen.** Renders 3 general cards (General Characters), each with: Name, Weapon, one-line Playstyle summary, and a **top-down geometry preview** of the Finisher shape centred on a player icon — Ring (full circle), Cone (90° forward wedge), PointBurst (circle with outward displacement arrows). All previews at the same scale (general-characters Selection Screen rules). Guan Yu pre-focused; keyboard/gamepad/mouse navigation; `Confirm` starts the run; `Cancel` returns to main menu.

5. **Control card.** A one-frame, dismissible card at run start ("Attack: auto | Parry: [KEY]") reading the active binding from Input Manager (game-concept onboarding). No pause required; dismissible immediately.

6. **Win / death screens.** On RSM resolution, render the end screen with the run stats RSM aggregates: total finishers landed, best parry chain, wave/step reached, time survived — and on death, the **cause-of-death** sentence built from `PlayerDead` kill-context fields (Pillar 4). A retry prompt and a main-menu option. Win and death are visually/audibly distinct.

7. **Keyboard-first.** All interactive screens (selection, win/death) support full keyboard navigation with a visible focus state; no hover-only interactions (technical-preferences).

8. **Presentation only.** Run UI reads run state and renders it; it never modifies HP, XP, the timer, or run flow.

### States and Transitions

| Screen | Shown When | Driven by |
|---|---|---|
| **Selection** | Run start (pre-run) | RSM (flow) + General Characters (roster/previews) |
| **In-run HUD** | During Running | Damage & Health (HP), Progression (XP), RSM (timer) |
| **Control card** | First seconds of a run | Input Manager (binding) |
| **Win screen** | RSM Resolved-Win | RSM (stats) |
| **Death screen** | RSM Resolved-Death | RSM (stats + cause-of-death) |

### Interactions with Other Systems

| System | Data In | Data Out | Interface Owner |
|---|---|---|---|
| **Damage & Health** | `current_hp`, `max_hp` (+ updates) | None | Damage & Health owns values |
| **Progression System** | current XP, `xp_to_next`, level | None | Progression owns values |
| **Run Session Manager** | run timer; win/death flow; aggregated run stats | selection confirm/cancel; retry/menu choice | RSM owns flow + stats |
| **General Characters** | roster: Name, Weapon, Playstyle, Finisher shape (for preview) | selected `GeneralId` | General Characters owns data; Run UI renders the geometry preview widget |
| **Input Manager** | active Parry binding (control card); `Navigate`/`Confirm`/`Cancel` (menus) | None | Input Manager owns capture |

## Formulas

Run UI owns no balance formulas — it renders values owned by other systems (`current_hp`/`max_hp`, XP vs `xp_to_next`, `time_remaining`). Display conversions only: e.g., HP shown as `ceil(current_hp)`; timer shown as `mm:ss` from `time_remaining`.

## Edge Cases

- **If `max_hp` increases mid-run (S1):** The HP bar ceiling expands; the current-HP fill does not jump (the player is at, e.g., 20/120, not 20/100 → 24/120) — matches damage-health "ceiling only" rule.
- **If `current_hp` is a non-integer (regen):** Displayed as `ceil(current_hp)` so the player never sees a confusing fractional value (damage-health note).
- **If the run timer reaches 0:00 and the player is alive:** Run UI shows the Win screen (RSM drives); if the player dies on that frame, Death takes priority (RSM edge case).
- **If the player cancels the selection screen:** Return to main menu; no run starts (general-characters rule).
- **If the death screen's cause-of-death fields are incomplete (e.g., a non-combat death):** Fall back to a generic "Defeated at mm:ss" line plus the available stats; never show a broken/empty sentence.
- **If the control card overlaps the first wave:** It is dismissible immediately and auto-fades after a few seconds; it never blocks input.

## Dependencies

| System | Direction | Nature | Interface |
|---|---|---|---|
| **Damage & Health** | Damage & Health → Run UI | Soft | Reads `current_hp` / `max_hp` for the HP bar (polling or event — delegated to this GDD; event-driven preferred). |
| **Progression System** | Progression → Run UI | Hard | Reads XP / `xp_to_next` / level for the XP bar. |
| **Run Session Manager** | Bidirectional | Hard | Reads the timer + aggregated stats; drives selection/win/death; returns selection + retry choices. |
| **General Characters** | General Characters → Run UI | Hard | Reads the roster + Finisher shape; renders the geometry preview widget per general-characters spec. |
| **Input Manager** | Input → Run UI | Hard | Reads the active Parry binding (control card); `Navigate`/`Confirm`/`Cancel` for menus. |

## Tuning Knobs

| Knob | Default | Safe Range | Affects |
|---|---|---|---|
| `control_card_autofade_s` | 4s | 0–8s | How long the run-start control card lingers before auto-fading |
| `timer_intensity_threshold_s` | 300 (final 5 min) | 0–600 | When the timer shifts to a high-intensity visual state |
| `hp_bar_anim_ms` | 120 | 0–300 | HP bar fill animation speed on damage/heal |

*Referenced, not owned: HP values (Damage & Health); XP values (Progression); timer + stats (RSM); roster + geometry (General Characters); bindings (Input Manager).*

## Visual/Audio Requirements

HUD elements (HP, XP, timer) anchor to screen edges, high-contrast, readable at 200 enemies (Pillar 1), and never obscure the action. The selection-screen geometry previews must clearly distinguish Ring vs Cone vs PointBurst by shape (general-characters: PointBurst shows outward displacement arrows). The death/win screens use a Three Kingdoms frame (art bible, once authored) with a clear win-vs-death visual/audio distinction. Full asset spec via VFX & Audio GDD / art bible.

## UI Requirements

Run UI owns the in-run HUD (HP/XP/timer), the general selection screen (+ geometry preview widget), the run-start control card, and the win/death screens. Built in **Unity UI Toolkit**. All interactive screens support keyboard/gamepad navigation with visible focus; no hover-only interaction.

> **📌 UX Flag — Run UI**: The selection, HUD, and win/death screens have UI requirements. Run `/ux-design` for each and `/ux-review` before writing stories.

## Acceptance Criteria

| # | Criterion | Story Type | Gate |
|---|---|---|---|
| AC-RU-01 | **GIVEN** `current_hp` changes via `DamageTaken` or regen, **WHEN** the HUD updates, **THEN** the HP bar reflects `ceil(current_hp)` / `max_hp`. | UI | ADVISORY |
| AC-RU-02 | **GIVEN** `max_hp` increases from S1 mid-run, **WHEN** the HUD updates, **THEN** the bar ceiling expands and the current fill does not jump. | Logic | BLOCKING |
| AC-RU-03 | **GIVEN** XP accrues and a level-up occurs, **WHEN** the HUD updates, **THEN** the XP bar fills, resets with overflow carry, and the level number increments. | UI | ADVISORY |
| AC-RU-04 | **GIVEN** the run clock, **WHEN** the HUD updates, **THEN** the timer shows `time_remaining` as `mm:ss` counting down from 20:00. | UI | ADVISORY |
| AC-RU-05 | **GIVEN** the selection screen, **WHEN** it renders, **THEN** 3 general cards display with distinct geometry previews (Ring/Cone/PointBurst) at equal scale, Guan Yu focused, navigable by keyboard. | UI | ADVISORY |
| AC-RU-06 | **GIVEN** a `PlayerDead` with kill-context fields, **WHEN** the death screen renders, **THEN** it shows the cause-of-death sentence plus finishers, best parry chain, wave reached, and time survived, with a retry prompt. | Integration | BLOCKING |
| AC-RU-07 | **GIVEN** the selection or end screen, **WHEN** navigated by keyboard only, **THEN** focus moves and selection/retry can be confirmed without a mouse. | UI | ADVISORY |

## Open Questions

| # | Question | Owner | Resolution Path |
|---|---|---|---|
| OQ-01 | Should HP read via polling or event subscription? (damage-health left this to Run UI.) | Architecture | Event-driven preferred (no per-frame poll); confirm in the UI architecture ADR. |
| OQ-02 | Exact HUD layout for readability at 200 enemies without obscuring the action. | UX | Resolve in `/ux-design` for the HUD; validate at density. |
| OQ-03 | Does the death screen's stat set best serve Pillar 4 (RSM OQ-03)? | Playtest | Validate the screen answers "what to try next." |

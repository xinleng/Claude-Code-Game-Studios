# Upgrade Selection UI

> **Status**: Approved (self-review 2026-06-19)
> **Author**: xinleng + Claude Code agents
> **Last Updated**: 2026-06-19
> **Implements Pillar**: Pillar 3 (Snappy Decisions)

## Overview

Upgrade Selection UI is the level-up pick screen: it presents the 3 drawn upgrade options (Name + one-line Description) and captures the player's single pick. It is the visible face of the entire upgrade system — the player never sees the catalogue, the draw logic, or the pool; they see three cards and choose one. Its design mandate is Pillar 3 (Snappy Decisions): three readable choices, plain language, an immediate, low-friction pick — no tooltips, no analysis paralysis. It receives the 3 options from the Progression System (drawn from Upgrade Definition's pool) and returns the pick; it owns layout, interaction, and keyboard/gamepad navigation only. The run is paused while it is open (Run Session Manager).

The player experiences it as the fork in the road — fast, clear, and final.

## Player Fantasy

Upgrade Selection UI is the feeling of a clean decision under pressure. The battlefield freezes for a beat, three cards slide in, and each is a single legible line — *this is who I could become.* The fantasy is **the snap decision that feels right**: no spreadsheet, no second-guessing, just three honest options and the one that matches the run you're already having. The screen respects the player's time and momentum — it asks one question, takes one answer, and throws them back into the fight. The best version is invisible: the player picks almost before they realise they've read, because the choice was already half-made on the battlefield.

## Detailed Design

### Core Rules

1. **Three-card layout.** Displays the 3 drawn options as 3 cards, each showing the upgrade **Name** (2–3 words) and **Description** (exactly one line, plain text). No tooltips, no secondary detail panels (Pillar 3 / Upgrade Definition display constraint).

2. **Single pick.** The player selects exactly one card; on confirm, the pick is returned to the Progression System, the screen closes, and the run resumes. The 2 unpicked options return to the pool (Progression).

3. **Fewer than 3 options.** If the eligible pool has only 1–2 upgrades, the screen shows that many cards (Upgrade Definition edge case). The player still picks one.

4. **Keyboard + gamepad navigation.** Per technical-preferences (PC-first, menus must support keyboard navigation; no hover-only): cards are navigable via `Navigate` (left/right), selected via `Confirm`. A clear focus highlight shows the current card. Mouse click also selects. Default focus on the leftmost card.

5. **No cancel/skip.** There is no "skip the pick" in MVP — a level-up always grants an upgrade (the player must pick one of the offered options). `Cancel` does nothing on this screen (the run is paused; there is nowhere to back out to).

6. **Description overflow is a content error.** If a Description exceeds one line at the target resolution, that is an upgrade-content error to be fixed in Upgrade Definition (the upgrade must be redesigned), not handled by truncation (Upgrade Definition dependency).

7. **Pause coupling.** The screen is shown only while the run is paused (`Time.timeScale = 0`, Menu input context) — Run Session Manager owns the pause; this UI owns what is shown during it.

8. **Presentation only.** This system renders options and captures the pick. It does not draw upgrades, apply stat effects, or know the catalogue internals.

### States and Transitions

| State | Behaviour | Exits To |
|---|---|---|
| **Hidden** | Not shown; run active | Showing (level-up → RSM pauses + requests screen) |
| **Showing** | 3 (or fewer) cards displayed; awaiting navigation/confirm; leftmost focused | Confirming (card selected) |
| **Confirming** | Pick returned to Progression; close animation | Hidden (run resumes) |

### Interactions with Other Systems

| System | Data In | Data Out | Interface Owner |
|---|---|---|---|
| **Progression System** | the 3 drawn options (IDs → Name + Description) | the player's pick (ID) | Progression owns the draw; UI owns display + capture |
| **Upgrade Definition** | Name + Description content per upgrade | None | Upgrade Definition owns content |
| **Input Manager** | `Navigate` / `Confirm` (Menu context) | None | Input Manager owns capture |
| **Run Session Manager** | show/hide trigger (pause coupling) | pick-confirmed (resume) | RSM owns the pause/flow |

## Formulas

Upgrade Selection UI owns no formulas — it is a presentation/interaction layer. The only quantitative target is interaction latency (the pick should feel instant; see Acceptance Criteria).

## Edge Cases

- **If only 1 option remains in the pool:** Show a single card; the player confirms it (no real choice, but the level-up still grants the upgrade).
- **If a Description would wrap to two lines:** Treated as an Upgrade Definition content error (flag, don't truncate). The UI assumes single-line content.
- **If the player provides no input for a long time (run paused indefinitely):** The screen waits — there is no timeout auto-pick in MVP (the run is paused; the player chooses when ready). Pillar 3 is about *readability*, not a forced timer.
- **If `PlayerDead` somehow fires while the screen is open (it shouldn't — run is paused):** RSM discards the pick and transitions to Death (RSM edge case); the screen closes without applying.
- **If the same upgrade ID appears twice in the drawn options (it must not — draw is without replacement):** Treated as a Progression draw bug; the UI renders what it's given but this is an upstream contract violation (Progression AC guarantees uniqueness).

## Dependencies

| System | Direction | Nature | Interface |
|---|---|---|---|
| **Progression System** | Bidirectional | Hard | Receives 3 options; returns the pick. |
| **Upgrade Definition** | Upgrade Definition → UI | Hard | Reads Name + Description; one-line constraint enforced upstream. |
| **Input Manager** | Input → UI | Hard | `Navigate` / `Confirm` in the Menu context; keyboard + gamepad + mouse. |
| **Run Session Manager** | RSM → UI | Hard | Shows the screen on pause; resumes on pick. |

## Tuning Knobs

| Knob | Default | Safe Range | Affects |
|---|---|---|---|
| `card_open_anim_ms` | 150 | 0–400 | Card slide-in time; must feel snappy (Pillar 3) — keep low |
| `card_count_max` | 3 | 3 (MVP) | Options shown per level-up |
| `default_focus` | leftmost | leftmost/center | Initial keyboard focus card |

*Referenced, not owned: Name + Description content (Upgrade Definition); draw of 3 (Progression); pause (RSM); input bindings (Input Manager).*

## Visual/Audio Requirements

Cards must be high-contrast and instantly readable (Three Kingdoms palette; consistent with the art bible once authored). Each card: Name (bold, larger) + Description (one line). A clear focus highlight (not hover-dependent) for keyboard/gamepad. A satisfying "pick" confirm SFX + brief flourish. The screen dims the paused battlefield behind the cards (so the player's eye goes to the choice) without fully hiding the action. Full asset spec via VFX & Audio GDD / art bible.

## UI Requirements

This system owns the upgrade pick screen: 3-card layout, Name + Description, focus highlight, keyboard/gamepad/mouse selection, dimmed-background pause overlay. Built in **Unity UI Toolkit** (UXML/USS). Must support keyboard navigation (technical-preferences) and never rely on hover-only interaction.

> **📌 UX Flag — Upgrade Selection UI**: This is a core interactive screen. Run `/ux-design` for the upgrade-selection screen and `/ux-review` before writing stories.

## Acceptance Criteria

| # | Criterion | Story Type | Gate |
|---|---|---|---|
| AC-US-01 | **GIVEN** a level-up with 3 drawn options, **WHEN** the screen shows, **THEN** exactly 3 cards display, each with its Name and one-line Description, leftmost focused. | UI | ADVISORY |
| AC-US-02 | **GIVEN** the screen is open, **WHEN** the player presses `Navigate` then `Confirm`, **THEN** the focused card is selected, the pick (ID) is returned to Progression, and the screen closes. | Integration | BLOCKING |
| AC-US-03 | **GIVEN** the eligible pool has only 2 options, **WHEN** the screen shows, **THEN** exactly 2 cards display and a pick is still required. | Logic | BLOCKING |
| AC-US-04 | **GIVEN** the screen is open, **WHEN** the player navigates with keyboard only (no mouse), **THEN** focus moves and a pick can be confirmed without any mouse input (keyboard navigation works). | UI | ADVISORY |
| AC-US-05 | **GIVEN** a pick is confirmed, **WHEN** the screen closes, **THEN** the run resumes (Gameplay context, `Time.timeScale = 1`) and the picked upgrade's effect is applied (via Progression → owning system). | Integration | BLOCKING |

## Open Questions

| # | Question | Owner | Resolution Path |
|---|---|---|---|
| OQ-01 | Should the cards hint at lane identity (color-coding Combo/Parry/Auto/Modifier) to speed the read, without becoming a build-crafter UI? | UX/Playtest | Resolve in `/ux-design`; subtle lane color may aid Pillar 3 without violating the "not a build-crafter" anti-pillar. |
| OQ-02 | Is a "lane mastered" callout on the 6th lane pick worth adding (Upgrade Definition OQ-09)? | V-Slice | Low-cost; evaluate in playtest. |
| OQ-03 | Does the pause + 3-card flow feel snappy enough, or does it break combat momentum? | Playtest | Pillar 3 validation; tune `card_open_anim_ms`. |

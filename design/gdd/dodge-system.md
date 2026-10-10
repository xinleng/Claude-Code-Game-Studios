# Dodge System

> **Status**: Approved (self-review 2026-06-19)
> **Author**: xinleng + Claude Code agents
> **Last Updated**: 2026-06-19
> **Implements Pillar**: Pillar 1 (Readable Chaos), Pillar 2 (Skill Has a Lane)

## Overview

The Dodge System is the player's evasive movement: a short, fast dash that grants brief invincibility, used to escape attacks that cannot be parried — specifically the Warlord Phase 2 ground slam (`Parriable = false`, 2.0u radius, 800ms telegraph). It consumes the `Dodge` button from Input Manager (bound but inert in MVP until this system existed — this GDD activates it), moves the player a fixed distance over a short window, requests an invulnerability window from Damage & Health, and enforces a cooldown so dodging is a deliberate tool, not a spammed crutch. The Dodge System exists primarily to make the Warlord's non-parriable slam survivable as designed; it is a focused, single-purpose evasive layer, not a full character-action dodge-roll system.

The player experiences it as the "get out of the way" button — the answer to the one attack the general cannot meet head-on.

## Player Fantasy

The Dodge System is the feeling of a hard read with your feet instead of your blade. Almost everything in Dynasty Survivors is answered by standing your ground — auto-attack, combo, parry are all about holding the line. The dodge is the exception that proves the rule: the Warlord raises the earth and there is no parrying it, only the instinct to *move now*. The fantasy is the clutch escape — the split-second dash out of a closing red circle that would have ended the run. It is rare and decisive, which is exactly why it lands hard when it matters.

## Detailed Design

### Core Rules

1. **Dodge input.** A `Dodge` button press (Input Manager Gameplay map, default Left-Shift / face button) triggers a dodge if the dodge is off cooldown and the player is not already mid-dodge.

2. **Dash movement.** A dodge dashes the player `dodge_distance` (default 3.0u) over `dodge_duration` (default 0.25s) in the dodge direction. **Direction** = `CurrentMoveDirection` if non-zero at the moment of input; else the last `LastSteerDirection`; else the canonical screen-forward `Vector3(0,0,1)` (consistent with the general-characters / Input Manager directional convention). The dash overrides normal locomotion for its duration.

3. **Invincibility.** On dodge start, the Dodge System requests Damage & Health to open an invulnerability window of `dodge_invuln_ms` (default 400ms), reusing the iframe mechanism (Damage & Health remains the single owner of invulnerability, as with parry). The invuln covers the dash plus a short recovery so a frame-perfect dodge through the slam is forgiving.

4. **Cooldown.** After a dodge starts, `dodge_cooldown` (default 1.0s) must elapse before the next dodge. The cooldown prevents continuous dodge-spam from trivialising positioning (Pillar 2 — dodge is a tool, not a movement replacement).

5. **Warlord slam contract.** The dodge must clear the Warlord Phase 2 slam: `dodge_distance` (3.0u) > slam radius (2.0u), and `dodge_invuln_ms` (400ms) covers the dash through the slam impact. A player who dodges within the 800ms slam telegraph survives. This is the Dodge System's primary design requirement and resolves enemy-definition OQ-04. **It also retires the provisional fallback** in enemy-definition.md (temporarily making the slam `Parriable = true`): with the Dodge System designed, the slam stays `Parriable = false`.

6. **No parry/combo interaction.** Dodging does not affect `combo_counter`, momentum, or trigger any upgrade. It is purely evasive movement in MVP. Auto-attack is suppressed during the dash (the player is dashing, not swinging).

7. **Not a combat tool in MVP.** No dodge-attack, no i-frame-cancel of animations, no upgrade modifies the dodge in MVP. Scope is deliberately minimal — the dodge exists for the slam and other future non-parriable threats.

### States and Transitions

| State | Behaviour | Exits To |
|---|---|---|
| **Ready** | Dodge available; normal locomotion | Dashing (on `Dodge` press) |
| **Dashing** | Player moves `dodge_distance` over `dodge_duration`; invuln active; auto-attack suppressed; further dodge input ignored | Cooldown (dash complete) |
| **Cooldown** | Normal locomotion resumes; dodge unavailable until `dodge_cooldown` elapses | Ready (cooldown elapsed) |

### Interactions with Other Systems

| System | Data In | Data Out | Interface Owner |
|---|---|---|---|
| **Input Manager** | `Dodge` button events; `CurrentMoveDirection` / `LastSteerDirection` for dash direction | None | Input Manager owns the binding + capture |
| **Combat Core** | — | Suppresses locomotion + auto-attack during the dash | Combat Core owns normal movement; Dodge overrides during the dash |
| **Damage & Health** | — | Requests `dodge_invuln_ms` invulnerability window | Damage & Health owns iframes |
| **Enemy Definition / Enemy AI** | Warlord slam telegraph (read by the player, not the system) | None | Enemy Definition owns the slam |

## Formulas

### 1. Dash Velocity

`dash_speed = dodge_distance / dodge_duration`; position integrates `dash_speed × Δt` in the dodge direction for `dodge_duration`.

| Variable | Type | Range | Description |
|---|---|---|---|
| `dodge_distance` | float (u) | 2.5–4.0; default 3.0 | Total dash distance (must exceed the 2.0u slam radius) |
| `dodge_duration` | float (s) | 0.15–0.4; default 0.25 | Dash time window |

**Output:** `3.0 / 0.25 = 12 u/s` dash speed. **Example:** a dodge from inside the 2.0u slam radius clears it within ~0.17s.

### 2. Slam-Survival Constraint

For the dodge to reliably clear the Warlord slam: `dodge_distance > slam_radius (2.0u)` AND `dodge_invuln_ms ≥ dodge_duration × 1000 + reaction_margin`. At defaults: `3.0 > 2.0` ✓ and `400ms ≥ 250ms + margin` ✓. If `slam_radius` (Enemy Definition) increases, re-validate this constraint.

## Edge Cases

- **If the player presses Dodge while already dashing:** The input is ignored (no dash-cancel, no chaining in MVP).
- **If the player presses Dodge during cooldown:** Ignored; no effect, no feedback beyond the unavailable state (a UI cue is optional, owned by Combat Feedback UI).
- **If `CurrentMoveDirection == 0` at dodge input (standing still):** Dash uses `LastSteerDirection`; if none, screen-forward `(0,0,1)`. The player always dashes *somewhere* — a stationary dodge would not escape the slam.
- **If the dodge ends still inside the slam radius (e.g., slam radius retuned above `dodge_distance`):** The player takes the slam if the invuln has expired. This is a tuning failure, not a designed outcome — Formula 2 constraint must hold (designer-warn at load if `dodge_distance ≤ slam_radius`).
- **If a dodge overlaps a parryable attack:** The dodge invuln discards that damage like any iframe (Damage & Health); the parry is not consumed (no `DamageTaken`, so no buffered press is spent). Dodging through a parryable attack simply avoids it without the parry reward — a valid but non-optimal play.
- **If the player dodges off the arena edge:** Clamp to arena bounds (arena owned by the map/Run Session Manager); the dash stops at the boundary. Dodge distance may be cut short at the edge.

## Dependencies

| System | Direction | Nature | Interface |
|---|---|---|---|
| **Input Manager** | Dodge → Input Manager | Hard | Consumes the `Dodge` button (previously inert) and reads dash direction from `CurrentMoveDirection` / `LastSteerDirection`. |
| **Combat Core** | Dodge → Combat Core | Hard | Overrides locomotion + suppresses auto-attack during the dash. |
| **Damage & Health** | Dodge → Damage & Health | Hard | Requests a `dodge_invuln_ms` invulnerability window (iframe mechanism). |
| **Enemy Definition / Enemy AI** | Enemy → Dodge (design contract) | Hard (Warlord) | The Warlord Phase 2 slam (`Parriable = false`) is the reason this system exists; the dodge must clear it (Formula 2). Retires the provisional `Parriable = true` fallback. |

## Tuning Knobs

| Knob | Default | Safe Range | Affects |
|---|---|---|---|
| `dodge_distance` | 3.0u | 2.5–4.0u | Dash reach; must exceed the 2.0u slam radius |
| `dodge_duration` | 0.25s | 0.15–0.4s | Dash time; shorter feels snappier, longer feels floaty |
| `dodge_invuln_ms` | 400ms | 250–600ms | Invulnerability during/after the dash; must cover the dash through the slam |
| `dodge_cooldown` | 1.0s | 0.5–2.5s | Seconds between dodges; below 0.5 trivialises positioning, above 2.5 risks being unavailable when the slam comes |

*Referenced, not owned: slam radius (2.0u) / telegraph (800ms) — Enemy Definition; iframe mechanism — Damage & Health.*

## Visual/Audio Requirements

A dodge must read clearly: a dash animation with a brief motion trail and a distinct "whoosh" SFX, and a visible i-frame cue (e.g., a brief character shimmer) so the player and observers can tell the dodge granted invulnerability. The cooldown state may show an optional UI indicator (Combat Feedback UI). The dodge VFX must not compete visually with the parry burst or Finisher (subordinate per game-concept VFX hierarchy). Full spec in VFX & Audio GDD.

## Acceptance Criteria

| # | Criterion | Story Type | Gate |
|---|---|---|---|
| AC-DG-01 | **GIVEN** the dodge is Ready and `CurrentMoveDirection = (1,0)`, **WHEN** `Dodge` is pressed, **THEN** the player dashes 3.0u in +X over `dodge_duration` and a `dodge_invuln_ms` invulnerability window opens. | Logic | BLOCKING |
| AC-DG-02 | **GIVEN** the player is inside the 2.0u Warlord slam radius, **WHEN** they dodge at slam telegraph start, **THEN** the player ends outside 2.0u and takes no slam damage (invuln covers the impact). | Integration | BLOCKING |
| AC-DG-03 | **GIVEN** a dodge has started, **WHEN** `Dodge` is pressed again mid-dash, **THEN** the second input is ignored (no chain). | Logic | BLOCKING |
| AC-DG-04 | **GIVEN** a dodge just completed, **WHEN** `Dodge` is pressed before `dodge_cooldown` elapses, **THEN** no dodge occurs; **WHEN** pressed after, **THEN** a dodge occurs. | Logic | BLOCKING |
| AC-DG-05 | **GIVEN** `CurrentMoveDirection = 0` and a `LastSteerDirection = (0,0,-1)`, **WHEN** `Dodge` is pressed, **THEN** the dash direction is `(0,0,-1)` (never a zero/stationary dodge). | Logic | BLOCKING |
| AC-DG-06 | **GIVEN** auto-attack is active, **WHEN** a dodge starts, **THEN** auto-attack is suppressed for `dodge_duration` and locomotion is overridden by the dash. | Integration | BLOCKING |

## Open Questions

| # | Question | Owner | Resolution Path |
|---|---|---|---|
| OQ-01 | Are `dodge_distance = 3.0u`, `dodge_invuln_ms = 400ms`, `dodge_cooldown = 1.0s` the right feel for clearing the slam without trivialising movement? | Prototype | Validate against the Warlord slam in prototype; this resolves enemy-definition OQ-04. |
| OQ-02 | Should the dodge ever interact with combat (i-frame-cancel, dodge-attack) post-MVP? MVP = purely evasive. | Post-MVP | Revisit if the dodge feels underused once more non-parriable threats exist. |
| OQ-03 | Should a dodge have a stamina/charge cost, or is a flat cooldown sufficient? MVP = flat cooldown. | Playtest | Confirm flat cooldown reads fairly; a charge system is post-MVP scope. |

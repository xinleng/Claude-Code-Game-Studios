# Combat Feedback UI

> **Status**: Approved (self-review 2026-06-19)
> **Author**: xinleng + Claude Code agents
> **Last Updated**: 2026-06-19
> **Implements Pillar**: Pillar 1 (Readable Chaos)

## Overview

Combat Feedback UI is the layer that makes combat *legible* — the on-screen, real-time read-outs of combat state at 200 enemies: the combo counter (1 → 2 → FINISH), the momentum meter, per-hit feedback differentiated by `DamageType` (a counter-hit must read differently from a poke), and the in-world telegraph indicators that signal incoming attacks. It is the front line of Pillar 1: if the player can't read what's happening, no other system matters. It subscribes to combat state from the Combo System (counter), Parry System (momentum), Damage & Health (`DamageTaken` + `DamageType`), and Enemy Definition/AI (telegraph shapes), and renders all of it through Unity UI Toolkit (HUD) and VFX Graph (in-world telegraphs, owned jointly with VFX & Audio). It owns presentation only — no combat rules.

The player experiences it as clarity: at any density, they always know their combo state, their momentum, what just hit them, and what is about to.

## Player Fantasy

Combat Feedback UI is the feeling of *seeing clearly in the storm*. Two hundred soldiers, finishers detonating, parries flashing — and the player is never lost. The combo counter pulses in the corner of the eye; the momentum meter glows toward its max; the dangerous counter-hit telegraph stands out from a hundred ordinary swings. The fantasy is the general's battlefield instinct made visible: the chaos has a HUD, and mastery is reading it without thinking. When this layer works, the player feels superhuman awareness; when it fails, the game feels unfair. Its whole job is to make the first true.

## Detailed Design

### Core Rules

1. **Combo counter HUD.** Displays `combo_counter` state as 1 → 2 → FINISH, pulsing on each increment (driven by the Combo System). On reset, it plays a visible **break/shatter** animation (not a fade) to teach the damage-reset rule without a tooltip (game-concept). Readable at 200 enemies (Pillar 1).

2. **Momentum meter HUD.** Displays `momentum_meter` (0–100) as a filling, glowing bar (driven by the Parry System). Has a distinct **full-meter** state (signals P6/ceiling). On a real hit it visibly empties (or halves with P4) in the same frame the meter resets.

3. **Per-hit feedback by DamageType.** On `DamageTaken`, render a hit response keyed to `DamageType` (damage-health contract): `Standard` → standard flash + hit vignette; `CounterHit` → larger/different-color screen shake (the most dangerous hit — must read distinctly); `Slam` → fullscreen impact (Warlord Phase 2); `Projectile` → arrow-hit feedback, directional if possible.

4. **Reduced-hit distinction.** When a Finisher or parry burst lands on an elite at `combo_counter < 2` (reduced, no stagger), the hit must read differently from a full-combo hit (e.g., smaller number / dimmer flash) — driven by whether `combo_counter < 2` at resolution, NOT by `IsCounterHit` (enemy-definition Combat Feedback contract).

5. **Telegraph indicators.** Renders each enemy's telegraph per Enemy Definition's shape/duration/color rules (shape-first, colorblind-safe): Levy ring, Pikeman line, Archer arrowhead, Iron Captain thick ring / counter-hit 270° arc, Warlord slam filled circle. The counter-hit arc is the **most important telegraph** and must stay readable at 200 enemies (enemy-definition). Telegraphs use VFX Graph with GPU instancing (enemy-definition Contract #4); no two types share both shape and color.

6. **Damage numbers (optional, lightweight).** Finisher hits show large, distinct numbers; normal hits show small or none (configurable). Must not clutter the screen at high density — capped/pooled.

7. **Parry-attempt flourish (optional).** May peek `HasLiveParryPress()` (Input Manager, non-consuming) to show a brief "parry attempted" cue even when no attack was present — cosmetic only.

8. **Presentation only.** Combat Feedback UI reads combat state and renders it. It never modifies combat state, computes damage, or decides outcomes.

### States and Transitions

Combat Feedback UI is event-driven; its "state" mirrors the combat systems it observes:

| Element | Driven by | Visual behaviour |
|---|---|---|
| Combo counter | Combo System | 0 (hidden/dim) → 1 → 2 (primed glow) → FINISH flash → shatter on reset |
| Momentum meter | Parry System | fill 0–100; full-meter glow; empty/half on reset |
| Hit feedback | Damage & Health (`DamageType`) | per-type flash/shake/vignette transient |
| Telegraphs | Enemy AI (`AttackEvent`) | shape appears for `TelegraphDurationMs`, clears on resolve |

### Interactions with Other Systems

| System | Data In | Interface Owner |
|---|---|---|
| **Combo System** | `combo_counter` state + transitions | Combo owns state; UI renders |
| **Parry System** | `momentum_meter` value + reset events | Parry owns state; UI renders |
| **Damage & Health** | `DamageTaken` + `DamageType`; reduced-hit signal | Damage & Health owns events |
| **Enemy Definition / Enemy AI** | telegraph shapes/durations; `AttackEvent` | Enemy Definition owns shapes |
| **Input Manager** | `HasLiveParryPress()` (optional flourish) | Input Manager owns peek |
| **VFX & Audio** | shared telegraph + burst VFX assets | VFX & Audio owns asset production |

## Formulas

Combat Feedback UI owns no balance formulas — it is a presentation layer. Rendering is driven directly by the state values it observes (combo_counter, momentum_meter, DamageType). Performance budget is its only quantitative constraint (see Acceptance Criteria / Tuning Knobs).

## Edge Cases

- **If many `DamageTaken` events fire in a short window (dense pack):** Hit feedback is throttled/pooled so the screen does not strobe; the most severe `DamageType` (CounterHit/Slam) takes visual priority over Standard.
- **If a counter-hit telegraph and many infantry telegraphs are active simultaneously:** The counter-hit arc renders with visual priority (distinct layer/color reservation) so it is never lost in the crowd (enemy-definition).
- **If the combo counter resets and re-builds within a few frames (C3, or fast re-hit):** The shatter animation may be interrupted by the next increment; prioritize showing the current state over completing the prior animation.
- **If `combo_counter` is at 2 and a reduced elite hit lands:** Show the reduced-hit visual (dim/small), not a full-combo flash, per Rule 4.
- **If the HUD would overlap the action at low resolution:** HUD elements anchor to screen edges and scale; they must never obscure the player or nearby telegraphs.

## Dependencies

| System | Direction | Nature | Interface |
|---|---|---|---|
| **Combo System** | Combo → UI | Hard | Renders combo counter state + transitions. |
| **Parry System** | Parry → UI | Hard | Renders momentum meter + reset. |
| **Damage & Health** | Damage & Health → UI | Hard | Renders per-`DamageType` hit feedback; reduced-hit distinction. |
| **Enemy Definition / Enemy AI** | Enemy → UI | Hard | Renders telegraphs per shape/duration/color spec. |
| **Input Manager** | Input → UI | Soft | Optional parry-attempt flourish. |
| **VFX & Audio** | VFX & Audio ↔ UI | Hard | Shares telegraph/burst VFX assets; VFX & Audio produces them. |

## Tuning Knobs

| Knob | Default | Safe Range | Affects |
|---|---|---|---|
| `hit_feedback_throttle_ms` | 50 | 16–120 | Min interval between full hit-feedback renders (anti-strobe at density) |
| `damage_number_pool_size` | 64 | 32–128 | Max simultaneous floating numbers |
| `counter_hit_shake_scale` | 1.5× | 1.0–2.5× | CounterHit screen-shake magnitude relative to Standard |
| `telegraph_concurrent_cap` | (TBD via prototype) | — | Max concurrent telegraph VFX before culling lowest-priority (enemy-definition OQ-03) |

## Visual/Audio Requirements

This system *is* the visual feedback layer. Hard requirements (full asset spec via VFX & Audio GDD): combo counter pulse + shatter; momentum meter fill + full-meter glow; per-`DamageType` hit responses (CounterHit shake distinct from Standard); telegraph shapes shape-distinguishable and colorblind-safe; the counter-hit arc visually dominant. All UI built in **Unity UI Toolkit** (UXML/USS) per technical-preferences; in-world telegraphs use VFX Graph. Keyboard navigation is irrelevant for the live HUD (no interaction) but the HUD must not rely on hover.

## UI Requirements

Combat Feedback UI owns the live combat HUD: combo counter, momentum meter, hit feedback, damage numbers, and in-world telegraph rendering. Built in UI Toolkit. Layout must be readable at 1080p and scale; HUD anchors to edges and never obscures the player or nearby telegraphs.

> **📌 UX Flag — Combat Feedback UI**: The combat HUD has UI requirements. Run `/ux-design` for the HUD layout before writing its stories.

## Acceptance Criteria

| # | Criterion | Story Type | Gate |
|---|---|---|---|
| AC-FB-01 | **GIVEN** the Combo System advances `combo_counter` 0→1→2, **WHEN** the HUD updates, **THEN** the counter displays 1, 2, then a primed state, pulsing on each increment; **WHEN** the counter resets, **THEN** a shatter animation plays. | UI | ADVISORY |
| AC-FB-02 | **GIVEN** the momentum meter increases on parry and resets on a real hit, **WHEN** the HUD updates, **THEN** the meter fill matches the value and visibly empties (or halves with P4) on reset. | UI | ADVISORY |
| AC-FB-03 | **GIVEN** a `DamageTaken` with `DamageType = CounterHit`, **WHEN** rendered, **THEN** the hit feedback (shake/flash) is visibly distinct from a `Standard` hit. | Visual | ADVISORY |
| AC-FB-04 | **GIVEN** a reduced elite hit at `combo_counter < 2`, **WHEN** rendered, **THEN** the feedback reads as reduced (driven by `combo_counter < 2`, not `IsCounterHit`). | Logic | BLOCKING |
| AC-FB-05 | **GIVEN** an Iron Captain counter-hit telegraph among 200 enemies, **WHEN** rendered, **THEN** the 270° arc is visually distinct and not occluded by infantry telegraphs (manual QA at density). | Visual | ADVISORY |
| AC-FB-06 | **GIVEN** 200 enemies with active telegraphs, **WHEN** the HUD + telegraphs render, **THEN** the frame stays within the 16.6ms budget (VFX Graph instancing, throttled feedback). | Performance | ADVISORY |

## Open Questions

| # | Question | Owner | Resolution Path |
|---|---|---|---|
| OQ-01 | Where does the combo counter sit so it's readable without drawing the eye off the action? | UX/Playtest | Resolve in `/ux-design` for the HUD; validate at density. |
| OQ-02 | What is the `telegraph_concurrent_cap` before readability breaks at 200 enemies? | Prototype | enemy-definition OQ-03 — establish empirically. |
| OQ-03 | Do damage numbers help or clutter at this density? Default lightweight (Finisher-only). | Playtest | Validate; may cut normal-hit numbers entirely. |

# Combo System

> **Status**: Approved (self-review 2026-06-19)
> **Author**: xinleng + Claude Code agents
> **Last Updated**: 2026-06-19
> **Implements Pillar**: Pillar 1 (Readable Chaos), Pillar 2 (Skill Has a Lane)

> **Note**: Combo System is flagged high-risk in systems-index.md and is intended to be validated by `/prototype combo-and-parry-system` before implementation. This GDD is the design target; the four combat-feel open questions are answered empirically in the prototype.

## Overview

The Combo System is the first of the two active skill systems: it tracks consecutive landing auto-attacks (the `combo_counter`, 0 → 1 → 2) and converts the 3rd landing swing into a **Finisher** — a stronger, larger-area attack whose shape is the selected general's signature geometry (Guan Yu ring, Zhao Yun cone, Lu Bu point-burst). The Combo System owns the counter state, the Finisher trigger and execution, the reset conditions (taking a real hit, or letting the chain time out), and the application of all Combo-lane upgrades (C1–C6). It consumes `SwingLanded` from Combat Core, reads Finisher geometry from General Characters, applies the elite stagger gate defined by Enemy Definition, and orients Zhao Yun's cone using Input Manager's directional recording.

The player experiences it as the core rhythm of the game: **hit, hit, FINISH.** Building to a Finisher is the constant micro-goal that creates the central tension — push in to land the 3rd hit, or back off to protect the chain.

## Player Fantasy

The Combo System is the feeling of a strike that *builds*. Two ordinary swings prime something, and the third is a release — the screen flowers into the general's geometry and a cluster of soldiers comes apart at once. The fantasy is **rhythm rewarded**: the player learns the cadence in their hands until "hit, hit, FINISH" is instinct, and the battlefield bends around the beat. Every Finisher is a small earned crescendo, and protecting the chain — refusing to take the hit that would reset it — is where positioning becomes artistry. The deepest expression is the Combo general who never drops below the beat: a continuous storm of finishers walking through formations.

## Detailed Design

### Core Rules

1. **Combo counter.** `combo_counter ∈ {0, 1, 2}`. Combat Core emits `SwingLanded` whenever an auto-attack lands ≥1 hit; the Combo System increments the counter by 1 on each `SwingLanded` (A4 multi-target swings count as 1, per Combat Core / upgrade-definition).

2. **Finisher trigger.** When `combo_counter == 2` and the next `SwingLanded` would occur, the Combo System fires a **Finisher** instead of a normal swing's effects: it resolves the general's Finisher AoE at the player's position, deals Finisher damage to all enemies in the shape, applies the elite stagger gate, then resets `combo_counter → 0` (or `→ 1` with C3 "Iron Discipline").

3. **Finisher geometry.** The AoE shape, radius, direction mode, and angle come from the selected general's data (General Characters): `Ring` (full circle), `Cone` (forward wedge, half-angle 45°, oriented by directional input), `PointBurst` (full circle + knockback). The Combo System is a consumer of this geometry, not an evaluator (per general-characters.md).

4. **Zhao Yun cone orientation.** For the Cone shape, direction = the most recent `LastSteerDirection` from Input Manager **if** `current_time − steer_time ≤ 300ms` (`zhao_yun_input_direction_window_ms`, owned by General Characters); else fall back to nearest-enemy direction; else (no enemies, no recent input) the canonical screen-forward `Vector3(0,0,1)` (matches general-characters AC-04/AC-09). This is the realisation of the `DirectionalInputBuffer` contract general-characters.md left open: Input Manager records, Combo applies the window.

5. **Finisher damage.** `effective_finisher_damage = finisher_damage × (1 + damage_delta_multiplier)`, where `finisher_damage` is the general's stored base (25), never mutated at runtime. **C1 "Heaven's Blow"** adds +0.6 to `damage_delta_multiplier` (→ 40 at base 25); **C2** adds +0.5 to `radius_delta_multiplier`. `damage_delta_multiplier` accumulates from Combo upgrades (0 at run start). *(Revised 2026-10-10 — GDD flag #4 resolved: delta model, no static overrides.)* The Combo System holds the `FinisherUpgradeDelta` struct and computes the final value at trigger time (general-characters forward dependency).

6. **Reset on damage.** The Combo System subscribes to `DamageTaken` (Damage & Health). On a `DamageTaken` whose `effective_damage ≥ reset_damage_threshold` (3) AND when `combo_reset_on_damage == true` (per-general default, may be overridden), `combo_counter → 0`. Sub-threshold grazes do not reset (matches damage-health.md Rule 4). A successful parry never resets the counter; **P3 "Deflect and Strike"** advances it (`combo_counter = min(combo_counter+1, 2)` on a parry that blocked an attack).

7. **Reset on timeout.** If `combo_counter > 0` and no `SwingLanded` occurs for `combo_counter_timeout` seconds (default 3.0), `combo_counter → 0`. C3's reset-to-1 also starts this timer.

8. **Finisher availability.** There is no separate Finisher cooldown in MVP: `finisher_cooldown_ms = 0`. Finisher rate is gated purely by rebuilding the 3-hit combo. (C3's draw condition and C3+P3 loop in upgrade-definition.md reference this value; 0 means availability is combo-gated only.)

9. **Elite stagger gate (read-only contract).** A Finisher landing on Iron Captain / Warlord staggers them **only at `combo_counter == 2`** (full combo); at `combo_counter < 2` it deals `× stagger_resist_multiplier` and no stagger. This rule is owned by Enemy Definition; the Combo System passes `combo_counter` at Finisher resolution so Enemy AI can apply it. C4 "Thunder Descent" staggers basic infantry only — it never bypasses the elite gate.

10. **Combo upgrade effects (C1–C6).** Applied at pick time from Upgrade Definition: C1 damage override 40; C2 radius override (`finisher_radius` 4.5u); C3 reset-to-1 flag; C4 infantry-stagger flag; C5 `finisher_heal_per_target` (+5 HP/target, capped by `finisher_max_targets`); C6 chain strike (nearest within 3u at half damage, classified as a non-Finisher hit for stagger purposes). The Combo System owns `finisher_max_targets` (default 8) bounding C5/C6 output.

### States and Transitions

| State | combo_counter | Entered When | Exits To |
|---|---|---|---|
| **Idle** | 0 | Run start; after Finisher (no C3); after reset (damage ≥ threshold or timeout) | Building (on `SwingLanded`) |
| **Building (1)** | 1 | 1st landing swing; or C3 Finisher; or P3 from 0 | Building(2) / Idle (timeout/damage) |
| **Building (2 — primed)** | 2 | 2nd landing swing; or P3 from 1 | Finisher (next SwingLanded) / Idle (timeout/damage) |
| **Finisher** | — | 3rd landing swing while primed | Idle (reset→0) or Building(1) (C3 reset→1) |

The Finisher state is transient (resolves within the frame it triggers); the visual flourish is cosmetic overlay (VFX & Audio).

### Interactions with Other Systems

| System | Data In | Data Out | Interface Owner |
|---|---|---|---|
| **Combat Core** | `SwingLanded` signal; melee geometry | Finisher trigger (suppresses the 3rd normal swing); `combo_counter` for knockback gate | Combat Core owns swing; Combo owns counter + Finisher |
| **General Characters** | Finisher shape/radius/angle/direction-mode/damage; KnockbackForce/Radius (Lu Bu) | None | General Characters owns geometry |
| **Input Manager** | `LastSteerDirection {direction, steer_time}` for cone | None | Input Manager owns raw recording; Combo applies 300ms window |
| **Damage & Health** | `DamageTaken` (for reset); applies C5 heal to player HP | Finisher damage events to enemies (via Combat Core / Enemy AI) | Damage & Health owns player HP + DamageTaken |
| **Parry System** | `combo_advance_on_parry` (P3) signal on successful parry | `combo_counter` value (read by Parry for elite burst gate) | Parry owns parry; Combo owns counter |
| **Enemy Definition / Enemy AI** | Elite type for stagger gate | `combo_counter` at Finisher resolution; Finisher/C4 stagger requests | Enemy Definition owns stagger rules |
| **Upgrade Definition** | C1–C6 deltas + `finisher_max_targets` at pick time | None | Upgrade Definition specifies; Combo applies |

## Formulas

### 1. Effective Finisher Damage

`effective_finisher_damage = finisher_damage × (1 + damage_delta_multiplier)`

| Variable | Type | Range | Description |
|---|---|---|---|
| `finisher_damage` | float | 25 base (stored, never mutated) | General's Finisher damage (base = `base_attack × 2.5`) |
| `damage_delta_multiplier` | float | 0.0–`finisher_damage_delta_cap` (0.6); default 0; C1 adds 0.6 | Accumulated Combo-upgrade delta |

**Output:** 25 (base) to 60 (C1 40 × 1.5 cap). **Example:** base 25, +0.0 delta → 25; C1 override 40, +0.0 → 40.

### 2. Effective Finisher Radius

`effective_finisher_radius = finisher_radius × (1 + radius_delta_multiplier)`

| Variable | Type | Range | Description |
|---|---|---|---|
| `finisher_radius` | float (u) | 3.0 base; C2 → 4.5 | General's Finisher AoE radius (`base_radius × 2.0`) |
| `radius_delta_multiplier` | float | 0.0–`finisher_radius_delta_cap` (0.5); default 0 | Accumulated radius delta |

**Example:** C2 override 4.5u, +0.0 → 4.5u.

### 3. C5 Finisher Heal

`finisher_heal = min(targets_hit, finisher_max_targets) × finisher_heal_per_target`

| Variable | Type | Range | Description |
|---|---|---|---|
| `targets_hit` | int | 0–200 | Enemies the Finisher connected with |
| `finisher_max_targets` | int | 5–15; default 8 | Cap on counted targets (owned here) |
| `finisher_heal_per_target` | int (HP) | 3–6; default 5 (C5) | HP healed per counted target |

**Output:** 0 to 40 HP (8 × 5). **Example:** Finisher hits 10 enemies with C5 → `min(10,8) × 5 = 40 HP` (subject to `max_hp`).

## Edge Cases

- **If `combo_counter == 2` and the priming chain times out before the 3rd swing:** Counter resets to 0; the Finisher is lost. The player must rebuild.
- **If the player takes a sub-threshold graze (`effective_damage < 3`) at `combo_counter == 2`:** No reset (damage-health threshold rule); the Finisher remains primed.
- **If C3 resets `combo_counter → 1` and no enemy is in range:** Counter holds at 1; the `combo_counter_timeout` timer begins (matches upgrade-definition C3 edge case).
- **If a Finisher fires with zero enemies in the AoE:** It resolves harmlessly (counter still resets per C3/normal); C5 heals 0; C6 chain does not fire. No error.
- **If P3 fires a parry at `combo_counter == 2`:** `min(2+1, 2) = 2` — no change; the parry burst still fires (upgrade-definition P3).
- **If a Finisher kills an elite mid-stagger-setup at `combo_counter == 2`:** The Finisher staggers per the gate; if HP reaches 0 the enemy dies (Enemy AI Dead priority) and the counter-hit is discarded (enemy-definition edge case).
- **If C6 chain strike kills an enemy and the player has A6:** The chain is a non-Finisher hit but is **not an auto-attack**, so it does NOT trigger A6's cooldown reset (A6 is auto-attack-kill only).

## Dependencies

| System | Direction | Nature | Interface |
|---|---|---|---|
| **Combat Core** | Bidirectional | Hard | Consumes `SwingLanded`; suppresses the 3rd normal swing to fire a Finisher; provides `combo_counter` for the knockback gate. |
| **General Characters** | General Characters → Combo | Hard | Reads Finisher geometry + damage; reuses Ring/Cone/PointBurst hit detection. |
| **Input Manager** | Input Manager → Combo | Hard | Reads `LastSteerDirection`; applies the 300ms staleness window for Zhao Yun's cone. |
| **Damage & Health** | Bidirectional | Hard | Subscribes to `DamageTaken` (reset); sends C5 heal to player HP. |
| **Parry System** | Bidirectional | Hard | Receives P3 combo-advance; exposes `combo_counter` for the elite burst gate. |
| **Enemy Definition / Enemy AI** | Combo → Enemy | Hard | Passes `combo_counter` at Finisher resolution; requests Finisher/C4 stagger; respects the elite full-combo gate. |
| **Upgrade Definition** | Upgrade → Combo | Hard | Applies C1–C6 + `finisher_max_targets` at pick time. Combo owns `finisher_cooldown_ms` (0) and `combo_counter_timeout`. |

## Tuning Knobs

| Knob | Default | Safe Range | Affects |
|---|---|---|---|
| `combo_counter_timeout` | 3.0s | 1.5–6.0s | Seconds without a landing swing before the chain resets; below 1.5s punishes repositioning, above 6s makes the chain nearly permanent |
| `finisher_cooldown_ms` | 0 | 0 only (MVP) | No separate Finisher cooldown; rate-limited by combo rebuild |
| `finisher_max_targets` | 8 | 5–15 | Caps C5 heal and C6 chain reach in dense waves (owned here; also referenced by Upgrade Definition) |
| `finisher_damage_delta_cap` | 0.6 | 0.3–1.0 | Max accumulated Combo damage delta. Raised 0.5 → 0.6 on 2026-10-10 so C1's +0.6 delta (flag #4 delta model) is not clipped and preserves the 40-damage balance point |
| `finisher_radius_delta_cap` | 0.5 | 0.3–1.0 | Max accumulated Combo radius delta |

*Referenced, not owned: `finisher_damage`/`finisher_radius`/geometry (General Characters); `reset_damage_threshold`, `combo_reset_on_damage` (Damage & Health); C1–C6 values (Upgrade Definition); `zhao_yun_input_direction_window_ms` (General Characters).*

## Visual/Audio Requirements

The Finisher is **the hero effect** (game-concept Visual Identity Anchor). Requirements (full spec in VFX & Audio GDD): the 1→2→FINISH escalation must be unmistakable visually (combo counter UI pulse) and audibly (hit → hit → CRASH); each general's Finisher uses its distinct geometry VFX (Ring/Cone/PointBurst must read as different moves — general-characters mandates Ring ≠ PointBurst assets); the Finisher flash is a radial starburst (shape-distinguishable from the parry ring, per game-concept colorblind rule). Combo counter reset must play a visible "break" animation (counter shatters) to teach the damage-reset rule without a tooltip.

## UI Requirements

The Combo System drives the **combo counter HUD** (owned by Combat Feedback UI): a visible 1 → 2 → FINISH indicator that pulses on each increment and shatters on reset, readable at 200 enemies (Pillar 1). This GDD mandates the counter exists and its state transitions; layout is owned by Combat Feedback UI.

## Acceptance Criteria

| # | Criterion | Story Type | Gate |
|---|---|---|---|
| AC-CB-01 | **GIVEN** `combo_counter = 0`, **WHEN** two consecutive `SwingLanded` fire, **THEN** `combo_counter == 2`; **WHEN** a 3rd `SwingLanded` fires, **THEN** a Finisher resolves and `combo_counter` resets to 0 (no C3). | Logic | BLOCKING |
| AC-CB-02 | **GIVEN** general = Guan Yu (Ring) at `combo_counter = 2`, **WHEN** the Finisher fires, **THEN** it deals `effective_finisher_damage` (25 at base) in a 3.0u full-circle. | Logic | BLOCKING |
| AC-CB-03 | **GIVEN** C1 held, **WHEN** a Finisher fires, **THEN** `damage_delta_multiplier == 0.6` and `effective_finisher_damage == 40` (25 × 1.6, delta model); the general's stored `FinisherDamage` is still 25. | Logic | BLOCKING |
| AC-CB-04 | **GIVEN** `combo_counter = 2` and a `DamageTaken` with `effective_damage = 5` (≥ threshold 3, `combo_reset_on_damage = true`), **WHEN** it fires, **THEN** `combo_counter == 0`. **GIVEN** `effective_damage = 1` (< threshold), **THEN** `combo_counter == 2` (no reset). | Logic | BLOCKING |
| AC-CB-05 | **GIVEN** `combo_counter = 1` and no `SwingLanded` for `combo_counter_timeout` (3.0s), **WHEN** the timer expires, **THEN** `combo_counter == 0`. | Logic | BLOCKING |
| AC-CB-06 | **GIVEN** Zhao Yun, a `LastSteerDirection = (1,0)` at `steer_time`, and a Finisher at `current_time − steer_time = 0.25s` (< 0.3s), **THEN** the cone orients to `(1,0,0)`. **GIVEN** the input is 0.5s stale and one enemy at `(4,0,0)`, **THEN** the cone orients toward that enemy. | Logic | BLOCKING |
| AC-CB-07 | **GIVEN** C3 held, **WHEN** a Finisher lands, **THEN** `combo_counter == 1` (not 0) and the timeout timer restarts. | Logic | BLOCKING |
| AC-CB-08 | **GIVEN** an Iron Captain and a Finisher at `combo_counter = 2`, **THEN** the Combo System passes `combo_counter = 2` so the stagger gate triggers; at `combo_counter < 2` it passes the lower value so no stagger occurs. | Integration | BLOCKING |
| AC-CB-09 | **GIVEN** C5 held and a Finisher hitting 10 enemies (`finisher_max_targets = 8`), **WHEN** it resolves, **THEN** player HP increases by `min(10,8) × 5 = 40` (capped at `max_hp`). | Integration | BLOCKING |

## Open Questions

| # | Question | Owner | Resolution Path |
|---|---|---|---|
| OQ-01 | Does the combo counter reset feel fair when the player takes damage mid-combo? | Prototype | The reset_damage_threshold (3) already softens trivial grazes; confirm the "real hit erases chain" feels fair, not punishing, in the prototype. |
| OQ-02 | Does layering combo on top of parry feel like depth or overwhelm? | Prototype/Playtest | First playtest — the central core-loop hypothesis. |
| OQ-03 | Is `combo_counter_timeout = 3.0s` right? Too short punishes repositioning; too long makes the chain free. | Prototype | Tune in prototype against `base_attack_interval`. |
| OQ-04 | Does the Zhao Yun cone fallback (nearest-enemy when input is stale) override player intent badly? | Prototype | Validate; general-characters OQ-02 flags the same concern. |

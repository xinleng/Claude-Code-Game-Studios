# Combat Core

> **Status**: Approved (self-review 2026-06-19)
> **Author**: xinleng + Claude Code agents
> **Last Updated**: 2026-06-19
> **Implements Pillar**: Pillar 1 (Readable Chaos), Pillar 2 (Skill Has a Lane)

## Overview

Combat Core is the auto-attack engine and player locomotion layer. It owns the 30-second loop tempo: the player character moves (reading `CurrentMoveDirection` from Input Manager) and automatically attacks the nearest enemy within melee range on a fixed interval. Combat Core resolves hit detection (which enemies a swing strikes), applies auto-attack damage through the Damage pipeline, advances the combo counter on each landing swing (consumed by the Combo System), and applies Lu Bu's PointBurst knockback impulse when the Combo System requests it. It owns no Finisher logic (Combo System), no parry logic (Parry System), and no enemy behaviour (Enemy AI) — it is the baseline "always-on" output that the two active skill systems layer on top of.

The player never configures Combat Core; they feel it as the steady rhythm of the general's blade. It is the floor of competence: even a player who never parries and never times a combo is auto-attacking, moving, and killing infantry. Everything skilful is an amplification of this baseline.

## Player Fantasy

Combat Core is the feeling of a weapon that never stops. The general doesn't fish for individual hits — the blade is always working, carving the nearest threat while the player's mind is free for the decisions that matter: where to stand, when to push, when to parry. The fantasy is *effortless lethality as a baseline* — your hands move the general through the horde and the killing takes care of itself, so the moments you *do* spend a deliberate input (a combo finish, a parry) feel like rising above an already-capable floor. A musou general wading through soldiers: the wading is automatic; the artistry is positioning and timing.

## Detailed Design

### Core Rules

1. **Auto-attack loop.** Combat Core fires an auto-attack whenever (a) at least one enemy is within `melee_range` AND (b) `attack_cooldown` has elapsed since the last swing. No manual attack input exists. If no enemy is in range, no swing fires and the cooldown timer does not advance toward a wasted swing (the next swing is available immediately once an enemy enters range and the interval has passed).

2. **Target selection.** Each swing targets the **nearest** enemy by Euclidean XZ distance to the player. Hit detection then strikes all enemies within `melee_range` up to `max_targets` (default 1; A4 "Cleaving Stance" raises it to 3), nearest-first.

3. **Attack interval.** `attack_cooldown` after each swing = `base_attack_interval × attack_interval_multiplier` (A2 "Relentless Press" sets the multiplier to 0.75). Default `base_attack_interval = 1.0s`.

4. **Hit detection.** A swing uses a layer-filtered `Physics.OverlapSphereNonAlloc` centred on the player, radius `melee_range`, into a pre-allocated buffer sized to `max_enemy_count` (200). The allocating `OverlapSphere` overload is forbidden (matches enemy-definition.md Implementation Contract #3).

5. **Damage application.** Each struck enemy takes `base_attack` damage (the general's `BaseAttack`, default 10; A1 "Sharpened Edge" overrides to 14). Combat Core forwards damage to the enemy's HP ledger (Enemy AI / Enemy Definition own enemy HP); it does not compute stagger or resist — basic infantry take full auto-attack damage, elites are unaffected by auto-attacks for stagger purposes (only Finishers/parry bursts stagger them, per enemy-definition.md).

6. **Combo counter advance.** A swing that **lands at least one hit** raises `combo_counter` by exactly 1 (A4 multi-target swings still advance by 1 regardless of targets struck — matches upgrade-definition A4). A swing that hits nothing does not advance the counter. The Combo System owns `combo_counter` state; Combat Core emits a `SwingLanded` signal it consumes. The 3rd landing swing triggers a Finisher (Combo System) instead of a normal swing.

7. **Player movement.** Each frame, `player.position += CurrentMoveDirection.normalized × move_speed × Time.deltaTime`, where `move_speed = base_move_speed × move_speed_multiplier` (A5 "Rushing Stream" applies a transient ×1.25 for 0.5s on hit). Movement and auto-attack are independent — the player attacks while moving. **Dodge override:** while the Dodge System is mid-dash, it overrides this locomotion and Combat Core suppresses auto-attack for the dash duration (see dodge-system.md); normal movement and auto-attack resume when the dash ends.

8. **A6 cooldown reset on auto-kill.** If an auto-attack swing's damage reduces an enemy to 0 HP and the player has A6 "Relentless Strike", the next swing becomes available immediately — `attack_cooldown` is set to `attack_cooldown_minimum` (not zero). Simultaneous kills from one A4 swing count as **one** A6 trigger (matches upgrade-definition A6).

9. **Lu Bu knockback application.** When the Combo System resolves a PointBurst Finisher (Lu Bu), it passes `KnockbackForce` and `KnockbackRadius` (from General Characters). Combat Core applies a radial impulse to all enemies within `KnockbackRadius`, directed away from the player. **Elite gate:** for Iron Captain / Warlord targets, knockback applies only when `combo_counter == 2`; suppress otherwise (matches enemy-definition.md / general-characters.md). Basic infantry have no knockback resist.

10. **No combat semantics beyond baseline.** Combat Core does not know parry windows, momentum, telegraphs, or upgrade catalogue internals — it receives resolved stat values (`base_attack`, `melee_range`, `attack_interval_multiplier`, `max_targets`, `move_speed_multiplier`, `attack_cooldown_reset_on_kill`) at upgrade-apply time and applies them.

### States and Transitions

Combat Core has no formal state machine for the player; it is a continuous tick with two independent sub-systems:

| Sub-system | Per-frame behaviour |
|---|---|
| **Locomotion** | Read `CurrentMoveDirection`; integrate position by `move_speed × dt`; apply/expire A5 transient speed buff |
| **Auto-attack** | Decrement cooldown; if cooldown ≤ 0 and an enemy is in `melee_range`, fire a swing → hit detection → damage → `SwingLanded` if any hit → reset cooldown (or to `attack_cooldown_minimum` on A6 kill) |

There is no attack "wind-up" state in MVP — swings resolve on the frame they fire (visual swing animation is cosmetic, owned by VFX & Audio).

### Interactions with Other Systems

| System | Data In | Data Out | Interface Owner |
|---|---|---|---|
| **Input Manager** | `CurrentMoveDirection` for locomotion | None | Input Manager owns capture |
| **General Characters** | `BaseAttack`, `KnockbackForce`/`KnockbackRadius` (Lu Bu) | None | General Characters owns the data |
| **Combo System** | Finisher trigger + Knockback params on the 3rd swing; reads `SwingLanded` to advance `combo_counter` | `SwingLanded` signal; melee range geometry for Finisher reuse | Combo owns Finisher; Combat Core owns the swing/hit + knockback application |
| **Enemy Definition / Enemy AI** | Enemy positions + HP for targeting and damage | Applies auto-attack damage; reads enemy elite type for knockback gate | Enemy AI owns enemy HP/state |
| **Damage & Health** | — | — (player damage flows through Parry → Damage&Health, not Combat Core) | — |
| **Upgrade Definition** | `base_attack`(A1), `attack_interval_multiplier`(A2), `melee_range`/`base_radius`(A3), `max_targets`(A4), `move_speed_on_hit_multiplier`+duration(A5), `attack_cooldown_reset_on_kill`(A6) at pick time | None | Upgrade Definition specifies deltas; Combat Core applies |

## Formulas

### 1. Attack Interval

`attack_cooldown = base_attack_interval × attack_interval_multiplier`

| Variable | Type | Range | Description |
|---|---|---|---|
| `base_attack_interval` | float (s) | 0.5–2.0; default 1.0 | Seconds between auto-attacks at baseline |
| `attack_interval_multiplier` | float | 0.6–1.0; default 1.0 (A2 → 0.75) | Speed multiplier from upgrades |

**Output:** 0.6–1.0s at default + A2. **Example:** A2 installed → `1.0 × 0.75 = 0.75s` between swings.

### 2. Melee Hit Radius

`melee_range = base_radius × base_radius_multiplier`

| Variable | Type | Range | Description |
|---|---|---|---|
| `base_radius` | float (u) | 0.8–2.5; default 1.5 | Base hit radius (owned by General Characters) |
| `base_radius_multiplier` | float | 1.0–1.5; default 1.0 (A3 → 1.5) | A3 "Long Reach" override (2.25u effective) |

**Example:** A3 installed → `1.5 × 1.5 = 2.25u`.

### 3. Player Movement Step

`Δposition = CurrentMoveDirection.normalized × base_move_speed × move_speed_multiplier × Δt`

| Variable | Type | Range | Description |
|---|---|---|---|
| `base_move_speed` | float (u/s) | 3.0–8.0; default 5.0 | Player locomotion speed |
| `move_speed_multiplier` | float | 1.0–1.5; default 1.0 (A5 → 1.25 transient) | A5 on-hit speed buff |
| `Δt` | float | >0 | `Time.deltaTime` |

**Example:** moving with A5 active → `5.0 × 1.25 = 6.25 u/s`.

## Edge Cases

- **If no enemy is within `melee_range`:** No swing fires; cooldown holds at ready so the first swing on a new target is immediate (subject to the interval having elapsed). `combo_counter` is unaffected by the absence of targets (Combo System owns its own decay timer).
- **If the 2nd (combo-priming) swing kills its target and no other enemy is in range:** The swing still landed, so `combo_counter` advances to 2; the Finisher is primed and fires on the next landing swing against any enemy (matches game-concept combo edge case).
- **If an A4 multi-target swing strikes 3 enemies:** Each takes full `base_attack`; `combo_counter` advances by exactly 1; if A6 is held and ≥1 enemy dies, that is one A6 trigger.
- **If the player stands still (`CurrentMoveDirection == 0`):** Position is unchanged; auto-attack continues normally against in-range enemies.
- **If a Finisher would fire but the target died between target-selection and resolution:** The Finisher fires at the player's position using its AoE geometry (it is positional, not target-locked); it simply may hit fewer enemies. No error.
- **If `attack_cooldown_minimum` (A6) is set ≥ `base_attack_interval`:** The A6 reset has no effect (cooldown floor ≥ normal interval). Designer-warn at load if so.
- **If two enemies are exactly equidistant for nearest-target selection:** Tie-break by lower entity index (matches general-characters cone tie-break convention) for deterministic tests.

## Dependencies

| System | Direction | Nature | Interface |
|---|---|---|---|
| **Input Manager** | Combat Core → Input Manager | Hard | Reads `CurrentMoveDirection` (never raw devices). |
| **General Characters** | General Characters → Combat Core | Hard | Reads `BaseAttack`, `KnockbackForce`, `KnockbackRadius`. Combat Core applies knockback. |
| **Combo System** | Bidirectional | Hard | Combat Core emits `SwingLanded`; Combo advances `combo_counter` and triggers Finisher on the 3rd; Combat Core applies the PointBurst knockback Combo requests. |
| **Enemy AI / Enemy Definition** | Combat Core → Enemy | Hard | Reads enemy positions/HP/elite-type for targeting, damage, and the knockback elite gate. Applies auto-attack damage to enemy HP. |
| **Dodge System** | Dodge → Combat Core | Hard | During a dash, Dodge overrides Combat Core locomotion and Combat Core suppresses auto-attack for the dash duration. |
| **Upgrade Definition** | Upgrade Definition → Combat Core | Hard | Applies A1–A6 deltas at pick time. Combat Core owns `attack_cooldown_minimum`. |

## Tuning Knobs

| Knob | Default | Safe Range | Affects |
|---|---|---|---|
| `base_attack_interval` | 1.0s | 0.5–2.0s | Auto-attack tempo; below 0.5s makes swings unreadable, above 2.0s feels sluggish |
| `base_move_speed` | 5.0 u/s | 3.0–8.0 | Player locomotion; below 3 can't escape packs, above 8 trivialises positioning |
| `attack_cooldown_minimum` | 0.2s | 0.1–0.4s | A6 reset floor; prevents infinite attack chains in dense waves |
| `melee_range` base | = `base_radius` (1.5u) | 0.8–2.5u | Auto-attack reach; owned via `base_radius` (General Characters) |
| `max_enemy_count` (buffer size) | 200 | 100–300 | OverlapSphere result buffer; must be ≥ peak on-screen enemies |

*Referenced, not owned: `base_attack`, `base_radius`, `KnockbackForce`, `KnockbackRadius` (General Characters); A1–A6 deltas (Upgrade Definition).*

## Visual/Audio Requirements

Combat Core owns the auto-attack swing cadence; VFX & Audio GDD owns the actual swing animation, hit spark, and the 1→2→FINISH audio escalation (the 3rd-swing Finisher is the hero effect, owned jointly with Combo System). Combat Core mandates only: every landing swing must produce a hit response (spark + sound) distinct from a whiff, so the player can read whether a swing connected. Lu Bu knockback must visibly displace enemies (owned by VFX & Audio per general-characters.md).

## Acceptance Criteria

| # | Criterion | Story Type | Gate |
|---|---|---|---|
| AC-CC-01 | **GIVEN** one enemy within `melee_range` and cooldown ready, **WHEN** the tick runs, **THEN** a swing fires dealing `base_attack` (default 10) to that enemy and `combo_counter` advances by 1. | Logic | BLOCKING |
| AC-CC-02 | **GIVEN** no enemy within `melee_range`, **WHEN** the tick runs, **THEN** no swing fires and `combo_counter` is unchanged. | Logic | BLOCKING |
| AC-CC-03 | **GIVEN** `attack_interval_multiplier = 0.75` (A2), **WHEN** a swing fires, **THEN** the next swing is available after 0.75s (assert against config, not a hardcoded constant). | Logic | BLOCKING |
| AC-CC-04 | **GIVEN** A4 active (`max_targets = 3`) and 4 enemies in range, **WHEN** a swing fires, **THEN** exactly 3 nearest enemies take `base_attack` and `combo_counter` advances by exactly 1. | Logic | BLOCKING |
| AC-CC-05 | **GIVEN** A6 active and a swing kills its target, **WHEN** the kill resolves, **THEN** `attack_cooldown == attack_cooldown_minimum` (not 0). **GIVEN** A6 and a Finisher kill (not auto-attack), **THEN** cooldown is NOT reset. | Logic | BLOCKING |
| AC-CC-06 | **GIVEN** `CurrentMoveDirection = (1,0)`, `base_move_speed = 5`, `Δt = 0.1`, no A5, **WHEN** locomotion ticks, **THEN** player X increases by 0.5 (±0.001). | Logic | BLOCKING |
| AC-CC-07 | **GIVEN** Lu Bu PointBurst Finisher at `combo_counter = 2` and an Iron Captain in `KnockbackRadius`, **WHEN** resolved, **THEN** Combat Core applies the knockback impulse. **GIVEN** the same at `combo_counter < 2`, **THEN** knockback is NOT applied to the Iron Captain (assert via spy on `ApplyKnockback`). | Logic | BLOCKING |
| AC-CC-08 | **GIVEN** the 2nd consecutive landing swing brings `combo_counter` to 2, **WHEN** the next landing swing occurs, **THEN** the Combo System fires a Finisher rather than a normal swing (Combat Core defers to Combo on the 3rd). | Integration | BLOCKING |

## Open Questions

| # | Question | Owner | Resolution Path |
|---|---|---|---|
| OQ-01 | Is `base_move_speed = 5.0 u/s` correct relative to enemy speeds (Levy 1.5, Warlord P2 2.0)? | Prototype | Validate kiting/positioning feel in the combat prototype; tune against enemy move speeds. |
| OQ-02 | Should auto-attack target nearest enemy, or lowest-HP / most-threatening? MVP = nearest (simplest, most readable). | Playtest | Confirm nearest-target reads as fair; revisit only if it feels like the general "ignores" the real threat. |
| OQ-03 | `base_attack_interval = 1.0s` — does the 1→2→FINISH cadence feel good at this rate? | Prototype | Tune alongside Combo System; the cadence is core to combo feel. |

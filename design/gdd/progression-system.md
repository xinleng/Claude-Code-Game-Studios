# Progression System

> **Status**: Approved (self-review 2026-06-19)
> **Author**: xinleng + Claude Code agents
> **Last Updated**: 2026-06-19
> **Implements Pillar**: Pillar 2 (Skill Has a Lane), Pillar 3 (Snappy Decisions)

## Overview

The Progression System is the run's growth engine: it accumulates XP from enemy kills, fires level-up events on a curve calibrated to ~12 level-ups per 20-minute run, and on each level-up **draws 3 upgrade options from the eligible pool** and presents them for the player's pick. It owns the XP curve and `xp_scalar`, the level thresholds, and all *draw logic* — drought weighting, the C3 draw condition, the Modifier pick cap, and category exhaustion. It reads `xp_weight` per enemy type from Enemy Definition and the upgrade catalogue + draw rules from Upgrade Definition; it does not own upgrade *content* (Upgrade Definition) or the pick *screen* (Upgrade Selection UI). On retry it resets XP, level, the eligible pool, drought streaks, and the Modifier pick count.

The player experiences it as the rhythm of growth — the meter fills, the screen offers three readable choices, and the run's identity takes shape one pick at a time.

## Player Fantasy

The Progression System is the feeling of a legend taking shape under pressure. The XP bar climbs as the general carves through the army, and at each threshold the battlefield offers a fork: three choices, one line each, *who are you becoming this run?* The fantasy is **earned identity** — not a build planned in advance, but one discovered in the moment the battlefield hands you the option you needed. The meter filling is the drumbeat of momentum; the pick is the punctuation. By the 12th level-up the run has a name — a Combo run, a Parry run, *your* run — written in the choices made while an army bore down.

## Detailed Design

### Core Rules

1. **XP accumulation.** Each enemy death emits `xp_drop = xp_weight[type] × xp_scalar` (Enemy Definition owns `xp_weight`; Progression owns `xp_scalar`). XP accumulates toward the next level threshold.

2. **Level-up.** When cumulative XP since the last level reaches `xp_to_next(level)`, a level-up fires: the level increments, a draw is triggered, and the pick screen is requested (Run Session Manager pauses the run; Input Manager switches to the Menu context). Overflow XP carries to the next level.

3. **Level curve.** `xp_to_next(level) = round(xp_base × xp_growth^(level−1))`. The curve is front-loaded so most level-ups land in the first 10 minutes (game-concept: ~12 per run, weighted early). `xp_scalar` is the global calibration knob tuned so the kill rate produces ~12 level-ups over 20 minutes.

4. **Draw of 3.** On each level-up, draw **3 unique options without replacement** from the **eligible pool** (Upgrade Definition's `total_catalogue` of 24 minus picked / held-out / capped upgrades; starts at 23 with C3 held out). Apply drought weighting to the draw. The player picks 1; the 2 unpicked options return to the eligible pool; the picked one is permanently removed (Upgrade Definition pick rules).

5. **Drought weighting.** If a skill lane (Combo/Parry/Auto-attack) goes `drought_weighting_threshold` (4) consecutive draws unrepresented, its eligible upgrades get `drought_weighting_multiplier` (2×) draw weight on the next draw until the lane appears (picked or not). Tracked independently per lane; Modifiers never participate (Upgrade Definition Formula). A fully-exhausted lane is excluded from drought tracking.

6. **C3 draw condition.** C3 "Iron Discipline" enters the eligible pool only when `combo_picks ≥ 2 AND combo_picks ≥ max(parry_picks, auto_attack_picks)` at draw evaluation time (Upgrade Definition). Progression evaluates this each draw and adds/holds C3 accordingly.

7. **Modifier pick cap.** A player may pick at most `modifier_pick_cap` (3) Modifiers per run. After the 3rd Modifier pick, Progression removes all remaining Modifiers from the eligible pool (Upgrade Definition).

8. **Category exhaustion.** When all 6 upgrades of a lane (or all remaining Modifiers after the cap) are removed, that category no longer appears in draws (Upgrade Definition).

9. **Pick application.** The picked upgrade's stat deltas are applied immediately by the owning system (Upgrade Definition routes C-deltas to Combo, A-deltas to Combat Core, P/S6 to Parry, S-deltas to Damage & Health). Progression triggers the application; it does not compute the stat effects.

10. **Reset on retry.** On retry, Run Session Manager calls Progression's reset: XP → 0, level → 0, eligible pool → 24 (C3 re-held by its condition → 23 drawable), drought streaks → 0, `modifier_pick_count` → 0. Downstream stat resets are each system's own (Upgrade Definition / Damage & Health).

### States and Transitions

| State | Behaviour | Exits To |
|---|---|---|
| **Accruing** | Accumulate XP from kills; check threshold each kill | Leveling (threshold reached) |
| **Leveling** | Trigger draw of 3; request pick screen (run pauses); await pick | Accruing (pick confirmed, deltas applied) / Pool-Exhausted |
| **Pool-Exhausted** | Eligible pool empty (unreachable in a normal 24-upgrade / ~12-pick run); level-ups show a "Pool exhausted" notice and grant no pick | Accruing |

### Interactions with Other Systems

| System | Data In | Data Out | Interface Owner |
|---|---|---|---|
| **Enemy Definition / Enemy AI** | `xp_weight` per type; XP emitted on Dead | None | Enemy Definition owns `xp_weight` |
| **Upgrade Definition** | catalogue, categories, draw rules, C3 condition, Modifier cap | None | Upgrade Definition owns pool content + rules; Progression owns draw execution |
| **Upgrade Selection UI** | — | The 3 drawn options (Name + Description); receives the pick | Upgrade Selection UI owns layout |
| **Combo / Combat Core / Parry / Damage & Health** | — | Triggers stat-delta application of the picked upgrade | Each system owns its deltas |
| **Run Session Manager** | reset call on retry; pauses run during pick | Requests pause + pick screen on level-up | Run Session Manager owns run flow |

## Formulas

### 1. XP Drop (referenced)

`xp_drop = xp_weight[enemy_type] × xp_scalar` — `xp_weight` owned by Enemy Definition (Levy 1, Pikeman 2, Archer 2, Iron Captain 15, Warlord 50). Progression owns `xp_scalar`.

| Variable | Type | Range | Description |
|---|---|---|---|
| `xp_scalar` | float | > 0; default 1.0 | Global calibration multiplier (tuned to hit ~12 level-ups) |

**Guard:** validate `xp_scalar > 0` at run start — `0` silently zeroes all XP (enemy-definition guard).

### 2. Level Threshold Curve

`xp_to_next(level) = round(xp_base × xp_growth^(level − 1))`

| Variable | Type | Range | Description |
|---|---|---|---|
| `level` | int | 1–~12 | Current level (1-indexed) |
| `xp_base` | float | 5–20; default 10 | XP for level 1 |
| `xp_growth` | float | 1.1–1.4; default 1.22 | Per-level growth (front-loads early levels) |

**Output (defaults):** L1 = 10, L2 = 12, L3 = 15, L4 = 18, … L12 ≈ 73. Cumulative ≈ 430 XP-units to reach level 12. **Calibration:** `xp_scalar` is then tuned so the run's total kill XP reaches ~430 by the 20-minute mark, weighted to the first 10 minutes. Final values are empirical (see OQ-01).

### 3. Draw Weight (referenced — owned by Upgrade Definition)

`weight(upgrade) = drought_weighting_multiplier if its lane drought_streak ≥ drought_weighting_threshold, else 1`; `draw_probability = weight / Σ(weights in eligible pool)`. Modifiers excluded from drought tracking.

## Edge Cases

- **If the eligible pool has fewer than 3 upgrades at draw time:** Draw all remaining (1 or 2); the pick screen shows fewer than 3 (Upgrade Definition edge case). Drought weighting still applies to remaining lane upgrades.
- **If a level-up occurs during a Finisher animation:** The pick screen queues until the animation completes; the 3 options are locked at the moment the level-up triggered, not when the screen appears (Upgrade Definition / game-concept).
- **If two level-ups would trigger in the same frame (e.g., a Warlord kill grants 50 XP crossing two thresholds):** Resolve them sequentially — fire the first draw, apply the pick, then re-check the threshold for the second. Never present two pick hands simultaneously.
- **If the C3 draw condition becomes true mid-draw-evaluation:** C3 joins the eligible pool for that draw and all future draws until picked (Upgrade Definition C3 edge case).
- **If the player reaches the Modifier cap (3 picks):** Remaining Modifiers leave the eligible pool; future draws are lane-only; drought weighting continues for lanes.
- **If `xp_scalar` is misconfigured to 0:** The run-start guard logs a designer error; no XP accrues (total progression failure surfaced, not silent).

## Dependencies

| System | Direction | Nature | Interface |
|---|---|---|---|
| **Enemy Definition / Enemy AI** | Enemy → Progression | Hard | Reads `xp_weight`; receives XP on enemy Dead. |
| **Upgrade Definition** | Upgrade Definition → Progression | Hard | Reads catalogue, categories, draw rules, C3 condition, Modifier cap. Progression executes the draw logic Upgrade Definition specifies. |
| **Upgrade Selection UI** | Progression → UI | Hard | Sends the 3 drawn options; receives the pick. |
| **Combo / Combat Core / Parry / Damage & Health** | Progression → systems | Hard | Triggers stat-delta application on pick. |
| **Run Session Manager** | Bidirectional | Hard | Requests pause + pick screen on level-up; receives reset on retry. |

## Tuning Knobs

| Knob | Default | Safe Range | Affects |
|---|---|---|---|
| `xp_scalar` | 1.0 | > 0 | Global XP multiplier; the primary lever to hit the ~12-level-up target |
| `xp_base` | 10 | 5–20 | Level-1 XP requirement |
| `xp_growth` | 1.22 | 1.1–1.4 | Per-level growth; above 1.4 starves late-run level-ups, below 1.1 floods them |
| `target_level_ups_per_run` | 12 | 8–18 | Design target the curve + scalar are calibrated to |

*Referenced, not owned: `xp_weight` per type (Enemy Definition); `drought_weighting_threshold`/`drought_weighting_multiplier`/`modifier_pick_cap`/C3 condition (Upgrade Definition).*

## Visual/Audio Requirements

Progression owns no VFX directly. It mandates: an XP bar that visibly fills on kills (owned by Run UI) and a clear, satisfying level-up moment (sound + visual) that cues the upcoming pick. The pick screen itself is owned by Upgrade Selection UI.

## UI Requirements

Progression drives the **XP bar** (Run UI) and triggers the **upgrade pick screen** (Upgrade Selection UI). This GDD mandates: the XP bar reflects current XP vs `xp_to_next`; the level-up triggers the pick flow (run paused). Layout is owned by the UI GDDs.

> **📌 UX Flag — Progression System**: The upgrade pick screen has UI requirements. Run `/ux-design` for the upgrade-selection screen before writing its stories.

## Acceptance Criteria

| # | Criterion | Story Type | Gate |
|---|---|---|---|
| AC-PG-01 | **GIVEN** `xp_scalar = 1.0` and an Iron Captain dies, **THEN** `xp_drop = 15` is added to XP. **GIVEN** cumulative XP reaches `xp_to_next(level)`, **WHEN** the next kill XP lands, **THEN** a level-up fires and overflow carries forward. | Logic | BLOCKING |
| AC-PG-02 | **GIVEN** a level-up, **WHEN** the draw runs, **THEN** 3 unique, not-yet-picked upgrades are drawn from the eligible pool. | Logic | BLOCKING |
| AC-PG-03 | **GIVEN** the player picks an option, **WHEN** confirmed, **THEN** it is removed from the eligible pool, the other 2 return, and the owning system applies its delta. | Integration | BLOCKING |
| AC-PG-04 | **GIVEN** Combo has gone 4 consecutive draws unrepresented, **WHEN** the 5th draw runs, **THEN** remaining Combo upgrades carry `drought_weighting_multiplier` (2×) weight; **WHEN** a Combo upgrade next appears, **THEN** weights normalize. | Logic | BLOCKING |
| AC-PG-05 | **GIVEN** `combo_picks = 1`, **WHEN** a draw runs, **THEN** C3 is absent. **GIVEN** `combo_picks = 2 AND ≥ max(parry, auto)`, **THEN** C3 is eligible. | Logic | BLOCKING |
| AC-PG-06 | **GIVEN** 3 Modifier upgrades picked, **WHEN** the next draw runs, **THEN** no Modifier appears. | Logic | BLOCKING |
| AC-PG-07 | **GIVEN** a Warlord kill grants XP crossing two level thresholds in one frame, **WHEN** processed, **THEN** two level-ups resolve sequentially (one pick at a time), never two hands at once. | Logic | BLOCKING |
| AC-PG-08 | **GIVEN** retry, **WHEN** reset fires, **THEN** XP = 0, level = 0, eligible pool = 23 (C3 held), drought streaks = 0, `modifier_pick_count` = 0. | Integration | BLOCKING |

## Open Questions

| # | Question | Owner | Resolution Path |
|---|---|---|---|
| OQ-01 | Do `xp_base = 10`, `xp_growth = 1.22`, `xp_scalar = 1.0` actually produce ~12 level-ups weighted to the first 10 minutes given the wave kill curve? | Balance/Prototype | Simulate against the Wave Spawner kill rate or measure in prototype; tune `xp_scalar` to hit the target. |
| OQ-02 | Is front-loaded pacing (most level-ups early) the right feel, or should level-ups spread evenly? | Playtest | Validate; front-loading gives early build identity but a quiet late game. |
| OQ-03 | Does the run pause cleanly for the pick screen without breaking the momentum loop (Pillar 3 snappy decisions)? | Playtest | Confirm the pause + 3-option pick feels fast, not interruptive. |

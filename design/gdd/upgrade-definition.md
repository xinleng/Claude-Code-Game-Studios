# Upgrade Definition

> **Status**: Approved — 2026-06-18
> **Author**: xinleng + Claude Code agents
> **Last Updated**: 2026-06-18
> **Implements Pillar**: Pillar 2 (Skill Has a Lane), Pillar 3 (Snappy Decisions)

## Overview

Upgrade Definition is Dynasty Survivors' run identity system. It is the complete catalogue of **24 upgrades** — 6 per skill lane (Combo, Parry, Auto-attack) plus 6 cross-lane Modifiers — together with the rules that govern how upgrades are drawn, presented, and applied. No game logic for drawing upgrades lives in this document — that belongs to the Progression System. No display logic lives here — that belongs to the Upgrade Selection UI. What lives here is: what every upgrade is called, what it does in one line of plain text, what stat it modifies and by how much, and what constraints govern its interaction with the rest of the game.

The draw system uses soft drought weighting: after 4 consecutive draws with no representation from a specific lane, that lane receives 2× draw probability until it next appears. Players can pick at most 3 Modifier upgrades per run — Modifiers supplement active-lane builds but do not replace them.

The player never sees the Upgrade Definition system directly. What they see is the three-option pick screen after every level-up — three readable choices, each a single line, each an answer to the question "what kind of general do I want to be this run?" Lean Combo and your Finishers become devastating. Lean Parry and your momentum meter becomes a force multiplier. Lean Auto-attack and your constant output grinds through anything. Modifiers adapt to any build — they improve how long you last, not which lane you fight from. The run identity that emerges from 12 level-ups across a 20-minute session — that is what Upgrade Definition owns, even though the player reads it as the Progression System's three-card hand.

## Player Fantasy

Every level-up is a fork in the road where the general you're becoming reveals itself. You didn't plan to be a Combo general — you became one, because that's the line the battlefield offered and that's the line you took. By the time the third pick lands and your finishers start tearing through ranks in formation, the run has a name: this is a Guan Yu run, this is a Parry run, this is *your* legend, written in the choices you made under pressure with an army bearing down.

Upgrade picks are not resource management — they are self-knowledge. Each one asks a single question: "Is this who I am this run?" Three options. One line. A clear yes or no. There is no analysis paralysis because the decision is already half-made — the battlefield has been showing you which lane works for the last two minutes. The pick screen only asks you to name it.

When the run ends, the upgrade list scrolls past like a campaign chronicle: the Combo path that turned a single sword into a finishing storm, or the Parry timing that turned every counter-hit into a second wind. The death screen doesn't just tell you how long you lasted. It tells you what kind of general you were. That's the hook to the next run.

## Detailed Design

### Core Rules

**1. Upgrade pool structure**

The upgrade pool is a flat collection of **24 unique upgrades** across 3 skill lanes and 1 modifier type. Each skill lane (Combo, Parry, Auto-attack) contains **6 upgrades**. The Modifier type contains **6 cross-lane defensive and utility upgrades**. The `eligible_generals` field on each upgrade defaults to `all` — reserved for post-MVP general-specific upgrades. In MVP, all 24 upgrades are available to all generals.

Lane upgrades define run identity: concentrating picks in a single lane builds a recognizable archetype. Modifier upgrades supplement any build without defining it — they improve survivability and create cross-lane synergy opportunities, subject to the Modifier pick cap below.

**Lane accessibility note:** The Auto-attack lane is the passive/entry-level option — all 6 Auto-attack upgrades require zero active player input. It is designed for players who prefer positioning decisions over timing inputs. Elite encounters (Iron Captain, Warlord) are the designed dual-system checkpoints that stress-test pure Auto-attack builds; outside of elite encounters, Auto-attack is a fully viable passive lane. This asymmetry with the other lanes is intentional.

Each upgrade has:
- **Name** — 2-3 words; Three Kingdoms flavor
- **Description** — exactly one line of plain text; the entire player-facing content
- **Category** — Combo | Parry | Auto-attack | Modifier
- **Stat effect** — precise, implementable stat modification
- **eligible_generals** — default: `all`; post-MVP override field

**2. Upgrade pick rules**

- **Two distinct collections govern draws.** The **total catalogue** is the fixed set of all **24 upgrades** — it never changes size during a run and is used only for content/coverage assertions. The **eligible pool** is the subset of the catalogue that is currently *drawable*: it excludes upgrades already picked, upgrades removed by category exhaustion or the Modifier cap, and any upgrade currently held out by an unmet draw condition (only C3 in MVP). Drought weighting and the 3-option draw operate exclusively on the eligible pool.
- Each upgrade is a **unique one-time pick**. Stacking is not implemented in MVP.
- The eligible pool **begins the run with 23 upgrades** — all 24 in the catalogue except C3, which is held out by its draw condition until `combo_picks ≥ 2 AND combo_picks ≥ max(parry_picks, auto_attack_picks)` (see C3 in the catalogue). C3 joins the eligible pool the moment that condition is met.
- On each level-up, the Progression System draws **3 unique options** from the eligible pool without duplicates in the hand.
- **The player picks 1.** The 2 unpicked options **return to the eligible pool** — unchosen upgrades can appear in future hands.
- The 1 picked upgrade is **permanently removed** from the eligible pool (and is no longer drawable from the catalogue).
- **Category exhaustion:** When all 6 upgrades in a category (or all remaining Modifiers after the cap is reached) are removed, that category no longer appears in future draws.
- **Modifier pick cap:** A player may pick at most 3 Modifier upgrades per run. After the 3rd Modifier pick, all remaining Modifier upgrades are removed from the eligible pool for the remainder of the run. This prevents passive Modifier stacking from substituting for active-lane engagement.

**3. Draw weight**

Under normal conditions, all upgrades in the eligible pool have **equal draw probability**. One exception applies: drought weighting.

**Drought weighting:** If 4 consecutive draws have contained no eligible upgrade from a specific skill lane, that lane's eligible upgrades receive **2× draw weight** for the next draw only. Weight normalizes back to 1× once any upgrade from the drought lane appears in a draw (whether picked or not). Drought weighting tracks independently per lane (Combo, Parry, Auto-attack). Modifier upgrades do not participate in drought tracking — they are never counted toward a lane's drought streak, and they never receive drought weighting themselves. A lane whose upgrades are fully exhausted (all 6 picked) is excluded from drought tracking — it has no eligible upgrades to weight, so its streak is irrelevant and never produces a draw effect.

*Example:* If Combo has appeared in no draw for 4 consecutive level-ups, all remaining Combo upgrades have 2× weight on the 5th draw. As soon as any Combo upgrade appears in a drawn hand, all weights return to 1× regardless of whether it was picked.

**4. Upgrade application**

Upgrades are applied **immediately** when picked. Effects are permanent for the run duration. Upgrades that modify the same stat are additive (e.g., S2 + S3 both modify `damage_reduction`: +0.25 + 0.25 = +0.50 combined). The Damage & Health system enforces all stat caps at upgrade-apply time; Upgrade Definition does not need to track cumulative totals.

**5. Display constraint (Pillar 3: Snappy Decisions)**

Each upgrade must be fully described in one line of plain text. No tooltip is shown. The description must be understandable without reading any other upgrade or GDD. If a description requires qualification ("requires S2"), the upgrade design is too complex for MVP.

---

### Upgrade Catalogue

All values are prototype defaults — all are tuning knobs.

| ID | Name | Description (player-facing, one line) | Stat Effect | Category |
|----|------|----------------------------------------|-------------|----------|
| C1 | **Heaven's Blow** | Your Finisher hits harder. | `damage_delta_multiplier += 0.6` at pick time (delta model — the general's stored `FinisherDamage` is never mutated; Combo applies `FinisherDamage × (1 + delta)`). At the base `finisher_damage = 25` this yields **40**, matching the pre-2026-10-10 balance. Any change requires matching updates to Enemy Definition Formula 2 and AC-UD-11. Does not interact with `base_attack`. *(Revised 2026-10-10 — GDD flag #4 resolved by user: delta multipliers, not static overrides.)* | Combo |
| C2 | **Dragon's Reach** | Your Finisher sweeps a wider area. | `radius_delta_multiplier += 0.5` (delta model). At the base `finisher_radius = 3.0u` this yields **4.5u**. *(Revised 2026-10-10 — flag #4.)* | Combo |
| C3 | **Iron Discipline** | Landing your Finisher immediately restores your combo chain. | On Finisher land: `combo_counter → 1` (not 0; next auto-attack begins building to Finisher immediately). **Draw condition:** C3 enters the eligible pool only when `combo_picks ≥ 2 AND combo_picks ≥ max(parry_picks, auto_attack_picks)` at draw time. Until eligible, C3 is held out of the pool but not removed — once the condition is met, C3 joins the eligible pool normally. | Combo |
| C4 | **Thunder Descent** | Your Finisher staggers all standard infantry it strikes. | Finisher applies 400ms stagger to ALL basic infantry hit (cosmetic freeze; they cannot attack or move during stagger). Elite stagger (Iron Captain, Warlord) is NOT affected — the full-combo gate from Enemy Definition GDD still applies regardless of C4. | Combo |
| C5 | **Heaven's Gate** | Your Finisher heals you for each enemy it strikes. | `finisher_heal_per_target: +5 HP` — applied immediately for each target the Finisher animation connects with. | Combo |
| C6 | **Tiger's Chain** | Your Finisher chains to the nearest enemy, striking again at half power. | `finisher_chain_enabled: true`; after primary Finisher hits, auto-targets the nearest enemy within 3u of any primary target; `finisher_chain_damage: floor(finisher_damage × 0.5)`. Chain does not independently trigger C3 (combo reset) or C4 (stagger) — only the primary Finisher hit activates those effects. **The C6 chain strike is classified as a non-Finisher auto-attack hit for all enemy stagger and resist formula purposes. It does not qualify as a 'Finisher landing' for Iron Captain or Warlord stagger gates, regardless of damage value.** | Combo |
| P1 | **Iron Will** | Your parry burst hits harder and farther. | Parry burst `damage_multiplier × 1.5`, `radius_multiplier × 1.5` | Parry |
| P2 | **River of Momentum** | Each successful parry fills your momentum meter faster. | Momentum gain per parry `× 1.5` | Parry |
| P3 | **Deflect and Strike** | A successful parry charges your next combo hit. | On successful parry (a parry input within `parry_window` that blocked an incoming attack, per Parry System GDD — does NOT trigger on parry inputs with no incoming attack): `combo_counter = min(combo_counter + 1, 2)`. If already at 2, no change. | Parry |
| P4 | **Unbroken Form** | Taking a hit only halves your momentum, not empties it. | `momentum_reset_multiplier: 1.0 → 0.5`. Parry System owns `momentum_reset_multiplier`. Damage & Health fires a momentum-reset event on DamageTaken; Parry System applies this multiplier when processing that event. P4 delivers the new value to Parry System at pick time. | Parry |
| P5 | **Crane's Eye** | Your parry window is wider, giving you more time to counter. | `parry_window_extension: +75ms` (added to the base `parry_window` defined in Parry System GDD, §Tuning Knobs). *Provisional: 75ms must be validated against the base window once Parry System GDD is authored; adjust if base < 200ms.* | Parry |
| P6 | **Second Wind** | Triggering your momentum burst at full meter restores 15 HP. | `momentum_burst_heal_at_max: +15 HP` — applies when burst fires with `momentum_meter == max_momentum` at trigger time; healed once per max-momentum burst event, subject to a minimum cooldown of `burst_heal_cooldown_p6` (default 10s) between heals. Does not trigger on partial-meter bursts. | Parry |
| A1 | **Sharpened Edge** | Your attacks deal more damage. | `base_attack: 10 → 14` | Auto-attack |
| A2 | **Relentless Press** | Your attack speed increases. | Attack interval `× 0.75` (33% faster; e.g., 1.0s interval → 0.75s) | Auto-attack |
| A3 | **Long Reach** | Your attacks hit enemies farther away. | `base_radius: 1.5u → 2.25u` | Auto-attack |
| A4 | **Cleaving Stance** | Your attacks hit up to 3 enemies at once. | Auto-attack becomes multi-target: up to 3 enemies per swing (nearest-first priority within base_radius). Multi-target swings increment `combo_counter` by 1 per swing, regardless of how many enemies are struck. Each struck enemy takes full auto-attack damage. | Auto-attack |
| A5 | **Rushing Stream** | Each auto-attack increases your movement speed briefly. | On auto-attack hit: `move_speed_multiplier × 1.25 for 0.5s`. Refreshes on each hit; does not stack (repeated hits reset the 0.5s window, not multiply the bonus). | Auto-attack |
| A6 | **Relentless Strike** | An auto-attack that kills an enemy immediately resets your attack cooldown. | `on_auto_kill: attack_cooldown_reset: true` — if the killing blow is from an auto-attack, the next auto-attack is available immediately (cooldown reduced to the `attack_cooldown_minimum` defined in Combat Core GDD, not to zero). Applies once per kill event: simultaneous kills from a single A4 multi-target swing count as **1 A6 trigger event**, not multiple. | Auto-attack |
| S1 | **Veteran's Constitution** | You can take more hits. | `max_hp_bonus: +40` → `max_hp: 100 → 140` | Modifier |
| S2 | **Battle Hardened** | Incoming damage is reduced. | `damage_reduction: +0.25` (from 0.0 at run start) | Modifier |
| S3 | **Iron Skin** | Incoming damage is further reduced. | `damage_reduction: +0.25` (cumulative with S2: 0.50 max from both; `damage_reduction_cap` of 0.75 enforced at apply time) | Modifier |
| S4 | **Veteran's Resilience** | Your HP slowly recovers when you're not taking damage. | `regen_rate: +1.0 HP/s` — active only when `time_since_last_damage ≥ regen_activation_delay_s4` (default 3.0s); deactivates immediately on DamageTaken. `regen_rate_max = 1.5 HP/s` enforced at apply time. Designed for recovery between wave pressure moments, not sustained in-combat healing. | Modifier |
| S5 | **Battle Hunger** | Defeating 5 enemies quickly restores 10 HP. | `kill_streak_heal_threshold: 5 kills within 5s → +10 HP`; heal fires once when threshold is reached; streak count resets to 0 after each heal event. Counts all kill sources (auto-attack, Finisher, parry burst). Designed for sustained in-combat healing. | Modifier |
| S6 | **Iron Stance** | When your momentum meter is above half, you take reduced damage. | `damage_reduction_at_half_momentum: +0.10` — conditional: active when `momentum_meter ≥ max_momentum × 0.5`; additive with S2/S3 reductions within `damage_reduction_cap = 0.75`. Reevaluated at DamageTaken time (see Edge Cases for required ADR on event ordering). | Modifier |

---

**Cumulative constraint validation (at extreme specialization):**

| Scenario | Cumulative effect | Notes |
|---|---|---|
| All 6 Modifier (impossible in MVP — cap is 3 picks) | N/A | Modifier pick cap of 3 prevents this scenario |
| S2+S3+S6 (3 Modifier picks — at cap) at ≥50% momentum | damage_reduction = 0.25+0.25+0.10 = 0.60 | ✅ Below cap (0.75) |
| S2+S3+S6+P4 vs. Levy Soldier (standard 5 dmg) | effective_damage = floor(5 × 0.40) = 2; no momentum reset fires (2 < threshold 3); S6 stays permanently active | ⚠️ Levy waves are HP-neutral at this build. This is the intended maximum-defense scenario. If playtesting reveals this is too passive, lever: raise `reset_damage_threshold` to 4 in Damage & Health GDD. |
| S2+S3 vs. Levy Soldier | effective_damage = floor(5 × 0.5) = 2 | ⚠️ Same reset immunity without S6. Acknowledged risk — see OQ-02. |
| S2+S3 vs. Iron Captain standard (12 dmg) | effective_damage = floor(12 × 0.5) = 6 | ✅ Above reset_damage_threshold (3); elite attacks still bite. Standard attack (12 base damage, not counter-hit). |
| S2+S3+S6 vs. Iron Captain standard (12 dmg) | effective_damage = floor(12 × 0.40) = 4 | ✅ Above reset_damage_threshold; elite attacks still bite. |
| All 6 Combo (within pick budget) | finisher_damage=40, radius=4.5u, combo→1 on Finisher (draw-gated), all targets stagger, +5 HP heal per target, chain strike enabled | ✅ No cap violations; Finisher max_targets cap bounds C5 heal output |
| C1 + A1 | base_attack=14, effective_finisher_damage=40 (C1 delta applies to the stored FinisherDamage, not base_attack — no interaction) | ✅ Clean lane separation |
| C1 + C6 | finisher_damage=40, chain_damage=floor(40×0.5)=20 | ✅ Reasonable chain output |

**Lane viability floor (Pillar 2):** A player holding 4 or more upgrades in any single lane must be able to clear standard waves (not elite encounters) without using inputs from other lanes. This is a playtesting gate — defined in Acceptance Criteria.

---

### Interactions with Other Systems

| System | Data Flow | Interface Owner |
|---|---|---|
| **Progression System** | Draws 3 options from eligible pool on level-up; applies drought weighting; detects category exhaustion; enforces C3 draw condition (`combo_picks ≥ 2 AND combo_picks ≥ max(parry_picks, auto_attack_picks)`); removes Modifiers from pool after 3rd Modifier pick | Progression System owns draw logic; Upgrade Definition owns pool content, draw rules, C3 eligibility condition, and Modifier pick cap |
| **Upgrade Selection UI** | Displays drawn upgrade Name + Description; receives player pick | Upgrade Definition owns content; Upgrade Selection UI owns layout and interaction |
| **Damage & Health** | Receives at pick time: `max_hp_bonus` (S1), `damage_reduction` delta (S2, S3), `regen_rate` delta (S4), `kill_streak_heal_threshold` and `kill_streak_heal_amount` (S5). Enforces `damage_reduction_cap = 0.75` and `regen_rate_max = 1.5 HP/s`. Fires DamageTaken event consumed by Parry System for momentum-reset handling. | Damage & Health enforces caps; Upgrade Definition specifies the delta |
| **Combo System** | Receives at pick time: `damage_delta_multiplier += 0.6` (C1), `radius_delta_multiplier += 0.5` (C2) — delta model, flag #4 resolved 2026-10-10, `combo_counter_on_finisher_land = 1` flag (C3), `finisher_stagger_all = true` flag (C4), `finisher_heal_per_target` (C5), `finisher_chain_enabled` + `finisher_chain_damage_multiplier` (C6), `finisher_max_targets` cap. Must define `finisher_cooldown_ms` — Upgrade Definition's C3 draw condition and combined C3+P3 behavior depend on this value. | Combo System owns finisher execution; Upgrade Definition specifies modifications |
| **Combat Core** | Receives at pick time: `base_attack` override (A1), `attack_interval_multiplier` (A2), `base_radius` override (A3), `max_targets` override (A4), `move_speed_on_hit_multiplier` + `move_speed_on_hit_duration` (A5), `attack_cooldown_reset_on_kill` flag + A6 kill-event handling (A6). Combat Core owns `attack_cooldown_minimum` — A6 resets to this floor, not to zero. | Combat Core owns auto-attack execution; Upgrade Definition specifies modifications |
| **Parry System** | Receives at pick time: `burst_damage_multiplier` (P1), `burst_radius_multiplier` (P1), `momentum_gain_multiplier` (P2), `combo_advance_on_parry` flag (P3), `momentum_reset_multiplier` (P4), `parry_window_extension` (P5), `momentum_burst_heal_at_max` + `burst_heal_cooldown_p6` (P6), `damage_reduction_at_half_momentum` (S6). Parry System owns `momentum_reset_multiplier`; applies it when processing DamageTaken momentum-reset events from Damage & Health. | Parry System owns parry execution and momentum meter; Upgrade Definition specifies modifications |
| **Run Session Manager** | Run Session Manager calls Upgrade Definition's pool reset API on retry. Upgrade Definition resets the eligible pool to all 24 upgrades, Modifier pick count to 0, and drought streak counters to 0. Downstream stat resets are handled by each downstream system's own reset API. | |

## Formulas

Upgrade Definition is a data catalogue — formulas in downstream systems apply the stat modifications this GDD defines. Two formulas live here that govern upgrade application rules.

**Draw weight (drought protection):** Under normal conditions, each eligible upgrade has equal draw probability. Drought weighting modifies this:
```
weight(upgrade_i) = 2  if lane(upgrade_i) has drought_streak ≥ drought_weighting_threshold
                   1  otherwise

draw_probability(upgrade_i) = weight(upgrade_i) / Σ(weight(j) for all j in eligible pool)
```
Drought weighting applies per-lane independently. Modifier upgrades are excluded from drought tracking.

### 1. Cumulative Damage Reduction

When multiple upgrades affecting `damage_reduction` are applied (S2, S3, and conditionally S6), their deltas are summed and capped at `damage_reduction_cap` at apply time.

```
damage_reduction_effective = min(Σ(damage_reduction_deltas) + conditional_reduction, damage_reduction_cap)
```

| Variable | Symbol | Type | Range | Description |
|---|---|---|---|---|
| Damage reduction delta | `damage_reduction_delta` | float | 0.25 per upgrade (S2, S3) | Value each S2/S3 upgrade contributes; must be an exact binary fraction |
| Conditional reduction | `damage_reduction_at_half_momentum` | float | 0.10 (S6) | Active only when `momentum_meter ≥ max_momentum × 0.5` |
| Sum of deltas | `Σ(damage_reduction_deltas)` | float | 0.0–0.75 | Cumulative total before cap |
| Cap | `damage_reduction_cap` | float | 0.75 | Defined in Damage & Health GDD; enforced at upgrade-apply time |
| Effective reduction | `damage_reduction_effective` | float | 0.0–0.75 | Applied to Damage & Health's effective_damage formula |

**Output range:** [0.0, 0.75]. In MVP at maximum (S2 + S3 + S6 at ≥50% momentum, 3 Modifier picks — at cap): 0.60.
**Example:** Player picks S2 (+0.25) then S3 (+0.25): `min(0.25 + 0.25, 0.75) = 0.50`. With S6 active at ≥50% momentum: `min(0.50 + 0.10, 0.75) = 0.60`.
**Guard:** Damage & Health clamps to `damage_reduction_cap` and logs a designer error if any single delta would push the cumulative value above 0.75.

---

### 2. Cumulative Regen Rate

```
regen_rate_effective = min(Σ(regen_rate_deltas), regen_rate_max)
```

| Variable | Symbol | Type | Range | Description |
|---|---|---|---|---|
| Regen delta | `regen_rate_delta` | float | 1.0 HP/s | Value S4 contributes (active only when `time_since_last_damage ≥ regen_activation_delay_s4`) |
| Effective regen | `regen_rate_effective` | float | 0.0–1.5 HP/s | Applied in Damage & Health's regen formula |
| Cap | `regen_rate_max` | float | 1.5 HP/s | Defined in Damage & Health GDD; enforced at apply time |

**Output range:** [0.0, 1.5 HP/s]. In MVP with only S4 (when active): 1.0 HP/s.
**Guard:** Damage & Health enforces the cap and logs a designer error if cumulative application would exceed `regen_rate_max`.

---

### Cross-GDD Baseline Definitions

The following values are referenced by formulas in this GDD and owned by other systems. All owning GDDs are now authored (2026-06-19) — these values are defined, not pending:

| Value | Used In | Owning GDD | Defined Value |
|---|---|---|---|
| `max_momentum` | S6 condition, P2, P4, P6 | parry-system.md | 100 |
| `parry_window` base | P5 (+75ms extension) | parry-system.md | telegraph-derived (per-attack `TelegraphDurationMs`); no global value |
| `base_move_speed` | A5 (×1.25 multiplier) | combat-core.md | 5.0 u/s |
| `base_attack_interval` | A2 (×0.75), A6 (cooldown reset) | combat-core.md | 1.0s |
| `attack_cooldown_minimum` | A6 (cooldown reset floor) | combat-core.md | 0.2s |
| `finisher_cooldown_ms` | C3 draw condition context, C3+P3 combined behavior | combo-system.md | 0 (combo-gated, no separate cooldown) |
| `finisher_max_targets` | C5 heal cap, C6 chain bound | combo-system.md (owner) | 8 (referenced here, owned by Combo System) |

These values are provisional defaults pending prototype validation, but their owners and definitions are now established.

## Edge Cases

- **If a player picks S3 without having picked S2:** S3 still applies `+0.25 damage_reduction`. S3 is not gated on S2 — both add the same delta independently.

- **If a pick screen appears during a Finisher animation:** The Upgrade Selection UI queues the pick screen until the Finisher animation completes. The 3 drawn options are locked at the moment the level-up triggers, not at the moment the screen appears.

- **If the eligible pool reaches fewer than 3 upgrades at draw time:** The Progression System draws all remaining upgrades (1 or 2). The pick screen shows fewer than 3 options. Valid behavior — player still picks 1. Drought weighting still applies to whatever lane upgrades remain.

- **If the eligible pool is exhausted (0 upgrades remain):** No further upgrade picks are possible. Level-up events show a "Pool exhausted" notice. In MVP with 24 upgrades and ~12 level-ups, this is unreachable in a normal run.

- **If the player has reached the Modifier pick cap (3 picks):** All remaining Modifier upgrades are removed from the eligible pool. Future draws contain only lane upgrades. Drought weighting continues normally for lane upgrades.

- **If P3 (Deflect and Strike) activates when combo_counter is already 2:** `min(2 + 1, 2) = 2` — no change to counter. The parry burst still fires normally.

- **If C3 (Iron Discipline) resets combo_counter to 1 and all enemies in Finisher range are dead:** combo_counter remains at 1. Decay timer (Combo System GDD) begins.

- **If C3 draw condition is met mid-run (player reaches combo_picks = 2 and Combo is the leading or tied-leading lane):** C3 enters the eligible pool at the next draw evaluation. It is immediately available to appear in any future draw until picked or the run ends. (The condition uses `combo_picks ≥ max(parry_picks, auto_attack_picks)`, so an exact tie for the lead satisfies it.)

- **C3 + S2+S3 combination — combo never resets against Levy Soldiers:** C3 resets combo_counter to 1 on Finisher (not 0). S2+S3 reduces Levy Soldier damage to floor(5 × 0.5) = 2, below reset_damage_threshold = 3 — no combo reset fires. Combined: a C3+S2+S3 player against basic infantry has a combo counter that effectively never reaches 0 during a wave. Finisher availability is continuous; the "push in or hold back?" tension is diminished against basic infantry only. Iron Captain and Warlord hits still reset at S2+S3 (effective_damage = 6, above threshold). This interaction is intentional for a high-investment Combo+Modifier build.

- **If C6 (Tiger's Chain) finds no target within 3u of the primary target after Finisher:** The chain does not fire. Primary Finisher damage and effects apply normally. No error is logged.

- **If P6 (Second Wind) triggers but heal would exceed max_hp:** Heal is capped at max_hp. Excess discarded. The `burst_heal_cooldown_p6` timer still starts from this heal event.

- **If S5 (Battle Hunger) kill streak counter reaches 5 mid-Finisher (multi-target):** The heal triggers as soon as the 5th kill registers within the 5s window. Streak resets to 0 after the heal.

- **S6 event ordering — ADR required:** S6's condition (`momentum_meter ≥ max_momentum × 0.5`) is evaluated at DamageTaken time. The Parry System's momentum reset also fires on the same DamageTaken event. The execution order between these two subscribers is not defined in either GDD. **Intended behavior:** S6 condition is evaluated using `momentum_meter` at the moment DamageTaken fires, before the Parry System applies its reset. This means S6 applies to the hit that causes momentum to drop below 50%. An Architecture Decision Record must formalize this event execution order before C# implementation begins.

- **If S6 (Iron Stance) is active and momentum drops below 50% between hit events:** S6's damage reduction deactivates for subsequent hits. The condition is re-evaluated fresh on each DamageTaken event.

- **On retry:** Run Session Manager resets the eligible pool to all 24 upgrades, Modifier pick count to 0, and all lane drought streak counters to 0. All stat modifications are reverted via each downstream system's reset API.

## Dependencies

| System | Direction | Nature | Interface |
|---|---|---|---|
| **Progression System** | Upgrade Definition → Progression | Hard | Progression System owns draw logic, drought weighting, C3 draw condition evaluation, Modifier cap enforcement, and category exhaustion tracking. Reads the upgrade pool (category, eligible_generals, stat effect, draw_condition) from Upgrade Definition. Pool reset on retry triggered by Run Session Manager. |
| **Upgrade Selection UI** | Upgrade Definition → Upgrade Selection UI | Hard | UI reads Name + Description for display. Overflow (description > 1 line) is a content error — the upgrade must be redesigned. |
| **Damage & Health** | Upgrade Definition → Damage & Health | Hard | Applies at pick time: `max_hp_bonus` (S1), `damage_reduction` delta (S2, S3), `regen_rate` delta (S4), `kill_streak_heal_threshold` + `kill_streak_heal_amount` (S5). Enforces `damage_reduction_cap = 0.75` and `regen_rate_max = 1.5 HP/s`. Fires DamageTaken event consumed by Parry System. |
| **Combo System** | Upgrade Definition → Combo System | Hard | Applies at pick time: `damage_delta_multiplier += 0.6` (C1), `radius_delta_multiplier += 0.5` (C2) — delta model, flag #4 resolved 2026-10-10, `combo_counter_on_finisher_land = 1` flag (C3), `finisher_stagger_all = true` flag (C4), `finisher_heal_per_target` + `finisher_max_targets` cap (C5), `finisher_chain_enabled` + `finisher_chain_damage_multiplier` (C6). **Combo System must define `finisher_cooldown_ms`** — C3 draw condition context and C3+P3 combined behavior depend on this value. |
| **Combat Core** | Upgrade Definition → Combat Core | Hard | Applies at pick time: `base_attack` override (A1), `attack_interval_multiplier` (A2), `base_radius` override (A3), `max_targets` override (A4), `move_speed_on_hit_multiplier` + duration (A5), `attack_cooldown_reset_on_kill` flag (A6). **Combat Core owns `attack_cooldown_minimum`** — A6 resets to this floor, not to zero; value must be defined in Combat Core GDD. |
| **Parry System** | Upgrade Definition → Parry System | Hard | Applies at pick time: `burst_damage_multiplier` (P1), `burst_radius_multiplier` (P1), `momentum_gain_multiplier` (P2), `combo_advance_on_parry` flag (P3), `momentum_reset_multiplier` (P4), `parry_window_extension` (P5), `momentum_burst_heal_at_max` + `burst_heal_cooldown_p6` (P6), `damage_reduction_at_half_momentum` (S6). Owns `momentum_reset_multiplier` and event ordering ADR (see Edge Cases). |
| **Run Session Manager** | Run Session Manager → Upgrade Definition | Hard | Calls Upgrade Definition's pool reset API on retry: resets eligible pool to 24, Modifier pick count to 0, drought streak counters to 0. |

## Tuning Knobs

All values are prototype defaults. Change during playtesting — not during implementation.

| Knob | Default | Safe Range | Affects |
|---|---|---|---|
| `modifier_pick_cap` | 3 | 2–5 | Maximum Modifier upgrades per run; below 2 prevents any defensive layering; above 5 approaches old Survivability-lane problem |
| `drought_weighting_threshold` | 4 | 2–6 | Consecutive draws without a lane before 2× weight applies; below 2 approaches pity rule; above 6 may allow real lane starvation |
| `drought_weighting_multiplier` | 2× | 1.5×–3× | Weight boost on drought lane; above 3× creates near-guaranteed lane appearance and approaches pity behavior |
| `finisher_damage_delta_c1` | 0.6 | 0.2–1.0 | C1 damage delta (×1.6 → 40 at base 25); below 0.2 (≈30 at base) feels underwhelming. Must not exceed Combo's `finisher_damage_delta_cap` |
| `finisher_radius_delta_c2` | 0.5 | 0.17–1.0 | C2 radius delta (×1.5 → 4.5u at base 3.0u); above 1.0 (6.0u) trivializes dense packs. Must not exceed Combo's `finisher_radius_delta_cap` |
| `finisher_max_targets` | 8 | 5–15 | **Owned by combo-system.md** (referenced here). Maximum targets the Finisher AoE can hit; bounds C5 heal and C6 chain output in dense waves; above 15 at 200 enemies creates readability issues |
| `finisher_heal_per_target_c5` | 5 HP | 3–6 HP | C5 HP healed per Finisher target; above 6 HP/target at finisher_max_targets=8 approaches 48 HP per Finisher, enabling near-full-heal cycles in dense packs |
| `finisher_chain_damage_multiplier_c6` | 0.5 | 0.3–0.75 | C6 chain damage as fraction of finisher_damage; above 0.75 approaches full second Finisher |
| `finisher_chain_range_c6` | 3u | 2–4u | C6 range to chain target |
| `burst_multiplier_p1` | 1.5× | 1.2–2.0× | P1 parry burst damage AND radius multiplier |
| `momentum_gain_multiplier_p2` | 1.5× | 1.2–2.0× | P2 momentum gain per parry; above 2.0× enables full-momentum bursts after only 2-3 parries |
| `parry_window_extension_p5` | 75ms | 25–150ms | P5 window addition; must be validated against Parry System GDD base window when authored |
| `burst_heal_cooldown_p6` | 10s | 5–20s | Minimum seconds between P6 max-meter heals; prevents P6 from exceeding effective regen cap in sustained parry sessions |
| `momentum_burst_heal_p6` | 15 HP | 8–25 HP | P6 HP restored on max-meter burst |
| `base_attack_a1` | 14 | 12–18 | A1 base_attack override |
| `attack_interval_multiplier_a2` | 0.75× | 0.6–0.9× | A2 attack speed |
| `base_radius_a3` | 2.25u | 1.8–3.0u | A3 base_radius override |
| `max_targets_a4` | 3 | 2–5 | A4 multi-target cap |
| `move_speed_multiplier_a5` | 1.25× | 1.1–1.5× | A5 movement speed bonus per hit |
| `move_speed_duration_a5` | 0.5s | 0.25–0.9s | A5 duration of movement bonus; **permanent-buff threshold = `base_attack_interval × 0.75` (≈0.75s at base 1.0s) with A2 installed** — do not raise to or above this value |
| `attack_cooldown_minimum` | (owned by Combat Core GDD) | — | A6 resets to this floor, not to zero; prevents infinite attack chains in dense waves |
| `max_hp_bonus_s1` | 40 | 20–80 | S1 flat HP addition |
| `damage_reduction_delta_s2` | 0.25 | **0.25 only** | S2 reduction; must remain an exact binary fraction |
| `damage_reduction_delta_s3` | 0.25 | **0.25 only** | S3 reduction; same constraint; cumulative S2+S3 must not exceed `damage_reduction_cap = 0.75` |
| `regen_rate_delta_s4` | 1.0 HP/s | 0.5–1.4 HP/s | S4 regen rate when active; only applies when `time_since_last_damage ≥ regen_activation_delay_s4` |
| `regen_activation_delay_s4` | 3.0s | 1.5–5.0s | Seconds without damage before S4 regen activates; below 1.5s makes S4 behave like passive regen again; above 5.0s makes S4 rarely trigger in intense waves |
| `kill_streak_heal_s5` | 10 HP | 5–20 HP | S5 HP restored on 5-kill streak |
| `kill_streak_threshold_s5` | 5 kills | 3–8 kills | S5 kills required to trigger heal |
| `kill_streak_window_s5` | 5s | 3–8s | S5 time window for streak |
| `damage_reduction_at_half_momentum_s6` | 0.10 | 0.05–0.15 | S6 conditional reduction; current max stack (S2+S3+S6) = 0.60, leaving 0.15 margin to cap |

## Visual/Audio Requirements

[To be designed]

## Acceptance Criteria

| # | Criterion | Story Type | Gate |
|---|---|---|---|
| AC-UD-01 | **GIVEN** a new run begins, **WHEN** the run initializes, **THEN** the **total catalogue** contains exactly 24 upgrades (6 Combo, 6 Parry, 6 Auto-attack, 6 Modifier) and the **eligible pool** contains exactly 23 — the catalogue minus C3, which is held out by its unmet draw condition. **Pass condition:** assert `total_catalogue.size == 24`; assert catalogue count per lane == 6; assert catalogue Modifier count == 6; assert `eligible_pool.size == 23`; assert C3 ∈ catalogue AND C3 ∉ eligible_pool. | Logic | BLOCKING |
| AC-UD-02 | **GIVEN** the player levels up, **WHEN** 3 options are drawn, **THEN** all 3 options are unique and none have been previously picked this run. **Pass condition:** assert 3 distinct upgrade IDs; assert none are in the picked-upgrades set. | Logic | BLOCKING |
| AC-UD-03 | **GIVEN** the player picks S2 on the first level-up (C3 still held out), **WHEN** confirmed, **THEN** (a) S2 removed from the eligible pool, (b) the 2 unpicked options return to the eligible pool, (c) `DamageHealthSystem.DamageReduction == 0.25f`. **Pass condition:** assert `eligible_pool.size == 22` after pick (23 start − 1 picked, C3 still held out); assert both unpicked IDs ∈ eligible_pool; assert stat value. | Integration | BLOCKING |
| AC-UD-04 | **GIVEN** the player has picked 3 Modifier upgrades (the cap), **WHEN** the next draw occurs, **THEN** no Modifier upgrade appears in the 3-card hand. **Pass condition:** assert all 3 drawn upgrade IDs have `category ≠ Modifier`. | Logic | BLOCKING |
| AC-UD-05 | *(Removed 2026-05-29 — pity rule removed; flat draw pool with drought weighting is now authoritative. See OQ-03.)* | — | — |
| AC-UD-06 | **GIVEN** player picks S2 then S3, **WHEN** both applied, **THEN** `DamageHealthSystem.DamageReduction == 0.50f` and no designer error logged. | Logic | BLOCKING |
| AC-UD-07 | **GIVEN** player picks S4, **WHEN** applied AND `time_since_last_damage ≥ 3.0s`, **THEN** `DamageHealthSystem.RegenRate == 1.0f`. **Pass condition:** assert stat == 1.0f during regen-active window; assert stat == 0.0f immediately after DamageTaken. | Logic | BLOCKING |
| AC-UD-08 | **GIVEN** player has P3 AND `combo_counter == 1`, **WHEN** successful parry, **THEN** `ComboSystem.ComboCounter == 2` after parry resolves. | Integration | BLOCKING |
| AC-UD-09 | **GIVEN** player has P3 AND `combo_counter == 2`, **WHEN** successful parry, **THEN** counter remains 2. | Integration | BLOCKING |
| AC-UD-10 | **GIVEN** a player holding 4+ upgrades in a single lane and 0 in other lanes at wave step 5+ density, **WHEN** running a full standard wave without inputs from other lanes, **THEN** player survives (does not enter Dead state). **Pass condition:** documented playtest session record in `production/qa/evidence/` confirming survival. *(Playtest gate — requires full combat harness. Playtest record is mandatory; this AC is not waived by implementation logistics.)* | Integration | **BLOCKING** |
| AC-UD-11 | **GIVEN** run ends and retry reset fires, **WHEN** reset API is called, **THEN** pool resets to 24, Modifier pick count resets to 0, drought streaks reset to 0, AND downstream stats return to base: `DamageHealthSystem.MaxHp == 100`, `DamageHealthSystem.DamageReduction == 0.0f`, `ComboSystem.FinisherDamage == 25`, `ParrySystem.MomentumResetMultiplier == 1.0f`, `CombatCore.BaseAttack == 10`. | Integration | BLOCKING |
| AC-UD-12 | **GIVEN** player has C3 AND C6, **WHEN** Finisher connects, **THEN** (a) C3 resets `combo_counter` to 1, (b) C6 fires chain strike on nearest target within 3u, (c) chain does NOT reset combo_counter again. | Integration | BLOCKING |
| AC-UD-13 | **GIVEN** player has P3 AND no incoming attack present, **WHEN** parry input, **THEN** `combo_counter` does NOT increment. | Integration | BLOCKING |
| AC-UD-14 | **GIVEN** player has C3 + P3 (both eligible and picked), **WHEN** parry succeeds into Finisher into parry succeeds again, **THEN** counter sequence: parry→2, auto→Finisher, C3→1, parry→2, auto→Finisher, C3→1. *(ADVISORY until Parry System GDD and Combo System GDD are both marked Approved — the test harness requires both systems' APIs to be defined. Before those GDDs are authored, split into unit tests: P3 advance in isolation, C3 reset in isolation.)* | Integration | ADVISORY |
| AC-UD-15 | **GIVEN** player has A4 and swing hits 3 enemies, **WHEN** swing resolves, **THEN** `combo_counter` increments by exactly 1. *(Confirm with lead-programmer whether mock hit API exists; reclassify to Integration | BLOCKING if physics contact callback is the only trigger path.)* | Logic | BLOCKING |
| AC-UD-16 | **GIVEN** player picks C5, **WHEN** Finisher connects with 3 enemies, **THEN** `ComboSystem.FinisherHealPerTarget == 5` and player HP increases by exactly 15. **Pass condition:** assert stat == 5; assert HP delta == 15 for 3 targets. | Integration | BLOCKING |
| AC-UD-17 | **GIVEN** player has C6 (no C3), **WHEN** Finisher connects and a valid target is within 3u, **THEN** chain strike fires dealing `floor(finisher_damage × 0.5)` to the chain target. **Pass condition:** assert chain_damage == floor(finisher_damage × 0.5); assert chain fired independently of C3. | Integration | BLOCKING |
| AC-UD-18 | **GIVEN** player has C6 and no enemy is within 3u of any primary Finisher target, **WHEN** Finisher connects, **THEN** no chain strike fires and no error is logged. **Pass condition:** assert chain_fire_count == 0; assert error log empty. | Integration | BLOCKING |
| AC-UD-19 | **GIVEN** player picks P5, **WHEN** applied, **THEN** `ParrySystem.ParryWindow == base_parry_window + 75ms`. **Pass condition:** assert window increased by exactly 75ms vs. pre-pick value. | Logic | BLOCKING |
| AC-UD-20 | **GIVEN** player has P6 AND `momentum_meter == max_momentum`, **WHEN** burst fires, **THEN** player HP increases by 15. **GIVEN** player has P6 AND `momentum_meter < max_momentum`, **WHEN** burst fires, **THEN** player HP does NOT increase. **Pass condition:** assert heal fires exactly at max-momentum, not at partial. | Integration | BLOCKING |
| AC-UD-21 | **GIVEN** player has A5, **WHEN** auto-attack connects, **THEN** `CombatCore.MoveSpeedMultiplier == 1.25` for 0.5s, then returns to 1.0. **GIVEN** player has A5 AND hits twice within 0.5s, **THEN** multiplier remains 1.25 (no stack, timer refreshed). **Pass condition:** assert multiplier == 1.25 post-hit; assert multiplier == 1.0 after 0.5s with no further hits; assert multiplier == 1.25 (not 1.5625) on double-hit. | Logic | BLOCKING |
| AC-UD-22 | **GIVEN** player has A6 AND auto-attack kill occurs, **WHEN** kill registers, **THEN** `CombatCore.AttackCooldown == attack_cooldown_minimum`. **GIVEN** player has A6 AND Finisher kill occurs (not auto-attack), **THEN** cooldown is NOT reset. **Pass condition:** assert cooldown at floor on auto-attack kill; assert no reset on Finisher/burst kills. | Integration | BLOCKING |
| AC-UD-23 | **GIVEN** player has S5 AND 5 kills register within 5s, **WHEN** 5th kill fires, **THEN** player HP increases by 10 AND kill streak counter resets to 0. **GIVEN** S5 and 4 kills in 5s with no 5th, **THEN** no heal fires. **Pass condition:** assert heal on 5th kill; assert streak == 0 after heal; assert no heal at 4 kills. | Integration | BLOCKING |
| AC-UD-24 | **GIVEN** player has S6 AND `momentum_meter ≥ max_momentum × 0.5`, **WHEN** DamageTaken, **THEN** effective damage reduction includes S6's +0.10. **GIVEN** player has S6 AND `momentum_meter < max_momentum × 0.5`, **WHEN** DamageTaken, **THEN** S6's bonus is NOT applied. **Pass condition:** assert DR includes 0.10 bonus at ≥50% momentum; assert DR excludes 0.10 below 50%. | Integration | BLOCKING |
| AC-UD-25 | **GIVEN** player has picked all 6 Combo upgrades, **WHEN** next draw occurs, **THEN** no Combo upgrade appears in the 3-card hand. **Pass condition:** assert all 3 drawn upgrade IDs have `category ≠ Combo`. (Symmetric test must also pass for Parry and Auto-attack exhaustion.) | Logic | BLOCKING |
| AC-UD-26 | **GIVEN** player has combo_picks = 1 (Combo not yet leading by ≥2), **WHEN** draw occurs, **THEN** C3 does NOT appear in drawn options. **GIVEN** player has combo_picks = 2 AND combo_picks ≥ max(parry_picks, auto_attack_picks), **WHEN** draw occurs, **THEN** C3 IS eligible to appear. **Pass condition:** assert C3 absent in draw before condition met; assert C3 in eligible pool after condition met. | Logic | BLOCKING |
| AC-UD-27 | **GIVEN** player has picked S2, S3, AND S6 is active (≥50% momentum), **WHEN** DamageTaken fires, **THEN** `DamageHealthSystem.DamageReduction ≤ 0.75f` (cap respected) AND actual value == 0.60f. **Pass condition:** assert DR == 0.60f; assert no designer error logged. | Logic | BLOCKING |

## Open Questions

| # | Question | Owner | Resolution Path |
|---|---|---|---|
| OQ-01 | Does the lane viability floor hold? Can a pure Combo build (C1+C2+C3+C4+C5+C6, with C3 earned by leading Combo) survive a step-5 standard wave without parrying? | Playtest | Validate in the combat prototype. If not viable, buff Combo DPS or add a Combo defensive upgrade. |
| OQ-02 | Does the S2+S3 Levy Soldier reset immunity feel intentional or exploitable? At DR=0.50, Levy Soldiers deal 2 effective damage — below reset_damage_threshold. Heavy Modifier players can absorb Levy hits without losing their chain. | Playtest | Observe during first playtest. If too risk-free, raise `reset_damage_threshold` to 4. |
| OQ-03 | **Resolved (2026-05-29):** Pity rule removed. Flat draw pool with drought weighting (2× after 4 dry draws per lane) is now authoritative. | — | Closed |
| OQ-04 | Do general-specific upgrades belong in the Vertical Slice tier? The `eligible_generals` field is reserved. | V-Slice design | Re-evaluate when authoring the Vertical Slice upgrade expansion. |
| OQ-05 | **Resolved (2026-05-29):** C4 description updated to "staggers all standard infantry it strikes." Elite stagger remains gated on full-combo Finisher regardless of C4. | — | Closed |
| OQ-06 | Does the C3 draw condition (Combo must have ≥2 picks and be at least tied for the lead) feel natural or punishing? A player who wants C3 early cannot access it until they have committed to Combo. This is by design — C3 is the Combo-lane's depth upgrade, not a general-purpose pick. Monitor player feedback to see if the gate creates frustration or a satisfying "earned it" moment. | Playtest | If players feel locked out, lower the threshold to combo_picks ≥ 1. If players reach C3 too easily as part of Iron Cyclone, raise to combo_picks ≥ 3. |
| OQ-07 | Does the drought weighting (2× after 4 dry draws) feel natural or like the system is "helping" the player in a way that undermines the emergent-lane identity? | Playtest | Monitor runs where drought triggers. If players notice the weighting, it may be too strong — reduce multiplier to 1.5×. If lane starvation persists (~1-in-8 runs still feel lane-dry), lower drought threshold from 4 to 3. |
| OQ-08 | Is the Modifier pick cap of 3 restrictive enough to prevent turtle builds? At max 3 Modifier picks (S2+S3+S6 or S1+S2+S3), does the surviving DR (0.50–0.60) + HP+40 still enable a "too passive" survival loop against Levy Soldiers? | Playtest | If Modifier-capped builds still feel passive-dominant, lower cap to 2 or raise reset_damage_threshold. |
| OQ-09 | Does category exhaustion at pick 6 (lane fully explored) feel like a mastery moment or like running out of content? Consider adding a "lane mastered" UI callout on the 6th lane pick to reframe the remaining picks as secondary identity. | V-Slice | Evaluate in first playtests. Low-cost cosmetic change — prioritize if players report runs feeling flat after a dedicated lane commitment. |
| OQ-10 | **Resolved (2026-06-18, via /review-all-gdds):** game-concept.md synced to this GDD and damage-health.md. Updated: Content Volume + Scope Tiers (24 upgrades MVP, 3 lanes + Modifier), Upgrade Pool Draw section (drought weighting summary), Tuning Knobs (removed `pity_threshold`, added drought + modifier-cap knobs), Core Mechanics #5 / MVP req #4-5, AC-03/AC-05 (reset_damage_threshold rule), base-stat ownership pointer → General Characters, and Status → Approved. | — | Closed. See gdd-cross-review-2026-06-18.md. |
| OQ-11 | **Aggregate discrete-heal ceiling:** C5 (Finisher heal, up to `finisher_heal_per_target × finisher_max_targets` ≈ 40 HP/cast), S5 (+10 HP per 5-kill streak), and P6 (+15 HP per max-momentum burst, 10s cooldown) have no *combined* cap — only continuous regen (S4) is bounded by `regen_rate_max = 1.5 HP/s`. A cross-lane C5+P6+S5 build (legal under the Modifier pick cap) could sustain aggregate healing above that regen ceiling. Each source is individually bounded and gated behind active offensive play, so this is a watch-item, not a known defect. | Playtest | Observe aggregate effective HP/s in heavy-heal builds during first playtest. If sustained healing trivializes attrition, add a combined heal-per-second soft cap enforced by Damage & Health, or reduce per-source values. |

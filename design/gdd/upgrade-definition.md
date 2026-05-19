# Upgrade Definition

> **Status**: In Design
> **Author**: xinleng + Claude Code agents
> **Last Updated**: 2026-05-18
> **Implements Pillar**: Pillar 2 (Skill Has a Lane), Pillar 3 (Snappy Decisions)

## Overview

Upgrade Definition is Dynasty Survivors' run identity system. It is the complete catalogue of 15–18 upgrades available across the Combo, Parry, Auto-attack, and Survivability categories, together with the rules that govern how upgrades are drawn, presented, and applied. No game logic for drawing upgrades lives in this document — that belongs to the Progression System. No display logic lives here — that belongs to the Upgrade Selection UI. What lives here is: what every upgrade is called, what it does in one line of plain text, what stat it modifies and by how much, and what constraints govern its interaction with the rest of the game.

The player never sees the Upgrade Definition system directly. What they see is the three-option pick screen after every level-up — three readable choices, each a single line, each an answer to the question "what kind of general do I want to be this run?" Lean Combo and your Finishers become devastating. Lean Parry and your momentum meter becomes a force multiplier. Lean Survivability and the battlefield becomes a war of attrition you can outlast. Every upgrade pick either deepens a lane or hedges across them. The run identity that emerges from 12 level-ups across a 20-minute session — that is what Upgrade Definition owns, even though the player reads it as the Progression System's three-card hand.

## Player Fantasy

Every level-up is a fork in the road where the general you're becoming reveals itself. You didn't plan to be a Combo general — you became one, because that's the line the battlefield offered and that's the line you took. By the time the third pick lands and your finishers start tearing through ranks in formation, the run has a name: this is a Guan Yu run, this is a Parry run, this is *your* legend, written in the choices you made under pressure with an army bearing down.

Upgrade picks are not resource management — they are self-knowledge. Each one asks a single question: "Is this who I am this run?" Three options. One line. A clear yes or no. There is no analysis paralysis because the decision is already half-made — the battlefield has been showing you which lane works for the last two minutes. The pick screen only asks you to name it.

When the run ends, the upgrade list scrolls past like a campaign chronicle: the Combo path that turned a single sword into a finishing storm, or the Parry timing that turned every counter-hit into a second wind. The death screen doesn't just tell you how long you lasted. It tells you what kind of general you were. That's the hook to the next run.

## Detailed Design

### Core Rules

**1. Upgrade pool structure**

The upgrade pool is a flat collection of **16 unique upgrades** across 4 categories (4 per category). The `eligible_generals` field on each upgrade defaults to `all` — reserved for post-MVP general-specific upgrades. In MVP, all 16 upgrades are available to all generals.

Each upgrade has:
- **Name** — 2-3 words; Three Kingdoms flavor
- **Description** — exactly one line of plain text; the entire player-facing content
- **Category** — Combo | Parry | Auto-attack | Survivability
- **Stat effect** — precise, implementable stat modification
- **eligible_generals** — default: `all`; post-MVP override field

**2. Upgrade pick rules**

- Each upgrade is a **unique one-time pick**. Stacking is not implemented in MVP.
- The **eligible pool** begins the run with all 16 upgrades.
- On each level-up, the Progression System draws **3 unique options** from the eligible pool without duplicates in the hand.
- **The player picks 1.** The 2 unpicked options **return to the eligible pool** — unchosen upgrades can appear in future hands.
- The 1 picked upgrade is **permanently removed** from the eligible pool.
- **Category exhaustion:** When all 4 upgrades in a category are picked, that category no longer appears in future draws.

**3. Pity rule (guaranteed lane representation)**

After any 3 consecutive draws that contained no upgrade from the player's most-picked category (or the player's first pick, if category scores are tied): the **next draw must include at least 1 upgrade** from that category, drawn before the remaining 2 options are sampled randomly. The pity rule does not fire if the player's most-picked category is exhausted (all 4 picked).

`pity_threshold = 3` (default; tuning knob).

**4. Draw weight**

All upgrades in the eligible pool have **equal draw probability** — no category weighting. A flat pool ensures the run identity emerges from player choices, not from supply-side nudging toward any lane.

**5. Upgrade application**

Upgrades are applied **immediately** when picked. Effects are permanent for the run duration. Upgrades that modify the same stat are additive (e.g., S2 + S3 both modify `damage_reduction`: +0.25 + 0.25 = +0.50 combined). The Damage & Health system enforces all stat caps at upgrade-apply time; Upgrade Definition does not need to track cumulative totals.

**6. Display constraint (Pillar 3: Snappy Decisions)**

Each upgrade must be fully described in one line of plain text. No tooltip is shown. The description must be understandable without reading any other upgrade or GDD. If a description requires qualification ("requires S2"), the upgrade design is too complex for MVP.

---

### Upgrade Catalogue

All values are prototype defaults — all are tuning knobs.

| ID | Name | Description (player-facing, one line) | Stat Effect | Category |
|----|------|----------------------------------------|-------------|----------|
| C1 | **Heaven's Blow** | Your Finisher hits harder. | `finisher_damage: 25 → 40` (static override; does not interact with base_attack) | Combo |
| C2 | **Dragon's Reach** | Your Finisher sweeps a wider area. | `finisher_radius: 3.0u → 4.5u` | Combo |
| C3 | **Iron Discipline** | Landing your Finisher immediately restores your combo chain. | On Finisher land: `combo_counter → 1` (not 0; next auto-attack begins building to Finisher immediately) | Combo |
| C4 | **Thunder Descent** | Your Finisher staggers all infantry it strikes. | Finisher applies 400ms stagger to ALL basic infantry hit (cosmetic freeze; they cannot attack or move during stagger). Elite stagger (Iron Captain, Warlord) is NOT affected — the full-combo gate from Enemy Definition GDD still applies regardless of C4. | Combo |
| P1 | **Iron Will** | Your parry burst hits harder and farther. | Parry burst `damage_multiplier × 1.5`, `radius_multiplier × 1.5` | Parry |
| P2 | **River of Momentum** | Each successful parry fills your momentum meter faster. | Momentum gain per parry `× 1.5` | Parry |
| P3 | **Deflect and Strike** | A successful parry charges your next combo hit. | On successful parry: `combo_counter = min(combo_counter + 1, 2)`. If already at 2, no change. | Parry |
| P4 | **Unbroken Form** | Taking a hit only halves your momentum, not empties it. | `momentum_reset_multiplier: 1.0 → 0.5` | Parry |
| A1 | **Sharpened Edge** | Your attacks deal more damage. | `base_attack: 10 → 14` | Auto-attack |
| A2 | **Relentless Press** | Your attack speed increases. | Attack interval `× 0.75` (33% faster; e.g., 1.0s interval → 0.75s) | Auto-attack |
| A3 | **Long Reach** | Your attacks hit enemies farther away. | `base_radius: 1.5u → 2.25u` | Auto-attack |
| A4 | **Cleaving Stance** | Your attacks hit up to 3 enemies at once. | Auto-attack becomes multi-target: up to 3 enemies per swing (nearest-first priority within base_radius) | Auto-attack |
| S1 | **Veteran's Constitution** | You can take more hits. | `max_hp_bonus: +40` → `max_hp: 100 → 140` | Survivability |
| S2 | **Battle Hardened** | Incoming damage is reduced. | `damage_reduction: +0.25` (from 0.0 at run start) | Survivability |
| S3 | **Iron Skin** | Incoming damage is further reduced. | `damage_reduction: +0.25` (cumulative with S2: 0.50 max from both; damage_reduction_cap of 0.75 enforced at apply time) | Survivability |
| S4 | **Veteran's Resilience** | You slowly recover HP over time. | `regen_rate: +1.0 HP/s` (regen_rate_max = 1.5 HP/s enforced at apply time) | Survivability |

**Cumulative constraint validation (at extreme specialization):**

| Scenario | Cumulative effect | Constraint violated? |
|---|---|---|
| All 4 Survivability | max_hp=140, damage_reduction=0.50, regen=1.0 HP/s | ✅ All caps respected |
| S2+S3 vs. Levy Soldier | effective_damage = floor(5 × 0.5) = 2 | ✅ Below reset_damage_threshold (3); no combo/momentum reset |
| S2+S3 vs. Iron Captain standard | effective_damage = floor(12 × 0.5) = 6 | ✅ Above reset_damage_threshold; elite attacks still bite |
| All 4 Combo | finisher_damage=40, radius=4.5u, combo→1 on Finisher, all targets stagger | ✅ No cap violations |
| C1 + A1 | base_attack=14, finisher_damage=40 (static — no interaction) | ✅ Clean lane separation |

**Lane viability floor (Pillar 2):** A player holding 4 upgrades in any single category must be able to clear standard waves (not elite encounters) without requiring the other category. This is a playtesting gate — defined in Acceptance Criteria.

---

### Interactions with Other Systems

| System | Data Flow | Interface Owner |
|---|---|---|
| **Progression System** | Draws 3 options from eligible pool on level-up; enforces pity rule; detects category exhaustion | Progression System owns draw logic; Upgrade Definition owns the pool content and draw rules |
| **Upgrade Selection UI** | Displays drawn upgrade Name + Description; receives player pick | Upgrade Definition owns content; Upgrade Selection UI owns layout and interaction |
| **Damage & Health** | Receives at pick time: `max_hp_bonus` (S1), `damage_reduction` delta (S2, S3), `regen_rate` delta (S4), `momentum_reset_multiplier` (P4). Enforces `damage_reduction_cap = 0.75` and `regen_rate_max = 1.5 HP/s`. | Damage & Health enforces caps; Upgrade Definition specifies the delta |
| **Combo System** | Receives at pick time: `finisher_damage` override (C1), `finisher_radius` override (C2), `combo_counter_on_finisher_land = 1` flag (C3), `finisher_stagger_all = true` flag (C4) | Combo System owns finisher execution; Upgrade Definition specifies modifications |
| **Combat Core** | Receives at pick time: `base_attack` override (A1), `attack_interval_multiplier` (A2), `base_radius` override (A3), `max_targets` override (A4) | Combat Core owns auto-attack execution; Upgrade Definition specifies modifications |
| **Parry System** | Receives at pick time: `burst_damage_multiplier` (P1), `burst_radius_multiplier` (P1), `momentum_gain_multiplier` (P2), `combo_advance_on_parry = true` flag (P3), `momentum_reset_multiplier` (P4) | Parry System owns parry execution and momentum meter; Upgrade Definition specifies modifications |

## Formulas

Upgrade Definition is a data catalogue — formulas in downstream systems (Damage & Health, Parry System, Combo System, Combat Core) apply the stat modifications this GDD defines. Three formulas live here that govern upgrade application rules.

### 1. Cumulative Damage Reduction

When multiple Survivability upgrades are applied, damage_reduction deltas are summed and capped at `damage_reduction_cap` at apply time.

```
damage_reduction_effective = min(Σ(damage_reduction_deltas), damage_reduction_cap)
```

| Variable | Symbol | Type | Range | Description |
|---|---|---|---|---|
| Damage reduction delta | `damage_reduction_delta` | float | 0.25 per upgrade | Value each S2/S3 upgrade contributes; must be an exact binary fraction (per Damage & Health GDD constraint) |
| Sum of deltas | `Σ(damage_reduction_deltas)` | float | 0.0–0.75 | Cumulative total before cap |
| Cap | `damage_reduction_cap` | float | 0.75 | Defined in Damage & Health GDD; enforced at upgrade-apply time |
| Effective reduction | `damage_reduction_effective` | float | 0.0–0.75 | Applied to Damage & Health's effective_damage formula |

**Output range:** [0.0, 0.75]. In MVP with S2 + S3 (maximum): 0.50.
**Example:** Player picks S2 (+0.25) then S3 (+0.25): `min(0.25 + 0.25, 0.75) = 0.50`.
**Guard:** Damage & Health clamps to `damage_reduction_cap` and logs a designer error if any single delta would push the cumulative value above 0.75.

---

### 2. Cumulative Regen Rate

```
regen_rate_effective = min(Σ(regen_rate_deltas), regen_rate_max)
```

| Variable | Symbol | Type | Range | Description |
|---|---|---|---|---|
| Regen delta | `regen_rate_delta` | float | 1.0 HP/s | Value S4 contributes |
| Effective regen | `regen_rate_effective` | float | 0.0–1.5 HP/s | Applied in Damage & Health's regen formula |
| Cap | `regen_rate_max` | float | 1.5 HP/s | Defined in Damage & Health GDD; enforced at apply time |

**Output range:** [0.0, 1.5 HP/s]. In MVP with only S4: 1.0 HP/s.
**Constraint rationale:** `regen_rate_effective` must remain below `effective_damage_min / invulnerability_duration_s` = `1 / 0.5 = 2.0 HP/s` to prevent immortality against minimum-damage enemies. The 1.5 HP/s cap provides a 25% margin of safety.
**Guard:** Damage & Health enforces the cap and logs a designer error if any cumulative application would exceed `regen_rate_max`.

---

### 3. Pity Window Count

```
draws_since_most_picked = count of consecutive draws without any option from most-picked category
```

| Variable | Symbol | Type | Range | Description |
|---|---|---|---|---|
| Draw streak | `draws_since_most_picked` | int | 0–N | Consecutive draws since last hand containing player's most-picked category |
| Pity threshold | `pity_threshold` | int | 3 (default) | Maximum streak before guaranteed category inclusion |

**Rule:** If `draws_since_most_picked ≥ pity_threshold`, the Progression System must include ≥1 upgrade from the most-picked category in the next draw (sampled first, then 2 remaining options sampled from the rest of the pool).
**Dormant condition:** Pity does not activate if the most-picked category is exhausted (all 4 picked).
**Tie-break:** If two or more categories are tied for most-picked, use the category whose last pick was most recent.

## Edge Cases

- **If a player picks S3 without having picked S2:** S3 still applies `+0.25 damage_reduction`. S3 is not gated on S2 — both add the same delta independently. A player receiving S3 before S2 gets the same cumulative benefit as one who picked S2 first. The descriptions deliberately omit ordering language.

- **If a pick screen appears during a Finisher animation:** The Upgrade Selection UI queues the pick screen until the Finisher animation completes. Player input is not frozen — the run continues; the screen appears when the animation ends. The 3 drawn options are locked at the moment the level-up triggers, not at the moment the screen appears (per game-concept.md edge cases).

- **If the eligible pool reaches fewer than 3 upgrades at draw time:** The Progression System draws all remaining upgrades (1 or 2). The pick screen shows fewer than 3 options. Valid behavior — player still picks 1. Pity rule still applies to whatever remains.

- **If the eligible pool is exhausted (0 upgrades remain):** No further upgrade picks are possible. Level-up events show a "Pool exhausted" notice. In MVP with 16 upgrades and ~12 level-ups, this is unreachable in a normal run. Future designs with fewer upgrades must handle this gracefully.

- **If the pity guarantee applies but only 1 upgrade from the most-picked category remains:** That upgrade fills the first slot. Remaining 2 slots are drawn from other eligible categories. Valid behavior.

- **If P3 (Deflect and Strike) activates when combo_counter is already 2:** `min(2 + 1, 2) = 2` — no change to counter. The parry burst still fires normally. The upgrade is not wasted; it simply has no visible counter effect when the chain is already primed.

- **If C3 (Iron Discipline) resets combo_counter to 1 and all enemies in Finisher range are dead:** combo_counter remains at 1. Decay timer (Combo System GDD) begins. The upgrade does not prevent counter decay — it sets the starting point at 1, not lock it there.

- **On retry:** Run Session Manager resets the upgrade pool to all 16 upgrades. All stat modifications from the previous run (base_attack changes, finisher_damage overrides, etc.) are reverted via the full reset API in each downstream system. Upgrade Definition does not track cumulative run state — each downstream system's reset API is responsible for restoring base values.

## Dependencies

| System | Direction | Nature | Interface |
|---|---|---|---|
| **Progression System** | Upgrade Definition → Progression | Hard | Progression System owns draw logic and pity tracking; reads the upgrade pool (category, eligible_generals, stat effect) from Upgrade Definition. Pool reset on retry is triggered by Run Session Manager and routed through Upgrade Definition's reset API. |
| **Upgrade Selection UI** | Upgrade Definition → Upgrade Selection UI | Hard | UI reads Name + Description for display. UI must display exactly 1 line per upgrade with no tooltip. Overflow (description > 1 line) is a content error — the upgrade must be redesigned, not the UI stretched. |
| **Damage & Health** | Upgrade Definition → Damage & Health | Hard | Applies at pick time: `max_hp_bonus` (S1), `damage_reduction` delta (S2, S3), `regen_rate` delta (S4), `momentum_reset_multiplier` (P4). Damage & Health enforces `damage_reduction_cap = 0.75` and `regen_rate_max = 1.5 HP/s`. |
| **Combo System** | Upgrade Definition → Combo System | Hard | Applies at pick time: `finisher_damage` override (C1), `finisher_radius` override (C2), `combo_counter_on_finisher_land = 1` flag (C3), `finisher_stagger_all = true` flag (C4). Combo System must expose a stat-modification API for each. |
| **Combat Core** | Upgrade Definition → Combat Core | Hard | Applies at pick time: `base_attack` override (A1), `attack_interval_multiplier` (A2), `base_radius` override (A3), `max_targets` override (A4). Combat Core must expose a stat-modification API for each. |
| **Parry System** | Upgrade Definition → Parry System | Hard | Applies at pick time: `burst_damage_multiplier` (P1), `burst_radius_multiplier` (P1), `momentum_gain_multiplier` (P2), `combo_advance_on_parry = true` flag (P3), `momentum_reset_multiplier` (P4). Parry System must expose a stat-modification API for each. |
| **Run Session Manager** | Run Session Manager → Upgrade Definition | Hard | Run Session Manager calls Upgrade Definition's pool reset API on retry. Upgrade Definition resets the eligible pool to all 16 upgrades. Downstream stat resets are handled by each downstream system's own reset API (not by Upgrade Definition). |

## Tuning Knobs

All values are prototype defaults. Change during playtesting — not during implementation.

| Knob | Default | Safe Range | Affects |
|---|---|---|---|
| `pity_threshold` | 3 | 2–5 | Consecutive draws before most-picked category is guaranteed; below 2 is too aggressive (rails the player's lane); above 5 is too loose (player may feel lane-starved) |
| `finisher_damage_c1` | 40 | 30–60 | C1 finisher_damage override; below 30 feels underwhelming vs. base 25; above 60 trivializes basic infantry in one Finisher |
| `finisher_radius_c2` | 4.5u | 3.5–6.0u | C2 finisher_radius override; above 6.0u trivializes dense packs, especially Guan Yu's 360° ring |
| `combo_counter_on_finisher_land_c3` | 1 | 0–1 | C3 post-Finisher counter value; 0 = no-op (reverts to base behavior); 1 = keeps combo charging; values above 1 are invalid |
| `finisher_stagger_duration_c4` | 400ms | 200–600ms | C4 stagger applied to all infantry on Finisher hit; below 200ms may not be perceptible; above 600ms trivializes infantry pressure |
| `burst_multiplier_p1` | 1.5× | 1.2–2.0× | P1 parry burst damage AND radius multiplier; above 2.0× makes parry burst the dominant kill source, undermining Pillar 2 |
| `momentum_gain_multiplier_p2` | 1.5× | 1.2–2.0× | P2 momentum gain per parry; above 2.0× enables full-momentum bursts after only 2-3 parries, trivializing the meter arc |
| `base_attack_a1` | 14 | 12–18 | A1 base_attack override; above 18 approaches one-shot territory on Levy Soldiers at run start |
| `attack_interval_multiplier_a2` | 0.75× | 0.6–0.9× | A2 attack speed; below 0.6× creates frame-budget pressure at 200 enemies; above 0.9× may not be perceptible to the player |
| `base_radius_a3` | 2.25u | 1.8–3.0u | A3 base_radius override; above 3.0u reaches Iron Captain attack range thresholds and may trigger unintended stagger interactions |
| `max_targets_a4` | 3 | 2–5 | A4 multi-target cap; above 5 creates readability issues at 200 enemies and may break finisher-chain target tracking |
| `max_hp_bonus_s1` | 40 | 20–80 | S1 flat HP addition; above 80 makes the player effectively resistant to basic infantry for extended periods |
| `damage_reduction_delta_s2` | 0.25 | **0.25 only** | S2 reduction; must remain an exact binary fraction; changing to 0.1 or 0.3 violates the IEEE 754 floor() constraint in Damage & Health GDD |
| `damage_reduction_delta_s3` | 0.25 | **0.25 only** | S3 reduction; same constraint as S2; cumulative S2+S3 must not exceed `damage_reduction_cap = 0.75` |
| `regen_rate_delta_s4` | 1.0 HP/s | 0.5–1.4 HP/s | S4 regen addition; must not bring cumulative regen above `regen_rate_max = 1.5 HP/s`; note: only one regen upgrade exists in MVP, so 1.4 HP/s is the effective single-upgrade safe ceiling |

## Visual/Audio Requirements

[To be designed]

## Acceptance Criteria

| # | Criterion | Story Type | Gate |
|---|---|---|---|
| AC-UD-01 | **GIVEN** a new run begins, **WHEN** the run initializes, **THEN** the eligible upgrade pool contains exactly 16 upgrades with exactly 4 per category (Combo, Parry, Auto-attack, Survivability). **Pass condition:** assert pool size == 16; assert count per category == 4. | Logic | BLOCKING |
| AC-UD-02 | **GIVEN** the player levels up, **WHEN** 3 options are drawn from the eligible pool, **THEN** all 3 options are unique and none have been previously picked this run. **Pass condition:** assert 3 distinct upgrade IDs in the hand; assert none are in the picked-upgrades set. | Logic | BLOCKING |
| AC-UD-03 | **GIVEN** the player picks upgrade S2, **WHEN** the pick is confirmed, **THEN** (a) S2 is removed from the eligible pool, (b) the 2 unpicked upgrades are returned to the eligible pool, (c) `DamageHealthSystem.DamageReduction == 0.25f` immediately. **Pass condition:** assert pool size == 15 after pick; assert both unpicked IDs are in pool; assert stat value. | Integration | BLOCKING |
| AC-UD-04 | **GIVEN** the player has picked all 4 Survivability upgrades, **WHEN** the next draw occurs, **THEN** no Survivability upgrade appears in the 3-card hand. **Pass condition:** assert all 3 drawn upgrade IDs have `category ≠ Survivability`. | Logic | BLOCKING |
| AC-UD-05 | **GIVEN** `pity_threshold == 3`, player's most-picked category is Combo, AND the last 3 consecutive draws contained no Combo upgrade, **WHEN** the next draw occurs, **THEN** at least 1 of the 3 options is from the Combo category. **Pass condition:** assert at least 1 drawn upgrade has `category == Combo`. | Logic | BLOCKING |
| AC-UD-06 | **GIVEN** player picks S2 (`+0.25`) then S3 (`+0.25`), **WHEN** both are applied, **THEN** `DamageHealthSystem.DamageReduction == 0.50f` and no designer error is logged. **Pass condition:** assert stat == 0.50f (exact binary fraction); assert no error log entry from upgrade apply. | Logic | BLOCKING |
| AC-UD-07 | **GIVEN** player picks S4 (`+1.0 HP/s`), **WHEN** applied, **THEN** `DamageHealthSystem.RegenRate == 1.0f` and no designer error is logged. **Pass condition:** assert stat == 1.0f; assert no error log entry. | Logic | BLOCKING |
| AC-UD-08 | **GIVEN** player has picked P3 AND `combo_counter == 1`, **WHEN** a parry is successfully executed, **THEN** `ComboSystem.ComboCounter == 2` after parry resolves. **Pass condition:** assert counter == 2 post-parry; assert no Finisher was triggered. | Integration | BLOCKING |
| AC-UD-09 | **GIVEN** player has picked P3 AND `combo_counter == 2`, **WHEN** a parry is successfully executed, **THEN** `combo_counter` remains 2 (capped). **Pass condition:** assert counter == 2 post-parry; assert no counter increment beyond 2. | Integration | BLOCKING |
| AC-UD-10 | **GIVEN** a player holding 4 Combo upgrades and 0 Parry upgrades at wave step 5+ density, **WHEN** running a full standard wave without parry input, **THEN** the player survives the wave (does not enter Dead state). **Pass condition:** assert `PlayerHealthSystem.State ≠ Dead` at wave end. *(Playtest gate — not fully automatable; requires a full combat harness with enemy spawner active.)* | Integration | ADVISORY |
| AC-UD-11 | **GIVEN** the run ends and Run Session Manager triggers retry, **WHEN** the retry reset API is called, **THEN** the eligible upgrade pool resets to all 16 upgrades AND downstream stats return to base values. **Pass condition:** assert pool size == 16; assert `DamageHealthSystem.DamageReduction == 0.0f`; assert `ComboSystem.FinisherDamage == 25`. | Integration | BLOCKING |

## Open Questions

| # | Question | Owner | Resolution Path |
|---|---|---|---|
| OQ-01 | Does the lane viability floor hold? Can a pure Combo build (C1+C2+C3+C4) survive a step-5 standard wave without parrying? | Playtest | Validate in the combat prototype before sprint planning. If not viable, buff Combo DPS or add a Combo defensive upgrade. |
| OQ-02 | Does the S2+S3 Levy Soldier reset immunity feel intentional or exploitable? At damage_reduction=0.50, Levy Soldiers deal 2 effective_damage (below reset_damage_threshold=3). Heavy Survivability players can absorb Levy Soldier hits without losing their chain. | Playtest | Observe during first playtest. If players exploit this to play completely risk-free against infantry, consider adjusting reset_damage_threshold upward or lowering damage_reduction_delta. |
| OQ-03 | Is pity_threshold=3 correct? The game-designer recommended raising from 2 to 3 to avoid "railroading." Playtest will reveal if players feel stranded when targeting a lane. | Playtest | Monitor playtests. If players frequently complain about "never seeing Combo options," lower to 2. If they feel forced into a lane, raise to 4. |
| OQ-04 | Do general-specific upgrades belong in the Vertical Slice tier? The eligible_generals field is reserved. If generalist players never notice the absence of general-specific options, they may not be worth the design overhead. | V-Slice design | Re-evaluate when authoring the Vertical Slice upgrade expansion. |
| OQ-05 | Should C4 (Thunder Descent) apply to Iron Captain/Warlord enemies? Currently: Finisher stagger on basic infantry (cosmetic freeze); elite stagger still gates on combo=2 per Enemy Definition GDD. The C4 description says "staggers everything it strikes" — this may confuse players who expect elites to stagger and find the combo gate still applies. | Design | Clarify C4 description to explicitly say "staggers all standard infantry"; elite stagger remains gated on full-combo finisher regardless of C4. |

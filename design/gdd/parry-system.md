# Parry System

> **Status**: Approved (self-review 2026-06-19)
> **Author**: xinleng + Claude Code agents
> **Last Updated**: 2026-06-19
> **Implements Pillar**: Pillar 1 (Readable Chaos), Pillar 2 (Skill Has a Lane)

> **Note**: Parry System is flagged high-risk in systems-index.md and is intended to be validated by `/prototype combo-and-parry-system` before implementation. This GDD is the design target; parry feel is confirmed empirically in the prototype.

## Overview

The Parry System is the second active skill system: a one-button, timing-based defence that converts a correctly-timed press against an incoming attack into an **area burst** (damage + brief invincibility) and fills the **momentum meter** (0–100), which scales burst power. It consumes buffered parry presses from Input Manager, derives each attack's parry window from that attack's telegraph duration (Enemy Definition's `AttackEvent`), owns the momentum meter and the burst formula, applies the elite stagger gate (shared with the Combo lane at `combo_counter == 2`), and applies all Parry-lane upgrades (P1–P6) plus the cross-lane S6 condition. Missing a parry has no penalty — only the opportunity cost.

The player experiences it as the high-risk, high-skill counterpart to the combo: read the telegraph, press at the right moment, and turn the horde's aggression into your power.

## Player Fantasy

The Parry System is the feeling of turning danger into power. The horde swings at you and — if you read it — that incoming blow becomes your detonation: a burst of force radiating out, the momentum meter climbing, the general feeding on the army's aggression. The fantasy is **the perfect read** — the fighting-game high of catching a strike at the last possible instant and answering with something bigger than you should have. Momentum makes it a build: each clean parry stacks toward a meter-max burst that clears the screen, and a parry chain — read after read after read — is the run's most expressive flex. It is the opposite pole from the combo's patient rhythm: combo is sustained, parry is the spike.

## Detailed Design

### Core Rules

1. **Parry window per attack.** Every enemy `AttackEvent` defines a parry window `[AttackStartTime, AttackStartTime + TelegraphDurationMs/1000]` (owned by Enemy Definition). There is **no separate global `parry_window_ms`** — the telegraph *is* the window (this resolves the TBD knob noted in game-concept.md). **P5 "Crane's Eye"** extends each window by `parry_window_extension` (+75ms) on the back edge.

2. **Parry resolution.** When an `AttackEvent`'s window is open, the Parry System calls Input Manager's `TryConsumeParryPress(notBefore = w_open)`. If a live buffered press is returned, the parry **succeeds**: the attack is suppressed (no `DamageTaken` for that event — Input Manager / damage-health contract), a burst fires, momentum increases, and a brief invincibility window opens. The Input Manager buffer front-extends the effective acceptance by `parry_input_buffer_ms` (120ms) so an early press still counts (Input Manager Formula 2).

3. **No-penalty miss.** If no live press is consumed before the window (incl. P5 extension and buffer) closes, the attack resolves normally as damage. The momentum meter is unchanged; there is no miss penalty (game-concept).

4. **Burst.** A successful parry fires an area burst centred on the player: `burst_damage` to all enemies within `burst_radius`. Burst scales with momentum (Formula 1). **P1 "Iron Will"** multiplies both burst damage and radius by 1.5.

5. **Brief invincibility.** On a successful parry, the Parry System requests Damage & Health to open an invulnerability window of `parry_invuln_ms` (default 300ms), reusing the iframe mechanism so Damage & Health remains the single owner of invulnerability. This is distinct from the post-`DamageTaken` 500ms iframe (a parry prevents the DamageTaken entirely).

6. **Momentum meter.** `momentum_meter ∈ [0, max_momentum]` (`max_momentum = 100`). Each successful parry adds `momentum_gain_per_parry` (default 25; **P2 "River of Momentum"** ×1.5). No passive decay in MVP. Clamped at `max_momentum`.

7. **Momentum reset on damage.** The Parry System subscribes to the momentum-reset event Damage & Health fires on a `DamageTaken` with `effective_damage ≥ reset_damage_threshold` (3). On that event, `momentum_meter −= momentum_meter × momentum_reset_multiplier` (default 1.0 = full reset to 0; **P4 "Unbroken Form"** sets 0.5 = lose half). The Parry System **owns `momentum_reset_multiplier`** and applies it (per damage-health.md / upgrade-definition.md). Sub-threshold grazes do not reset momentum.

8. **Elite burst gate (Pillar 2).** A parry burst against Iron Captain / Warlord staggers them **only at `combo_counter == 2`** (read from the Combo System at burst-resolution time). At `combo_counter < 2`, the burst deals `× stagger_resist_multiplier` and does not stagger (enemy-definition Formula 5). At `combo_counter == 2`, the burst deals full damage AND triggers the Staggered → counter-hit sequence — identical to a full-combo Finisher. Both lanes share the same elite gate.

9. **P3 combo advance.** On a successful parry (one that blocked an attack), the Parry System signals the Combo System to apply P3 "Deflect and Strike" (`combo_counter = min(combo_counter+1, 2)`) if held. A parry input with no incoming attack does not trigger P3 (matches upgrade-definition P3).

10. **P6 max-meter heal.** When a burst fires with `momentum_meter == max_momentum` and P6 "Second Wind" is held, the player heals `momentum_burst_heal_at_max` (+15 HP), subject to `burst_heal_cooldown_p6` (10s) between heals. Partial-meter bursts do not heal.

11. **S6 condition exposure.** The Parry System exposes the current `momentum_meter` at `DamageTaken` time so Damage & Health can apply S6 "Iron Stance" (+0.10 damage reduction when `momentum_meter ≥ max_momentum × 0.5`). Evaluation order (S6 reads momentum **before** the Parry reset applies) is fixed by the S6 event-ordering ADR (upgrade-definition Edge Cases).

### States and Transitions

The Parry System is event-driven, not a player state machine. Its tracked state is the momentum meter and a per-frame set of open attack windows it is watching:

| Element | Behaviour |
|---|---|
| **Open windows** | For each unresolved `AttackEvent` whose window is open this frame, attempt `TryConsumeParryPress`. Resolve on success (burst) or on window close (pass to damage). |
| **Momentum meter** | Increases on parry success; decreases on damage-reset event by `momentum_reset_multiplier`; clamped [0, 100]. |
| **Burst** | Transient — resolves in the frame the parry succeeds; cosmetic flourish owned by VFX & Audio. |

Execution order: Input Manager (capture/buffer) → **Parry System (query/consume/burst)** → Damage & Health (apply remaining damage). This satisfies same-frame parry priority (damage-health Contract #3).

### Interactions with Other Systems

| System | Data In | Data Out | Interface Owner |
|---|---|---|---|
| **Input Manager** | `TryConsumeParryPress(notBefore)`, `HasLiveParryPress()` | None | Input Manager owns the buffer + API |
| **Enemy Definition / Enemy AI** | `AttackEvent` (window, Parriable, IsCounterHit); elite type for gate | Suppresses attack on success; elite stagger requests at combo=2 | Enemy Definition owns the AttackEvent contract |
| **Damage & Health** | momentum-reset event on DamageTaken; opens parry invuln on request | Requests `parry_invuln_ms` iframe; suppresses parried attack; exposes momentum for S6 | Damage & Health owns iframes + DamageTaken |
| **Combo System** | reads `combo_counter` for the elite burst gate | P3 combo-advance signal on parry success | Combo owns counter; Parry owns parry |
| **Upgrade Definition** | P1–P6 + S6 deltas at pick time; owns `momentum_reset_multiplier` value delivery | None | Upgrade specifies; Parry applies. Parry owns `momentum_reset_multiplier` runtime + the S6 ordering ADR |

## Formulas

### 1. Burst Damage

`burst_damage = base_burst × (0.25 + (momentum_meter / max_momentum) × 1.75) × burst_damage_multiplier`

| Variable | Type | Range | Description |
|---|---|---|---|
| `base_burst` | float | 20–60; default 40 | Burst damage at 0 momentum scale-point |
| `momentum_meter` | float | 0–100 | Current meter |
| `max_momentum` | float | 100 | Meter ceiling |
| `burst_damage_multiplier` | float | 1.0 (P1 → 1.5) | Parry-upgrade multiplier |

**Output:** at momentum 0 → `40 × 0.25 = 10` (floor — parrying is never worthless); at momentum 100 → `40 × 2.0 = 80` (ceiling). Linear between. Matches game-concept Momentum Burst formula. **Example:** momentum 50, P1 held → `40 × (0.25 + 0.5×1.75) × 1.5 = 40 × 1.125 × 1.5 = 67.5`.

### 2. Burst Radius

`burst_radius = base_burst_radius × burst_radius_multiplier`

| Variable | Type | Range | Description |
|---|---|---|---|
| `base_burst_radius` | float (u) | 2.0–4.0; default 3.0 | Burst AoE radius |
| `burst_radius_multiplier` | float | 1.0 (P1 → 1.5) | Radius scaling (P1) |

**Example:** P1 held → `3.0 × 1.5 = 4.5u`.

### 3. Momentum Gain

`momentum_meter = min(momentum_meter + momentum_gain_per_parry × momentum_gain_multiplier, max_momentum)`

| Variable | Type | Range | Description |
|---|---|---|---|
| `momentum_gain_per_parry` | float | 10–40; default 25 | Meter gained per successful parry |
| `momentum_gain_multiplier` | float | 1.0 (P2 → 1.5) | P2 scaling |

**Output:** ~4 parries to full at default; ~3 with P2. **Example:** P2 held → `25 × 1.5 = 37.5` per parry.

### 4. Momentum Reset on Damage

`momentum_meter = momentum_meter − (momentum_meter × momentum_reset_multiplier)`  [only when `effective_damage ≥ reset_damage_threshold`]

| Variable | Type | Range | Description |
|---|---|---|---|
| `momentum_reset_multiplier` | float | 0.0–1.0; default 1.0 (P4 → 0.5) | Fraction of momentum lost on a real hit (owned by Parry) |

**Example:** momentum 80, P4 held, real hit → `80 − 80×0.5 = 40`.

## Edge Cases

- **If a parry succeeds against the Warlord Phase 2 ground slam (`Parriable = false`):** Impossible — the Parry System opens no window for a non-parriable attack (enemy-definition AC-06). A press during the slam telegraph has no effect.
- **If two attack windows are open and one buffered press is consumed:** The press resolves the most-recently-received unresolved event (Input Manager returns most-recent; `notBefore` guard prevents reuse). The other attack resolves normally — simultaneous threats are genuine pressure (enemy-definition edge case).
- **If the player parries at `momentum_meter == max_momentum`:** Burst fires at the ceiling (×2.0); P6 heal fires if held and off cooldown; momentum stays clamped at 100 (no overflow).
- **If a sub-threshold graze (`effective_damage < 3`) lands while momentum is high:** No momentum reset (threshold rule). S6 still evaluates against the current momentum for that hit's reduction.
- **If a parry succeeds against an Iron Captain counter-hit (`IsCounterHit = true`):** Treated as a normal successful parry — burst + momentum + invuln. The counter-hit being parried is the designed escape from the elite's post-stagger pressure.
- **If the parry burst kills the elite that was about to counter-hit:** Elite enters Dead; the counter-hit AttackEvent is discarded (enemy-definition edge case).
- **If P6 fires but the player is at `max_hp`:** Heal is capped at `max_hp`; the `burst_heal_cooldown_p6` timer still starts (upgrade-definition P6).

## Dependencies

| System | Direction | Nature | Interface |
|---|---|---|---|
| **Input Manager** | Parry → Input Manager | Hard | `TryConsumeParryPress(notBefore)` / `HasLiveParryPress()`. Parry owns the window; Input Manager owns the buffer. |
| **Enemy Definition / Enemy AI** | Enemy → Parry | Hard | Subscribes to `AttackEvent`; derives window from `TelegraphDurationMs`; respects `Parriable`; reads elite type + `IsCounterHit`. |
| **Damage & Health** | Bidirectional | Hard | Receives momentum-reset event; requests `parry_invuln_ms` iframe; suppresses parried attacks; exposes momentum for S6. |
| **Combo System** | Bidirectional | Hard | Reads `combo_counter` for the elite burst gate; signals P3 combo-advance on parry success. |
| **Upgrade Definition** | Upgrade → Parry | Hard | Applies P1–P6 + S6 at pick time. Parry owns `momentum_reset_multiplier` runtime and the S6 event-ordering ADR. |

## Tuning Knobs

| Knob | Default | Safe Range | Affects |
|---|---|---|---|
| `base_burst` | 40 | 20–60 | Burst damage scale-point; balances parry lane vs combo lane (above 60 makes combo irrelevant) |
| `base_burst_radius` | 3.0u | 2.0–4.0 | Burst AoE; matches Finisher radius by design |
| `momentum_gain_per_parry` | 25 | 10–40 | Parries to full meter; 25 = ~4 parries; above 40 enables meter-max bursts after 2–3 parries |
| `parry_invuln_ms` | 300 | 150–500 | Invincibility granted on a successful parry; below 150 risks chip during the burst, above 500 trivialises follow-ups |
| `parry_window_extension` (P5) | 75ms | 25–150ms | Back-edge window extension from P5 |
| `burst_heal_cooldown_p6` | 10s | 5–20s | Min seconds between P6 max-meter heals |
| `momentum_reset_multiplier` | 1.0 | 0.0–1.0 | Fraction of momentum lost on a real hit (P4 → 0.5) |

*Referenced, not owned: `TelegraphDurationMs` / `Parriable` / `IsCounterHit` (Enemy Definition); `parry_input_buffer_ms` (Input Manager); `reset_damage_threshold`, iframe mechanism (Damage & Health); P1–P6/S6 values (Upgrade Definition); `max_momentum = 100`, burst formula constants (game-concept).*

## Visual/Audio Requirements

The parry burst is the **second-most prominent** effect after the Finisher (game-concept). Requirements (full spec in VFX & Audio GDD): the burst is an **expanding ring** (shape-distinguishable from the Finisher starburst, per colorblind rule); a dedicated accent color is reserved exclusively for parry feedback; a crisp, distinct parry SFX on success; the momentum meter fills and glows as it climbs, with a distinct max-meter state. The successful-parry flash must read instantly even at 200 enemies (Pillar 1).

## UI Requirements

The Parry System drives the **momentum meter HUD** (owned by Combat Feedback UI / Run UI): a visible meter that fills on parry and visibly empties/halves on a real hit, with a distinct full-meter state (signals P6/ceiling). This GDD mandates the meter exists and its state transitions; layout is owned by the UI GDDs.

## Acceptance Criteria

| # | Criterion | Story Type | Gate |
|---|---|---|---|
| AC-PR-01 | **GIVEN** an `AttackEvent` with a 400ms telegraph and a buffered parry press inside the window, **WHEN** the Parry System evaluates, **THEN** the parry succeeds: the attack is suppressed (no `DamageTaken`), a burst fires, and momentum increases by `momentum_gain_per_parry`. | Integration | BLOCKING |
| AC-PR-02 | **GIVEN** no buffered press before the window (incl. P5 + buffer) closes, **WHEN** the window closes, **THEN** the attack resolves as `DamageTaken` and momentum is unchanged (no miss penalty). | Integration | BLOCKING |
| AC-PR-03 | **GIVEN** `momentum_meter = 0`, `base_burst = 40`, **WHEN** a parry burst fires, **THEN** `burst_damage == 10`. **GIVEN** `momentum_meter = 100`, **THEN** `burst_damage == 80`. | Logic | BLOCKING |
| AC-PR-04 | **GIVEN** P2 held, **WHEN** a parry succeeds, **THEN** momentum increases by `25 × 1.5 = 37.5`. | Logic | BLOCKING |
| AC-PR-05 | **GIVEN** `momentum_meter = 80`, P4 held, **WHEN** a `DamageTaken` with `effective_damage ≥ 3` fires, **THEN** `momentum_meter == 40`. **GIVEN** `effective_damage < 3`, **THEN** momentum is unchanged. | Logic | BLOCKING |
| AC-PR-06 | **GIVEN** an Iron Captain and a parry burst at `combo_counter = 2`, **THEN** the burst deals full damage and triggers Staggered. **GIVEN** `combo_counter < 2`, **THEN** the burst deals `× stagger_resist_multiplier` and does not stagger. | Integration | BLOCKING |
| AC-PR-07 | **GIVEN** a successful parry, **WHEN** it resolves, **THEN** Damage & Health opens a `parry_invuln_ms` (300ms) invulnerability window. | Integration | BLOCKING |
| AC-PR-08 | **GIVEN** P6 held, `momentum_meter = max_momentum`, off cooldown, **WHEN** the burst fires, **THEN** player HP increases by 15 and the 10s cooldown starts. **GIVEN** `momentum_meter < max_momentum`, **THEN** no heal. | Integration | BLOCKING |
| AC-PR-09 | **GIVEN** a Warlord Phase 2 slam (`Parriable = false`), **WHEN** the Parry System receives it, **THEN** no window opens and a press has no effect. | Integration | BLOCKING |

## Open Questions

| # | Question | Owner | Resolution Path |
|---|---|---|---|
| OQ-01 | Is the telegraph-derived window + 120ms input buffer the right total forgiveness? Too tight feels broken; too loose trivialises. | Prototype | Tune `parry_input_buffer_ms` (Input Manager) and telegraph durations together; the central parry-feel question. |
| OQ-02 | Is `base_burst = 40` balanced against the combo lane so neither dominates (Pillar 2)? | Prototype/Balance | Validate via prototype + `/balance-check`; game-concept flags parry-vs-combo balance as a key risk. |
| OQ-03 | Does the momentum meter need passive decay, or is reset-on-damage-only sufficient? MVP = no decay. | Playtest | Observe whether hoarding a full meter without parrying feels degenerate. |
| OQ-04 | Is `parry_invuln_ms = 300` enough to survive the burst animation in dense packs without trivialising follow-ups? | Prototype | Tune against enemy attack density. |

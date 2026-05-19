# Enemy Definition

> **Status**: In Revision — 2026-05-18
> **Author**: xinleng + Claude Code agents
> **Last Updated**: 2026-05-18
> **Implements Pillar**: Pillar 1 (Readable Chaos), Pillar 2 (Skill Has a Lane)

## Overview

Enemy Definition is the data layer that specifies Dynasty Survivors' complete enemy cast: five types (three basic infantry variants, one elite checkpoint enemy, and one final boss). For each type it defines the stat profile (health, move speed, attack damage, attack range), the attack event contract (the timing and signal data that downstream systems — Parry and Enemy AI — subscribe to), XP drop values, and telegraph behavior (the visual and timing rules that signal an incoming attack). No game logic lives in this document; it is a pure specification that four other systems consume: the Parry System hooks into the attack event contract, Enemy AI reads behavior parameters, the Progression System reads XP drop values, and the Wave Spawner reads type identifiers and spawn weights.

The five enemy types form a deliberate escalation curve. Basic infantry establish the auto-attack and combo rhythm without requiring parry responses. Elites — appearing at the 5, 10, and 15-minute checkpoints — are the game's designed dual-system tests: each requires a full combo chain to stagger and a well-timed parry to survive its counter-hit. The final boss at 18 minutes is the run's skill expression capstone. Every type has a distinct telegraph shape and color, readable at 200 enemies on screen and distinguishable without relying on color alone, in direct service of Pillar 1 (Readable Chaos).

## Player Fantasy

A Three Kingdoms general doesn't just swing harder — they see clearer. Every enemy type in Dynasty Survivors is a legibility challenge before it is a combat challenge. The basic infantry are the vocabulary lesson: 200 faceless soldiers that train your hands to feel the combo cadence without thinking, so the rhythm of "hit, hit, FINISH" becomes instinct. The elite is the punctuation mark — a single armored officer every five minutes who presents a two-clause sentence you have to read in full: commit to the combo stagger, then catch the counter-blow in time. Miss either clause and you learn exactly what you did wrong. The boss at 18 minutes is the final exam. Mastery is the moment the battlefield stops feeling chaotic and starts feeling like a script you're already three steps ahead of — when you recognize the telegraph before it completes and your hands move before your brain does.

## Detailed Design

### Core Rules

**Enemy type roster (5 types):**

All stat values are prototype defaults — all are tuning knobs.

| Type | Name | HP | Move Speed | Attack Damage | Attack Range | Parriable | Stagger |
|------|------|----|-----------|---------------|--------------|-----------|---------|
| Basic 1 | Levy Soldier | 30 | Slow (1.5 u/s) | 5 | Melee (0.8u) | Yes | Any Finisher |
| Basic 2 | Halberd Pikeman | 40 | Medium (2.0 u/s) | 8 | Medium (1.8u) | Yes | Any Finisher |
| Basic 3 | Fire Arrow Archer | 25 | Very Slow (0.8 u/s) | 10 | Long (6.0u) | Yes (projectile) | Any Finisher |
| Elite (5 min) | Iron Captain | 300 | Med-Slow (1.6 u/s) | 12 standard / 25 counter-hit | 1.0u / 1.5u | Yes | Full-combo Finisher only |
| Elite (10 min) | Iron Captain | 375 | Med-Slow (1.6 u/s) | 12 standard / 25 counter-hit | 1.0u / 1.5u | Yes | Full-combo Finisher only |
| Elite (15 min) | Iron Captain | 450 | Med-Slow (1.6 u/s) | 12 standard / 25 counter-hit | 1.0u / 1.5u | Yes | Full-combo Finisher only |
| Boss | Warlord | 800 (Phase 1: 800→400, Phase 2: 400→0) | Slow→Medium (1.2→2.0 u/s) | 20 standard / 30 counter-hit / 40 slam | 1.0u / 1.5u / 2.0u slam | Yes (standard + counter-hit) / **No** (Phase 2 ground slam) | Full-combo Finisher only |

**Telegraph behavior by type:**

| Type | Telegraph Shape | Duration | Color / Shape Rule |
|------|----------------|----------|--------------------|
| Levy Soldier | Horizontal ring glow around torso | 400ms | Muted gold — ring shape |
| Halberd Pikeman | Directional lance glow from weapon tip toward player | 350ms | Orange-red — line shape |
| Fire Arrow Archer | Arrowhead indicator above enemy (not a ground ring) | 600ms | Orange — downward arrow shape |
| Iron Captain — standard | Expanding thick ring around body | 500ms | Bright red — ring, thicker than Levy |
| Iron Captain — counter-hit (5 min) | Wide 270° arc sweeping from body along ground | 600ms | Bright red — arc shape, largest telegraph in MVP |
| Iron Captain — counter-hit (10 min) | Wide 270° arc sweeping from body along ground | 550ms | Bright red — arc shape (same shape, tighter window) |
| Iron Captain — counter-hit (15 min) | Wide 270° arc sweeping from body along ground | 450ms | Bright red — arc shape (faster than standard 500ms; perceptibly faster than Levy Soldier 400ms — maximum legibility-range pressure; smooth escalation from 550ms) |
| Warlord Phase 1 | Same as Iron Captain (standard: thick ring; counter-hit: 270° arc) | Standard: 500ms; Counter-hit: 450ms | Same as Iron Captain 15-min tier — Warlord inherits the maximum Iron Captain pressure window |
| Warlord Phase 2 ground slam | Circular red fill expanding outward from Warlord position | 800ms | Bright red — filled circle; **NOT a parry prompt**; player must dodge out of radius |

All telegraph shapes must be distinguishable by shape alone, not color — Pillar 1 / colorblind accessibility requirement.

**Iron Captain / Warlord stagger rules:**
- A Finisher landing when player combo counter = 0 or 1 deals 50% damage to the elite — no stagger, and **no knockback** (Iron Captain / Warlord holds ground; Lu Bu's PointBurst knockback impulse is suppressed for these targets at combo_counter < 2).
- A Finisher landing when combo counter = 2 (full 3-hit chain) deals full Finisher damage, triggers stagger for `stagger_duration_ms`, **and — if the Finisher is Lu Bu's PointBurst — applies the knockback impulse**. After stagger resolves, the elite immediately enters the counter-hit Telegraph (deterministic, uninterruptible sequence).

**Stagger window interactivity (designed escape path):** During the elite's stagger animation, the player's auto-attack system continues normally against all targets in melee range — including the staggered elite itself. The player CAN build `combo_counter` on surrounding infantry during the stagger window. This is the designed escape from the post-Finisher pressure: a skilled player who reaches `combo_counter = 2` on other enemies before the counter-hit telegraph fires may use a parry burst to trigger Staggered again and suppress the counter-hit. A player who does not rebuild combo must accept the counter-hit and use the 500ms invulnerability window. This interaction must be documented in: (1) Combo System GDD — auto-attack targeting is unrestricted during any enemy's stagger animation; (2) Enemy AI GDD — the staggered elite continues to exist as a valid auto-attack target; (3) Combat Core — hitting the staggered elite while it is in Staggered state does NOT re-trigger stagger or interrupt the counter-hit Telegraph sequence.

**Lu Bu knockback gate (cross-GDD contract):** Lu Bu's PointBurst knockback component (`KnockbackForce`, `KnockbackRadius` from General Characters GDD) is subject to the same `combo_counter` gate as stagger. Combat Core must check the target's elite type before applying knockback: if target is Iron Captain or Warlord AND `combo_counter < 2`, suppress the knockback impulse entirely. Basic infantry have no knockback-resist — Lu Bu's knockback applies at any `combo_counter` against Levy Soldier, Halberd Pikeman, and Fire Arrow Archer.

**Iron Captain escalation design intent:** The three Iron Captain appearances (5/10/15 min) are intentional pressure-escalation tests of the same skill, not new mechanics. The 10-minute and 15-minute appearances teach nothing mechanically different from the first — they shorten the counter-hit window (600 → 550 → 450ms) and increase HP. The lesson is the same two-clause sentence at higher pressure. This is by design: Survivors-genre players are expected to repeat and refine the same reads rather than learn new ones mid-run. New mechanics arrive via the Warlord and via Lu Bu's knockback, not via Iron Captain variants. Playtest should confirm whether the 50ms window reductions are perceptible as escalation or invisible; if invisible, adjust values.

**Parry burst resist rule (Pillar 2 — both lanes face the same gate):**
Parry burst damage against Iron Captain and Warlord is subject to the same resist gate as Finishers. If the player's `combo_counter < 2` at the moment the parry burst resolves against an elite, the burst deals `burst_damage × stagger_resist_multiplier` effective damage and does not trigger stagger. If `combo_counter = 2`, burst deals full damage. Both upgrade lanes must commit to the full 3-hit combo to unlock the elite's stagger — parry investment alone does not bypass this gate. **At `combo_counter = 2`, the parry burst deals full `burst_damage` AND triggers the Staggered state — entering the same Staggered → counter-hit Telegraph → Attack → Cooldown sequence as a full-combo Finisher.** Both upgrade lanes therefore share identical elite interaction paths at `combo_counter = 2`. **Implementation note:** The Parry System must read `combo_counter` from the Combo System at burst-resolution time when the target is an elite type. Document as a hard dependency in the Parry System GDD.

**Warlord Phase 2 transition:**
- At 50% HP (400 damage taken), Warlord enters a 1.2-second roar animation (no damage, no AttackEvent, state locked). After roar: Phase 2 data values load (increased speed, damage, slam radius). Warlord re-enters Chase. No new state machine states — data changes only.
- Phase 2 ground slam is the only non-parriable attack in MVP. Player must move outside the 2.0-unit radius within the 800ms telegraph window. This requires a dedicated dodge roll input — see Dependencies and Open Questions.

**Archer projectile rules:**
- AttackEvent is emitted when the telegraph begins (arrowhead indicator appears), consistent with all other enemy types. `AttackStartTime` = timestamp of telegraph-start, not projectile-fire.
- The arrow launches during the telegraph window (approximately mid-way through the 600ms duration). A successful parry within `[AttackStartTime, AttackStartTime + 0.6]` cancels the in-flight projectile regardless of whether the arrow has already launched.
- The Archer fires `archer_projectile_count` projectiles per attack animation (tuning knob; default: 1). One AttackEvent is emitted per attack animation regardless of `archer_projectile_count`. Parrying cancels all in-flight projectiles for that attack.

**Attack Event Contract:**

When any enemy begins an attack it emits one `AttackEvent`. This is the minimum contract — no additional fields until a downstream system demonstrates a need.

```
AttackEvent {
    AttackerId          : int      // Unique instance ID per spawn instance. Assigned from a private static _nextInstanceId counter (int) incremented at spawn time — not per pool slot. A recycled pool slot receives a new ID on each spawn. This prevents ghost-cancel bugs from orphaned in-flight projectiles holding a stale ID.
    AttackStartTime     : float    // Timestamp (seconds since run start) when telegraph begins
    TelegraphDurationMs : int      // Duration of parry window in ms (tuning knob per type)
    AttackDamage        : int      // Damage on hit — read by Damage & Health system
    AttackerPosition    : Vector3  // World position at attack-start time
    Parriable           : bool     // false only for Warlord Phase 2 ground slam
    IsCounterHit        : bool     // true when emitted from Staggered state exit (counter-hit Telegraph);
                                   // false for all other emissions. Enemy AI reads this to select counter-hit
                                   // damage values and telegraph shape. Parry System reads this for
                                   // future counter-hit-specific parry feedback (e.g., tighter window visual).
}
```

Parry window = `[AttackStartTime, AttackStartTime + (TelegraphDurationMs / 1000.0)]`. All enemy-type-specific logic (stagger resistance, phase transitions) lives in Enemy AI, not in the Parry System.

---

### States and Transitions

All five types share the same state machine. Elite/boss behavior is data-driven (different values, not different states).

| State | Triggered By | Behavior | Exits To |
|-------|-------------|----------|----------|
| Idle | Spawn | Stand still, face player direction | Chase — when player enters aggro range |
| Chase | Aggro range entered | Move toward player at move speed | Telegraph — when in attack range AND cooldown elapsed |
| Telegraph | (a) In attack range AND cooldown elapsed; or (b) **Archer fallback:** `archer_chase_fallback_s` elapsed in Chase without closing to attack range — fires from current position regardless of range | Emit AttackEvent; play telegraph for `TelegraphDurationMs` | Attack |
| Attack | Telegraph complete | Resolve damage / projectile / stagger check | **Basic infantry:** Cooldown (with cosmetic stagger animation overlay if killed by Finisher — no formal Staggered state). **Elite/boss:** Staggered (if full-combo Finisher) or Cooldown. |
| **Staggered** | Full-combo Finisher OR full-combo parry burst on Iron Captain or Warlord (`combo_counter = 2`) | Play stagger animation for `stagger_duration_ms`; emit counter-hit AttackEvent on exit | Telegraph (counter-hit — not Cooldown); **or Dead if HP reaches 0 during stagger animation** |
| Cooldown | Attack resolved (non-stagger path) | Wait `attack_cooldown_ms`; no movement | Chase — on cooldown expiry. **Shortcut (non-Archer only):** if attack range condition is already satisfied at expiry, EnemySystemManager may transition directly to Telegraph (bypasses Chase). Fire Arrow Archer is explicitly exempted from this shortcut — Archer always exits to Chase after fallback-triggered Cooldown so the fallback timer resets correctly. Do NOT apply separation force during a 0-frame Chase pass-through. |
| **Roar** | `current_hp ≤ phase_2_trigger_hp` (Warlord only — Phase 2 threshold crossed) | HP clamped to 1; dead state suppressed; no AttackEvents emitted; player attacks processed but HP cannot drop below 1; Phase 2 data swapped at exit. Duration: `roar_duration_ms` (1200ms default). **Watchdog:** if roar callback does not fire within `roar_duration_ms + 500ms`, roar lock force-clears and dead state resolves normally. | Chase (Phase 2 now active) |
| Dead | HP reaches 0 (or Roar watchdog resolves) | Death animation; emit XP drop; return to object pool | — |

Aggro ranges (tuning knobs): Levy Soldier 5u, Halberd Pikeman 5u, Fire Arrow Archer 8u, Iron Captain 6u, Warlord 10u.

---

### Interactions with Other Systems

| System | Data In from Enemy Definition | Data Out to Enemy Definition | Interface Owner |
|--------|------------------------------|------------------------------|----------------|
| **Parry System** | Receives full `AttackEvent` | None — reads only | Enemy Definition owns the contract |
| **Enemy AI** | Reads: aggro range, move speed, attack range, stagger resistance per type | Drives: enemy state, position, behavior execution | Enemy AI GDD |
| **Progression System** | Reads: XP drop value per enemy type | None | Enemy Definition owns XP values |
| **Wave Spawner** | Reads: type identifiers, spawn weights | None | Enemy Definition owns type IDs |
| **Damage & Health** | Receives: `AttackDamage` on hit | Sets: player HP | Damage & Health GDD |
| **Dodge System** *(new — required for Warlord Phase 2)* | None yet — system not yet designed | Required for non-parriable slam to be survivable | Dodge System GDD (to be created; see Open Questions) |

## Formulas

### 1. XP Drop Per Enemy Type

Enemy Definition owns the `xp_weight` per type. The Progression System owns `xp_scalar` and the XP-per-level thresholds.

```
xp_drop = xp_weight[enemy_type] × xp_scalar
```

| Variable | Symbol | Type | Range | Description |
|----------|--------|------|-------|-------------|
| XP weight | `xp_weight[enemy_type]` | int | 1–50 | Relative XP weight per type; constant defined below |
| XP scalar | `xp_scalar` | float | > 0 | Multiplier owned by Progression System to hit 12-level-up target |
| XP drop | `xp_drop` | float | > 0 | Actual XP emitted on enemy death |

**XP weight table (tuning knobs):**

| Enemy Type | `xp_weight` |
|------------|------------|
| Levy Soldier | 1 |
| Halberd Pikeman | 2 |
| Fire Arrow Archer | 2 |
| Iron Captain | 15 |
| Warlord | 50 |

**Output range:** Unbounded above zero. Clamping is the Progression System's responsibility. **Guard:** The Progression System GDD must validate `xp_scalar > 0` at run start — `xp_scalar = 0` silently zeroes all XP drops, causing total progression failure with no error.
**Example:** If `xp_scalar = 10`: Levy Soldier = 10 XP, Iron Captain = 150 XP, Warlord = 500 XP.

---

### 2. Elite Stagger Resistance

Applies only to Iron Captain and Warlord. Basic infantry are staggered by any Finisher.

```
effective_finisher_damage = finisher_damage × stagger_resist_multiplier   [if combo_counter < 2]
effective_finisher_damage = finisher_damage                                [if combo_counter = 2]
```

| Variable | Symbol | Type | Range | Description |
|----------|--------|------|-------|-------------|
| Finisher damage | `finisher_damage` | float | > 0 | Full Finisher hit damage from Combo System GDD |
| Combo counter | `combo_counter` | int | {0, 1, 2} | Player's combo count when Finisher lands |
| Stagger resist multiplier | `stagger_resist_multiplier` | float | **0.1–0.8** | Damage fraction on non-stagger Finisher; prototype default: **0.5**. Clamp floor 0.1, ceiling 0.8 — authoritative range (matches Tuning Knobs table). |
| Effective finisher damage | `effective_finisher_damage` | float | ≥ 0 | Damage dealt; also determines stagger trigger |

**Stagger rule:** Stagger triggers if and only if `combo_counter = 2`. Damage reduction applies independently. **Both Finisher and parry burst trigger Staggered at combo_counter = 2** — the same Staggered → counter-hit Telegraph → Attack → Cooldown sequence follows regardless of which upgrade lane triggered it.
**Knockback gate (Lu Bu only):** Lu Bu's PointBurst knockback impulse is subject to the same gate. At `combo_counter < 2`, Combat Core must suppress the knockback impulse for Iron Captain and Warlord targets. At `combo_counter = 2`, knockback fires alongside stagger. See Core Rules — Lu Bu Knockback Gate.
**Output range:** `[finisher_damage × 0.1, finisher_damage]` (at authoritative clamp floor of 0.1)
**Example:** `finisher_damage = 100`, `stagger_resist_multiplier = 0.5`: counter=1 → 50 damage, no stagger. counter=2 → 100 damage, stagger triggered.
**Input lower-bound guard:** Enemy Definition requires `finisher_damage ≥ 1.0` on receipt from the Combo System. Log a designer error and clamp to 1.0 if the Combo System delivers a value below 1.0.
**Runtime guard:** Clamp `stagger_resist_multiplier` to **`[0.1, 0.8]`** at load time (authoritative range — Tuning Knobs table is the single source of truth). Log a designer error if set outside `[0.2, 0.8]`.

---

### 3. Warlord Phase 2 Transition HP Threshold

```
phase_2_trigger_hp = warlord_max_hp × phase_2_threshold_ratio
```

| Variable | Symbol | Type | Range | Description |
|----------|--------|------|-------|-------------|
| Warlord max HP | `warlord_max_hp` | int | > 0; default: **800** | Warlord's maximum HP at run start |
| Phase threshold ratio | `phase_2_threshold_ratio` | float | **(0.0, 1.0) exclusive**; default: **0.5** | HP fraction at which Phase 2 transition fires |
| Phase 2 trigger HP | `phase_2_trigger_hp` | float | **(0, warlord_max_hp) exclusive** | HP value at which roar animation and Phase 2 data load. Cannot be 0 or warlord_max_hp — enforced by the (0.0, 1.0) exclusive constraint on phase_2_threshold_ratio. |

**Trigger rule:** Fires the moment `current_hp ≤ phase_2_trigger_hp`. Fires exactly once per run. If a single hit drops HP from above the threshold to 0, the roar animation fires before the death check resolves — the boss always completes its transition before dying. During the roar, HP is held at 1 (dead state is suspended); death resolves only after the roar completes (see Implementation Contracts section). **Phase 1 AttackEvent flush at transition:** At the moment Phase 2 transition fires, all pending Phase 1 AttackEvents for this enemy instance must be flushed via `ParrySystem.FlushEventsFor(enemy.Id)`. Phase 1 parry windows do not survive the transition.
**Output range:** 0 to `warlord_max_hp`. At defaults: `800 × 0.5 = 400 HP`.
**Example:** Warlord at 420 HP takes 25 damage → HP = 395 ≤ 400 → Phase 2 transition fires.
**Runtime guard:** `phase_2_threshold_ratio` must be validated in `(0.0, 1.0)` exclusive at load time. `0.0` silently removes Phase 2 and corrupts roar semantics; `1.0` triggers Phase 2 at fight start before the player lands a single hit. Log a designer error if set outside `[0.3, 0.7]`.

---

### 4. Attack Cooldown Constants

Flat constants per enemy type — no formula. Read directly by the Cooldown state in the enemy state machine.

| Enemy Type | `attack_cooldown_ms` |
|------------|---------------------|
| Levy Soldier | 2000ms |
| Halberd Pikeman | 1800ms |
| Fire Arrow Archer | 2500ms |
| Iron Captain | 3000ms |
| Warlord Phase 1 | 2500ms |
| Warlord Phase 2 | 1800ms |

All values are prototype defaults and tuning knobs.

---

### 5. Parry Burst Resist Against Elites

Applies only to Iron Captain and Warlord. Mirrors Formula 2 for the parry upgrade lane.

```
effective_burst_damage = burst_damage × stagger_resist_multiplier   [if combo_counter < 2; no Stagger]
effective_burst_damage = burst_damage                                [if combo_counter = 2; triggers Staggered]
```

| Variable | Symbol | Type | Range | Description |
|----------|--------|------|-------|-------------|
| Parry burst damage | `burst_damage` | float | > 0 | Burst damage from Parry System at current momentum |
| Combo counter | `combo_counter` | int | {0, 1, 2} | Player's combo count when burst resolves against target |
| Stagger resist multiplier | `stagger_resist_multiplier` | float | 0.1–0.8 | Same tuning knob as Formula 2; shared gate |
| Effective burst damage | `effective_burst_damage` | float | ≥ `burst_damage × 0.1` | Damage dealt to the elite; also determines Stagger trigger |

**Stagger rule:** At `combo_counter = 2`, burst triggers Staggered → counter-hit sequence identical to a full-combo Finisher. At `combo_counter < 2`, burst deals reduced damage and no Stagger.
**Output range:** `[burst_damage × 0.1, burst_damage]`
**Example:** `burst_damage = 80`, `stagger_resist_multiplier = 0.5`: counter=1 → 40 damage, no stagger. counter=2 → 80 damage, Staggered triggered.
**Input lower-bound guards:** The Combo System GDD is the source of truth for `finisher_damage` minimum — guard defined in Formula 2 above. The **Parry System GDD** is the source of truth for `burst_damage` minimum. Enemy Definition requires `burst_damage ≥ 1.0` on receipt from the Parry System — log a designer error and clamp to 1.0 if the Parry System delivers a value below 1.0.

## Edge Cases

- **If two enemies emit AttackEvents simultaneously in the same frame:** Both are queued and processed independently by the Parry System. A single parry resolves against the most recently received unresolved event only. The other event's damage resolves normally when its window elapses. Simultaneous threats are a genuine pressure situation — not a soft lock.

- **If the Iron Captain is killed during its counter-hit Telegraph (after stagger, before Attack fires):** Dead state takes priority. The counter-hit AttackEvent is discarded — no damage resolves. Enemy emits XP and returns to pool. Telegraph state grants no invulnerability.

- **If the Iron Captain is killed in the same frame its counter-hit Attack damage resolves:** AttackEvent is already queued in the Parry System. Damage resolves before the Dead state processes. Enemy then dies. Damage resolution precedes state-transition checks within a frame — document implementation order in Enemy AI GDD.

- **If the Archer's projectile is in flight but the player moves outside attack range before it lands:** Projectile resolves regardless of player position at landing. Range is checked at fire time (AttackEvent emission), not at hit time.

- **If the player takes damage from a secondary enemy while an Iron Captain or Warlord is in Staggered state:** The stagger sequence (Staggered → counter-hit Telegraph → counter-hit Attack) continues uninterrupted. Player damage from other sources does not break the sequence.

- **If the Warlord spawns at 18 minutes while the player's combo_counter = 1 or 2:** No effect on combo state. Counter resolves normally — a Finisher landing at counter=2 staggers the Warlord; at counter<2 it deals 50% damage with no stagger.

- **If a single hit drops Warlord HP from above 400 to exactly 0:** Phase 2 transition fires before the death check resolves. Warlord HP is held at 1 for the duration of the roar animation — dead state is suppressed. No AttackEvents are emitted during the roar. Player attacks deal damage normally but HP cannot drop below 1 until roar completes. After the 1.2-second roar, the death check resolves and the Warlord dies. Specified behavior — flag for playtest to confirm it reads correctly. (Implementation: EnemySystemManager must check phase transition before dead state in the same frame; see Implementation Contracts.)

- **If the Warlord Phase 2 ground slam AttackEvent is received by the Parry System:** `Parriable = false` is the gate. The Parry System must not open a parry window. No parry UI appears. Contract the Parry System GDD must enforce.

- **If an enemy is returned to the object pool while mid-Telegraph or mid-Attack animation:** Outside Enemy Definition scope. The object pool implementation must flush all pending AttackEvents for that `AttackerId` from the Parry System queue before the object is recycled. Flag as a required contract in Enemy AI GDD and Parry System GDD.

- **Chase steering and separation:** Enemies in Chase state apply a minimum separation force against adjacent enemies within `separation_radius` (tuning knob; default 0.5u). Separation priority is lower than goal pursuit — enemies still close on the player but avoid stacking. Exact steering implementation (Unity NavMesh avoidance or custom separation vector) is the Enemy AI GDD's responsibility; this GDD requires the behavior exists.

- **Aggro spread:** Enemies do not snap to Chase simultaneously when the player enters range. Each enemy applies a per-instance random aggro-delay jitter of 1–3 frames before transitioning from Idle to Chase. This distributes the AI update load across frames without observable gameplay change.

- **Archer chase-range fallback:** If the Fire Arrow Archer's Chase state has been active for `archer_chase_fallback_s` seconds (tuning knob; default 5s) without closing to 6u attack range, the Archer transitions to Telegraph state at its current position and fires from range. This prevents the Archer from looping indefinitely in Chase against a kiting player. **Readability requirement (Pillar 1):** The fallback transition must produce a visible signal before the Telegraph arrowhead appears. Required: play a distinct stance-change animation or audio cue of ≥200ms when the fallback fires. The player must have at least one frame of visual/audio warning that the Archer switched to ranged mode before the parry window opens. The fallback respects `attack_cooldown_ms` — if cooldown has not elapsed when the 5-second timer expires, the Archer enters Cooldown first, then fires from position on next cooldown exit. **Timer reset rule:** The 5-second fallback timer does NOT reset when entering this cooldown-gated Cooldown state. After the fallback fires once from range, the timer resets and the Archer re-enters normal Chase behavior. If the Archer still cannot close to attack range in the next `archer_chase_fallback_s` seconds, the fallback fires again. The fallback telegraph window uses the full `archer_telegraph_ms` (default 600ms) — it is not a shortened window.

- **If the Warlord crosses the Phase 2 HP threshold (≤ 400 HP) while in Staggered state:** The Phase 2 threshold check occurs in the damage phase, before state-transition processing. If the threshold is crossed mid-Stagger (e.g., from player AoE or auto-attack damage during the stagger window), the EnemySystemManager atomically clamps HP to 1 and locks the Roar state within the damage phase. State-transition phase then processes: the Stagger timer is cancelled and the enemy enters Roar — the counter-hit Telegraph that would normally follow Stagger is **skipped**. Phase 2 data loads at Roar exit. The player is not penalized with a counter-hit for damage dealt during the stagger window; they receive the Phase 2 roar cutscene instead.

- **Playtest-first, no rule needed yet:** Maximum simultaneous AttackEvents before readability breaks under 200-enemy load; whether the Iron Captain counter-hit feels unfair when the player is under infantry pressure during the stagger window; whether the Warlord roar on a one-shot kill reads correctly to players.

## Implementation Contracts

Architecture requirements that must be established before any enemy code is written. These are structural constraints — not tuning knobs.

**1. EnemySystemManager — frame-order contract**
Enemy behavior must be driven by a single `EnemySystemManager`, not by individual `MonoBehaviour.Update()` calls. The manager processes all 200 enemies in two sequential phases per frame:
1. **Damage phase** — apply incoming damage → clamp HP to `max(0, hp - damage)` → check Phase 2 HP threshold (`current_hp ≤ phase_2_trigger_hp`, evaluated on post-damage value) → if threshold met: clamp HP to 1 atomically and lock roar state (dead state suppressed) → resolve AttackEvents → check stagger triggers
2. **State-transition phase** — process Dead checks (only after roar lock is cleared), Staggered exits, Cooldown expiry

**Phase 2 clamp ordering:** The HP clamp to 1 and the roar-state lock are applied atomically inside the damage phase the moment `current_hp ≤ phase_2_trigger_hp` is detected — before the state-transition phase evaluates dead state. If a single hit reduces HP from above 400 to ≤ 0, both the phase-2 threshold check and the clamp execute within the damage phase; dead state evaluation is deferred to the next state-transition phase after the roar completes. There is no race condition between the clamp and dead state if this order is followed.

**Cooldown → Telegraph shortcut:** When an enemy is in Cooldown state and its attack range condition is already satisfied (player in range), the EnemySystemManager may transition Cooldown → Telegraph directly on cooldown expiry, bypassing Chase. This avoids 200 one-frame Chase ticks per cooldown cycle at full enemy density. If Chase is entered, minimum hold = 0 frames (immediate Telegraph on the same tick if range condition is met). Do NOT apply separation force during a 0-frame Chase pass-through.

**Phase 2 data load hook:** Phase 1 and Phase 2 ScriptableObject references must both be loaded at enemy spawn time (not on-demand at transition). At the START of the State-transition phase in the frame where Phase 2 triggers, the manager atomically swaps the active data reference from Phase 1 to Phase 2 — before Dead checks, Staggered exits, or Cooldown expiry processing. Systems that cache `attack_cooldown_ms` or move speed at frame-start will read Phase 2 values starting the next frame; the swap-frame uses Phase 2 values for all state-transition processing.

**Deferred registration:** `EnemyController` MonoBehaviours must NOT register with `EnemySystemManager` in `OnEnable()`. Object pool re-enables fire `OnEnable()` mid-frame, which mutates the manager's active entity list during iteration. Instead: `OnEnable()` adds the controller to a `RegisterPending` staging list. The manager flushes `RegisterPending` at the very start of each `Tick()`, before phase 1 processing begins.

**`AttackStartTime` precision:** `AttackStartTime` in the AttackEvent must be computed as `Time.time - runStartTime` (run-local float), where `runStartTime` is recorded at the first frame of the current run. Raw `Time.time` (application uptime) degrades to ~4ms float precision error after ~4,000 seconds of application runtime — detectable at 350ms parry windows in long playtest sessions.

**`IsPhaseTransitionLocked` property contract:** `EnemyController` must expose a `bool IsPhaseTransitionLocked { get; private set; }` property. Set to `true` atomically when HP clamp to 1 fires (Roar lock engaged). Set to `false` after Roar exits (either via animation callback or watchdog). This property is the observable used by AC-05 and AC-10 acceptance tests — it must be a `public` readable property on the MonoBehaviour (or ECS component equivalent) so unit tests can assert its state without requiring animation system integration.

This order guarantees: (a) counter-hit AttackEvent emits before Staggered exits, (b) Phase 2 roar fires before Dead state in the same frame, (c) Phase 2 data is active for all state processing in the transition frame, (d) Iron Captain simultaneous-death-and-attack-resolution edge case resolves correctly. **Create an ADR before writing any enemy code.**

**2. AttackEvent pool flush — explicit method contract**
The pool's `ReturnToPool(EnemyController enemy)` method must call `ParrySystem.FlushEventsFor(enemy.Id)` **before** deactivating the GameObject. Do not use `OnDisable()` or `OnDestroy()` as the flush trigger — Unity does not guarantee ordering. The Parry System owns an `AttackerId → List<AttackEvent>` dictionary (not a flat queue) so flush is O(1). **`SetActive(false)` restriction:** Calling `GameObject.SetActive(false)` on enemy GameObjects is architecturally forbidden from any code path other than `ObjectPool.ReturnToPool`. Any other deactivation path bypasses the flush contract silently. Enforce this as an ADR invariant. **`AttackerId` uniqueness:** `AttackerId` must be unique per spawn instance, not per pool slot. A new spawn always receives a new ID even if it reuses an existing pool slot — prevents ghost-cancel bugs from orphaned in-flight projectiles.

**3. Spatial partitioning — range check budget**
Aggro and attack range checks at 200 enemies must not be brute-force O(n²) comparisons (~20,100/frame). Required approach (choose one, document in Enemy AI GDD):
- Unity Physics layer-filtered **`Physics.OverlapSphereNonAlloc`** with a pre-allocated `Collider[]` results buffer — buffer must be sized to `max_enemy_count` (200) and allocated once at `EnemySystemManager` initialization. `Physics.OverlapSphere` (allocating overload) is forbidden; it allocates a managed array every call. An undersized buffer silently drops enemies from aggro/attack-range checks — appears as catatonic AI, not a crash, or
- Custom spatial grid (cell size = max aggro range ≈ 10u; update on enemy move)

**4. Telegraph VFX rendering — draw call budget**
200 simultaneous telegraph VFX must not use individual `ParticleSystem` instances (200 × ~2 draw calls = 400 of the 1,500 budget). Required: VFX Graph with GPU instancing, one VFX Graph asset per telegraph shape type. VFX Graph integrates with URP's render graph automatically — **no custom `ScriptableRendererFeature` or `RecordRenderGraph` pass is needed for the VFX itself.** `RecordRenderGraph` is only required if the team adds a screen-space overlay effect on top of combat (e.g., parry flash vignette, finisher screen shake post-process) — that path must use `RecordRenderGraph` per Unity 6.3 LTS URP requirements. **Mandate VFX Graph approach in the VFX & Audio GDD before that system is authored.**

## Dependencies

| System | Direction | Nature | Interface |
|--------|-----------|--------|-----------|
| **Parry System** | Enemy Definition → Parry | Hard | Parry System subscribes to `AttackEvent`. Owns parry window derivation from `TelegraphDurationMs`. Must respect `Parriable = false`. Must flush AttackEvents for pooled enemies on death. |
| **Enemy AI** | Enemy Definition → Enemy AI | Hard | Enemy AI reads all per-type data: aggro range, move speed, attack range, stagger resistance, attack cooldown. Drives state machine execution. Must document damage-resolution-before-state-transition frame order. |
| **Progression System** | Enemy Definition → Progression | Hard | Progression System reads `xp_weight` per type and applies its own `xp_scalar`. XP curve shape and level thresholds are Progression System's responsibility. |
| **Wave Spawner** | Enemy Definition → Wave Spawner | Hard | Wave Spawner reads type identifiers and spawn weights. Spawn scheduling (density escalation formula, elite/boss timing) belongs to Wave Spawner GDD. |
| **Damage & Health** | Enemy Definition → Damage & Health | Hard | `AttackDamage` from AttackEvent is passed to Damage & Health on hit. Player HP management and death event are Damage & Health's responsibility. |
| **Combo System** | Combo System → Enemy Definition | Hard | Combo System produces `combo_counter` at Finisher trigger time. Stagger resistance formula reads this value at hit resolution. **Also:** Parry System must read `combo_counter` when resolving burst damage against elite types — both upgrade lanes gate on the same full-combo requirement. |
| **Combat Feedback UI** | Enemy Definition → Combat Feedback UI | Soft | Telegraph shapes and durations defined here are the source of truth for VFX/UI telegraph rendering. Combat Feedback UI must implement each telegraph type per this spec. |
| **Combat Core** | Enemy Definition → Combat Core | Hard | Combat Core must check target's elite type before applying Lu Bu's PointBurst knockback impulse: suppress knockback for Iron Captain and Warlord at `combo_counter < 2`; apply knockback at `combo_counter = 2`. Basic infantry have no knockback-resist. |
| **Dodge System** *(new — not yet in systems index)* | Dodge System → Enemy Definition | Hard (Warlord Phase 2 only) | Warlord Phase 2 ground slam is not parriable — player must dodge. Without a functional Dodge System, the Warlord's Phase 2 cannot be completed as designed. Dodge System must be added to the systems index before Warlord implementation begins. |

## Tuning Knobs

All values are prototype defaults. Change during playtesting — not during implementation.

| Knob | Default | Safe Range | Affects |
|------|---------|------------|---------|
| `levy_hp` | 30 | 10–80 | Time-to-kill for most common enemy; drives combo cadence pacing |
| `pikeman_hp` | 40 | 15–100 | — |
| `archer_hp` | 25 | 10–60 | Fragile by design — dies to a Finisher; raising above 60 disrupts this |
| `iron_captain_hp` (5 min) | 300 | 150–500 | First elite encounter duration |
| `iron_captain_hp` (10 min) | 375 | 200–600 | Second appearance; +25% HP over first |
| `iron_captain_hp` (15 min) | 450 | 250–700 | Third appearance; +50% HP over first |
| `warlord_max_hp` | 800 | 400–1200 | Boss fight length; drives `phase_2_trigger_hp` automatically via ratio |
| `levy_attack_cooldown_ms` | 2000 | 800–3000 | Infantry attack pace; below 800 creates unreadable spam |
| `pikeman_attack_cooldown_ms` | 1800 | 800–3000 | — |
| `archer_attack_cooldown_ms` | 2500 | 1200–4000 | Ranged pressure frequency |
| `iron_captain_attack_cooldown_ms` | 3000 | 1500–5000 | Elite aggression pacing |
| `warlord_p1_attack_cooldown_ms` | 2500 | 1200–4000 | — |
| `warlord_p2_attack_cooldown_ms` | 1800 | 800–3000 | Phase 2 feels faster; below 800 becomes unfair |
| `levy_telegraph_ms` | 400 | 200–800 | Parry window fairness for most common enemy |
| `pikeman_telegraph_ms` | 350 | 200–700 | — |
| `archer_telegraph_ms` | 600 | 300–1000 | Must allow player to close 6u OR time a parry mid-approach |
| `iron_captain_std_telegraph_ms` | 500 | 300–800 | Standard elite attack readability |
| `iron_captain_counter_telegraph_ms` (5 min) | 600 | 400–900 | Post-stagger counter-hit; below 400 may feel unfair given stagger setup cost |
| `iron_captain_counter_telegraph_ms` (10 min) | 550 | 350–800 | Tighter window; escalates challenge at second checkpoint |
| `iron_captain_counter_telegraph_ms` (15 min) | **450** | 350–550 | Smooth escalation from 550ms (10-min); faster than Levy Soldier 400ms standard — maintains legibility range while maximising pressure |
| `warlord_slam_telegraph_ms` | 800 | 500–1200 | Must allow dodge out of 2.0u radius; below 500 may require faster dodge input |
| `stagger_resist_multiplier` | 0.5 | 0.2–0.8 | Elite damage on non-full-combo Finisher/burst; below 0.2 makes elite near-immune; above 0.8 makes the gate nearly invisible (99% of full damage = mechanic disabled silently). Runtime: clamp to [0.1, 0.8]; log error if set outside [0.2, 0.8] (not just a warning — values above 0.8 silently disable the mechanic) |
| `iron_captain_stagger_duration_ms` | 800 | 400–1500 | Window before counter-hit; shorter = higher pressure on the player |
| `warlord_stagger_duration_ms` | 1000 | 500–1800 | Same as Iron Captain; longer reflects higher-stakes encounter |
| `phase_2_threshold_ratio` | 0.5 | 0.3–0.7 | When Warlord Phase 2 triggers; above 0.7 makes Phase 2 brief; below 0.3 nearly eliminates it. Runtime: validate in (0.0, 1.0) exclusive; log error outside [0.3, 0.7] |
| `xp_weight[levy_soldier]` | 1 | 1–5 | Relative XP pacing; change only if kill rate changes significantly |
| `xp_weight[pikeman]` | 2 | 1–5 | — |
| `xp_weight[archer]` | 2 | 1–5 | — |
| `xp_weight[iron_captain]` | 15 | 5–30 | Elite XP reward feeling |
| `xp_weight[warlord]` | 50 | 20–100 | Final-kill reward; very high values distort overall XP pacing |
| `archer_projectile_speed` | 8 u/s | 4–16 u/s | Travel time; below 4 feels floating; above 16 makes parry timing functionally instant |
| `separation_radius` | 0.5u | 0.3–1.0u | Minimum enemy-to-enemy separation in Chase state; prevents physics clumping at 200 enemies |
| `archer_chase_fallback_s` | 5s | 3–10s | Seconds in Chase without closing to 6u before Archer fires from current position; prevents infinite Chase loop against kiting player |
| `archer_projectile_count` | 1 | 1–3 | Number of projectiles fired per attack animation; all cancelled by a single successful parry; above 3 creates readability issues at 200 enemies |

## Visual/Audio Requirements

*Full VFX and audio specs generated by `/asset-spec system:enemy-definition` after art bible is approved. This section defines the behavioral requirements the art pass must satisfy.*

**Telegraph requirements (shape-first, color-second — Pillar 1 / colorblind safety):**

| Enemy | Telegraph Shape | Color | Must be distinguishable by |
|-------|----------------|-------|---------------------------|
| Levy Soldier | Horizontal ring glow around torso | Muted gold | Shape: ring |
| Halberd Pikeman | Directional line from weapon tip toward player | Orange-red | Shape: directional line |
| Fire Arrow Archer | Downward arrowhead above enemy model | Orange | Shape: arrowhead (above, not ground) |
| Iron Captain (standard) | Thick expanding ring around body | Bright red | Shape: thicker ring than Levy |
| Iron Captain (counter-hit) | 270° arc sweeping from body along ground | Bright red | Shape: arc — largest telegraph in MVP |
| Warlord Phase 2 slam | Filled circle expanding from Warlord position | Bright red | Shape: filled circle — NOT a parry ring |

**Critical rule:** No two enemy types may share both the same shape AND the same color. Shape is the primary differentiator; color is secondary reinforcement only.

**The counter-hit arc (Iron Captain / Warlord) is the most important telegraph in the game** — it must remain readable at 200 enemies on screen. No other VFX may use a similar arc shape or compete for the same visual space when an elite counter-hit is active.

**Parry prompt vs. dodge prompt differentiation:** Parriable attacks show a parry-feedback indicator (defined in Combat Feedback UI GDD). The Warlord Phase 2 ground slam (`Parriable = false`) must NOT show the parry indicator. A dodge-prompt indicator (if designed by the Dodge System GDD) should be visually distinct from the parry indicator — different color or shape.

**Audio:** Per-type attack audio is defined in the VFX & Audio GDD. Minimum requirement: the counter-hit attack (Iron Captain and Warlord) must have a distinct, louder audio cue than standard attacks. The Warlord Phase 2 roar must have a unique sound that communicates a state change, not just another attack.

## Acceptance Criteria

| # | Criterion | Story Type | Gate |
|---|-----------|------------|------|
| AC-01 | **GIVEN** a Levy Soldier spawns, **WHEN** the player is held at 6u distance (outside 5u aggro range) for 60 frames (1 second), **THEN** the enemy remains in Idle state and does not move. **Pass condition:** assert `enemy.position == spawnPosition` (within 0.001u tolerance) at t=0 AND t=1s; assert `enemy.velocity == Vector3.zero` throughout the observation window. | Logic | BLOCKING |
| AC-02 | **GIVEN** a Levy Soldier is in Chase state, **WHEN** the player enters 0.8-unit melee range, **THEN** in the same frame the enemy emits one AttackEvent with `TelegraphDurationMs = levy_telegraph_ms` (read from config, default 400) and begins its telegraph animation. **Pass condition:** `AttackEvent.TelegraphDurationMs == levy_telegraph_ms` as set in the active config, not a hardcoded 400 constant. | Logic | BLOCKING |
| AC-03 | **GIVEN** an Iron Captain in Chase state, **WHEN** a Finisher lands with `combo_counter ∈ {0, 1}`, **THEN** the Iron Captain takes `finisher_damage × stagger_resist_multiplier` damage (default: × 0.5), does not enter Staggered state, enters Cooldown state (not Chase directly — Chase follows after cooldown elapses), and no knockback impulse is applied. **Pass condition:** assert state == Cooldown immediately after hit; assert stagger animation did not play; assert knockback was NOT applied — verify via test mock/spy on `CombatCore.ApplyKnockback()`, asserting the method was NOT called for this Iron Captain target during Finisher resolution. Do not use position delta (Chase pathfinding movement is indistinguishable from knockback displacement). | Logic | BLOCKING |
| AC-04 | **GIVEN** an Iron Captain (5-min tier) in Chase state, **WHEN** a Finisher lands with `combo_counter = 2`, **THEN** the Iron Captain takes full `finisher_damage`, enters Staggered state for `iron_captain_stagger_duration_ms` (default 800ms), then emits a counter-hit AttackEvent with `TelegraphDurationMs = iron_captain_counter_telegraph_ms` (5-min config value, default 600) and `IsCounterHit = true`. Enters Cooldown after Attack resolves (not Chase directly). **Pass condition:** assert state sequence Staggered → Telegraph → Attack → Cooldown; assert AttackEvent.IsCounterHit == true; assert TelegraphDurationMs matches config value (not hardcoded 600). **Note:** parameterise this test for all three tier configs (600ms / 550ms / **450ms**) — AC-04 must pass for each Iron Captain appearance. | Logic | BLOCKING |
| AC-05 | **GIVEN** the Warlord is at 420 HP, **WHEN** a hit deals 25 damage (HP → 395 ≤ phase_2_trigger_hp = 400), **THEN** in the same frame: the Phase 2 roar animation begins, Warlord HP is clamped to 1 (dead state suppressed), and no AttackEvents are emitted for the 1.2-second roar duration. **Pass condition:** assert `enemy.IsPhaseTransitionLocked == true` immediately after hit (Roar state active — use this observable flag, not animation state); assert HP == 1 immediately after hit; assert AttackEvent count == 0 during the 1.2s window; assert dead state not entered during roar. (Post-roar stat verification is covered by AC-13.) | Logic | BLOCKING |
| AC-06 | **GIVEN** the Warlord emits a Phase 2 ground slam AttackEvent (`Parriable = false`), **WHEN** the Parry System receives it, **THEN** no parry window opens and a parry input has no effect. **Pass condition:** assert `ParrySystem.HasOpenWindowFor(attackEvent.AttackerId) == false` after receiving the AttackEvent; assert that a parry input during the slam telegraph window does not trigger a burst or modify `combo_counter`. (Parry UI state is ADVISORY — test separately as a Visual/Feel AC.) | Integration | BLOCKING |
| AC-07 | **GIVEN** `xp_scalar = 10.0` AND a Levy Soldier enters Dead state, **THEN** `xp_drop = 10.0` XP is emitted. **GIVEN** `xp_scalar = 10.0` AND an Iron Captain enters Dead state, **THEN** `xp_drop = 150.0` XP is emitted. **Pass condition:** assert emitted XP value matches expected to within 0.001 tolerance (float comparison, not integer equality). | Logic | BLOCKING |
| AC-08 | **GIVEN** a Fire Arrow Archer begins its attack animation (AttackEvent emitted at `AttackStartTime`, `TelegraphDurationMs = archer_telegraph_ms`), **WHEN** the player parries within `[AttackStartTime, AttackStartTime + (archer_telegraph_ms / 1000.0)]`, **THEN** the Parry System suppresses the AttackEvent (marks it resolved without damage) and all in-flight projectiles with the matching `AttackerId` are returned to the projectile pool with `AttackDamage = 0`. **Pass condition:** assert Damage & Health receives no damage event from this AttackerId after the parry; assert projectile pool contains a newly-returned projectile from this Archer. The Parry System owns suppression — Damage & Health must not receive the AttackEvent at all (not neutralise it internally). | Integration | BLOCKING |
| ~~AC-09~~ | ~~Removed — fully subsumed by AC-03 (which covers `combo_counter ∈ {0, 1}`; parameterize that test for both values).~~ | — | — |
| AC-10 | **GIVEN** the Warlord transitions to Phase 2 and enters the roar animation, **WHEN** 0.6 seconds have elapsed mid-roar, **THEN** zero AttackEvents have been emitted since roar started; the enemy state machine reports state = Roar; player attacks are received and processed by the damage phase (HP clamp prevents reduction below 1 — player attacks do not kill the Warlord mid-roar). The state machine must remain in Roar state for the full 1.2 seconds regardless of any player attack events received during that window. **Pass condition:** assert `enemy.IsPhaseTransitionLocked == true` at t=0.6s (Roar state active); assert AttackEvent count == 0; assert HP == 1 even if player deals 999 damage during the window. | Logic | BLOCKING |
| AC-11a | **GIVEN** a Fire Arrow Archer performs an attack animation (any projectile count), **THEN** exactly one AttackEvent is emitted for that animation. **Pass condition:** assert `AttackEvent` count emitted by this Archer during one attack animation == 1. (Unit test — no Parry System required.) | Logic | BLOCKING |
| AC-11b | **GIVEN** a Fire Arrow Archer emits an AttackEvent and fires `archer_projectile_count` projectiles (config value, default 1), **WHEN** the player parries within the parry window, **THEN** all `archer_projectile_count` in-flight projectiles are deactivated (`projectile.IsActive == false`) and no damage event reaches Damage & Health. **Pass condition:** assert `projectile.IsActive == false` for each launched projectile from this AttackerId after parry; assert Damage & Health receives 0 damage events from this AttackerId. Parameterize across `archer_projectile_count` values 1 and 2. (Integration test — requires live Parry System.) | Integration | BLOCKING |
| AC-12 | **GIVEN** a Levy Soldier in Chase state with `current_hp > finisher_damage` (ensures the hit does not kill — preventing a Dead state assertion instead of Cooldown), **WHEN** a Finisher lands with `combo_counter ∈ {0, 1, 2}`, **THEN** the Levy Soldier takes full `finisher_damage` (no resist multiplier applied), transitions to Cooldown state (not Staggered), and plays a stagger animation overlay. **Pass condition:** for each combo_counter value — assert HP reduced by full finisher_damage; assert state == Cooldown (never Staggered); assert no stagger-resist formula was applied. Parameterize the test across all three combo_counter values. | Logic | BLOCKING |
| AC-13 | **GIVEN** the Warlord completes the Phase 2 roar, **WHEN** it re-enters Chase state, **THEN** its move speed matches `config.warlord_p2_move_speed` and `attack_cooldown_ms` matches `config.warlord_p2_attack_cooldown_ms`. **Pass condition:** assert `enemy.MoveSpeed == config.warlord_p2_move_speed` (do not hardcode 2.0); assert `enemy.AttackCooldownMs == config.warlord_p2_attack_cooldown_ms` (do not hardcode 1800). | Config/Data | ADVISORY |
| AC-14 | **GIVEN** an Iron Captain is within range of the player's parry burst AND `combo_counter ∈ {0, 1}`, **WHEN** the burst resolves, **THEN** the Iron Captain takes `burst_damage × stagger_resist_multiplier` damage (default × 0.5) and does not enter Staggered state. **Pass condition:** assert Iron Captain HP reduced by `burst_damage × stagger_resist_multiplier`; assert state ≠ Staggered after burst resolves. Parameterize across both `combo_counter` values. | Logic | BLOCKING |
| AC-15 | **GIVEN** an Iron Captain is within range of the player's parry burst AND `combo_counter = 2`, **WHEN** the burst resolves, **THEN** the Iron Captain takes full `burst_damage`, enters Staggered state for `iron_captain_stagger_duration_ms`, then emits a counter-hit AttackEvent (`IsCounterHit = true`). **Pass condition:** assert HP reduced by full `burst_damage`; assert state sequence enters Staggered → Telegraph → Attack → Cooldown; assert `AttackEvent.IsCounterHit == true`. | Logic | BLOCKING |
| AC-16 | **GIVEN** a Fire Arrow Archer has been in Chase state for `archer_chase_fallback_s` (5s) without closing to 6u attack range, AND `attack_cooldown_ms` has already elapsed, **WHEN** the fallback timer expires, **THEN** the Archer transitions to Telegraph at its current position and emits an AttackEvent with `TelegraphDurationMs = archer_telegraph_ms`. **Pass condition:** assert Archer is NOT within 6u of player at transition; assert AttackEvent.AttackerPosition ≠ player position (fires from Archer's own position); assert `AttackEvent.TelegraphDurationMs == archer_telegraph_ms` (default 600 — full window, not shortened). Logic portion is BLOCKING. **Also assert** a distinct visual/audio warning cue fires ≥200ms before AttackEvent is emitted — this assertion is Visual/Feel, ADVISORY, verified by screenshot + lead sign-off. | Logic | BLOCKING |
| AC-17 | **GIVEN** a Fire Arrow Archer's fallback timer expires while `attack_cooldown_ms` has NOT yet elapsed, **THEN** the Archer enters Cooldown (not Telegraph). **GIVEN** cooldown then elapses, **THEN** the Archer transitions to Telegraph from its current (non-6u-close) position. **Pass condition:** assert fallback timer did NOT reset during the cooldown state (timer continues from expiry point — no restart); assert Telegraph fires from Archer's current position (still not within 6u) after cooldown expires; assert `TelegraphDurationMs == archer_telegraph_ms`. | Logic | BLOCKING |
| AC-19 | **GIVEN** the Warlord has an open Phase 1 AttackEvent in the Parry System's event queue (parry window still active), **WHEN** a hit drops Warlord HP to ≤ `phase_2_trigger_hp` triggering the Phase 2 transition, **THEN** `ParrySystem.HasOpenWindowFor(warlord.AttackerId) == false` immediately after the transition frame (Phase 1 windows flushed at transition). **Pass condition:** assert no Phase 1 event window survives the transition; assert Parry System receives no damage callback from the pre-transition event after the transition frame. | Integration | BLOCKING |

## Open Questions

| # | Question | Owner | Resolution Path |
|---|----------|-------|----------------|
| OQ-01 | ~~Is one Iron Captain type for all three checkpoints (5/10/15 min) sufficient?~~ | ~~Playtest~~ | **RESOLVED (2026-05-13, updated 2026-05-18):** Stat escalation adopted. 5-min: 300 HP / 600ms counter-hit. 10-min: 375 HP / 550ms counter-hit. 15-min: 450 HP / **450ms** counter-hit (smooth escalation — 600→550→450ms; faster than Levy Soldier standard 400ms while staying in legibility range). No new entity required. |
| OQ-02 | Does the Warlord Phase 2 roar animation on a one-shot kill read correctly, or does it feel like a bug? | Playtest | See Edge Cases — specified behavior. Monitor first playtest reaction. |
| OQ-03 | Maximum simultaneous AttackEvents before readability breaks under 200-enemy load? | Prototype | Establish empirically during prototype phase. May require a cap on simultaneous telegraph animations. |
| OQ-04 | Dodge System design — what input, timing window, and invincibility frames? | New GDD required | Add Dodge System to the systems index. Warlord Phase 2 cannot be fully tested without it. Target: design before Warlord implementation begins. |
| OQ-05 | Does the Fire Arrow Archer's projectile parry feel fair mid-approach (player moving toward Archer while timing the parry)? | Playtest | The 600ms window and generous parry input from the Parry System GDD should cover this. Confirm in prototype. Fallback: make Archer non-parriable (pressure mechanic only). |

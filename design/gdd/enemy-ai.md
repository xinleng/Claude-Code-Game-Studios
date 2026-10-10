# Enemy AI

> **Status**: Approved (self-review 2026-06-19)
> **Author**: xinleng + Claude Code agents
> **Last Updated**: 2026-06-19
> **Implements Pillar**: Pillar 1 (Readable Chaos)

## Overview

Enemy AI is the runtime executor of the enemy behaviour that Enemy Definition specifies as data. Where Enemy Definition is the pure spec (stats, the state table, the `AttackEvent` contract, telegraph rules), Enemy AI is the system that *runs* it: it drives every enemy through the shared state machine (Idle → Chase → Telegraph → Attack → Cooldown, with Staggered/Roar/Dead branches), steers them toward the player while avoiding stacking, performs perception (aggro and attack-range checks via spatial partitioning), emits `AttackEvent`s with the correct fields (including `IsCounterHit` and `DamageType`), and enforces the damage-resolution-before-state-transition frame order. All 200 enemies are driven by a single `EnemySystemManager`, not per-enemy `Update()` calls (Enemy Definition Implementation Contract #1).

The player never sees Enemy AI as a system; they read it as an army that behaves — soldiers that close in, telegraph honestly, and don't clump into an unreadable blob. Enemy AI exists to make 200 enemies legible and fair (Pillar 1).

## Player Fantasy

Enemy AI is the feeling of a *real* army, not a swarm of noise. Soldiers advance with intent, spread so the player can read individual threats, and every attack is announced before it lands. The fantasy this protects is the general's mastery: the horde is dangerous but *knowable* — the player who reads the battlefield is always one telegraph ahead. Enemy AI succeeds when the chaos has grammar: you can always point at what is about to hit you and why. It is infrastructure in service of Pillar 1 — the enemies are interesting because they are *readable*, never because they are clever.

## Detailed Design

### Core Rules

1. **Single manager.** All enemies are ticked by one `EnemySystemManager` (`[DefaultExecutionOrder(-100)]`, runs before Parry/UI), processing two sequential phases per frame — **damage phase** then **state-transition phase** — exactly as specified in enemy-definition.md Implementation Contract #1. Enemy AI implements that contract; it does not redefine it.

2. **State machine execution.** Enemy AI advances each enemy through the states defined in enemy-definition.md (Idle, Chase, Telegraph, Attack, Cooldown, Staggered, Roar, Dead). Transitions are data-driven (same machine for all types; elites/boss differ by values, not states). Enemy AI owns the *evaluation* of transition conditions each tick.

3. **Perception (aggro + attack range).** Range checks use `Physics.OverlapSphereNonAlloc` (or a custom spatial grid) against a pre-allocated buffer sized to `max_enemy_count` — never brute-force O(n²) (enemy-definition Implementation Contract #3). Aggro range per type (Levy 5u, Pikeman 5u, Archer 8u, Iron Captain 6u, Warlord 10u). On entering aggro range, the enemy transitions Idle → Chase after a 1–3 frame per-instance jitter (aggro spread, enemy-definition edge case).

4. **Steering (Chase).** In Chase, an enemy moves toward the player at its `move_speed`, combined with a **separation force** against neighbours within `separation_radius` (0.5u) so enemies avoid stacking (Pillar 1 readability). Separation is lower priority than goal pursuit — enemies still close in but spread out. Implementation: Unity NavMesh obstacle avoidance OR a custom separation vector (chosen in the architecture ADR; both satisfy the behavioural requirement). Movement is locked (velocity = 0) during Staggered and Roar (enemy-definition).

5. **Telegraph + AttackEvent emission.** On entering Telegraph (in attack range and cooldown elapsed, or the Archer chase-fallback), the enemy emits exactly one `AttackEvent` with: `AttackerId`, `AttackStartTime` (run-local), `TelegraphDurationMs` (per type/tier), `AttackDamage`, `AttackerPosition`, `Parriable`, `IsCounterHit`, and `DamageType`. Enemy AI sets `IsCounterHit = true` only on the counter-hit emitted from Staggered exit, and sets `DamageType` deterministically: `CounterHit` if counter-hit, `Slam` for the Warlord Phase 2 ground slam (`Parriable = false`), `Projectile` for the Archer, else `Standard` (matches enemy-definition AttackEvent contract).

6. **Attack resolution.** On Telegraph completion, the Attack state resolves: deal `AttackDamage` (basic infantry / elite standard), launch the Archer projectile, or resolve the slam. Damage to the player flows through the Parry System (which may suppress it) → Damage & Health; the Slam (`DamageType.Slam`) bypasses Parry and is forwarded directly to Damage & Health (damage-health DamageType source contract). Damage resolution precedes state-transition checks within the frame (enemy-definition edge cases).

7. **Stagger handling.** When the Combo System / Parry System reports a full-combo (`combo_counter == 2`) Finisher or parry burst on an elite, Enemy AI transitions the elite to Staggered for `stagger_duration_ms`, locks movement, then emits the counter-hit `AttackEvent` on exit (deterministic, uninterruptible) — unless HP reaches 0 during stagger (→ Dead, counter-hit discarded). The staggered elite remains a valid auto-attack target during the stagger window (the designed escape path, enemy-definition).

8. **Warlord phase transition.** Enemy AI implements the Roar lock, HP-clamp-to-1, Phase 2 data swap, and the watchdog exactly per enemy-definition.md (the `IsPhaseTransitionLocked` property, the 1700ms watchdog). It flushes Phase 1 parry windows via `ParrySystem.FlushEventsFor(enemy.Id)` at transition.

9. **Archer chase-fallback.** If an Archer cannot close to 6u within `archer_chase_fallback_s` (5s), Enemy AI fires from its current position (with the ≥200ms readability cue), respecting cooldown and the timer-reset rules in enemy-definition.md. The Cooldown → Telegraph shortcut is applied for non-Archer types only.

10. **Pooling discipline.** Enemy AI uses deferred registration (`RegisterPending` staging list flushed at tick start) and calls `ParrySystem.FlushEventsFor(enemy.Id)` via `ObjectPool.ReturnToPool` before deactivation — never `SetActive(false)` elsewhere (enemy-definition Implementation Contract #2). `AttackerId` is unique per spawn instance.

### States and Transitions

Enemy AI executes the state table defined in **enemy-definition.md §States and Transitions** verbatim — that table is the authoritative contract. Enemy AI adds no states; it owns the per-tick evaluation of each transition condition and the steering behaviour within Chase. (See enemy-definition.md for the full Idle/Chase/Telegraph/Attack/Cooldown/Staggered/Roar/Dead table.)

### Interactions with Other Systems

| System | Data In | Data Out | Interface Owner |
|---|---|---|---|
| **Enemy Definition** | All per-type data, the state table, the AttackEvent contract, implementation contracts | None | Enemy Definition owns the spec; Enemy AI executes it |
| **Parry System** | — | Emits `AttackEvent`; receives parry suppression; flushes events on pool return | Enemy Definition owns the contract |
| **Combat Core / Combo / Parry** | full-combo stagger requests (`combo_counter == 2`); auto-attack/Finisher/burst damage to enemy HP | Staggered state + counter-hit; provides enemy elite type for gates | Combo/Parry own the player side; Enemy AI owns enemy state |
| **Damage & Health** | — | Forwards `AttackDamage` + `DamageType` (incl. the non-parriable Slam directly) | Damage & Health owns player HP |
| **Progression System** | — | Emits XP on enemy Dead (Enemy Definition owns `xp_weight`) | Progression owns `xp_scalar` |
| **Wave Spawner** | spawn requests (type, position) | Registers spawned enemies; returns to pool on death | Wave Spawner owns scheduling |

## Formulas

Enemy AI introduces no new balance formulas — it executes the formulas owned by Enemy Definition (stagger resistance, phase-2 threshold, XP drop, cooldowns). The one runtime algorithm it owns is the **steering vector**:

`steer = normalize(toPlayer) × goal_weight + Σ(separation_pushes) × separation_weight`, then `velocity = normalize(steer) × move_speed`

| Variable | Type | Range | Description |
|---|---|---|---|
| `toPlayer` | Vector3 | — | Player position − enemy position (XZ) |
| `goal_weight` | float | 1.0 (fixed) | Pursuit priority |
| `separation_pushes` | Vector3[] | — | Away-from-neighbour vectors for neighbours within `separation_radius` |
| `separation_weight` | float | 0.2–0.6; default 0.4 | Anti-stacking strength (lower than goal) |
| `move_speed` | float (u/s) | per type | From Enemy Definition |

**Output:** a velocity that closes on the player while spreading. **Example:** with no neighbours, `velocity = normalize(toPlayer) × move_speed` (pure pursuit).

## Edge Cases

- **If an enemy is in attack range but cooldown has not elapsed:** It holds in Chase/Cooldown and re-evaluates next tick; it does not telegraph early.
- **If the player leaves aggro range during Chase:** The enemy continues toward the last-known player position for MVP (no de-aggro in a survivors arena — the player is always the target). Enemies do not return to Idle once aggroed.
- **If two enemies occupy nearly the same cell (separation fails to resolve in one frame):** Separation accumulates over frames; a single frame of overlap is acceptable (cosmetic). Hard physics overlap is prevented by the separation force, not collision resolution.
- **If a staggered elite is hit by auto-attacks during its stagger window:** Damage applies; the stagger timer and counter-hit Telegraph are NOT interrupted or re-triggered (enemy-definition Combat Core contract).
- **If the Warlord crosses the Phase 2 threshold while Staggered:** HP clamps to 1, the stagger timer is cancelled, the counter-hit is skipped, and the enemy enters Roar (enemy-definition edge case).
- **If an Archer's fallback fires while the player is mid-approach:** The projectile resolves per range-at-fire-time; a parry within the window cancels all in-flight projectiles for that attack (enemy-definition).
- **If an enemy is returned to the pool mid-Telegraph:** `ReturnToPool` flushes its pending `AttackEvent`s before deactivation; the parry window for that `AttackerId` is removed (enemy-definition Contract #2).

## Dependencies

| System | Direction | Nature | Interface |
|---|---|---|---|
| **Enemy Definition** | Enemy Definition → Enemy AI | Hard | Reads all per-type data, the state table, the AttackEvent contract, and all Implementation Contracts. Enemy AI is the executor; Enemy Definition is the spec. |
| **Parry System** | Enemy AI → Parry | Hard | Emits `AttackEvent`; honours `Parriable`; flushes events on pool return; supports `FlushEventsFor`. |
| **Combat Core / Combo / Parry** | Bidirectional | Hard | Receives damage + full-combo stagger requests; provides enemy state + elite type. |
| **Damage & Health** | Enemy AI → Damage & Health | Hard | Forwards `AttackDamage` + `DamageType`; routes the non-parriable Slam directly. |
| **Progression System** | Enemy AI → Progression | Hard | Emits XP (`xp_weight`) on Dead. |
| **Wave Spawner** | Wave Spawner → Enemy AI | Hard | Receives spawn requests; registers/deregisters enemies via the pool. |

## Tuning Knobs

| Knob | Default | Safe Range | Affects |
|---|---|---|---|
| `separation_weight` | 0.4 | 0.2–0.6 | Anti-stacking strength; too low → clumping (Pillar 1 fail), too high → enemies can't close on the player |
| `aggro_jitter_frames` | 1–3 | 1–5 | Frames of randomised aggro delay to spread AI load |
| Steering implementation | NavMesh avoidance OR custom grid | — | Chosen in architecture ADR; both satisfy the separation requirement |

*Referenced, not owned: all stats, aggro ranges, `separation_radius` (0.5u), `archer_chase_fallback_s`, stagger durations, cooldowns — owned by Enemy Definition.*

## Visual/Audio Requirements

Enemy AI owns no VFX directly. It mandates the behavioural hooks VFX & Audio must visualise: the telegraph (per enemy-definition shape/duration), the Archer fallback ≥200ms stance-change cue, the stagger animation, and the Warlord roar. Movement must read as purposeful advance, not jitter — the separation behaviour must not produce visible vibration (tune `separation_weight`).

## Acceptance Criteria

| # | Criterion | Story Type | Gate |
|---|---|---|---|
| AC-AI-01 | **GIVEN** a Levy Soldier in Idle and the player entering 5u aggro range, **WHEN** 1–3 jitter frames pass, **THEN** the enemy transitions to Chase and moves toward the player. | Logic | BLOCKING |
| AC-AI-02 | **GIVEN** an enemy in Chase reaching attack range with cooldown elapsed, **WHEN** the tick runs, **THEN** it transitions to Telegraph and emits exactly one `AttackEvent` with the correct per-type fields. | Logic | BLOCKING |
| AC-AI-03 | **GIVEN** an Iron Captain receiving a full-combo (`combo_counter = 2`) Finisher, **WHEN** resolved, **THEN** it enters Staggered for `stagger_duration_ms`, locks movement, then emits a counter-hit `AttackEvent` (`IsCounterHit = true`, `DamageType = CounterHit`) on exit. | Logic | BLOCKING |
| AC-AI-04 | **GIVEN** two enemies within `separation_radius` both chasing, **WHEN** they steer, **THEN** their separation distance increases over successive frames (no permanent stacking). | Logic | BLOCKING |
| AC-AI-05 | **GIVEN** an Archer in Chase for `archer_chase_fallback_s` without closing to 6u, **WHEN** the timer expires (cooldown elapsed), **THEN** it telegraphs from its current position with a ≥200ms warning cue. | Logic | BLOCKING |
| AC-AI-06 | **GIVEN** the Warlord crossing the Phase 2 HP threshold, **WHEN** the damage phase detects it, **THEN** `IsPhaseTransitionLocked == true`, HP clamps to 1, Phase 1 parry windows are flushed, and no AttackEvents fire during the roar. | Integration | BLOCKING |
| AC-AI-07 | **GIVEN** 200 enemies active, **WHEN** range checks run for one frame, **THEN** no allocating `Physics.OverlapSphere` call occurs (NonAlloc only) and frame time stays within budget. | Performance | ADVISORY |
| AC-AI-08 | **GIVEN** the Warlord Phase 2 slam, **WHEN** it resolves, **THEN** Enemy AI forwards `DamageType.Slam` directly to Damage & Health (bypassing the Parry System). | Integration | BLOCKING |

## Open Questions

| # | Question | Owner | Resolution Path |
|---|---|---|---|
| OQ-01 | NavMesh avoidance vs custom separation grid for 200 enemies — which performs better on target hardware? | Architecture/Prototype | Decide in the enemy-architecture ADR; benchmark both at 50/100/200 enemies. |
| OQ-02 | Should enemies de-aggro / lose the player at all, or always pursue? MVP = always pursue (survivors arena). | Playtest | Confirm always-pursue reads correctly; revisit only if pursuit feels oppressive. |
| OQ-03 | Maximum simultaneous telegraph animations before readability breaks at 200 enemies? | Prototype | enemy-definition OQ-03 — may require a cap on concurrent telegraph VFX. |

# Wave Spawner

> **Status**: Approved (self-review 2026-06-19)
> **Author**: xinleng + Claude Code agents
> **Last Updated**: 2026-06-19
> **Implements Pillar**: Pillar 1 (Readable Chaos)

> **Note**: Wave Spawner is flagged high-risk in systems-index.md (performance with 200+ enemies). Object pooling must be implemented from day one; the escalation curve cannot be stress-tested without it.

## Overview

The Wave Spawner is the run's pacing engine: it spawns basic infantry around the player at an escalating density and speed, and fires the scripted elite and boss events that punctuate the 20-minute run (Iron Captains at 5/10/15 minutes, the Warlord at 18 minutes). It reads the enemy roster, type IDs, and spawn weights from Enemy Definition; instantiates enemies via an object pool (never `Instantiate`/`Destroy` per enemy); positions them just outside the player's view so the arena always feels surrounded but never cheats; and applies the wave escalation formulas from game-concept.md. It owns spawn scheduling, density/speed escalation, and the elite/boss event timeline; it owns no enemy behaviour (Enemy AI) or stats (Enemy Definition).

The player experiences it as the relentless, rising tide — the battlefield that never empties and gets harder exactly on schedule.

## Player Fantasy

The Wave Spawner is the feeling of an army that keeps coming. The horde is endless by design: clear a pocket and the gap fills, push forward and more close in behind. The fantasy is the legendary stand — one general against a tide that rises every two minutes until the final boss crests it. The spawner's job is to make "surrounded" feel true and fair at once: enemies arrive from the dark edges of vision, never popping in on top of you, always giving you the half-second to read and react. The escalation is the run's heartbeat — the player *feels* the pressure climb and braces for each checkpoint.

## Detailed Design

### Core Rules

1. **Escalation steps.** The run is divided into `step`s of `escalation_interval` (120s). At each step the density multiplier and speed multiplier increase (Formulas 1–2). Step 0 = run start; the density cap (`wave_density_cap` = 5×) is reached around 18:00.

2. **Spawn scheduling.** The spawner maintains a target concurrent enemy count `target_concurrent(step) = base_concurrent × density(step)`, capped at `max_enemy_count` (200). When live enemies fall below target, it spawns infantry to refill, on a `spawn_tick_interval` cadence (default 0.5s) and up to `max_spawns_per_tick` to avoid frame spikes.

3. **Spawn weights.** Each spawned infantry's type (Levy / Pikeman / Archer) is chosen by the spawn-weight distribution owned by Enemy Definition. Composition may shift by step (e.g., more Pikemen/Archers later) — the weight table is Enemy Definition's; the spawner applies it.

4. **Spawn positioning.** Enemies spawn on a ring of radius `spawn_ring_radius` (default 14u) around the player — just outside typical camera view — at a random angle, so the player is surrounded without on-screen pop-in. Spawn points are validated against the arena bounds; a point outside the arena is rejected and re-rolled.

5. **Object pooling (mandatory).** All enemies come from a pre-warmed object pool sized to `max_enemy_count` (200) per type as needed. `Instantiate`/`Destroy` per enemy is forbidden. Dead enemies return to the pool via `ObjectPool.ReturnToPool` (which flushes their parry events — enemy-definition Contract #2). `AttackerId` is unique per spawn instance.

6. **Speed escalation.** The spawner applies a global `wave_speed_multiplier(step)` to enemy move speeds (Enemy AI reads the active multiplier). This scales the per-type base speeds from Enemy Definition; it does not overwrite them.

7. **Elite events.** Iron Captains spawn at t = 300s, 600s, 900s (±5s tolerance) with the per-appearance stats (300/375/450 HP, 600/550/450ms counter-hit — Enemy Definition). An elite spawns on the ring like infantry but is a single scripted spawn, not part of the density refill.

8. **Boss event.** The Warlord spawns at t = 1080s (18:00). **Boss spawn clears all active Iron Captains** (game-concept edge case) — they are returned to the pool — leaving only the boss and standard infantry. Infantry spawning continues during the boss fight at the capped density.

9. **Run end.** At t = 1200s (20:00) with the player alive, spawning stops and the win condition fires (Run Session Manager). On player death, the spawner halts and clears via the pool on retry.

10. **No behaviour ownership.** The spawner decides *what* spawns *where* and *when*; Enemy AI decides how spawned enemies behave. The spawner never drives enemy state.

### States and Transitions

| State | Behaviour | Exits To |
|---|---|---|
| **Spawning** | Refill toward `target_concurrent`; advance escalation steps; fire scheduled elite/boss events | Boss (t = 1080s) / Ended (t = 1200s or player death) |
| **Boss** | Warlord active; active elites cleared; infantry continues at capped density | Ended (boss death, player death, or t = 1200s) |
| **Ended** | Spawning halted; pool cleared on retry | Spawning (on retry/new run) |

### Interactions with Other Systems

| System | Data In | Data Out | Interface Owner |
|---|---|---|---|
| **Enemy Definition** | type IDs, spawn weights, per-type stats | None | Enemy Definition owns IDs + weights |
| **Enemy AI** | — | Spawns enemies into the AI manager's registry; applies wave speed multiplier | Enemy AI owns behaviour |
| **Run Session Manager** | run clock (run-local time); start/stop/retry | win/loss timeline events (boss spawned, run end) | Run Session Manager owns the run clock |
| **Progression System** | — | (indirect) more enemies → more XP | Progression owns XP |

## Formulas

### 1. Density Escalation (referenced — owned by game-concept.md)

`density(step) = min(base_density_mult × wave_density_step^step, base_density_mult × wave_density_cap)`; `step = floor(run_time / escalation_interval)`

| Variable | Type | Range | Description |
|---|---|---|---|
| `wave_density_step` | float | 1.1–1.4; default 1.2 | Per-step density growth |
| `wave_density_cap` | float | 3.0–8.0; default 5.0 | Maximum density multiplier |
| `base_concurrent` | int | 20–60; default 40 | Target concurrent enemies at step 0 |

`target_concurrent(step) = min(base_concurrent × density(step), max_enemy_count)`. **Output:** step 0 → 40; step 9 (18:00) → min(40×5, 200) = 200. **Example:** step 3 (6:00) → `40 × 1.2^3 = 69` concurrent.

### 2. Speed Escalation (referenced)

`wave_speed_multiplier(step) = wave_speed_step^step`

| Variable | Type | Range | Description |
|---|---|---|---|
| `wave_speed_step` | float | 1.05–1.2; default 1.1 | Per-step enemy speed growth |

**Output:** step 0 → 1.0×; step 9 → 1.1^9 ≈ 2.36× base speed. Applied to per-type move speeds.

### 3. Spawn Position

`spawn_pos = player_pos + (cos θ, 0, sin θ) × spawn_ring_radius`, θ ∈ [0, 2π) uniform random, rejected + re-rolled if outside arena bounds.

| Variable | Type | Range | Description |
|---|---|---|---|
| `spawn_ring_radius` | float (u) | 10–20; default 14 | Ring radius (just outside camera view) |

## Edge Cases

- **If live enemy count is already at `max_enemy_count` (200):** No infantry spawn this tick regardless of target density — the hard cap protects the performance budget.
- **If a spawn point rolls outside the arena bounds:** Reject and re-roll the angle; never spawn outside the playable area.
- **If the boss spawns while 3 Iron Captains happen to be alive (e.g., player ignored the 15-min elite):** All active elites are returned to the pool on boss spawn; the boss and infantry remain (game-concept edge case). Their potential XP is forfeit (they did not die).
- **If the player kills enemies faster than the spawner refills:** The arena thins until the next `spawn_tick`; the spawner refills toward target but never exceeds `max_spawns_per_tick`, so kill-streaks briefly clear space (rewarding, intended).
- **If two escalation steps would advance in one frame (impossible at 120s spacing, but defensively):** Apply the step for the current `floor(run_time / escalation_interval)`; escalation is a pure function of run time, not incremental.
- **If an elite spawn time (e.g., 300s) is missed by a frame hitch:** The ±5s tolerance window covers it; spawn on the first tick within tolerance.

## Dependencies

| System | Direction | Nature | Interface |
|---|---|---|---|
| **Enemy Definition** | Enemy Definition → Wave Spawner | Hard | Reads type IDs, spawn weights, per-type stats. |
| **Enemy AI** | Wave Spawner → Enemy AI | Hard | Spawns enemies into the AI manager's registry (deferred registration); applies the wave speed multiplier; returns to pool on death. |
| **Run Session Manager** | Bidirectional | Hard | Reads the run clock; emits boss-spawn and run-end timeline events; halts/resets on retry. |
| **Object Pool** | Wave Spawner → Pool | Hard | Pre-warms and recycles enemies; `ReturnToPool` flushes parry events (enemy-definition Contract #2). |

## Tuning Knobs

| Knob | Default | Safe Range | Affects |
|---|---|---|---|
| `escalation_interval` | 120s | 60–180s | Seconds per escalation step; shorter ramps faster |
| `base_concurrent` | 40 | 20–60 | Target concurrent enemies at step 0 |
| `wave_density_step` | 1.2 | 1.1–1.4 | Density growth per step (owned by game-concept) |
| `wave_density_cap` | 5.0× | 3.0–8.0× | Max density (owned by game-concept) |
| `wave_speed_step` | 1.1 | 1.05–1.2 | Speed growth per step (owned by game-concept) |
| `spawn_ring_radius` | 14u | 10–20 | Spawn distance from player |
| `spawn_tick_interval` | 0.5s | 0.2–1.0s | Refill cadence |
| `max_spawns_per_tick` | 10 | 4–20 | Anti-spike cap on spawns per tick |
| `max_enemy_count` | 200 | 100–300 | Hard concurrent cap (performance budget) |

*Referenced, not owned: type IDs, spawn weights, per-type stats — Enemy Definition; escalation formulas — game-concept.md.*

## Visual/Audio Requirements

Spawning must not pop in on-screen — enemies fade/walk in from the ring outside view (`spawn_ring_radius`). Elite and boss spawns should have a distinct audio cue (drum hit / horn) signalling the checkpoint, and the boss spawn a unique, ominous stinger. Music intensity escalates with wave steps (game-concept audio note). Full spec in VFX & Audio GDD.

## Acceptance Criteria

| # | Criterion | Story Type | Gate |
|---|---|---|---|
| AC-WS-01 | **GIVEN** run time 6:00 (step 3), `base_concurrent = 40`, `wave_density_step = 1.2`, **WHEN** density is computed, **THEN** `target_concurrent == round(40 × 1.2^3) == 69` (asserted against config). | Logic | BLOCKING |
| AC-WS-02 | **GIVEN** live enemies below target and a spawn tick, **WHEN** the spawner refills, **THEN** new enemies spawn on the `spawn_ring_radius` ring, within arena bounds, up to `max_spawns_per_tick`. | Logic | BLOCKING |
| AC-WS-03 | **GIVEN** live enemy count == 200, **WHEN** a spawn tick runs, **THEN** no infantry spawn (hard cap respected). | Logic | BLOCKING |
| AC-WS-04 | **GIVEN** run time crossing 300s/600s/900s, **WHEN** each elite event fires, **THEN** one Iron Captain spawns with the correct per-appearance stats (300/375/450 HP) within ±5s. | Integration | BLOCKING |
| AC-WS-05 | **GIVEN** run time 1080s with Iron Captains alive, **WHEN** the boss spawns, **THEN** the Warlord spawns and all active Iron Captains are returned to the pool. | Integration | BLOCKING |
| AC-WS-06 | **GIVEN** any spawn, **WHEN** an enemy is created, **THEN** it comes from the object pool (no `Instantiate`); **WHEN** it dies, **THEN** it returns to the pool (no `Destroy`). | Integration | BLOCKING |
| AC-WS-07 | **GIVEN** 200 enemies active across a 60s window, **WHEN** spawning runs, **THEN** frame time stays within the 16.6ms budget (object pooling + spawn cap). | Performance | ADVISORY |

## Open Questions

| # | Question | Owner | Resolution Path |
|---|---|---|---|
| OQ-01 | Is `base_concurrent = 40` ramping to 200 the right density curve for readability + challenge? | Prototype | Establish a performance + readability baseline at 50/100/200 enemies (systems-index high-risk mitigation). |
| OQ-02 | Should spawn-weight composition shift by step (more Pikemen/Archers late), and where is that table owned? | Design | Define the per-step weight table in Enemy Definition if composition should evolve; MVP may use a flat weight. |
| OQ-03 | Does the boss clearing active elites read as fair, or feel like lost reward? | Playtest | game-concept edge case; monitor first playtest. |

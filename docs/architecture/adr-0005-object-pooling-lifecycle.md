# ADR-0005: Object Pooling and Enemy Lifecycle

## Status
Accepted
*(Proposed and Accepted 2026-07-04, autonomous session — user directive to proceed; lean review mode.)*
*(Amended 2026-10-10 by /architecture-review: pre-warm phantom-registration guard; `AttackerId` counter becomes pool instance state (removes the domain-reload static hazard structurally); spawn jitter RNG injected per ADR-0010.)*

## Date
2026-07-04

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS |
| **Domain** | Core |
| **Knowledge Risk** | LOW — GameObject activation/instantiation APIs are stable |
| **References Consulted** | `docs/engine-reference/unity/VERSION.md`, `current-best-practices.md` |
| **Post-Cutoff APIs Used** | None (custom pool; `UnityEngine.Pool.ObjectPool<T>` considered but a bespoke pool is needed for the flush contract) |
| **Verification Required** | Pool pre-warm time at scene load; zero `Instantiate` calls during a run (Profiler) |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | ADR-0001 (manager registration model) |
| **Enables** | Wave Spawner implementation; Enemy AI implementation |
| **Blocks** | Any story that spawns or kills an enemy or projectile |
| **Ordering Note** | The pool is "the very first piece of code" per systems-index high-risk mitigation |

## Context

### Problem Statement
200 concurrent enemies with continuous churn make per-spawn `Instantiate`/`Destroy` unaffordable (GC + instantiation spikes). Beyond performance, the GDDs attach *correctness* contracts to the pool: stale parry windows must be flushed before an enemy deactivates (ghost-parry bugs), `AttackerId` must be unique per spawn (ghost projectile cancel bugs), and re-enables must not mutate the manager's iteration list mid-frame.

### Constraints
- 16.6ms budget; no per-spawn allocation
- `ParrySystem.FlushEventsFor(id)` must complete **before** `SetActive(false)` — Unity gives no ordering guarantee for `OnDisable`
- Warlord/elite are single scripted spawns; infantry churn constantly

### Requirements
- Pre-warmed pools per enemy type + archer projectiles (+ pooled VFX/damage numbers later)
- `ReturnToPool` is the **only** legal deactivation site
- Deferred registration; fresh `AttackerId` per spawn; spawn cooldown jitter applied in `Initialize`

## Decision

**A bespoke `ObjectPool` module (Foundation) owns the full enemy/projectile lifecycle.**

1. **Pre-warm**: at scene load, instantiate pool contents inactive — per-type counts from `TuningConfig` (sum ≥ `max_enemy_count` 200 for infantry; 1 per elite/boss type; `archer_projectile_count × archers` ceiling for projectiles). **Phantom-registration guard (amended)**: `Instantiate` of an active prefab runs `Awake`/`OnEnable` before the pool can call `SetActive(false)`, which would stage 200+ phantom enemies on `RegisterPending`. Pre-warm therefore instantiates under an inactive container GameObject (and `OnEnable` stages only when an `_initialized` flag is set — set by `Initialize`, cleared by `ReturnToPool`).
2. **Spawn path (amended)**: `Spawn(type, position)` → pop inactive instance → `controller.Initialize(new SpawnParams { attackerId: _nextInstanceId++, initialCooldownMs: _rng.Next(0, attack_cooldown_ms), data: EnemyDataSO, position })` → `SetActive(true)` → controller stages itself on `EnemySystemManager.RegisterPending` (flushed at next tick start, never mid-iteration). The jitter RNG is an injected `System.Random` (ADR-0010 rule 4), never global `UnityEngine.Random` — AC-18's distribution assertion requires a seedable source.
3. **Return path (binding order)**: `ReturnToPool(enemy)` → `ParrySystem.FlushEventsFor(enemy.AttackerId)` → deregister from manager → `SetActive(false)` → push to pool. **Calling `SetActive(false)` on an enemy anywhere else is a forbidden pattern** (registered; grep-checkable).
4. **`AttackerId` (amended)**: `_nextInstanceId` is an **instance field on the pool**, incremented per spawn (never per slot). One pool instance per run/bootstrap means the counter resets naturally with the pool — no static state, no domain-reload hazard, consistent with the project's DI-over-statics standard. `ResetInstanceIdCounter()` survives as a test seam on the pool instance (damage-health Contract #6). Should a static ever be reintroduced, the correct reset hook is `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]` (works in player builds), not editor-only `[InitializeOnEnterPlayMode]`.
5. **Exhaustion policy**: infantry pool exhausted → spawn request dropped this tick (the 200 cap already gates upstream in WaveSpawner, so this is defensive); elite/boss pool exhausted → designer error (scripted events must never double-spawn).
6. Unity's built-in `UnityEngine.Pool.ObjectPool<T>` is not used for enemies: its `actionOnRelease` callback cannot express the *ordered* flush-then-deregister-then-deactivate contract as auditable code, and we need the static-ID assignment inside the spawn path. (It remains fine for trivial pooled cosmetics.)

### Key Interfaces
`IObjectPool.Spawn(EnemyTypeId, Vector3)` / `ReturnToPool(EnemyController)` per architecture.md; `EnemyController.Initialize(in SpawnParams)`.

## Alternatives Considered

### Alternative 1: `Instantiate`/`Destroy` per enemy
- **Rejection Reason**: Forbidden by wave-spawner.md Rule 5; GC + spike cost at churn rates of several spawns/sec.

### Alternative 2: `UnityEngine.Pool.ObjectPool<T>` as the primary pool
- **Pros**: Battle-tested, less code.
- **Cons**: Release callbacks obscure the ordered flush contract; no natural home for `AttackerId` assignment and jitter; per-type multi-pool management still needed on top.
- **Rejection Reason**: The correctness contracts are the point; a thin bespoke pool makes them explicit and testable.

## Consequences

### Positive
- Ghost-parry and ghost-projectile bug classes eliminated structurally; zero mid-run instantiation
### Negative
- Pool sizing is a tuning liability (undersized elite/projectile pools are silent until hit) → sizes in `TuningConfig` with load-time sanity checks

### Risks
- **A stray `Destroy()` call** → forbidden pattern + review grep (`Destroy(` outside pool/bootstrap)
- **Pre-warm hitch at load** → measured; acceptable before a run starts (no loading-screen constraint violated at these counts)
- **`OnTriggerExit` not fired on `SetActive(false)`** *(engine-specialist finding)*: deactivating a trigger collider does not reliably fire exit events on overlapping colliders. No system may rely on trigger-exit for enemy-death cleanup — all exit-cleanup flows through the explicit `ReturnToPool` sequence (this is the authority for the deactivation path)
- **Static `AttackerId` counter vs disabled domain reload** *(engine-specialist finding)*: with Enter Play Mode Options skipping domain reload, the counter persists across editor play sessions. `ResetInstanceIdCounter()` must be called on every play-mode entry in editor (e.g., `[InitializeOnEnterPlayMode]`) and in every test `[SetUp]` — not only in tests

## GDD Requirements Addressed

| GDD System | Requirement | How This ADR Addresses It |
|------------|-------------|--------------------------|
| enemy-definition.md | Contract #2: flush before deactivate; `SetActive(false)` restriction; per-spawn AttackerId | Return-path binding order; forbidden pattern; static counter |
| enemy-definition.md | Deferred registration; spawn cooldown jitter | Spawn path steps 2 |
| wave-spawner.md | Rule 5: mandatory pooling, no Instantiate/Destroy | The module itself |
| damage-health.md | Contract #6: ResetInstanceIdCounter test seam | Editor-conditional method |

## Performance Implications
- **CPU**: O(1) spawn/return; no instantiation during runs
- **Memory**: ~200 enemy instances + projectiles resident for the whole run (bounded, known)
- **Load Time**: Pre-warm cost at scene load — measure; acceptable
- **Network**: N/A

## Migration Plan
Greenfield.

## Validation Criteria
- AC-WS-06 (pool-only spawn/death); AC-19 (flush on phase transition uses the same `FlushEventsFor`); AC-18 (jitter)
- Profiler capture of a 2-minute run: zero `Instantiate` calls, zero GC alloc from spawn churn

## Related Decisions
- ADR-0001 (registration), ADR-0007 (buffers sized to pool capacity)

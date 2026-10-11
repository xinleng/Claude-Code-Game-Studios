# ADR-0007: Spatial Queries and Enemy Steering

## Status
Accepted
*(Proposed and Accepted 2026-07-04, autonomous session — user directive to proceed; lean review mode. Steering choice carries a prototype-benchmark condition — see Validation.)*
*(Amended 2026-10-10 by /architecture-review: the uniform grid is now the PRIMARY mechanism for ALL queries and enemies carry no physics colliders in the primary path — resolves blocking finding N1 (a collider moved by transform without a Rigidbody is a static collider in PhysX: moving 200/frame is the documented anti-pattern, and with `Physics.autoSyncTransforms = false` by default, Update-time overlap queries silently see 0–2-frame-stale positions — a parry/hit fairness bug). The physics-query path is demoted to a benchmarked fallback with the required kinematic-Rigidbody setup documented below.)*

## Date
2026-07-04

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS |
| **Domain** | Physics / Navigation |
| **Knowledge Risk** | MEDIUM — PhysX 5.1 in Unity 6 (solver changes don't affect overlap queries); NonAlloc query APIs stable |
| **References Consulted** | `docs/engine-reference/unity/modules/physics.md`, `breaking-changes.md`, `deprecated-apis.md` |
| **Post-Cutoff APIs Used** | None |
| **Verification Required** | Benchmark range-check + steering cost at 50/100/200 enemies in the prototype (enemy-ai OQ-01 / AOQ-02) |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | ADR-0001 (manager tick), ADR-0005 (buffer sizes = pool capacity) |
| **Enables** | Enemy AI implementation; Combat Core hit detection |
| **Blocks** | Enemy AI + Combat Core stories |
| **Ordering Note** | Steering implementation is provisional pending the prototype benchmark |

## Context

### Problem Statement
At 200 enemies, brute-force range checks are ~20,100 pair comparisons per frame (forbidden by enemy-definition Contract #3). The project needs one strategy for: aggro checks, attack-range checks, melee/Finisher/burst hit detection, and C6 chain targeting — plus a steering approach that closes on the player without visible clumping (Pillar 1).

### Constraints
- No allocating physics APIs (`OverlapSphere`, `RaycastAll` forbidden — deprecated-apis.md + GDD)
- Flat, open, bounded arena; one player; enemies never path around static obstacles in MVP
- Separation must not produce visible jitter (enemy-ai visual requirement)

### Requirements
- All range/hit queries via layer-filtered `Physics.OverlapSphereNonAlloc` into pre-allocated `Collider[200]` buffers, or an equivalent grid
- Steering: pursuit + separation force (weight 0.4, radius 0.5u), movement locked in Staggered/Roar
- Deterministic tie-breaks (lower entity index) for tests

## Decision

**One uniform spatial grid serves ALL range/hit queries — player-centric AoE (melee, Finisher, burst, C6 chain) and enemy-neighbour separation. Enemies carry NO physics colliders in the primary path. Custom separation-vector steering (no NavMesh). Physics overlap queries are the benchmarked fallback only.**

1. **Queries (amended)**: the manager rebuilds a lightweight uniform grid (cell = 1u, O(n) bucket fill) from enemy positions once per frame, immediately after the enemy move phase. All range checks — player-centric AoE in CombatCore/Combo/Parry via the shared `IEnemyWorld.NearestEnemies` view, and enemy-centric aggro/attack range (cheap distance-squared vs the single player, no spatial structure needed) — resolve against this grid with sphere-vs-cell scans. Buffers (`Span<EnemyHit>`/arrays) are allocated once at manager init, sized to `max_enemy_count` (sizing validated by GameDataValidator against pool capacity). Benefits over the physics path: positions are never stale (same-frame data), range semantics are consistent (centre distance everywhere — collider-surface overlap would disagree with aggro's centre distance at the edges, which matters for parry-window feel), and the `queriesHitTriggers` / layer-matrix / buffer-truncation hazards disappear.
   **Fallback (only if the grid benchmark fails at 200)**: physics queries require — a kinematic Rigidbody on every moving enemy collider (a collider without one is a static collider; moving it per frame is the documented anti-pattern); exactly one `Physics.SyncTransforms()` after the enemy move phase and before any query (with `autoSyncTransforms` left false); Enemy×Enemy disabled in the layer collision matrix; explicit `QueryTriggerInteraction.Collide` on every call; and consider `Physics.simulationMode = Script` with `Simulate` never called, since nothing uses dynamics.
2. **Steering**: `steer = normalize(toPlayer) + Σ(separation_pushes) × 0.4`, velocity = `normalize(steer) × move_speed × wave_speed_multiplier`. Separation neighbour lookup uses the same per-frame grid. Enemies move by direct transform integration in the manager tick — **no Rigidbody physics movement** and, in the primary path, no colliders at all.
3. **NavMesh rejected for MVP**: the arena is flat and obstacle-free; NavMeshAgent avoidance costs per-agent update overhead and fights the manager-tick model. Re-evaluate only if maps gain obstacles (post-MVP).
4. **Determinism**: query results sorted by distance with entity-index tie-break before use in tests and targeting (general-characters/combat-core tie-break convention).

### Key Interfaces
- `IEnemyWorld.NearestEnemies(Vector3 origin, float radius, Span<EnemyHit> buffer)` — sorted, tie-broken
- `SpatialGrid.Rebuild(enemies)` / `NeighboursWithin(pos, r, buffer)` — internal to EnemySystemManager

## Alternatives Considered

### Alternative 1: NavMesh + NavMeshAgent avoidance
- **Pros**: Built-in avoidance; future obstacle support.
- **Cons**: Per-agent overhead ×200; agent avoidance quality tuned for pathing, not horde-separation feel; conflicts with manager-owned movement (agents want to own the transform).
- **Rejection Reason**: Wrong tool for a flat arena with manager-driven movement. (Kept as the fallback if the prototype benchmark shows the custom grid failing — AOQ-02.)

### Alternative 2: All queries through the custom grid (no physics at all)
- **Pros**: No collider/layer setup; fully engine-free logic (nice for tests).
- **Cons**: Reimplements what layer-filtered NonAlloc queries already do well for the player-centric cases; more bespoke code to maintain.
- **Rejection Reason**: Hybrid keeps bespoke code minimal — grid only where physics would be hot (separation).

### Alternative 3: Brute-force O(n²)
- **Rejection Reason**: Explicitly forbidden (enemy-definition Contract #3).

## Consequences

### Positive
- Query cost bounded and allocation-free; separation cost O(n × small-neighbourhood); logic tests can stub `IEnemyWorld` without physics
### Negative
- Two query mechanisms (physics + grid) to understand — documented split: physics for player-centric AoE, grid for enemy-neighbour separation, plain math for enemy→player distance

### Risks
- **Separation jitter at high density** → tune `separation_weight` (0.2–0.6) in prototype; damping if needed
- **Grid rebuild cost surprises** → it's an O(n) bucket fill; benchmark says go/no-go at 200 (AOQ-02)
- **`Physics.queriesHitTriggers` global dependency** *(engine-specialist finding)*: enemy colliders are triggers; the project-wide default (`true`) can be silently changed by an unrelated physics-tuning pass, zeroing out every enemy query with no compile-time signal. Mitigate: always pass the explicit `QueryTriggerInteraction.Collide` argument on every overlap call — never rely on the global setting
- **Silent buffer truncation** *(engine-specialist finding)*: `OverlapSphereNonAlloc` gives no signal when results exceed buffer capacity — it just stops. Treat `count == buffer.Length` as a "buffer possibly too small" designer warning at runtime, in addition to the load-time size-vs-pool check

## GDD Requirements Addressed

| GDD System | Requirement | How This ADR Addresses It |
|------------|-------------|--------------------------|
| enemy-definition.md | Contract #3: no O(n²), NonAlloc + pre-allocated buffer or grid | Hybrid adopts both halves where each fits |
| enemy-ai.md | Steering formula, separation ≤ goal priority, movement locks | Steering rule 2; locks in state handling |
| combat-core.md | Rule 4: NonAlloc melee hit detection; nearest-target tie-break | Player-centric query path + deterministic sort |
| combo-system.md / parry-system.md | Finisher/burst AoE resolution; C6 chain targeting | Same `IEnemyWorld` query surface |

## Performance Implications
- **CPU**: one O(n) grid rebuild + ~3–6 player-centric sphere-vs-cell scans/frame + n small neighbour lookups — well inside budget at n=200 (verify in prototype). **Budget note (audit 2026-10-10): this ADR's ≤~2ms is a NESTED SUBSET of ADR-0001's ≤~4ms enemy tick, not additive** — leaving ~2ms for the damage phase, state machines, and transform writes. Neither budget covers Animator/SkinnedMeshRenderer cost or rendering; benchmark in a player build (IL2CPP, min-spec), never the Editor (2–5× pessimistic, would falsely trigger the DOTS escalation)
- **Memory**: Fixed buffers (200 × ref) + grid buckets, allocated once
- **Load Time / Network**: N/A

## Migration Plan
Greenfield. If the benchmark fails: swap separation to NavMesh avoidance behind the same steering call site (superseding ADR).

## Validation Criteria
- Prototype benchmark at 50/100/200: range checks + steering ≤ ~2ms total at 200
- AC-AI-04 (separation increases distance), AC-AI-07 (no allocating query), AC-CC-04 (nearest-3 targeting) pass

## Related Decisions
- ADR-0001, ADR-0005 (buffer/pool sizing), architecture.md AOQ-02

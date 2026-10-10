# Story 001: Pool core: pre-warm, Spawn/ReturnToPool, exhaustion policy

> **Epic**: Object Pool & Enemy Lifecycle
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Logic
> **Manifest Version**: 2026-10-10

## Context

**GDD**: `design/gdd/wave-spawner.md` (Rule 5), `design/gdd/enemy-definition.md` (Implementation Contract #2)
**Requirement**: `TR-WS-001`
*(Requirement text lives in `docs/architecture/tr-registry.yaml`. Read it fresh at review time.)*

**ADR Governing Implementation**: ADR-0005: Object Pooling and Enemy Lifecycle
**ADR Decision Summary**: A bespoke `ObjectPool` module (Foundation) owns the full enemy/projectile lifecycle. It pre-warms per-type instances inactive at scene load with counts from `TuningConfig`, and it guarantees zero `Instantiate`/`Destroy` during a run. Infantry exhaustion drops the request; elite/boss exhaustion is a designer error.

**Engine**: Unity 6.3 LTS | **Risk**: LOW
**Engine Notes**: No post-cutoff APIs. `UnityEngine.Pool.ObjectPool<T>` is **not** used for enemies (ADR-0005 §6). Verification required by the ADR: pool pre-warm time at scene load (measured in Story 006). **Verify against Unity 6.3 docs:** an object instantiated as a child of an inactive parent does not run `Awake`/`OnEnable` until it first becomes active in the hierarchy. `Awake` therefore runs at the first `Spawn`, not at pre-warm. That is one more reason why `Awake` must not self-initialise (manifest Foundation Forbidden).

**Control Manifest Rules (Foundation)**:
- Required: all enemy/projectile lifecycles go through `ObjectPool`. Pre-warm under an inactive container. Boot order is GameData validation → construct + inject → **ObjectPool pre-warm** → RSM `Selection` (ADR-0012).
- Required: wire dependencies by constructor/initializer injection at Bootstrap. Use interfaces only, with no singletons and no `FindObjectOfType`/`FindFirstObjectByType`.
- Required: all gameplay values live in `GameData` ScriptableObjects (`TuningConfig` holds pool sizes, ADR-0006 §4).
- Forbidden: never call `SetActive(false)` on an enemy, or `Destroy` any pooled object, outside `ObjectPool.ReturnToPool`. Never hardcode a gameplay/tuning value. Never use `Resources.Load` or Addressables.
- Guardrail: zero `Instantiate`/`Destroy` during a run (Profiler-verified, Story 006).

---

## Acceptance Criteria

*From GDD `design/gdd/wave-spawner.md` Rule 5 + ADR-0005 Decision §1, §2, §5, scoped to this story:*

- [ ] **AC-1**: At pre-warm, the pool instantiates, for each enemy type, exactly the count read from the injected `TuningConfig` pool-size view. Every instance ends pre-warm with `activeSelf == false`, parented under the pool's container. No count is hardcoded. (ADR-0005 §1)
- [ ] **AC-2**: Load-time sanity check (ADR-0005 Consequences: "sizes in `TuningConfig` with load-time sanity checks"): if the infantry pool total < `max_enemy_count` (200), or any elite/boss type's pool count < 1, a `[DESIGNER-ERROR]` is logged at pre-warm. (ADR-0005 §1: "sum ≥ `max_enemy_count` 200 for infantry; 1 per elite/boss type")
- [ ] **AC-3**: `Spawn(EnemyTypeId type, Vector3 position)` pops an inactive instance of `type`, places it at `position`, activates it, and returns it. It never calls `Instantiate`. (ADR-0005 §2; wave-spawner Rule 5)
- [ ] **AC-4**: `ReturnToPool(EnemyController enemy)` deactivates the instance and pushes it back to its type's pool, and a later `Spawn` of the same type can reuse that same instance. *(Basic path only. The binding flush → deregister → deactivate → push order is Story 004.)*
- [ ] **AC-5**: When the infantry pool for a type is exhausted, `Spawn` drops the request: it returns `null`, does not instantiate, and throws no exception. (ADR-0005 §5)
- [ ] **AC-6**: When an elite/boss pool is exhausted, `Spawn` logs a `[DESIGNER-ERROR]` ("scripted events must never double-spawn"), returns `null`, and does not instantiate. (ADR-0005 §5)
- [ ] **AC-7**: `ReturnToPool` raises no death/XP signal. The pool is lifecycle-only, so a boss-spawn elite clear (manifest Feature rule: "returns active elites to the pool WITHOUT death/XP signals") can route through it safely.

---

## Implementation Notes

*Derived from ADR-0005 Implementation Guidelines + architecture.md "Lifecycle + reset":*

- Implement `IObjectPool` exactly as declared in `docs/architecture/architecture.md`:
  `EnemyController Spawn(EnemyTypeId type, Vector3 position)` and `void ReturnToPool(EnemyController enemy)`. Per the existing signature, a dropped request returns `null`. Document this in the doc comment so callers (WaveSpawner) treat `null` as "dropped this tick".
- Bootstrap constructs the pool and injects a **read-only `TuningConfig` view** for per-type counts (ADR-0006 §4: "pool sizes ... exposed to systems as read-only typed views"). Tests construct it in-memory (`TestConfigs.*`, ADR-0010). Do not read a static `Config.Instance`.
- Keep the per-type free storage pre-allocated at pre-warm (for example, one stack/array per type sized to its count) so `Spawn`/`ReturnToPool` are O(1) and allocation-free (ADR-0005 Performance: "O(1) spawn/return"). Story 006 asserts zero GC.
- **Pre-warm under an inactive container** (ADR-0005 §1, amended). `Instantiate` the prefab as a child of an inactive container GameObject so no `Awake`/`OnEnable` runs during warm-up. Every instance must end pre-warm `activeSelf == false`. How instances become active-in-hierarchy at `Spawn` is an implementation choice. Two options are to self-deactivate each instance and then activate the container, or to reparent on spawn. Record the choice in a code comment and confirm it with unity-specialist at `/code-review`. The `_initialized` registration guard is Story 003.
- Pre-warm runs once from Bootstrap, after GameData validation (ADR-0012 boot order). It is the **only** place the pool calls `Instantiate`.
- Exhaustion (ADR-0005 §5): infantry is defensive, because WaveSpawner's 200 cap already gates upstream, so drop silently. Elite/boss exhaustion is a designer error logged through the shared `[DESIGNER-ERROR]` channel (TR-TP-002, ADR-0010), so tests can assert it.
- Doc comments on all public APIs. Use PascalCase public members and `_camelCase` private fields.
- **Open question (do not resolve unilaterally):** ADR-0005 does not specify what happens when `ReturnToPool` is called on an instance that is already pooled (double return). A double push would hand one instance to two spawns. Raise it at `/story-readiness`. If a guard is added, it must be ratified as an ADR-0005 amendment, not improvised.

---

## Out of Scope

*Handled by neighbouring stories. Do not implement here:*

- Story 002: `AttackerId` assignment, `ResetInstanceIdCounter()`, spawn jitter via `Initialize(in SpawnParams)`
- Story 003: `_initialized` guard and `RegisterPending` staging (phantom-registration prevention)
- Story 004: binding return order (`FlushEventsFor` → deregister → `SetActive(false)` → push) and the forbidden-pattern grep
- Story 005: projectile pool
- Story 006: zero-Instantiate churn test, GC-alloc assertion, player-build Profiler capture, pre-warm timing
- WaveSpawner epic: spawn scheduling, the 200 live cap, boss-spawn elite clear (AC-WS-05)

---

## QA Test Cases

*Written at story creation (lean mode: QL-STORY-READY gate skipped). The developer implements against these cases and does not invent new test cases during implementation.*

- **AC-1**: Pre-warm creates config-driven inactive instances
  - Given: an in-memory `TuningConfig` with per-type counts from a test fixture (for example, a fixture with two infantry types and one elite type, values from a `TestConfigs` factory and not inline literals)
  - When: the pool pre-warms
  - Then: the instance count per type equals the fixture count, every instance has `activeSelf == false`, and every instance is parented under the pool container
  - Edge cases: a type with count 0 in config produces 0 instances and does not throw; changing the fixture count changes the instance count (proves nothing is hardcoded)

- **AC-2**: Load-time sizing sanity check
  - Given: a fixture whose infantry total = `max_enemy_count − 1`, then a fixture with an elite count of 0
  - When: the pool pre-warms
  - Then: exactly one `[DESIGNER-ERROR]` is captured on the designer-error channel for each violating fixture
  - Edge cases: infantry total == `max_enemy_count` (boundary) logs no error; every elite/boss type at 1 logs no error

- **AC-3**: Spawn pops without instantiating
  - Given: a pre-warmed pool and a recorded set of instance references per type
  - When: `Spawn(type, position)` is called
  - Then: the returned instance is a member of the pre-warmed set for `type`, it is active, its position equals `position`, and the free count for `type` drops by 1
  - Edge cases: spawning type A never returns an instance from type B's pool

- **AC-4**: Return and reuse
  - Given: an instance obtained via `Spawn`
  - When: `ReturnToPool(instance)` is called, followed by `Spawn` of the same type with a pool of count 1
  - Then: after the return, the instance is inactive and the free count is restored. The second `Spawn` returns the **same** object reference.
  - Edge cases: returning instances in a different order than they were spawned still restores the full free count

- **AC-5**: Infantry exhaustion drops the request
  - Given: an infantry type with all instances spawned
  - When: `Spawn` is called once more for that type
  - Then: it returns `null`, no exception is thrown, the pooled object count is unchanged, and no `[DESIGNER-ERROR]` is logged
  - Edge cases: after one `ReturnToPool`, the next `Spawn` succeeds

- **AC-6**: Elite/boss exhaustion is a designer error
  - Given: an elite type with count 1, already spawned
  - When: `Spawn` is called again for that type
  - Then: it returns `null`, exactly one `[DESIGNER-ERROR]` is captured, and the pooled object count is unchanged
  - Edge cases: the boss type behaves the same as the elite type

- **AC-7**: Return raises no death/XP signal
  - Given: a pool wired with a test subscriber/recorder on every event the pool exposes (if any)
  - When: `Spawn` then `ReturnToPool` is called
  - Then: no death/XP-shaped callback is recorded. The pool exposes no `EnemyDied`/XP event (that event is owned by `IEnemyWorld`).
  - Edge cases: none

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: `tests/unit/object-pool/object_pool_core_test.cs`. It must exist and pass (EditMode, deterministic; a fresh pool built in `[SetUp]`).

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: bootstrap-combat-frame Story 001 (project setup). It also needs the bootstrap-combat-frame epic's `[DESIGNER-ERROR]` channel (TR-TP-002) and `TestConfigs` factories (ADR-0010), and the game-data epic's `TuningConfig` type (tests may use an in-memory instance until the game-data stories land).
- Unlocks: Story 002, Story 003, Story 004, Story 005

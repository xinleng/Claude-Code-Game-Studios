# Story 004: Binding ReturnToPool order: flush parry events → deregister → deactivate → push

> **Epic**: Object Pool & Enemy Lifecycle
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Logic
> **Manifest Version**: 2026-10-10

## Context

**GDD**: `design/gdd/enemy-definition.md` (Implementation Contract #2 "AttackEvent pool flush"), `design/gdd/wave-spawner.md` (Rule 5)
**Requirement**: `TR-ED-004`
*(Requirement text lives in `docs/architecture/tr-registry.yaml`. Read it fresh at review time.)*

**ADR Governing Implementation**: ADR-0005: Object Pooling and Enemy Lifecycle
**ADR Decision Summary**: `ReturnToPool(enemy)` is the **only** legal deactivation site. Its order is binding: `ParrySystem.FlushEventsFor(enemy.AttackerId)` → deregister from the manager → `SetActive(false)` → push to the pool. `OnDisable`/`OnDestroy`/`OnTriggerExit` are never used for cleanup, because Unity gives no ordering guarantee.

**Engine**: Unity 6.3 LTS | **Risk**: LOW
**Engine Notes**: No post-cutoff APIs. Unity gives no ordering guarantee for `OnDisable`, so the flush must be an explicit call **before** `SetActive(false)` (ADR-0005 Constraints). `OnTriggerExit` is not reliably fired on `SetActive(false)` (ADR-0005 Risks, engine-specialist finding). No system may rely on it for enemy-death cleanup.

**Control Manifest Rules (Foundation)**:
- Required: the `ReturnToPool` order is binding: `ParrySystem.FlushEventsFor(id)` → deregister → `SetActive(false)` → push.
- Forbidden: never call `SetActive(false)` on an enemy (or `Destroy` any pooled object) outside `ObjectPool.ReturnToPool` (registered forbidden pattern `setactive_false_outside_pool`, grep-checkable). Never rely on `OnTriggerExit` for deactivation cleanup.
- Required: wire by interface injection at Bootstrap. ParrySystem is not built yet, so the pool depends on a narrow flush interface.

---

## Acceptance Criteria

*From GDD `design/gdd/enemy-definition.md` Contract #2 + ADR-0005 Decision §3, scoped to this story:*

- [ ] **AC-1**: `ReturnToPool(enemy)` performs, in this exact order: (1) `FlushEventsFor(enemy.AttackerId)`, (2) deregister from the manager, (3) `SetActive(false)`, (4) push to the free pool. (TR-ED-004; ADR-0005 §3)
- [ ] **AC-2**: The ID passed to `FlushEventsFor` is the enemy's **current-spawn** `AttackerId`, never a stale ID from an earlier life of the same slot. (enemy-definition Contract #2: AttackerId-keyed flush)
- [ ] **AC-3**: At the moment `FlushEventsFor` executes, the enemy is still active (`activeSelf == true`). The flush is an explicit call and does not depend on `OnDisable`/`OnDestroy`. (enemy-definition Contract #2: "Do not use `OnDisable()` or `OnDestroy()` as the flush trigger")
- [ ] **AC-4**: Each `ReturnToPool` calls `FlushEventsFor` exactly once and deregister exactly once.
- [ ] **AC-5**: A static check confirms that no code outside `ObjectPool.ReturnToPool` calls `SetActive(false)` on an enemy, and no code outside the pool/bootstrap calls `Destroy(` on a pooled object. (enemy-definition Contract #2 "`SetActive(false)` restriction"; registered forbidden pattern)

---

## Implementation Notes

*Derived from ADR-0005 Decision §3 and architecture.md (`IParrySystem.FlushEventsFor`, `IObjectPool.ReturnToPool`):*

- The pool depends on a **narrow interface**, suggested as `IParryEventFlush { void FlushEventsFor(int attackerId); }`, injected at Bootstrap. architecture.md's `IParrySystem` already declares `FlushEventsFor(int attackerId)`. When the Parry epic lands, `IParrySystem` extends or implements `IParryEventFlush`, so ParrySystem satisfies it with no pool change. Tests use a spy. Confirm the interface split at `/code-review`. It must not duplicate or contradict the `IParrySystem` signature.
- Deregistration goes through the same registration seam as Story 003 (for example, `IEnemyRegistration.Deregister(EnemyController)`; the name is a story-time choice). Tests use a recording fake.
- Clear `_initialized` (Story 003) inside `ReturnToPool` before `SetActive(false)`, so the deactivation/reactivation path cannot stage a phantom registration.
- Implement the four steps as straight-line code in one method, so the contract is auditable (ADR-0005 §6: "the correctness contracts are the point; a thin bespoke pool makes them explicit and testable"). Do not move any step into a callback.
- **Mid-iteration deregistration:** `ReturnToPool` may be invoked while EnemySystemManager is iterating its active list (for example, a death in the state-transition phase). Safe removal during iteration is the manager's responsibility (Enemy AI epic). Note it in the interface doc comment, but do not solve it in the pool.
- Static check (AC-5): add a test that scans the game source directory for `SetActive(false)` and `Destroy(` and fails on any hit outside the allow-listed files (the pool's `ReturnToPool` implementation; Bootstrap/pool teardown for `Destroy`). Keep the allow-list in the test fixture. Expect false positives to be reviewed, not silenced with broad patterns.
- The Warlord Phase-2 flush (AC-19) uses the same `FlushEventsFor`, but EnemySystemManager calls it at the phase transition, not the pool. It is out of scope here.

---

## Out of Scope

*Handled by neighbouring stories. Do not implement here:*

- Story 003: `_initialized` set/clear semantics and staging
- Story 005: projectile return/cancel path
- Parry epic: `FlushEventsFor` implementation (AttackerId-keyed dictionary, O(1)), `HasOpenWindowFor`
- Enemy AI epic: AC-19 (Warlord Phase-2 transition flush, Integration), manager-side safe removal during iteration
- WaveSpawner epic: AC-WS-05 boss-spawn elite clear (calls `ReturnToPool`)

---

## QA Test Cases

*Written at story creation (lean mode: QL-STORY-READY gate skipped). The developer implements against these cases and does not invent new test cases during implementation.*

- **AC-1**: Exact four-step order
  - Given: a pool injected with an `IParryEventFlush` spy and an `IEnemyRegistration` fake, both appending to one shared ordered `EventRecorder` (ADR-0010 helper). A test `EnemyController` double also appends "deactivated" from its deactivation observation, and the pool's free count is sampled into the recorder after return.
  - When: `Spawn` then `ReturnToPool(enemy)` is called
  - Then: the recorder sequence for the return is exactly `[Flush(id), Deregister(enemy), Deactivated, Pushed]`
  - Edge cases: returning two enemies back-to-back yields two complete, non-interleaved four-step sequences

- **AC-2**: Flush uses the current-spawn ID
  - Given: a type with pool count 1
  - When: spawn (id₁) → return → spawn (id₂) → return
  - Then: the spy recorded `FlushEventsFor(id₁)` then `FlushEventsFor(id₂)`, with `id₂ != id₁`
  - Edge cases: `ResetInstanceIdCounter()` between tests keeps IDs deterministic

- **AC-3**: Enemy still active during flush
  - Given: an `IParryEventFlush` spy that, when invoked, reads the enemy's `gameObject.activeSelf` (the enemy is resolved by its ID from a test-side map)
  - When: `ReturnToPool(enemy)` is called
  - Then: the value captured inside the flush call is `true`, and after the return `activeSelf == false`
  - Edge cases: a test controller whose `OnDisable` would record a flush shows that no flush originates from `OnDisable` (spy count stays 1)

- **AC-4**: Exactly-once calls
  - Given: the spies from AC-1
  - When: one `ReturnToPool` is called
  - Then: the flush call count == 1 and the deregister call count == 1
  - Edge cases: none

- **AC-5**: Forbidden-pattern static check
  - Given: the game source tree and an allow-list fixture
  - When: the scan test runs
  - Then: zero `SetActive(false)` occurrences outside `ReturnToPool`, and zero `Destroy(` occurrences outside the pool/bootstrap allow-list
  - Edge cases: plant a deliberate violation in a temporary test-only file and confirm the scanner detects it (prove the check can fail), then remove it within the same test

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: `tests/unit/object-pool/object_pool_return_order_test.cs`. It must exist and pass (EditMode spies + EventRecorder; static-scan test included).

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 002 (AttackerId), Story 003 (registration seam + `_initialized`). It also needs the bootstrap-combat-frame epic's `EventRecorder` helper (ADR-0010).
- Unlocks: Story 006; Parry epic (ParrySystem implements the flush interface); Enemy AI epic AC-19; WaveSpawner AC-WS-05/06

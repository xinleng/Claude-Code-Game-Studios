# Story 003: Deferred registration staging and pre-warm phantom-registration guard

> **Epic**: Object Pool & Enemy Lifecycle
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Logic
> **Manifest Version**: 2026-10-10

## Context

**GDD**: `design/gdd/enemy-definition.md` (Implementation Contract #1 "Deferred registration")
**Requirement**: `TR-ED-008`
*(Requirement text lives in `docs/architecture/tr-registry.yaml`. Read it fresh at review time.)*

**ADR Governing Implementation**: ADR-0001: Entity Architecture (registration model: `RegisterPending` staging) + ADR-0005: Object Pooling and Enemy Lifecycle (amended 2026-10-10: pre-warm phantom-registration guard)
**ADR Decision Summary**: `EnemyController.OnEnable` never registers directly. It stages itself on `EnemySystemManager.RegisterPending`, which the manager flushes at the start of its next `Tick()`. `OnEnable` stages **only** when `_initialized` is set. The flag is set by `Initialize` and cleared by `ReturnToPool`, so pre-warm (which instantiates under an inactive container) can never stage 200+ phantom enemies.

**Engine**: Unity 6.3 LTS | **Risk**: LOW
**Engine Notes**: No post-cutoff APIs. Pooled re-enables fire `OnEnable` mid-frame (ADR-0001 Risks). That is the reason staging exists. **Test-harness caveat (verify against Unity 6.3 Test Framework docs):** in EditMode, Unity does not invoke `Awake`/`OnEnable` on MonoBehaviours that lack `[ExecuteAlways]`. The phantom-registration test exercises real engine activation behaviour, so it must run in the **PlayMode** test assembly. Pure guard logic can additionally be unit-tested by calling the extracted staging method directly in EditMode.

**Control Manifest Rules (Foundation)**:
- Required: pre-warm under an inactive container. `OnEnable` stages registration only when `_initialized` is set (set by `Initialize`, cleared by `ReturnToPool`).
- Required: wire dependencies by injection at Bootstrap, with no `FindObjectOfType`/`FindFirstObjectByType` and no singletons.
- Forbidden: never register with a manager directly from `OnEnable`; stage on `RegisterPending` (ADR-0001, ADR-0005). Never let a gameplay object self-initialise or touch another system in `Awake`/`Start` (ADR-0012).

---

## Acceptance Criteria

*From GDD `design/gdd/enemy-definition.md` Contract #1 (Deferred registration) + ADR-0005 §1–§2 (amended), scoped to this story:*

- [ ] **AC-1**: Pre-warming the full pool produces **zero** `RegisterPending` entries (EPIC Definition of Done; ADR-0005 §1 amended).
- [ ] **AC-2**: `Spawn` (`Initialize` → `SetActive(true)`) causes the controller's `OnEnable` to stage **exactly one** `RegisterPending` entry for that controller. It never calls any direct registration / active-list mutation API. (TR-ED-008)
- [ ] **AC-3**: `_initialized` is set by `Initialize` and cleared by `ReturnToPool`. `OnEnable` with `_initialized == false` stages nothing.
- [ ] **AC-4**: A recycled instance (spawn → return → spawn) stages exactly one new `RegisterPending` entry on its second spawn. No stale staging carries over from the first life.

---

## Implementation Notes

*Derived from ADR-0001 Decision §1 / Risks and ADR-0005 Decision §1–§2 (amended):*

- The pool depends on a **narrow registration interface** (suggested: `IEnemyRegistration` with `RegisterPending(EnemyController)`), not on the concrete `EnemySystemManager`. EnemySystemManager (Enemy AI epic) will implement it later. Tests use a recording fake. The interface name is a story-time choice; confirm it against `docs/registry/architecture.yaml` (`enemy_state_and_hp` is EnemySystemManager-owned) at `/code-review`.
- The controller needs its registration sink without self-wiring in `Awake` (manifest Foundation Forbidden). The suggested approach: the pool hands each instance the injected sink during pre-warm through an explicit bind method that the pool calls. Bootstrap injects the sink into the pool. Never use `FindFirstObjectByType` or a static accessor.
- `OnEnable` body: `if (!_initialized) return;` then `_registration.RegisterPending(this)`. Nothing else, per ADR-0001 Key Interfaces: "no `Update`/`FixedUpdate`/`OnEnable` registration side effects beyond staging".
- `Initialize(in SpawnParams)` (Story 002) sets `_initialized = true` as its last step. `ReturnToPool` clears it (`_initialized = false`) **before** `SetActive(false)` (Story 004 owns the full order).
- The pre-warm container stays inactive while instantiating (Story 001). The `_initialized` guard is the second line of defence if any activation callback runs during warm-up. Both defences are required by the amendment.
- The manager-side flush of `RegisterPending` at the start of `Tick()` ("no list mutation mid-iteration") belongs to EnemySystemManager. Do not build it here. This story only guarantees that the pool/controller side stages correctly.
- `EnemyController` implements no `Update`/`FixedUpdate` (ADR-0001 Validation Criteria). Do not add one here.

---

## Out of Scope

*Handled by neighbouring stories. Do not implement here:*

- Story 002: setting `AttackerId`/jitter inside `Initialize`
- Story 004: deregistration and the full binding return order
- Enemy AI / EnemySystemManager epic: `RegisterPending` flush at `Tick()` start, active list iteration, the two-phase tick

---

## QA Test Cases

*Written at story creation (lean mode: QL-STORY-READY gate skipped). The developer implements against these cases and does not invent new test cases during implementation.*

- **AC-1**: Pre-warm stages nothing (PlayMode)
  - Given: a recording `IEnemyRegistration` fake and a pool fixture whose infantry total = `max_enemy_count` (from `TestConfigs`) plus one per elite/boss type, using a real test prefab that has an `EnemyController`
  - When: the pool pre-warms, and then one frame is yielded (so any deferred activation callbacks would have fired)
  - Then: the fake's `RegisterPending` call count == 0
  - Edge cases: also assert 0 after the container becomes active-in-hierarchy, if the Story 001 implementation activates it

- **AC-2**: Spawn stages exactly once (PlayMode)
  - Given: a pre-warmed pool with the recording fake
  - When: one `Spawn` is called
  - Then: the fake recorded exactly 1 `RegisterPending` call, whose argument is the returned controller. No other method on the fake was called.
  - Edge cases: 3 spawns in the same frame record 3 calls with 3 distinct controllers, in spawn order

- **AC-3**: The guard flag lifecycle
  - Given: a controller from the pool
  - When: its `OnEnable` path runs (a) before any `Initialize`, (b) after `Initialize`, (c) after `ReturnToPool`
  - Then: staging calls are (a) 0, (b) 1, (c) 0. The `_initialized` observable (internal/test-visible accessor) reads false, true, false respectively.
  - Edge cases: the EditMode variant calls the extracted staging method directly. The PlayMode variant uses real `SetActive` toggles. Both must agree.

- **AC-4**: Recycled instance stages fresh
  - Given: a type with pool count 1 and the recording fake
  - When: spawn → `ReturnToPool` → spawn
  - Then: the total `RegisterPending` count == 2, both with the same controller reference. The return itself caused 0 staging calls.
  - Edge cases: none

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: `tests/unit/object-pool/object_pool_deferred_registration_test.cs`. It must exist and pass. The phantom/activation cases live in the PlayMode test assembly. Guard logic is also covered in EditMode.

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001 (pre-warm under the inactive container), Story 002 (`Initialize` sets `_initialized`)
- Unlocks: Story 004; Enemy AI epic (EnemySystemManager implements the registration interface)

# Story 002: Per-spawn AttackerId and spawn-time cooldown jitter via Initialize(in SpawnParams)

> **Epic**: Object Pool & Enemy Lifecycle
> **Status**: Complete
> **Layer**: Foundation
> **Type**: Logic
> **Manifest Version**: 2026-10-10

## Context

**GDD**: `design/gdd/enemy-definition.md` (Implementation Contracts #1 "Spawn-time cooldown jitter" + #2 "`AttackerId` uniqueness"; AC-18), `design/gdd/damage-health.md` (Implementation Contract #6)
**Requirement**: `TR-ED-003`, `TR-ED-009`
*(Requirement text lives in `docs/architecture/tr-registry.yaml`. Read it fresh at review time.)*

> **Superseded wording (do not implement):** TR-ED-003, damage-health Contract #6, and enemy-definition's AttackEvent field comment all still say "static counter" / `public static void ResetInstanceIdCounter()`. The 2026-10-10 ADR-0005 amendment makes `_nextInstanceId` a **pool instance field**, and the control manifest forbids a static `AttackerId` counter. The ADR and manifest are authoritative for implementation. The GDD/registry wording is a known documentation lag. Do not edit those docs from this story.

**ADR Governing Implementation**: ADR-0005: Object Pooling and Enemy Lifecycle (amended 2026-10-10). Also ADR-0010 rule 4 (injected RNG) and rule 5 (test seams).
**ADR Decision Summary**: `Spawn` calls `controller.Initialize(new SpawnParams { attackerId: _nextInstanceId++, initialCooldownMs: _rng.Next(0, attack_cooldown_ms), data, position })` before `SetActive(true)`. `_nextInstanceId` is an instance field (per spawn, never per slot). The RNG is an injected `System.Random`, never `UnityEngine.Random`.

**Engine**: Unity 6.3 LTS | **Risk**: LOW
**Engine Notes**: No post-cutoff APIs. Because the counter is instance state, there is no domain-reload hazard and no `[InitializeOnEnterPlayMode]` hook is needed. If a static is ever reintroduced, the reset hook must be `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]` (ADR-0005 §4). `System.Random.Next(int, int)` is virtual, so tests may subclass it for a scripted sequence.

**Control Manifest Rules (Foundation)**:
- Required: `Spawn()` assigns `AttackerId` from the pool's **instance field** `_nextInstanceId++` (never static, never per slot) and jitter from the **injected `System.Random`** via `Initialize(in SpawnParams)`.
- Required: gameplay logic reads randomness via injected `System.Random` (ADR-0010).
- Forbidden: never use a static `AttackerId` counter. Never use `UnityEngine.Random` inside testable gameplay logic (Core-layer rule, applies here too). Never hardcode a gameplay/tuning value. `attack_cooldown_ms` comes from `EnemyDataSO`.
- Guardrail: no per-spawn allocation. `SpawnParams` is a struct passed by `in`.

---

## Acceptance Criteria

*From GDD `design/gdd/enemy-definition.md` Contracts #1–#2, AC-18; `design/gdd/damage-health.md` Contract #6 (as amended by ADR-0005), scoped to this story:*

- [ ] **AC-1**: Every `Spawn` assigns `AttackerId = _nextInstanceId++` from the pool **instance** field. IDs are unique and strictly increasing across all spawns of all types within one pool instance. (TR-ED-003)
- [ ] **AC-2**: `AttackerId` is unique per spawn instance, **not per pool slot**. A recycled slot receives a new ID on each spawn. (enemy-definition Contract #2)
- [ ] **AC-3**: `ResetInstanceIdCounter()` exists on the pool instance as a test seam, marked `[Conditional("UNITY_EDITOR")]` (ADR-0010 rule 5). After a reset, the next spawn receives the initial ID value. Two separate pool instances keep independent counters (no shared/static state).
- [ ] **AC-4**: At spawn, `initialCooldownMs = _rng.Next(0, attack_cooldown_ms)` is computed from the injected `System.Random` and the spawned type's `EnemyDataSO.attack_cooldown_ms`. It is delivered through `EnemyController.Initialize(in SpawnParams)` together with `attackerId`, `data`, and `position`. (TR-ED-009; ADR-0005 §2)
- [ ] **AC-5**: `Initialize` is called on **every** spawn, including recycled instances, and **before** `SetActive(true)`. `initialCooldownMs` is a required field that is always assigned from the RNG and never left at its default. (enemy-definition Contract #1: "it must NOT default to 0")
- [ ] **AC-6** *(AC-18 pool-side precondition)*: When 10 Levy Soldiers (`attack_cooldown_ms` 2000) spawn in the same frame with a fixed-seed RNG, no more than 3 of their `initialCooldownMs` values fall in the same 60fps frame bucket (`floor(initialCooldownMs / (1000/60))`). *(The full AC-18 assertion, "≤ 3 new Telegraph entries in any single frame", needs the EnemySystemManager state machine and is owned by the Enemy AI epic. This story proves the jitter input that AC-18 depends on.)*

---

## Implementation Notes

*Derived from ADR-0005 Decision §2, §4 (amended) and ADR-0010 rules 4–5:*

- Spawn sequence for this story: pop the instance (Story 001), then `controller.Initialize(new SpawnParams { attackerId = _nextInstanceId++, initialCooldownMs = _rng.Next(0, data.attack_cooldown_ms), data = <type's EnemyDataSO>, position })`, then `SetActive(true)`. `Initialize` must come before activation so Story 003's `OnEnable` guard sees `_initialized == true`.
- `SpawnParams` is a `readonly struct` passed `in` (manifest: payload structs passed by `in`, no per-spawn allocation). The four fields come from ADR-0005 §2. Do not add fields here without an ADR amendment.
- `_nextInstanceId` is a private instance `int` on the pool. One pool instance per run/bootstrap means it resets naturally with the pool (ADR-0005 §4).
- `ResetInstanceIdCounter()` is a public instance method with `[Conditional("UNITY_EDITOR")]` (return type `void`, as `Conditional` requires). Tests that assert specific ID values call it in `[SetUp]` or build a fresh pool per test (damage-health Contract #6 intent: deterministic IDs regardless of test order).
- The pool receives `System.Random` by constructor/initializer injection from Bootstrap. Tests pass a fixed seed or a scripted subclass. Never touch `UnityEngine.Random`.
- Use the spawned type's **active** `EnemyDataSO` for `attack_cooldown_ms`. The Warlord spawns with its Phase 1 data (enemy-definition Contract #1 "Phase 2 data load hook": both SOs serialized on the prefab).
- `System.Random.Next(0, n)` returns `[0, n)`. This matches the GDD's `Random.Range(0, attack_cooldown_ms)` integer semantics. Do not add 1.
- Doc comments on `Initialize`, `SpawnParams`, and `ResetInstanceIdCounter` (include the "test seam, editor-only" note).

---

## Out of Scope

*Handled by neighbouring stories. Do not implement here:*

- Story 001: pre-warm, pop/push storage, exhaustion
- Story 003: what `OnEnable` does with `_initialized` (registration staging)
- Story 004: using `AttackerId` for `FlushEventsFor` on return
- Story 005: projectile tagging with `AttackerId`
- Enemy AI / EnemySystemManager epic: consuming `initialCooldownMs` in the first Cooldown state, full AC-18 Telegraph-entry count, AttackEvent emission

---

## QA Test Cases

*Written at story creation (lean mode: QL-STORY-READY gate skipped). The developer implements against these cases and does not invent new test cases during implementation.*

- **AC-1**: IDs unique and increasing across types
  - Given: a fresh pool (`ResetInstanceIdCounter()` in `[SetUp]`) with ≥2 types
  - When: N spawns are interleaved across types (N = total pool capacity from the fixture)
  - Then: the N `AttackerId`s are pairwise distinct and strictly increasing in spawn order
  - Edge cases: the first ID after reset equals the documented initial value (assert the constant, not a magic number)

- **AC-2**: Recycled slot gets a new ID
  - Given: a type with pool count 1
  - When: spawn (record id₁) → `ReturnToPool` → spawn again (same object reference, id₂)
  - Then: `id₂ != id₁` and `id₂ > id₁`
  - Edge cases: 3 consecutive recycle cycles yield 3 distinct IDs on the same object

- **AC-3**: Reset seam and instance isolation
  - Given: pool A has spawned 5 times
  - When: `A.ResetInstanceIdCounter()` is called, then A spawns once. Separately, a new pool B spawns once.
  - Then: A's next ID == the initial value. B's first ID == the initial value, unaffected by A's history.
  - Edge cases: static check that the pool type declares no `static` field for the counter (reflection assert: no static int fields on the pool type)

- **AC-4**: Jitter comes from the injected RNG and the type's cooldown
  - Given: a scripted `System.Random` subclass whose `Next(min, max)` records its arguments and returns a scripted value; Levy fixture `attack_cooldown_ms` from `TestConfigs`
  - When: one Levy Soldier is spawned
  - Then: `Next` was called with `(0, attack_cooldown_ms)`, and the controller's received `SpawnParams.initialCooldownMs` equals the scripted return value. `attackerId`, `data`, and `position` also match.
  - Edge cases: an Iron Captain spawn passes its own cooldown (fixture value) and not the Levy one; a Warlord spawn uses Phase 1 data's cooldown

- **AC-5**: Initialize precedes activation on every spawn
  - Given: a test `EnemyController` double that records `activeSelf` at the moment `Initialize` is invoked
  - When: a first spawn and a recycled spawn occur
  - Then: `Initialize` is invoked exactly once per spawn, and the recorded `activeSelf == false` both times
  - Edge cases: a scripted RNG returning `attack_cooldown_ms − 1` (upper bound) is passed through unchanged; a scripted 0 is passed through as 0 (legitimately zero, but still assigned)

- **AC-6**: Same-frame jitter spread (AC-18 precondition)
  - Given: a fixed-seed `System.Random` (seed is a named test constant), Levy fixture with `attack_cooldown_ms` = 2000 (boundary value from the GDD Formula 4 table), 10 Levy Soldiers spawned in one frame
  - When: each `initialCooldownMs` is bucketed into `floor(value / (1000/60))`
  - Then: the maximum bucket population is ≤ 3
  - Edge cases: also assert all 10 values are in `[0, 2000)`. If the chosen seed fails, that is a real finding to report, not a reason to re-roll the seed.

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: `tests/unit/object-pool/object_pool_spawn_identity_test.cs`. It must exist and pass (EditMode, deterministic, seeded/scripted RNG only).

**Status**: [x] `tests/unit/object-pool/object_pool_spawn_identity_test.cs` (9 tests), EditMode 107/107

---

## Dependencies

- Depends on: Story 001 (pool core). Also needs the game-data epic's `EnemyDataSO` type with `attack_cooldown_ms` (tests may use `ScriptableObject.CreateInstance` in-memory).
- Unlocks: Story 003, Story 004, Story 005; Enemy AI epic's AC-18 test

---

## Completion Notes
**Completed**: 2026-10-11
**Criteria**: 6/6 passing. AC-6 (same-frame jitter spread) passes with the named seed `TestPoolSpawnData.Seed = 20261010` — not re-rolled.
**Deviations**: Advisory —
- `ObjectPool` constructor gains `spawnData` (per-type active `EnemyDataSO`; Warlord = Phase 1) and the injected `System.Random`; a type with count > 0 and no data reports a designer error at pre-warm.
- `ObjectPool.FirstAttackerId = 1` (0 is never a valid id) — the documented initial value AC-1/AC-3 assert against.
- `EnemyController.Initialize(in SpawnParams)` is `internal` (pool-only); it also records two internal test observations (`InitializeCount`, `ActiveSelfAtLastInitialize`) for AC-5 instead of a test double subclass (the controller is sealed).
- **Open item:** Iron Captain tiers share one pool and `Spawn(type, position)` has no tier argument, so the injected map supplies one tier's data. WaveSpawner needs a way to pick the 5/10/15-min tier (e.g. per-spawn data override) — raise with the WaveSpawner / Enemy AI epics; would need an ADR-0005 amendment since `SpawnParams` fields are fixed.
**Test Evidence**: Logic — `tests/unit/object-pool/object_pool_spawn_identity_test.cs` (9 tests); helper `tests/helpers/TestPoolSpawnData.cs`. EditMode 107/107.
**Code Review**: Self-review during the autonomous Sprint 1 batch; consolidated independent review at batch end. LP/QL gates skipped (lean).

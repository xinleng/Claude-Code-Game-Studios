# Story 006: Zero-Instantiate churn validation and player-build Profiler capture

> **Epic**: Object Pool & Enemy Lifecycle
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Integration
> **Manifest Version**: 2026-10-10

## Context

**GDD**: `design/gdd/wave-spawner.md` (Rule 5; AC-WS-06 pool-side portion)
**Requirement**: `TR-WS-001`
*(Requirement text lives in `docs/architecture/tr-registry.yaml`. Read it fresh at review time.)*

**ADR Governing Implementation**: ADR-0005: Object Pooling and Enemy Lifecycle
**ADR Decision Summary**: The pool guarantees zero mid-run `Instantiate`/`Destroy` and O(1), allocation-free spawn/return. ADR-0005 Validation Criteria require a Profiler capture of a 2-minute run showing zero `Instantiate` calls and zero GC alloc from spawn churn. Verification Required also lists pool pre-warm time at scene load.

**Engine**: Unity 6.3 LTS | **Risk**: LOW
**Engine Notes**: No post-cutoff gameplay APIs. **Benchmark conditions are binding** (ADR-0001 amended; manifest Feature guardrail): measure in a **player build** (record Mono vs IL2CPP) on the pinned min-spec. Editor numbers run 2–5× worse and include editor-only allocations. The GC assertion uses the Unity Test Framework constraint `Is.Not.AllocatingGCMemory()` (`UnityEngine.TestTools.Constraints`). Verify its availability and semantics in the Unity 6.3 Test Framework package docs before relying on it.

**Control Manifest Rules (Foundation)**:
- Guardrail: **zero `Instantiate`/`Destroy` during a run (Profiler-verified)** (ADR-0005).
- Guardrail: benchmark in a player build (record Mono/IL2CPP) on the pinned min-spec, never in the Editor (ADR-0001).
- Forbidden: never call `Destroy` on any pooled object outside `ObjectPool.ReturnToPool`; never hardcode a tuning value (churn rates and live counts come from `TuningConfig`/test fixtures).

---

## Acceptance Criteria

*From GDD `design/gdd/wave-spawner.md` Rule 5 + AC-WS-06 (pool-side) and ADR-0005 Validation Criteria / Verification Required:*

- [ ] **AC-1** *(AC-WS-06, pool-side)*: Across a scripted churn that cycles **every** pooled enemy instance and every pooled projectile at least twice (spawn all → return all, ×2), plus interleaved partial churn, no new pooled GameObject is created and none is destroyed after pre-warm. The pooled object set is identical (same references, same count) before and after. (wave-spawner Rule 5: "`Instantiate`/`Destroy` per enemy is forbidden")
- [ ] **AC-2**: Steady-state `Spawn` + `ReturnToPool` (enemies) and projectile spawn + cancel/return allocate **zero** GC memory. (ADR-0005: "no per-spawn allocation"; EPIC DoD "zero spawn-churn GC")
- [ ] **AC-3**: A Profiler capture of a **2-minute** stress run in a **player build** with ~`max_enemy_count` (200) live enemies and continuous spawn/return churn shows **zero `Instantiate` calls** and **zero GC alloc attributable to spawn churn**. Build backend (Mono/IL2CPP) and hardware are recorded. (ADR-0005 Validation Criteria; EPIC DoD)
- [ ] **AC-4**: Pool pre-warm time at scene load is measured in the same player build and recorded in the evidence doc. (ADR-0005 Verification Required / Performance Implications: "Pre-warm cost at scene load: measure; acceptable". No numeric threshold is defined, so record only and flag if it looks anomalous.)
- [ ] **AC-5**: A static check confirms that `Instantiate(` appears only in the pool's pre-warm path and Bootstrap, and `Destroy(` only in the pool/Bootstrap teardown allow-list. (ADR-0005 Risks: "A stray `Destroy()` call → forbidden pattern + review grep")

---

## Implementation Notes

*Derived from ADR-0005 Validation Criteria, Performance Implications, and Risks; ADR-0001 benchmark conditions:*

- **AC-1/AC-2 harness (automated):** an integration test that builds the real pool (Stories 001–005) with in-memory configs, records the full set of pooled object references after pre-warm, then drives the churn script. Count pooled objects by enumerating the pool container's children, or by instrumenting the pool's own instance registry. Do **not** use `FindObjectsByType` in production code. Using it inside the test only is acceptable. GC is asserted with `Assert.That(() => { /* spawn+return loop */ }, Is.Not.AllocatingGCMemory())` after a warm-up iteration (first-call JIT/static-init allocations are excluded by design).
- Use test doubles for the injected seams (`IParryEventFlush`, `IEnemyRegistration`, `[DESIGNER-ERROR]` channel). The doubles must themselves be allocation-free in the measured loop, or they will pollute AC-2.
- **AC-3 stress driver (manual capture):** a test-only stress scene/driver (under the test or tools area, not shipped gameplay) that keeps ~200 enemies live and churns spawns/returns for 2 minutes, reading counts/rates from config. WaveSpawner does not exist yet. The real WaveSpawner-driven checks (full AC-WS-06, AC-WS-07 frame budget) are re-verified in the WaveSpawner epic.
- Capture with a Development Build + Profiler connected (Unity 6.3: Profiler / Memory Profiler package; verify current menu paths against `docs/engine-reference/unity/`). Record the `GC.Alloc` and `Instantiate` markers over the full 2 minutes and attach screenshots/summary numbers to the evidence doc.
- AC-5 reuses the Story 004 scanner with an `Instantiate(`/`Destroy(` allow-list. Extend that scanner rather than writing a second one.

---

## Out of Scope

*Handled by neighbouring stories or other epics. Do not implement here:*

- Stories 001–005: the pool behaviour itself (this story only proves it under churn)
- WaveSpawner epic: AC-WS-06 driven by the real spawner and death path; AC-WS-07 (16.6ms frame time at 200 enemies, ADVISORY)
- Enemy AI epic: enemy tick budget (≤ ~4ms at 200, ADR-0001), which is a separate benchmark

---

## QA Test Cases

*Written at story creation (lean mode: QL-STORY-READY gate skipped). The developer implements against these cases and does not invent new test cases during implementation.*

- **AC-1**: Pooled object set invariant under churn
  - Given: a pre-warmed pool (all enemy types + projectiles, counts from `TestConfigs`) and a snapshot `HashSet` of every pooled object reference
  - When: the churn script runs: spawn all → return all, twice, then 3 interleaved rounds of partial spawn/return (sizes from fixture constants)
  - Then: the post-churn reference set equals the snapshot exactly (no additions, no missing/destroyed objects), and the total pooled count is unchanged
  - Edge cases: include exhaustion attempts mid-churn (infantry drop, elite designer error), which must still create no objects; include a projectile cancel-all in the churn

- **AC-2**: Zero GC in steady state
  - Given: the pool after one warm-up spawn/return cycle, with allocation-free test doubles
  - When: a loop of enemy `Spawn`+`ReturnToPool` and projectile spawn+cancel runs inside `Is.Not.AllocatingGCMemory()`
  - Then: the constraint passes
  - Edge cases: run the loop once with a recycled slot (ID increment path) and once across multiple types

- **AC-3**: Player-build 2-minute Profiler capture (manual)
  - Setup: a Development player build of the stress scene (record Mono or IL2CPP), Profiler attached, ~200 live enemies with continuous churn for 120 s
  - Verify: the `Instantiate` call count and the GC.Alloc samples attributable to pool spawn/return over the whole capture
  - Pass condition: `Instantiate` count == 0 after pre-warm, and zero GC.Alloc in pool spawn/return markers for the full 120 s. Backend + hardware recorded.

- **AC-4**: Pre-warm timing (manual, record-only)
  - Setup: the same player build, timed from pre-warm start to end (Profiler marker or `Stopwatch` log around the pre-warm call)
  - Verify: the measured duration and pool sizes used
  - Pass condition: the value is recorded in the evidence doc. There is no threshold (ADR-0005 defines none), so anomalies are flagged to technical-director rather than failing the story.

- **AC-5**: Instantiate/Destroy static scan
  - Given: the game source tree and an allow-list fixture (pool pre-warm, Bootstrap, pool teardown)
  - When: the scan runs
  - Then: zero `Instantiate(` or `Destroy(` hits outside the allow-list
  - Edge cases: a planted violation in a temporary file is detected (scanner can fail), then removed in the same test

---

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- Integration: `tests/integration/object-pool/object_pool_zero_instantiate_test.cs` (AC-1, AC-2, AC-5). It must exist and pass.
- Profiler/timing evidence: `production/qa/evidence/object-pool-zero-instantiate-evidence.md` (AC-3, AC-4): capture summary, screenshots, backend, hardware, pre-warm duration.

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001, Story 002, Story 003, Story 004, Story 005 (all DONE). It also needs bootstrap-combat-frame Story 001 (project setup) for player-build configuration.
- Unlocks: Epic Definition of Done; WaveSpawner epic (AC-WS-06/07 re-verification)

# Story 005: Archer projectile pool keyed by AttackerId with cancel-all linkage

> **Epic**: Object Pool & Enemy Lifecycle
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Logic
> **Manifest Version**: 2026-10-10

## Context

**GDD**: `design/gdd/enemy-definition.md` (Fire Arrow Archer rules; Implementation Contract #2 "AttackerId uniqueness"; AC-08 and AC-11b pool-side portions; tuning knob `archer_projectile_count`)
**Requirement**: `TR-PR-005` (**partial**: pool-side linkage only)
*(Requirement text lives in `docs/architecture/tr-registry.yaml`. Read it fresh at review time.)*

**ADR Governing Implementation**: ADR-0005: Object Pooling and Enemy Lifecycle
**ADR Decision Summary**: The same bespoke pool pre-warms archer projectiles (`archer_projectile_count × archers` ceiling, from `TuningConfig`) and owns their lifecycle. EPIC.md marks the cancel-linkage detail as **"⚠️ partial: decide at story time"**, and this story records that detail.

**Engine**: Unity 6.3 LTS | **Risk**: LOW
**Engine Notes**: No post-cutoff APIs. Same pre-warm caveats as Story 001 (instantiate under an inactive container; `Awake` is deferred to first activation).

**Control Manifest Rules (Foundation)**:
- Required: all enemy/**projectile** lifecycles go through `ObjectPool`. Pre-warm under an inactive container. Pool sizes come from `TuningConfig`.
- Forbidden: never `Destroy` any pooled object outside `ObjectPool.ReturnToPool`. Never hardcode a tuning value (`archer_projectile_count` is a `TuningConfig`/`EnemyDataSO` knob, default 1, range 1–3).
- Guardrail: zero `Instantiate`/`Destroy` during a run; no per-spawn allocation.

---

## Acceptance Criteria

*From GDD `design/gdd/enemy-definition.md` (Archer rules, AC-08, AC-11b) + TR-PR-005 + ADR-0005 §1, scoped to the pool side:*

- [ ] **AC-1**: The projectile pool pre-warms inactive under the pool container. Its capacity comes from `TuningConfig`. A load-time `[DESIGNER-ERROR]` is logged if capacity < `archer_projectile_count × <Fire Arrow Archer pool count>` (ADR-0005 §1: "`archer_projectile_count × archers` ceiling for projectiles"; Consequences: "undersized ... projectile pools are silent until hit → load-time sanity checks").
- [ ] **AC-2**: Spawning a projectile pops a pooled instance (no `Instantiate`) and tags it with the firing Archer's `AttackerId` and its `AttackDamage`. (TR-PR-005: "Archer projectiles pooled and keyed by AttackerId")
- [ ] **AC-3**: Cancelling for an `AttackerId` returns **all** in-flight projectiles carrying that ID to the pool with `AttackDamage = 0`. Afterwards each one is inactive (`IsActive == false`), and projectiles with other IDs are untouched. Parameterize across `archer_projectile_count` ∈ {1, 2}. (TR-PR-005; GDD AC-08 "returned to the projectile pool with `AttackDamage = 0`" and AC-11b "parameterize across `archer_projectile_count` values 1 and 2", pool portions)
- [ ] **AC-4**: Cancelling for an `AttackerId` with no in-flight projectiles (including any ID from a previous spawn of a recycled Archer slot) is a no-op that affects no projectile. (enemy-definition Contract #2: per-spawn IDs prevent ghost-cancel bugs from orphaned in-flight projectiles)
- [ ] **AC-5**: A projectile's normal end of life (hit or expiry, as reported by its owner) returns it through the pool's projectile return path. The pool is the only place projectiles are deactivated.

---

## Implementation Notes

*Derived from ADR-0005 Decision §1, §3, §5 and the TR-PR-005 requirement text:*

- **Cancel-linkage detail (recorded here per EPIC.md "decide at story time"):** expose two pool methods, a per-projectile return (normal end of life) and a cancel-all-by-`AttackerId` (parry linkage). The cancel sets `AttackDamage = 0`, deactivates, and pushes each matching in-flight projectile. ParrySystem (Parry epic) is the intended caller on a successful parry (GDD AC-08/AC-11b). Method names are a story-time choice; doc-comment them with the TR-PR-005 reference.
- The lookup must be **keyed by `AttackerId`** (TR-PR-005) and must allocate nothing in steady state, because Story 006 asserts zero GC. In-flight counts are tiny (`archer_projectile_count` ≤ 3 per attack). The keyed structure must be pre-allocated at pre-warm. A plain `Dictionary<int, List<…>>` that creates lists on new keys does **not** meet this. Pick a pre-sized structure and justify it in a code comment.
- Projectile capacity and `archer_projectile_count` are read from the injected config views (`TuningConfig` / `EnemyDataSO`). Nothing is hardcoded.
- Use Story 001's pre-warm path (inactive container, `activeSelf == false` at the end of warm-up).
- **Open question 1 (do not resolve unilaterally):** ADR-0005 §5 defines exhaustion policy for infantry and elite/boss, but **not for projectiles**. Until an ADR-0005 amendment decides, implement "drop the projectile request this tick + `[DESIGNER-ERROR]`". It mirrors the infantry drop and surfaces undersizing. Flag this choice at `/story-readiness` for technical-director confirmation.
- **Open question 2 (do not resolve unilaterally):** neither the GDDs nor ADR-0005 say whether an Archer's in-flight projectiles are cancelled when **that Archer** is returned to the pool (death). Contract #2's wording ("orphaned in-flight projectiles holding a stale ID") implies they may outlive their Archer. This story does **not** auto-cancel on enemy return. Raise it with the Parry/Enemy AI owners. Note that Story 004's `FlushEventsFor` on Archer death removes the parry window for those projectiles.
- Projectile flight, hit detection, and "range resolved at fire time, not hit time" (TR-PR-005 tail) belong to the Enemy AI / Parry epics, not the pool.

---

## Out of Scope

*Handled by neighbouring stories or other epics. Do not implement here:*

- Story 006: zero-GC / zero-Instantiate verification across projectile churn
- Parry epic: invoking cancel on a successful parry; full GDD AC-08 / AC-11b Integration tests (no damage reaches Damage & Health)
- Enemy AI epic: firing projectiles (count per attack, fire timing mid-telegraph), flight/hit resolution, range at fire time, AC-11a (one AttackEvent per attack animation)

---

## QA Test Cases

*Written at story creation (lean mode: QL-STORY-READY gate skipped). The developer implements against these cases and does not invent new test cases during implementation.*

- **AC-1**: Config-sized pre-warm + sanity check
  - Given: an in-memory config with projectile capacity, `archer_projectile_count`, and an Archer pool count (all from `TestConfigs`)
  - When: the pool pre-warms
  - Then: the projectile instance count == the configured capacity, all `activeSelf == false` under the container, and no `[DESIGNER-ERROR]` when capacity == `archer_projectile_count × archer count`
  - Edge cases: capacity = that product − 1 → exactly one `[DESIGNER-ERROR]`; `archer_projectile_count` = 3 (top of the GDD safe range) is sized correctly

- **AC-2**: Spawn tags ID and damage
  - Given: a pre-warmed projectile pool
  - When: a projectile is spawned for `AttackerId` A with `AttackDamage` D (fixture value)
  - Then: the returned instance is from the pre-warmed set, it is active, and it carries A and D. The pooled object count is unchanged.
  - Edge cases: two spawns for different IDs carry their own IDs

- **AC-3**: Cancel-all for one AttackerId (parameterized 1, 2)
  - Given: `archer_projectile_count` = k ∈ {1, 2}, k projectiles in flight for Archer A, plus 1 in flight for Archer B
  - When: cancel is called for A
  - Then: all k projectiles for A are `IsActive == false`, carry `AttackDamage == 0`, and are back in the free pool (free count +k). B's projectile is still active with its original damage.
  - Edge cases: cancelling A twice in a row leaves the second call a no-op (free count unchanged)

- **AC-4**: Stale/unknown ID cancel is a no-op
  - Given: Archer slot S spawned as id₁, fired one projectile, then Archer S was returned and respawned as id₂ and fired one projectile
  - When: cancel is called for id₁ after id₁'s projectile has already ended (normal return), and then cancel is called for an ID never used
  - Then: id₂'s projectile is unaffected in both cases, and the free count is unchanged by both calls
  - Edge cases: cancel with id₂ then cancels only id₂'s projectile

- **AC-5**: Normal return path
  - Given: one in-flight projectile
  - When: the per-projectile return method is called
  - Then: the projectile is inactive and back in the free pool, and a later spawn can reuse that same object reference
  - Edge cases: the static scan from Story 004 also covers projectile code (no `SetActive(false)`/`Destroy(` on projectiles outside the pool)

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: `tests/unit/object-pool/object_pool_projectile_test.cs`. It must exist and pass (EditMode, deterministic).

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001 (pre-warm/storage), Story 002 (`AttackerId` source)
- Unlocks: Story 006; Parry epic (AC-08, AC-11b integration); Enemy AI epic (Archer firing)

# Story 008: Run statistics aggregation and cause-of-death summary

> **Epic**: Run Session Manager
> **Status**: Blocked
> **Layer**: Foundation
> **Type**: Integration
> **Manifest Version**: 2026-10-10

> **BLOCKED**: depends on Combo/Parry stat signals (FinisherResolved, ParrySucceeded chain) — Core/Feature epics are held until the combo-and-parry prototype reports (Pre-Production gate 2026-10-10).

## Context

**GDD**: `design/gdd/run-session-manager.md`
**Requirement**: `TR-RS-003`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0003: Event Architecture — Struct Delegates and Ordered DamageTaken Dispatch
**ADR Decision Summary**: All gameplay events are publisher-owned `System.Action<TPayload>` delegates with `readonly struct` payloads; unordered multicast is fine for stats consumers. `PlayerDead` fires once, after `DamageTaken`, with kill context captured pre-reset.

**Engine**: Unity 6.3 LTS | **Risk**: LOW
**Engine Notes**: No post-cutoff APIs. Subscribe with cached delegates (no capturing lambdas) — zero GC per dispatch.

**Control Manifest Rules (this layer)**:
- Required: Events are publisher-owned `System.Action<T>` with `readonly struct` payloads, subscribed once at init with cached delegates — ADR-0003
- Forbidden: Never write state owned by another system (Damage & Health stays a ledger; RSM only reads signals) — ADR-0003
- Forbidden: Never allocate a closure lambda in a hot dispatch path — ADR-0003
- Guardrail: Zero GC allocation per event dispatch — ADR-0003

---

## Acceptance Criteria

*From GDD `design/gdd/run-session-manager.md` (AC-RS-04, Core Rules 7–8, Visual/Audio Requirements), scoped to this story:*

- [ ] **AC-RS-04**: GIVEN a `PlayerDead` event with `KillerDamageType = CounterHit`, `ComboCounterAtDeath = 2`, `TimeOfDeath = 842`, WHEN RSM handles it, THEN the Death screen shows a cause-of-death summary built from those fields plus the aggregated run stats.
- [ ] RSM maintains run stats from published signals: **total finishers landed** (`FinisherResolved`), **best parry chain** (`ParrySucceeded` chain), **wave/step reached** (WaveSpawner step events / Story 006 step trigger), **time survived** (`RunTime` at resolution).
- [ ] The Win screen receives the same aggregated stats (framed as triumph — layout is Run UI's).
- [ ] Stats counters reset to zero on retry (extends Story 007).
- [ ] Damage & Health is not asked for history — RSM is the historian (no new D&H API).

---

## Implementation Notes

*Derived from ADR-0003 Decisions 1/3/5 and architecture.md event table:*

- Subscriptions (architecture.md §2): `PlayerDead` (PlayerHealthSystem → RSM), `ParrySucceeded` (ParrySystem → RSM chain stats), `FinisherResolved` (ComboSystem → RSM stats), wave/elite/boss events (WaveSpawner → RSM wave stat). All unordered multicast; subscribe once at init with method-group delegates.
- `PlayerDeadData` fields: `FinalHP, TimeOfDeath, MomentumAtDeath, KillerAttackerId, ComboCounterAtDeath, KillerDamageType`. The cause-of-death summary is built from these (GDD Rule 7). Example sentence (GDD Visual/Audio): "Killed by Iron Captain counter-hit while holding a 2-chain at 14:02" — 842s → 14:02.
- Resolving `KillerAttackerId` to an enemy display name ("Iron Captain") needs a lookup that no current interface specifies — flag in `/story-readiness`; do not invent a registry inside RSM.
- **Best parry chain** depends on what `ParrySuccessData` carries (chain length vs per-success events) — the payload shape is owned by the Parry epic, which is held. This is the core reason the story is blocked.
- RSM provides the summary/stats as data to the Run UI presenter (`Show` with a readonly struct/view model); it does not format layout. Sentence wording may need localisation hooks later — out of scope here.
- OQ-03 (which stats best teach the player) is open; implement the five listed stats only.

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 002: the transition to Resolved-Death and retention of the `PlayerDeadData` payload
- Story 006: the wave-step trigger source
- Story 007: the retry reset path (this story only adds stats counters to it)
- run-ui epic: death/win screen layout, sting, rising-tension cue

---

## QA Test Cases

*Written at story creation (lean review — QL-STORY-READY skipped). The developer implements against these — do not invent new test cases during implementation.*

- **AC-1 (AC-RS-04)**: Cause-of-death summary from kill context
  - Given: Running; fake `IPlayerHealth`; death-presenter double
  - When: `PlayerDead` raised with `KillerDamageType = CounterHit`, `ComboCounterAtDeath = 2`, `TimeOfDeath = 842`
  - Then: the presenter's `Show` payload carries a cause-of-death summary containing CounterHit, a 2-chain, and 14:02, plus the aggregated stats fields
  - Edge cases: `ComboCounterAtDeath = 0` produces a summary with no chain clause; `TimeOfDeath = 0` formats as 00:00

- **AC-2**: Stats aggregate from signals
  - Given: Running with Combo/Parry/WaveSpawner doubles
  - When: 5 `FinisherResolved`; parry chains of 3, 7, 2; wave step 4 reached; death at `RunTime = 600`
  - Then: finishers = 5; best parry chain = 7; wave/step reached = 4; time survived = 600
  - Edge cases: no signals at all → all counters 0 and time survived = `RunTime` at resolution

- **AC-3**: Win screen receives the same stats
  - Given: aggregated stats as in AC-2 but the player survives to `run_duration_s`
  - When: Resolved-Win
  - Then: win-presenter double receives the same stat values (no cause-of-death)
  - Edge cases: N/A

- **AC-4**: Stats reset on retry
  - Given: Resolved-Death with non-zero stats
  - When: retry (Story 007) then a new run starts
  - Then: all counters are zero at the new run's start
  - Edge cases: signals arriving while in Selection do not increment counters

- **AC-5**: Zero allocation per stat signal
  - Given: subscriptions established
  - When: 1,000 `ParrySucceeded` dispatches in a test loop
  - Then: GC allocation delta is 0 B (Unity Test Framework `Is.Not.AllocatingGCMemory()` constraint or equivalent)
  - Edge cases: N/A

---

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- Integration: `tests/integration/run-session-manager/run_session_manager_run_stats_test.cs` — must exist and pass

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 002, Story 006, Story 007 DONE; **Combo epic** (`FinisherResolved` payload) and **Parry epic** (`ParrySucceeded` chain data) — held until the combo-and-parry prototype reports (Pre-Production gate 2026-10-10); Damage & Health epic — `PlayerDeadData` published
- Unlocks: run-ui epic death/win screen stories

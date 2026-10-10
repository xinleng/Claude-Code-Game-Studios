# ADR-0003: Event Architecture — Struct Delegates and Ordered DamageTaken Dispatch

## Status
Accepted
*(Proposed and Accepted 2026-07-04, autonomous session — user directive to proceed; lean review mode. This is the S6 event-ordering ADR that upgrade-definition.md mandates before implementation.)*

## Date
2026-07-04

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS |
| **Domain** | Core / Scripting |
| **Knowledge Risk** | LOW — C# delegates and structs are engine-version-independent |
| **References Consulted** | `docs/engine-reference/unity/current-best-practices.md` (C# 9), `deprecated-apis.md` |
| **Post-Cutoff APIs Used** | None |
| **Verification Required** | GC allocation profile: zero allocations per DamageTaken/AttackEvent dispatch under 200-enemy load |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | ADR-0002 (dispatch happens inside the orchestrated tick) |
| **Enables** | All Feature-layer implementation; ADR-0009 (UI subscribes via the same pattern) |
| **Blocks** | Damage & Health, Parry, Combo implementation stories |
| **Ordering Note** | The ordered-dispatch rule below is binding on every `DamageTaken` subscriber |

## Context

### Problem Statement
Combat events fire at high frequency (hits at 200-enemy density, AttackEvents every telegraph). damage-health.md forbids `UnityEvent` on these paths (reflection + boxing → GC pressure). Separately, upgrade-definition.md found an ordering hazard: S6 "Iron Stance" evaluates damage reduction from the momentum meter *on the same DamageTaken event* that resets the momentum meter — undefined subscriber order silently changes balance. An ADR must fix both the event mechanism and the dispatch order.

### Constraints
- Zero per-dispatch heap allocation (60fps, 16.6ms, minimal GC)
- Subscriber ordering must be explicit where semantics depend on it
- Testable without engine infrastructure

### Requirements
- `DamageTaken` / `PlayerDead` / `AttackEvent` / `SwingLanded` / `ParrySucceeded` / `FinisherResolved` as typed events with struct payloads
- S6 reads momentum **before** the Parry System applies the momentum reset
- `DamageTaken` fires before `PlayerDead` in the same frame; combo/momentum resets complete in the same frame the event fires

## Decision

**All gameplay events are publisher-owned `System.Action<TPayload>` delegates where `TPayload` is a `readonly struct`. `UnityEvent`, `SendMessage`, and string-keyed buses are forbidden on gameplay paths. Where subscriber order carries semantics, the publisher dispatches subscribers in an explicit, code-declared sequence instead of exposing a bare multicast event.**

1. **Payloads** are `readonly struct`s defined next to their publisher (`DamageTakenData`, `PlayerDeadData`, `AttackEvent`, `SwingData`, `ParrySuccessData`, `FinisherData`, `XpDrop`). Passed by `in` reference where profiling warrants.
2. **Ordered dispatch for `DamageTaken`.** `PlayerHealthSystem` does not expose a bare event for gameplay consumers. It invokes, in order:
   1. `parry.EvaluateS6Reduction(momentum)` — S6's conditional reduction is computed **inside the damage formula step**, reading the momentum meter as it stood when the hit landed (pre-reset);
   2. damage applied, `DamageTakenData` built;
   3. **if** `effective_damage ≥ reset_damage_threshold`: `parry.ApplyMomentumReset()` then `combo.ResetFromDamage()`;
   4. presentation event `DamageTaken` (multicast, unordered) for UI/VFX/analytics.
   This makes the S6 rule ("applies to the hit that drops momentum below 50%") structural rather than subscription-order-dependent.
3. **`PlayerDead`** fires after step 4 in the same frame when HP hits 0, exactly once, with kill context captured at step 2 (pre-reset combo/momentum values).
4. **AttackEvent delivery** is a direct call (`parry.ReceiveAttackEvent(in evt)`) plus a presentation multicast for telegraph rendering — the Parry System's copy is the authoritative one (it keys the `AttackerId → events` dictionary; TR-ED-004).
5. Unordered multicast events remain plain `event Action<T>` — fine for presentation, stats, and any consumer whose order is semantically irrelevant.

### Key Interfaces
```csharp
public readonly struct DamageTakenData { public readonly float Amount, RawAmount; public readonly int AttackerId; public readonly DamageType Type; }
// PlayerHealthSystem internal dispatch order (binding):
// S6 pre-read → apply damage → threshold-gated resets (Parry, Combo) → multicast DamageTaken → PlayerDead?
```

## Alternatives Considered

### Alternative 1: `UnityEvent` / Inspector-wired events
- **Pros**: Designer-visible wiring.
- **Cons**: Reflection dispatch + argument boxing per hit → GC spikes at density; explicitly forbidden by damage-health.md.
- **Rejection Reason**: Hard GDD prohibition; performance.

### Alternative 2: Global event bus (string/type-keyed, e.g. static `EventBus.Publish<T>`)
- **Pros**: Total decoupling; easy to add listeners.
- **Cons**: Hidden control flow; ordering unspecifiable (the exact S6 hazard); harder to trace in review; encourages god-events.
- **Rejection Reason**: The S6 hazard is precisely a bus-ordering bug waiting to happen; publisher-owned events keep causality local and reviewable.

### Alternative 3: Priority-ordered multicast (subscribers register with a priority int)
- **Pros**: Generic ordering solution.
- **Cons**: Order still assembled at runtime — a mis-registered priority silently reorders balance-critical logic; more framework code.
- **Rejection Reason**: Explicit sequential calls in the publisher are simpler and stronger for the one case that needs order.

## Consequences

### Positive
- Zero-allocation dispatch; ordering bugs impossible for the balance-critical path; contracts match damage-health.md verbatim

### Negative
- `PlayerHealthSystem` holds direct references to Parry/Combo interfaces (acceptable: wired by bootstrap DI; the coupling is real and now visible)

### Risks
- **A future subscriber needs ordered access** → extend the explicit sequence in the publisher via code review, never via runtime priority
- **Struct payload copies grow large** → keep payloads ≤ ~48 bytes; pass by `in`
- **Lambda-capture allocation at dispatch sites** *(engine-specialist finding)*: delegate *invocation* is allocation-free, but a closure lambda created per-frame/per-hit is not. Subscriptions happen once at init with cached delegates; no `x => ...` capture inside hot loops

## GDD Requirements Addressed

| GDD System | Requirement | How This ADR Addresses It |
|------------|-------------|--------------------------|
| damage-health.md | `DamageTaken` as delegate + readonly struct; UnityEvent forbidden | Adopted as the project-wide event rule |
| upgrade-definition.md | S6 event-ordering ADR required (Edge Cases) | Ordered dispatch, S6 pre-read step 1 |
| damage-health.md | DamageTaken → resets same frame; DamageTaken before PlayerDead; kill context pre-reset | Steps 2–4 sequence |
| enemy-definition.md | Parry System receives AttackEvents; pool flush via dictionary | Direct-call delivery to the authoritative consumer |

## Performance Implications
- **CPU**: Direct delegate invocation, no reflection — cheapest available dispatch
- **Memory**: Zero per-event allocation (struct payloads on the stack)
- **Load Time / Network**: N/A

## Migration Plan
Greenfield. If modding/analytics later needs late-bound events, add a read-only multicast tier — never insert into the ordered sequence.

## Validation Criteria
- AC-DH-09 event-order log test (`DamageTaken` then `PlayerDead`); AC-UD-24 (S6 at ≥50% momentum applies to the resetting hit); AC-DH-06 same-frame resets
- Profiler: 0 B GC alloc per dispatch in a 200-enemy stress scene

## Related Decisions
- ADR-0002 (dispatch happens inside `PlayerHealth.Apply` step of CombatFrame), ADR-0009 (UI subscribes to the presentation tier)

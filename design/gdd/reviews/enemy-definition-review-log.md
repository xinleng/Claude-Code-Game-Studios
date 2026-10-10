# Review Log: Enemy Definition

## Addendum — 2026-06-18 — post-approval field addition (still Approved)
During the same-day Damage & Health re-review, a `DamageType : enum {Standard|CounterHit|Slam|Projectile}` field was added to the AttackEvent contract to satisfy a bidirectional contract Damage & Health depends on. This is a purely additive change consistent with enemy-definition's own existing forwarding contracts (Combat Feedback UI / Parry dependencies). Assignment is deterministic per emission (CounterHit↔IsCounterHit, Slam↔Warlord P2 slam, Projectile↔Archer, else Standard). Approval stands.

## Review — 2026-06-18 — Verdict: APPROVED
Scope signal: L
Specialists: lean mode — single-session adversarial analysis (no agent spawns)
Blocking items: 0 | Recommended: 2
Summary: Fresh re-review after the 2026-05-28 revision. All 19 prior blockers traced against current text and confirmed resolved (Roar watchdog, runStartTime contract, serialized Phase 2 SO refs, cooldown jitter, int current_hp, DefaultExecutionOrder(-100), velocity=0 locks, stagger-resist feedback contract, Dodge fallback, AC-18/AC-20). No new blocking issues. Two advisory items: non-sequential AC numbering (cosmetic) and bidirectional-dependency back-references pending downstream GDD authoring. OQ-04 (Dodge System) remains open but has a documented provisional fallback. Doc moved to Approved.
Prior verdict resolved: Yes (19 blockers from 2026-05-28 closed; 0 new found)

## Revision — 2026-05-28 — 19 blockers resolved (re-review required)
Scope signal: L
Specialists: N/A — revision pass only (no fresh specialist review)
Blocking items resolved: 19
Summary: Four Tier S decisions made by user: (S1) Roar watchdog clears IsPhaseTransitionLocked and defers to normal Dead check — does not force-kill; (S2) runStartTime recorded by RunSessionManager using Time.time, passed to EnemySystemManager at init; (S3) Phase 2 SO uses direct serialized references on prefab (not Addressables); (S4) spawn-time cooldown jitter added (initialCooldownMs = Random.Range(0, attack_cooldown_ms)). Tier A: stagger-resist feedback contract added to Combat Feedback UI dependency; Pillar 2 cross-GDD contract language strengthened in parry burst rule; Dodge System provisional fallback (temporary Parriable=true stub) specified. Tier B: current_hp declared as int; EnemySystemManager marked [DefaultExecutionOrder(-100)]; Staggered and Roar states now explicitly specify velocity=0 movement lock. Tier C: AC-18 (spawn jitter) and AC-20 (watchdog) added; AC numbering gap closed.
Prior verdict resolved: Partial — fourth review found 19 blockers; all resolved in-session. Full re-review not yet run.

## Review — 2026-05-28 — Verdict: NEEDS REVISION (19 blockers found)
Scope signal: L
Specialists: game-designer, systems-designer, qa-lead, creative-director
Blocking items: 19 | Recommended: 6
Summary: Fourth review after three prior rounds (44 total prior blockers resolved). Foundation is sound. 19 new blockers across four tiers: Tier S (4) — Roar watchdog contradicts HP=1 clamp, runStartTime source unspecified, Phase 2 SO load strategy unspecified, cooldown sync burst from synchronized wave spawns. Tier A (4) — stagger-resist feedback contract missing, Pillar 2 cross-GDD language weak, Dodge System fallback absent. Tier B (8) — current_hp type undeclared, script execution order unspecified, movement lock during Staggered/Roar unspecified, confirmed existing guards on OverlapSphere/SetActive/stagger-resist-zones. Tier C (3) — AC-18 numbering gap, watchdog has no testable AC, AC vagueness. All 19 resolved in same session.
Prior verdict resolved: Yes (12 prior blockers from 2026-05-18 review were closed; 19 new found)

## Review — 2026-05-18 — Verdict: MAJOR REVISION NEEDED (revised in session → re-review required)
Scope signal: XL
Specialists: game-designer, systems-designer, qa-lead, unity-specialist, creative-director
Blocking items: 12 | Recommended: 13 | Advisory: 5
Summary: Third review found 12 new blockers despite 24 prior fixes. Three required design decisions resolved by user: (1) 15-min counter-hit reverted from 300ms to 450ms — smooth 600→550→450ms escalation; 300ms broke Pillar 1 by entering reflex territory. (2) Post-Finisher stagger window now explicitly documented as the designed escape path — auto-attacks continue during elite stagger, player can rebuild combo=2 on infantry. (3) AttackerId changed from string to int throughout (GC rationale, matches damage-health.md). Nine mechanical blockers resolved: state machine table updated with Cooldown→Telegraph shortcut and Archer exemption; Formula 2 and 3 variable table ranges corrected; AC-04 tier reference updated; AC-03 KnockbackImpulseApplied replaced with mock/spy approach; AC-16/17/19 added for Archer fallback and Phase 2 flush; mid-stagger Phase 2 threshold crossing edge case documented; IsPhaseTransitionLocked formally defined. Registry updated to 450ms.
Prior verdict resolved: Yes (24 prior blockers were closed; 12 new found and resolved in-session)

## Review — 2026-05-17 — Verdict: MAJOR REVISION NEEDED (revised in session → re-review required)
Scope signal: XL
Specialists: game-designer, systems-designer, ai-programmer, qa-lead, unity-specialist, creative-director
Blocking items: 24 | Recommended: 11 | Advisory: 8
Summary: Re-review found 24 new blocking issues despite 8-blocker fix from prior session, indicating prior revision was patch-fixes without a structural sweep. Six critical clusters addressed: (1) parry/stagger/elite interaction loop — parry burst formula missing, Staggered trigger unspecified, AC coverage absent; (2) state machine completeness — missing Roar state, missing Staggered→Dead, Archer fallback trigger contradiction; (3) numerical contradictions — stagger_resist_multiplier clamp inconsistency, phase_2_threshold_ratio range contradiction; (4) 15-min counter-hit window collision with standard attack (Pillar 1 violation); (5) Unity implementation contract gaps — OverlapSphere allocation, deferred registration, AttackStartTime precision, SetActive restriction; (6) untestable ACs. All 24 resolved in-session. Key design decisions: 15-min counter-hit window lowered to 400ms; parry burst at combo=2 triggers Staggered (both lanes identical path); Archer fires 1 projectile by default (tuning knob); stagger_resist_multiplier clamp authoritative at [0.1, 0.8].
Prior verdict resolved: Yes (8 prior blockers were closed; 24 new blockers found and resolved)

## Review — 2026-05-13 — Verdict: NEEDS REVISION (revised in session → re-review recommended)

Scope signal: L
Specialists: game-designer, systems-designer, ai-programmer, qa-lead, performance-analyst, unity-specialist, creative-director
Blocking items: 8 | Recommended: 7 | Advisory: 8
Summary: Strong design intent with clear player fantasy and well-structured telegraph system. Critical gaps were at system boundary contracts: archer AttackEvent timing ambiguity, no EnemySystemManager frame-order contract, no AttackEvent pool ownership, missing spatial partitioning and VFX budget constraints, tuning knob boundary guards absent, and a Pillar 2 violation in the stagger-resist formula (parry lane bypassed the elite gate). All 8 blocking items revised in the same session. Iron Captain escalation (OQ-01) also resolved. Re-review in a fresh session recommended before marking Approved.
Prior verdict resolved: First review

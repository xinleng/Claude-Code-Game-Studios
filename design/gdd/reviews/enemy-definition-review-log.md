# Review Log: Enemy Definition

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

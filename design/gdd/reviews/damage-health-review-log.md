# Review Log: Damage & Health

## Review — 2026-05-18 — Verdict: NEEDS REVISION (revised in session)
Scope signal: M
Specialists: game-designer, systems-designer, qa-lead, creative-director
Blocking items: 10 | Recommended: 7 | Advisory: 4
Summary: Second review. Two design decisions resolved: (1) reset_damage_threshold added — combo/momentum resets now gate on effective_damage ≥ 3 (prevents 1-damage trivial grazes from erasing full chains, resolves Pillar 2 Player Fantasy conflict). (2) regen_rate_max = 1.5 HP/s hard cap set with steady-state immortality formula documented. Mechanical fixes: PlayerDead event expanded with kill-context fields (KillerAttackerId, KillerDamageType, ComboCounterAtDeath, MomentumAtDeath) to fulfill the "what cost you the run?" Player Fantasy promise. DamageType assignment source documented; Slam bypass path specified. AC-DH-05 string→int fixed; AC-DH-07 variable name fixed; AC-DH-12 and AC-DH-13 added. Two new implementation contracts added (reset API guard, AttackerId counter reset). iframe/regen time asymmetry documented as intentional. OQ-01 updated to 450-600ms.
Prior verdict resolved: Yes (16 prior blockers closed; 10 new found and resolved in-session)

## Review — 2026-05-17 — Verdict: MAJOR REVISION NEEDED (revised in session)
Scope signal: M
Specialists: game-designer, systems-designer, qa-lead, unity-specialist, creative-director
Blocking items: 16 | Recommended: 9 | Advisory: 5
Summary: First review of a structurally sound but underspecified document. Key design decision: dual simultaneous reset of combo_counter and momentum_meter retained as default baseline, now documented as per-general-tunable via combo_reset_on_damage and momentum_reset_multiplier knobs. Core specification gaps resolved: regen-while-Dead contradiction fixed (Rule 6 now has explicit Dead-state gate), retry reset now explicitly resets max_hp, raw_damage = 0 intake guard added, floor() rounding contract specified (binary fractions only), event payload implementation mandated as C# delegate with value-type struct, AttackerId changed from string to int, iframe timer specified as timestamp with Time.unscaledTime. All 5 AC blocking issues rewritten with concrete, testable pass conditions. Cross-GDD fix: Iron Captain 15-min counter-hit tightened to 300ms (was 400ms, collided with Levy Soldier standard 400ms) — escalation curve now 600 → 550 → 300ms.
Prior verdict resolved: First review

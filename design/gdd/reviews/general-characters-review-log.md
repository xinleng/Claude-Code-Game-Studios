# Review Log: General Characters

## Review — 2026-05-15 — Verdict: APPROVED (user accepted after round-3 revision)

Scope signal: M
Specialists: game-designer, systems-designer, qa-lead, ux-designer, creative-director
Blocking items: 5 areas | Recommended: 6
Summary: Core structure sound; 7 of prior 13 blockers cleanly resolved. Five remaining blockers: (1) Lu Bu Player Fantasy undeliverable at MVP — resolved by promoting knockback to MVP scope (radial displacement on PointBurst, `KnockbackForce=5.0`, `KnockbackRadius=3.0u`); (2) same-frame parry+Finisher resolution order undefined — fixed with explicit FixedUpdate ordering rule; (3) selection screen had no cancel/back path — Rule 8 added; (4) AC-03 Vector2/Vector3 type mismatch — fixed; (5) AC refinement pass — 7 ACs rewritten with concrete assertion targets and testable preconditions. PointBurst `FinisherDirectionMode` renamed from `PlayerPosition` to `Omnidirectional`. Zhao Yun card now includes one-sentence mechanical disclosure. AC-08 promoted to BLOCKING.
Prior verdict resolved: Yes (round-2 6 blockers); round-3 5 new blockers resolved in same session.

## Review — 2026-05-13 — Verdict: MAJOR REVISION NEEDED

Scope signal: M
Specialists: game-designer, systems-designer, qa-lead, ux-designer, gameplay-programmer, creative-director
Blocking items: 7 | Recommended: 6
Summary: Strong player fantasy and clean data-contract design, but seven blocking gaps prevent implementation. Two are architectural: the Combo System upgrade-delta structure is undefined (no formula, no struct), and Zhao Yun's directional-aim requires a named DirectionalInputBuffer subsystem that is unspecified. Additional blockers: "nearest enemy" fallback lacks metric/search radius/tie-break; finisher_radius can fall below auto-attack radius at min tuning; FinisherAngle=0.0 risks silent zero-width cone; selection screen shows identical geometry preview for Lu Bu and Guan Yu (misleads at the only decision point); AC-03/AC-04/AC-07 are untestable. Lu Bu's mechanical parity with Guan Yu is accepted MVP scope, not a blocker. All items are fixable in one focused revision pass.
Prior verdict resolved: First review

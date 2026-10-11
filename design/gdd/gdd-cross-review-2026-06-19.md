# Cross-GDD Review Report

**Date:** 2026-06-19
**GDDs Reviewed:** 16 system GDDs (17 systems) + game-concept
**Mode:** full (consistency + design holism + scenario walkthrough), inline
**Context:** Run immediately after 12 GDDs were self-authored & self-approved in one pass. Reviewer authored 12 of the 17 — not fully independent; compensated with adversarial self-review.

---

## Verdict: CONCERNS

One blocking contract gap (missing invulnerability API), reconciled in-session. The 17 GDDs are pillar-aligned, non-contradictory in design, and the dependency web mostly closes. Remaining items are balance concerns (validate in prototype) and stale-reference cleanups from dependency-order authoring.

---

## Consistency Issues

### 🔴 Blocking — Missing invulnerability API
`parry-system.md` (Rule 5) and `dodge-system.md` (Rule 3) both request Damage & Health to "open an invulnerability window, reusing the iframe mechanism." But `damage-health.md` only opened iframes on a `DamageTaken` event — no API to start an invuln window on request (parry/dodge produce no DamageTaken).
**Resolution (applied):** Added `OpenInvulnerability(duration_ms)` to damage-health.md Rule 5 — reuses the iframe mechanism, reconciles overlapping windows by taking the latest end-time. Parry (`parry_invuln_ms = 300`) and Dodge (`dodge_invuln_ms = 400`) call it.

### ⚠️ Warnings (applied)
- **input-manager.md** marked the Dodge action "post-MVP/inert" though dodge-system.md is MVP-active → updated to MVP-active.
- **combat-core.md** didn't acknowledge the Dodge locomotion/auto-attack override → added.
- **finisher_max_targets** ownership overlap (combo-system vs upgrade-definition) → Combo owns; Upgrade Definition references.
- **upgrade-definition.md "Pending Baseline Definitions"** table called six values "not yet authored" though all are now owned (Parry/Combat Core/Combo) → updated to point to real owners.
- **game-concept.md** `combo_counter_timeout` "set in Combat System GDD" → updated to Combo System.

### ℹ️ Info
- parry-system "momentum-reset event" vs damage-health "subscribes to DamageTaken" — same behaviour, wording aligned during edits.

---

## Game Design Issues

Design-theory passes: single progression loop; ~3–4 active systems (Dodge rare); pillar alignment clean across all 12 new GDDs; player fantasies cohere; anti-pillars honored.

### ⚠️ Balance CONCERNS (validate in prototype / /balance-check — NOT applied; design decisions)
1. **Parry burst ceiling (80) vs Finisher (25–40)** — `base_burst = 40` → full-momentum burst 80 AoE may make the parry lane dominant (Pillar 2 risk). Invented value; tune in prototype.
2. **Base Finisher under-kills cheapest enemy** — `finisher_damage = 25` < `levy_hp = 30`. One-shots once A1/C1 picked; base feel is off. Tune `finisher_damage` or `levy_hp`.
3. **C5 sustain** reinforces aggregate-heal watch-item (upgrade-definition OQ-11) — up to 40 HP/Finisher at ~3s cadence; consider a combined heal cap if too tanky.
4. **No automatic player-power scaling vs rising density** — density/speed escalate on a fixed curve; player power only via ~12 picks. Validate the density-vs-DPS curve.

---

## Cross-System Scenario Issues
Scenarios walked: parry-into-Finisher (combo=2); level-up mid-combat pause; Warlord slam→dodge; Warlord one-shot→roar; DamageTaken dual-reset; A6 auto-kill reset.
- The invuln API gap (now fixed) surfaced in the parry-into-Finisher and slam→dodge scenarios.
- Level-up pause: parry/dodge invuln drains during pause (unscaled iframes) — documented asymmetry, acceptable.
- All other scenarios coherent.

---

## GDDs Flagged / Revised

| GDD | Reason | Type | Resolution |
|---|---|---|---|
| damage-health.md | Added OpenInvulnerability API | Consistency (blocking) | Applied |
| input-manager.md | Dodge MVP status | Consistency | Applied |
| combat-core.md | Dodge override note | Consistency | Applied |
| upgrade-definition.md | Pending-baseline table; finisher_max_targets ownership | Consistency | Applied |
| game-concept.md | combo_counter_timeout pointer | Consistency | Applied |

Balance concerns (#1–4) are deferred to prototype/balance-check by design — not GDD edits.

---

## Recommended Next
- `/prototype combo-and-parry-system` — validate the invented combat-feel numbers (esp. balance concerns #1–4).
- `/create-architecture` — the design layer is now consistent; the invuln API and ~7 ADR triggers are ready to formalize.

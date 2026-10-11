# Cross-GDD Review Report

**Date:** 2026-06-18
**GDDs Reviewed:** 5 — enemy-definition, general-characters, damage-health, upgrade-definition, game-concept
**Mode:** full (consistency + design theory + scenario walkthrough), inline single-session analysis
**Context:** Run immediately after the Foundation re-review batch moved enemy-definition, damage-health, and upgrade-definition to Approved (general-characters already Approved).

---

## Verdict: CONCERNS

The four implementable system GDDs are internally coherent and consistent with each other. The single material issue is documentation debt in the **parent concept doc** (`game-concept.md`), which now contradicts its own approved children. Not FAIL — no two implementable GDDs conflict in a build-breaking way — but `game-concept.md` must be reconciled before `/create-architecture` or `/sprint-plan`, since both consume it as the vision source.

---

## Consistency Issues

### 🔴 Blocking — `game-concept.md` stale vs approved children (OQ-10, confirmed + quantified)

`game-concept.md` declares itself the parent authority and requires children not to contradict it, but design evolved in the children and the parent was never synced:

| game-concept.md | Approved GDD | Locations |
|---|---|---|
| "15-18 upgrades across 4 categories" | upgrade-definition: 24 upgrades, 3 lanes + Modifier type | concept L69, L212, L223, L251, L364 |
| Survivability is a 4th upgrade lane | Survivability demoted to "Modifier" type, 3-pick cap | concept L69, L251 |
| Pity rule + live `pity_threshold` knob | Pity removed → drought weighting (2× after 4 dry draws/lane); `pity_threshold` deprecated in registry | concept L366, L437 |
| Scope Tiers: 24 upgrades = Vertical Slice; MVP = 15-18 | 24 upgrades are the MVP catalogue | concept L268-269 |
| AC-03 / AC-05: damage always resets combo & momentum | damage-health: resets only when `effective_damage ≥ reset_damage_threshold` (3); P4 halves momentum loss | concept L451, L453 vs damage-health Rule 4 |

The AC-03/AC-05 ↔ threshold-rule contradiction is an addition beyond the original OQ-10 catalogue.

### ℹ️ Info (minor, non-blocking)
- game-concept Combo Finisher formula points `base_attack`/`base_radius` to a nonexistent "Combat System GDD"; both are owned by General Characters (per registry).
- enemy-definition Dependencies table has no General Characters row despite the Lu Bu knockback cross-reference — acceptable because it routes through Combat Core (the integrator).
- damage-health has no explicit Enemy Definition dependency row — acceptable because AttackDamage/DamageType flow in via the Parry System.
- game-concept header still reads `Status: Draft` though it was Approved 2026-05-10.

---

## Game Design Issues

All design-theory checks PASS:
- **Single progression loop** (XP → level-up → pick). No competing loops.
- **Attention budget ~3 concurrent active systems** (positioning, combo, parry; auto-attack passive). Within the 3-4 comfort limit.
- **Anti-pillar "NOT a build-crafter" honored** — Iron Cyclone/Named Synergies removed; C3+P3 documented as emergent mastery; one-line-description rule structurally blocks synergy spreadsheets.
- **Pillar alignment clean**; no drift; player fantasies cohere around "legendary general vs army, skill expression."

### ⚠️ Warnings — all already tracked, none new
- S2+S3 Modifier reset-immunity vs Levy Soldiers (upgrade-definition OQ-02/OQ-08) — mitigated by the 3-Modifier cap.
- Aggregate C5+P6+S5 healing has no combined ceiling (upgrade-definition OQ-11) — playtest watch-item.
- S6 damage-reduction event-ordering on DamageTaken needs an ADR before implementation (upgrade-definition Edge Cases).

---

## Cross-System Scenario Issues

Scenarios walked: parry-into-Finisher on elite at combo=2; level-up mid-Finisher; Warlord one-shot Phase 2; DamageTaken dual-reset; S6 on DamageTaken.

**No new blockers.** All well-specified across the relevant GDDs. The DamageTaken dual-reset scenario is where game-concept AC-03/AC-05 fall out of sync (captured above). The S6-ordering ADR is the one implementation-time item.

---

## GDDs Flagged for Revision

| GDD | Reason | Type | Priority |
|---|---|---|---|
| game-concept.md | Stale vs upgrade-definition (count/categories/pity/scope) + damage-health (AC-03/05 threshold); base-stat ownership pointer to nonexistent doc; Draft status | Consistency | Blocking (pre-architecture) |

---

## Required Actions Before Architecture

Reconcile `game-concept.md` with the approved GDDs:
1. Upgrade count 15-18 → 24; categories "4 (incl. Survivability)" → "3 lanes (Combo/Parry/Auto-attack) + Modifier type."
2. Pity rule → drought weighting; remove the deprecated `pity_threshold` knob.
3. Scope Tiers: 24 upgrades = MVP (not Vertical Slice).
4. AC-03 / AC-05: soften "always resets" to the `reset_damage_threshold` rule; note P4 halves momentum loss.
5. base_attack / base_radius ownership pointer → General Characters GDD.
6. Status: Draft → Approved (2026-05-10).

# Session State — Dynasty Survivors

*Last updated: 2026-05-18*

<!-- STATUS -->
Epic: Pre-Production
Feature: GDD Authoring
Task: upgrade-definition.md DESIGNED (all 8 sections). Run /design-review upgrade-definition.md in fresh session to validate.
<!-- /STATUS -->

---

## Current Status

Session 2026-05-18: Both re-reviews completed. Key decisions:
- enemy-definition: 15-min counter-hit → 450ms (smooth 600→550→450ms); stagger window interactive (player rebuilds combo); AttackerId → int; Cooldown→Telegraph shortcut documented + Archer exempted; Warlord mid-stagger Phase 2 edge case documented. Status: In Review.
- damage-health: reset_damage_threshold = 3 added (resets gate on effective_damage ≥ 3); regen_rate_max = 1.5 HP/s cap; PlayerDead gets kill-context fields; DamageType source contract documented. Status: In Review.
- registry updated: reset_damage_threshold + regen_rate_max added; iron_captain_15min_counter_hit = 450ms.

Session 2026-05-17: Three major sessions of work completed:
1. enemy-definition.md re-review — MAJOR REVISION NEEDED — 24 blockers resolved in-session. Status: In Revision.
2. damage-health.md first review — MAJOR REVISION NEEDED — 16 blockers resolved in-session. Status: In Revision.
3. /consistency-check — PASS — 2 conflicts resolved, registry updated. Iron Captain 15-min counter-hit now 300ms.

Key design decisions made this session:
- Dual reset (combo + momentum on DamageTaken): kept as default baseline; made per-general-tunable via combo_reset_on_damage + momentum_reset_multiplier knobs
- Iron Captain 15-min counter-hit: 400ms → 300ms (distinct from Levy Soldier 400ms standard)
- Parry burst at combo=2 on elite: triggers Staggered (both lanes face identical elite interaction path)
- Archer fires archer_projectile_count projectiles (default 1, tuning knob)
- stagger_resist_multiplier clamp: [0.1, 0.8] authoritative (Tuning Knobs table)

**Enemy Definition** — In Revision (24 blockers resolved 2026-05-17). Re-review required before Approved.

**General Characters** — Round-2 revision complete 2026-05-15. All 6 blockers resolved:
1. Zhao Yun direction: kept last-input with fallback (user decision). Added Pillar 1 rationale note to doc.
2. Lu Bu parity label: removed (user decision). Player Fantasy rewritten — no more "executioner/right spot" language. Dashed border removed from visual spec. Overview updated.
3. AC-04: concrete scene fixture added (player 0,0,0; enemy 4,0,0 → expected (1,0,0)) + tie-break sub-case.
4. AC-09: replaced Combat Core GDD reference with concrete Vector3(0,0,1) value.
5. PointBurst → Ring geometry mapping: explicit note added to data contract.
6. Combat Core dependency: updated entry to note both hard (BaseAttack) and soft (screen-forward convention) dependency.
Status: In Review — ready for /design-review re-run.

---

## Completed Work

- [x] Game concept document — `design/gdd/game-concept.md` *(Approved 2026-05-10)*
- [x] Systems index — `design/gdd/systems-index.md`
- [x] `/design-review design/gdd/game-concept.md` — APPROVED
- [x] Engine configured — Unity 6.3 LTS (`docs/engine-reference/unity/VERSION.md`)
- [x] Technical preferences set — `.claude/docs/technical-preferences.md`
- [x] Breaking changes documented — `docs/engine-reference/unity/breaking-changes.md`

---

## Decision Point — Choose Next Step

The following steps were identified in the concept doc checklist. **Pick one to proceed:**

| Option | Skill | Purpose | Notes |
|--------|-------|---------|-------|
| A | `/design-review design/gdd/game-concept.md` | Validate concept completeness before investing in deeper docs | Quick — recommended first |
| B | `/art-bible` | Visual identity spec | Must be done before system GDDs per CLAUDE.md rules |
| C | `/map-systems` | Decompose concept into individual systems with dependencies | Produces the list of GDDs to write |

**Recommended order:** A → B → C → `/design-system` (×N) → `/create-architecture`

---

## Pending / Not Started

- [x] `/design-review design/gdd/game-concept.md` — APPROVED
- [ ] `/art-bible`
- [x] `/map-systems` — 17 systems, design order set (Dodge System added)
- [x] `/design-system enemy-definition` — DESIGNED (pending review)
- [x] `/design-system general-characters` — DESIGNED (pending review)
- [x] `/design-review design/gdd/enemy-definition.md` — revised in session (2026-05-13); re-review recommended in next fresh session
- [x] `/design-review design/gdd/general-characters.md` — APPROVED 2026-05-15 (3 review rounds; knockback added to Lu Bu MVP scope)
- [x] `/design-review design/gdd/enemy-definition.md` — MAJOR REVISION (24 blockers); revised 2026-05-17; **re-review required**
- [x] `/design-review design/gdd/damage-health.md` — MAJOR REVISION (16 blockers); revised 2026-05-17; **re-review required**
- [x] `/consistency-check` — PASS 2026-05-17; 2 conflicts resolved; registry updated
- [ ] `/design-system upgrade-definition` — next in design order (#4)
- [ ] `/design-system dodge-system` — required before Warlord implementation
- [ ] `/design-system` per remaining system (13 left)
- [ ] `/create-architecture`
- [ ] `/architecture-decision` (×N)
- [ ] `/gate-check`
- [ ] `/prototype combo-and-parry-system`
- [ ] `/playtest-report`
- [ ] `/sprint-plan new`

---

## Open Questions (from game concept)

1. Does the combo counter reset feel fair when the player takes damage mid-combo?
2. Does adding combo on top of parry feel like depth or overwhelm?
3. What's the right momentum meter reset penalty — full reset or half?
4. How many enemies on screen before readability breaks?

→ All answered via the 2-hour combat prototype, before building other systems.

---

## Key Design Decisions Already Made

- **Genre**: Roguelite Survivors / Action
- **Theme**: Three Kingdoms (Guan Yu / Zhao Yun / Lu Bu)
- **MVP scope**: 1 map, 5 enemy types, 3 generals, 15-18 upgrades, 20-min timer
- **Active systems**: Combo finisher (2-hit → Finisher on 3rd) + Parry (timing window → burst + momentum)
- **Rendering**: URP stylized 3D, top-down/isometric
- **Platform**: PC (Steam / itch.io)

---

## Unstaged Git Changes (as of session start)

- `.claude/agents/unity-specialist.md` (modified)
- `.claude/docs/technical-preferences.md` (modified)
- `CLAUDE.md` (modified)
- `docs/CLAUDE.md` (modified)
- `docs/engine-reference/unity/VERSION.md` (modified)
- `docs/engine-reference/unity/breaking-changes.md` (modified)
- `design/gdd/` (untracked — new)
- `production/review-mode.txt` (untracked — value: `lean`)

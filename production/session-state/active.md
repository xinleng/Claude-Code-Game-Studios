# Session State — Dynasty Survivors

*Last updated: 2026-10-10*

<!-- STATUS -->
Epic: Pre-Production — M1 "Core Loop Playable" · Sprint 1 (2026-10-12 → 2026-10-23)
Feature: Sprint 1 planned (production/sprints/sprint-1.md) + QA plan (production/qa/qa-plan-sprint-1.md)
Task: RESUME → user: Unity project location + UNITY_LICENSE secret + OK to move example test → /story-readiness BCF-001 → /dev-story BCF-001; then PROTO-1 /prototype combo-and-parry-system
<!-- /STATUS -->

## Decisions recorded 2026-10-10 (user, after the overnight batch)
- **Commit**: yes (local commit, no push)
- **Min-spec**: GTX 1650 / Ryzen 5 3600 / 16GB @1080p — pinned in technical-preferences.md; art bible header updated
- **Timeline**: M1 "Core Loop Playable" cut line — production/milestones/m1-core-loop-playable.md (Guan Yu only, 3 infantry types, ~12 upgrades, 600s run, combat HUD only, greybox; elites/Warlord/Dodge/generals 2–3/full catalogue → M2)
- **GDD flag #4**: delta-multiplier model — C1 = damage_delta +0.6 (→40 at base 25), C2 = radius_delta +0.5 (→4.5u); combo-system finisher_damage_delta_cap 0.5→0.6; upgrade-definition + combo-system updated; game-data story 008 unblocked (38/39 stories Ready)
- **Engine version**: stay on **Unity 6.3 LTS** (user considered 6.6 = non-LTS Update release; declined). Plan: one LTS→LTS upgrade to 6.7 LTS when it ships, ideally between M1 and M2. Install the latest 6.3 patch via Hub before BCF-001 (6000.3.6f1 currently installed)
- **CI**: user added Pro-licence secrets (UNITY_SERIAL/EMAIL/PASSWORD); workflow updated. Untested — needs the Unity project (BCF-001) + a push/PR to main; gh CLI is signed in as XinLeng-Como (403 on xinleng repo)
- Still open: Unity project location + UNITY_LICENSE secret + moving the example test out of the build; art bible RATIFY list (after prototype); play the prototype

## ⏸ Session Wrap 2026-10-10 (autonomous overnight batch) — Resume Instructions

**Done this session (all uncommitted):** /architecture-review (3 blocking conflicts found + fixed via ADR amendments) → /art-bible (provisional) → /ux-design combat-hud (provisional) → ADR-0012 → /gate-check pre-production **CONCERNS → advanced** (`production/stage.txt` = Pre-Production) → TD must-fix doc sync (control manifest v2026-10-10, architecture.md v1.1, technical-preferences, engine-reference corrections) → /create-epics layer: foundation (5 epics) → /create-stories × 5.

**Stopped because the remaining steps need the user:**
1. **Pin min-spec** (CPU + GPU + RAM) — every prototype benchmark needs a pass/fail reference
2. **Timeline re-baseline** — producer estimates ~55–60 dev-days (11–12 wks) for full MVP vs 3–6 wk target; pick: M1 "Core Loop Playable" cut (recommended) / extend / cut systems now
3. **GDD flag #4** — upgrade C1/C2 static override vs general-characters delta model (blocks the UpgradeCatalogue story + Combo stories)
4. **Art bible RATIFY list** (7 taste calls) — needed before /asset-spec, not before the greybox prototype
5. **Commit** the ~5 months of uncommitted work (producer's #1 recommendation)
6. **UNITY_LICENSE** GitHub secret + create the Unity project (story bootstrap-combat-frame 001)
7. Play the prototype (CD sign-off criteria in the gate report)

**Gate report:** production/gate-checks/gate-check-2026-10-10-pre-production.md

## Session Extract — /ux-design combat-hud 2026-10-10 (autonomous overnight batch)
- **design/ux/combat-hud.md authored — PROVISIONAL** (single-pass autonomous; validate with /ux-review). Scope: combat-feedback layer (combo pips, momentum meter, hit feedback, damage numbers, parry-ready tick). Non-interactive pure-subscriber layer; fires no events; no UI-owned state.
- Layout: bottom-centre combo+momentum cluster (RATIFY — OQ-01 density validation); directional edge flash (never a ring/vignette); damage numbers Finisher-focused.
- 2 new patterns flagged for interaction-patterns.md: Parry-Ready Tick, Directional Edge Flash.
- Payload need flagged to architecture: projectile direction on the DamageTaken presentation payload (fold into TR-CC-007).

## Session Extract — /art-bible 2026-10-10 (autonomous overnight batch)
- **design/art/art-bible.md authored — PROVISIONAL, all 9 sections + binding UX constraints appendix.** Built from the approved Visual Identity Anchor; drafted by art-director agent, hard constraints by technical-artist, 43 UX rules by ux-designer. AD-ART-BIBLE gate skipped (lean mode).
- **RATIFY list (user must confirm before /asset-spec)**: Mandate Violet #B14DFF Finisher accent + Parry Azure #3DD8FF; Lu Bu gold-dominant; player inverted-hull outline; square arena; combo+momentum bottom-centre; fonts (Cinzel/Barlow Semi Condensed/Noto Serif TC subset); soft instanced blob shadows.
- Key binding decisions: hand-painted no-PBR toon ("DynastyToon" single Shader Graph); infantry 500-tri cap / 1 shared material / ≤20-bone Generic rigs / root motion forbidden / LOD0-only; VAT-ready authoring mandatory with measured escalation gates (3.0ms/250 enemies/1100 draws); telegraphs own all ground rings/arcs — world is orthogonal, arena square not circular.
- Conflicts resolved (documented §8.7): tech caps override art-ideal polys; PascalCase naming per tech-prefs; no Addressables (ADR-0006); distance LODs cut.
- Follow-ups: accessibility doc "spawn ring"→"spawn mark" wording; no-circular-Finisher-outline notes for general-characters/vfx-audio; manager-stepped Animators needs unity-specialist+ADR-0002 sign-off.

## Session Extract — /architecture-review 2026-10-10 (autonomous overnight batch)
- Verdict: **CONCERNS** (was FAIL on discovery: 3 BLOCKING conflicts → all resolved in-session via ADR amendments)
- Requirements: 72 total (58 baseline + 14 new, incl. previously-empty TR-UD family) — 66 covered, 4 partial, 2 gaps (both → staged ADR-0012)
- New TR-IDs registered: 14 (TR-CC-006/007, TR-DH wording, TR-UD-001..003, TR-AI-004, TR-PR-005, TR-VA-002, TR-UI-003, TR-WS-004, TR-RS-005/006, TR-TP-002); tr-registry.yaml → v2
- **Blocking conflicts fixed**: C1 clock-domain (scaled vs unscaled run-local timestamps would break ALL parry matching after first level-up pause → unified IGameClock.RunTime; ADR-0004/0010 + enemy-definition.md + input-manager.md amended); N1 ADR-0007 collider model (no-Rigidbody moving colliders = stale queries → grid is now primary for ALL queries, no enemy colliders); N2 input action-asset instance model (UI module clone would ignore SetContext → single PlayerControls instance + UiInputModuleBinder + asmdef gate; ADR-0004/0009 amended). Also C2: Finisher-replaces-3rd-swing handshake added to ADR-0002 (TR-CC-006)
- ADR amendments: 0001, 0002, 0004, 0005, 0007, 0008 (Decision rewritten — single-system VFX instancing), 0009, 0010 — all with dated in-file notes
- GDD revision flags: 2 applied (enemy-definition, input-manager clock wording); 7 pending minor (see report §GDD Revision Flags; flag #4 C1/C2 static-vs-delta model must be resolved before Combo stories)
- Reports: docs/architecture/architecture-review-2026-10-10.md + docs/architecture/traceability-index.md

## ⏸ Session Wrap 2026-07-04 — Resume Instructions

**Where we stopped:** Technical Setup is functionally complete (architecture v1.0, ADR-0001..0010 Accepted + specialist-validated, control manifest, registries, tests/CI scaffold, accessibility + interaction-pattern docs). Gate-check → Pre-Production returned **FAIL with exactly 3 blockers**, all queued for next time.

**Resume order (next session):**
1. `/architecture-review` — MUST be the first thing in a session that did NOT author the ADRs (independence rule). Produces the traceability index + populates `tr-registry.yaml`.
2. `/art-bible` — interactive; needs the user's visual taste (sections 1–4 minimum clear the gate).
3. `/ux-design combat-hud` (or upgrade-pick screen) — first screen spec clears the last gate blocker.
4. `/gate-check pre-production` — should now PASS → write `production/stage.txt`.
5. `/create-epics layer: foundation` → `/create-stories [epic]` → `/sprint-plan new`.
6. `/prototype combo-and-parry-system` — user at the keyboard; answers the 4 balance concerns + 4 feel questions.

**Pending user ratifications (flag if not yet discussed):**
- ADRs were Accepted-immediately under the "proceed" directive — flip any to Proposed to contest (esp. ADR-0001 MonoBehaviour-over-DOTS, ADR-0006 no-Addressables).
- Accessibility tier chosen autonomously: Basic + 2 elevated commitments (`design/accessibility-requirements.md`).

**Housekeeping:**
- ALL of this work is **uncommitted** on branch `xin.dynasty-surviours` (user instruction required to commit).
- One-time CI setup pending: add `UNITY_LICENSE` GitHub secret before the first real CI run.
- Dashboard: `python tools/serve-dashboard.py` → http://localhost:8321/production/dashboard.html (fully synced 2026-07-04).

---

## Session Extract — ADRs + manifest + tests + gate 2026-07-04 (autonomous, continued)

- **ADR-0001..0010 authored and Accepted** (`docs/architecture/adr-*.md`): entity architecture (MonoBehaviour+manager, DOTS deferred w/ ~4ms trigger), frame order/CombatFrame, events+S6 ordered dispatch, input authority/buffer, pooling/lifecycle, SO config (no Addressables), spatial queries/steering (custom grid, no NavMesh), VFX/RenderGraph, UI Toolkit, deterministic test harness (IGameClock + RunFrame pump + injected RNG). Accepted-immediately per user "proceed" directive — flip any back to Proposed to contest.
- **unity-specialist batch validation**: no BLOCKING findings; 5 ADRs got engine-gotcha risk additions (domain-reload statics, Active Input Handling "Both" loophole, OnTriggerExit-on-SetActive(false), queriesHitTriggers, VFX Graph instancing = single-system authoring, UI Toolkit input routing, same-value layout writes).
- **`docs/registry/architecture.yaml` populated**: 7 state owners, 6 interface contracts, 7 API decisions, 8 forbidden patterns.
- **`docs/architecture/control-manifest.md`** written (Manifest Version 2026-07-04) — stories embed this version.
- **Test scaffold**: `tests/` (unit/integration/EditMode/PlayMode/smoke/evidence/helpers), example test (damage-health effective damage, IGameClock pattern), `.github/workflows/tests.yml` (game-ci v4; auto-skips until a Unity project exists; needs UNITY_LICENSE secret before first real run).
- **Gate docs created**: `design/accessibility-requirements.md` (tier: Basic + 2 elevated commitments — shape-first colorblind safety, deaf/HoH visual-primary; RATIFY THE TIER), `design/ux/interaction-patterns.md` (16 seed patterns).
- **Gate-check Technical Setup → Pre-Production: FAIL** (`production/gate-checks/gate-check-2026-07-04-pre-production.md`). 3 blockers: /architecture-review (fresh session — never same session as ADR authoring), /art-bible (user taste), first /ux-design spec. stage.txt not written.
- **Deliberately deferred**: /create-epics + /create-stories + /sprint-plan — stories need TR-IDs from tr-registry.yaml, which /architecture-review populates. Sequence: fresh session → /architecture-review → epics → stories → sprint plan.
- **Dashboard**: `production/dashboard.html` updated; serve via `python tools/serve-dashboard.py` → http://localhost:8321/production/dashboard.html.

---

## Session Extract — /create-architecture 2026-07-04 (autonomous)

- **Output:** `docs/architecture/architecture.md` v1.0 draft (all 8 sections complete in one pass; user asked to "move the development process forward" — collaborative per-section approval was not possible, so decisions are documented with rationale for audit).
- **TR Baseline:** 58 technical requirements extracted from all 17 GDDs + technical preferences (TR-GC0/ED/DH/GC/IM/CC/CB/PR/DG/AI/PG/WS/RS/UI/VA/TP families). Coverage: 0/58 by existing ADRs (none exist) → 12 Required ADRs generated.
- **Key structural decisions (recommendations — user can override before ADRs lock):**
  - 5-layer map; Enemy/General/Upgrade Definitions become ScriptableObject data assets (`GameData`), not runtime modules. Two new infrastructure modules identified: `CombatFrame` (tick orchestrator) and `ObjectPool`.
  - Canonical frame order: `[-200] InputManager → [-100] EnemySystemManager → [0] CombatFrame (Parry→Dodge→CombatCore→Combo→Health→Progression) → [+100] Presentation`.
  - Recommended MonoBehaviour + single-manager over DOTS (ADR-0001; DOTS only if 200-enemy profiling fails 16.6ms). No Addressables in MVP (direct SO refs). No save/load domain in MVP.
  - Events = publisher-owned C# delegates with readonly-struct payloads; ordered DamageTaken dispatch (S6 → momentum reset → combo reset → UI) — this is upgrade-definition's mandated S6-ordering ADR (ADR-0003).
- **Required ADRs (author before coding):** 0001 entity architecture, 0002 frame order/CombatFrame, 0003 events+S6 ordering, 0004 input authority/buffer, 0005 pooling/lifecycle, 0006 SO config (no Addressables), 0007 spatial queries/steering. Then: 0008 VFX/RenderGraph, 0009 UI Toolkit, 0010 test harness; deferred: 0011 audio mix, 0012 scene/boot.
- **TD self-review (TD-ARCHITECTURE, lean):** APPROVED WITH CONDITIONS — (a) ADRs 0001–0007 Accepted before implementation; (b) user reviews this autonomous draft. LP-FEASIBILITY skipped (lean mode).
- **Balance concerns from 2026-06-19 cross-review remain deferred to prototype** (AOQ-04 in the doc): parry-vs-Finisher ceiling, Finisher 25 vs Levy 30 HP, C5 aggregate heal, no power-vs-density scaling.

---

## Session Extract — /review-all-gdds 2026-06-18
- Verdict: CONCERNS
- GDDs reviewed: 5 (enemy-definition, general-characters, damage-health, upgrade-definition, game-concept)
- Flagged for revision: game-concept.md (stale parent doc) — FIXED in session
- Blocking issue: game-concept.md contradicted approved children (upgrade count/categories/pity/scope vs upgrade-definition; AC-03/05 reset-always vs damage-health threshold rule). Resolved — game-concept synced to children, OQ-10 closed, pity_threshold registry ref cleared.
- Design theory: all PASS (single progression loop; ~3 active systems; anti-pillar "build-crafter" honored; pillars aligned). Watch-items all pre-tracked (S2+S3 reset immunity OQ-02/08, aggregate heal OQ-11, S6 event-ordering ADR).
- Recommended next: /create-architecture (all design-layer blockers now clear) — or author Input Manager + Combat Core GDDs first.
- Report: design/gdd/gdd-cross-review-2026-06-18.md

## Session Extract — /review-all-gdds 2026-06-19 (full 17-GDD cross-review)
- Verdict: CONCERNS → blocking item resolved in-session
- GDDs reviewed: 16 system GDDs (17 systems) + game-concept
- Blocking (fixed): missing invulnerability API — parry/dodge requested an invuln window Damage & Health didn't expose. Added `OpenInvulnerability(duration_ms)` to damage-health.md (shared single iframe timer, latest-end-time overlap). Added Parry + Dodge dependency rows.
- Warnings (fixed): input-manager Dodge "post-MVP/inert" → MVP-active; combat-core now notes Dodge override; upgrade-definition Pending-Baseline table updated to real owners + finisher_max_targets ownership → Combo; game-concept combo_counter_timeout + parry_window pointers updated.
- Balance CONCERNS (deferred to prototype, NOT edited): (1) parry burst ceiling 80 vs Finisher 25–40 → possible parry dominance (Pillar 2); (2) base Finisher 25 < Levy HP 30 (under-kills cheapest enemy); (3) C5 sustain reinforces aggregate-heal OQ-11; (4) no auto player-power scaling vs rising density.
- Design theory: all PASS (single loop, ~3–4 active, pillars aligned, fantasies cohere).
- Recommended next: /prototype combo-and-parry-system (validate balance concerns), then /create-architecture.
- Report: design/gdd/gdd-cross-review-2026-06-19.md

## Current Status

Session 2026-06-19 (autonomous batch): **Authored + self-approved the remaining 12 MVP GDDs** at user request ("self approve till all required GDD is authored and approved"). New: combat-core, combo-system, parry-system, enemy-ai, progression-system, dodge-system, wave-spawner, run-session-manager, combat-feedback-ui, upgrade-selection-ui, run-ui, vfx-audio. **Design layer now 17/17 authored & approved** (4 multi-agent reviewed: enemy/generals/damage-health/upgrades; 13 self-reviewed). Authored in dependency order; cross-referenced against the 5 prior GDDs + registry. Key invented MVP values (provisional, validate in prototype): base_move_speed=5, base_attack_interval=1.0s, attack_cooldown_minimum=0.2s, combo_counter_timeout=3.0s, base_burst=40, max_momentum=100, momentum_gain_per_parry=25, parry_invuln_ms=300, dodge_distance=3.0u/dodge_invuln_ms=400, xp_base=10/xp_growth=1.22, base_concurrent=40, spawn_ring_radius=14, run_duration=1200s. Dodge System retires the enemy-definition Parriable=true slam fallback. **CAVEAT: the 12 new GDDs have NOT had an independent /review-all-gdds or per-GDD /design-review pass** — recommend running that before architecture. Systems-index → all Approved; progress 17/17. Dashboard updated.

Session 2026-06-19: **Input Manager GDD authored** (`design/gdd/input-manager.md`, Designed — pending review). Foundation/infrastructure system; serves Pillar 1 (fair input under chaos). Key decisions: (1) single input authority — all systems read through Input Manager, never raw devices (ADR invariant); (2) parry input buffer = timestamped-press consumer-pull model — `TryConsumeParryPress(notBefore)` / `HasLiveParryPress()`; Input Manager owns the buffer, Parry owns the window; effective acceptance = front-extend window by `parry_input_buffer_ms` (default 120). (3) Input Manager owns raw `LastSteerDirection` recording; Combo applies the 300ms window → resolves general-characters' open DirectionalInputBuffer forward-dep. (4) Gamepad = best-effort MVP (per user); KB/M primary. 13 ACs (deterministic harness). Registry: added `parry_input_buffer_ms`; added input-manager.md to `zhao_yun_input_direction_window_ms` referenced_by. Systems-index row 5 → Designed; progress 5/17 designed, 4 approved. **Not yet design-reviewed** (run /design-review input-manager.md in a fresh session).

Session 2026-06-18: **Foundation re-review batch — 3 GDDs moved to Approved.** Ran lean `/design-review` on the three In-Review docs.
- **enemy-definition.md → APPROVED.** All 19 prior blockers (2026-05-28) traced and confirmed resolved. 0 new blockers. 2 advisory (non-sequential AC numbering; bidirectional back-refs pending downstream GDDs). Post-approval additive edit: added `DamageType` enum field to the AttackEvent contract (see below).
- **damage-health.md → NEEDS REVISION → fixed in session → APPROVED.** 10 prior blockers (2026-05-18) resolved. 1 NEW blocker: `DamageType` enum carried on `DamageTaken` (and required by the Parry-forward contract) did not exist on enemy-definition's AttackEvent — broken bidirectional contract. **Resolved (user chose to add the field):** added `DamageType : enum {Standard|CounterHit|Slam|Projectile}` to enemy-definition.md AttackEvent with deterministic per-emission assignment (CounterHit↔IsCounterHit, Slam↔Warlord P2 slam, Projectile↔Archer, else Standard). Advisory fixes applied: damage_reduction now references `damage_reduction_cap` symbolically + cap-note; AC-DH-11 regen 2.0→1.5; stale header fixed.
- **upgrade-definition.md → APPROVED.** Single prior blocker (2026-06-07 AC-UD-01/03 catalogue-vs-eligible-pool) confirmed resolved. 0 new blockers. Re-verified cross-consistency vs the same-day damage-health edits — all shared values still match. 2 advisory carried forward (OQ-10 game-concept staleness; AC-UD-15 embedded impl question).
- Systems-index rows 1/3/4 → Approved. All three review logs appended. enemy-definition log got a post-approval addendum noting the DamageType addition.
- **Top open thread:** OQ-10 — game-concept.md (Approved) still says "15-18 upgrades / 4 categories / Survivability lane / pity_threshold" and contradicts the now-Approved upgrade-definition.md. Resolve before `/sprint-plan`.

Session 2026-06-07: upgrade-definition.md re-reviewed (lean). Verdict NEEDS REVISION — all 15 prior (2026-05-29) blockers confirmed resolved; 1 new blocker found + fixed in-session: AC-UD-01/03 conflated total pool (24) with the C3-holdout eligible pool (23). Fix: introduced `total_catalogue` (24, fixed) vs `eligible_pool` (drawable, starts 23) terms in Rule 2; rewrote AC-UD-01 (catalogue 24 / eligible 23) + AC-UD-03 (eligible 22 after first pick). Recommended fixes also applied: OQ-11 added (C5+S5+P6 aggregate heal watch-item, no mechanic); C3 "leading"→"leading or tied" (Edge Case + OQ-06); exhausted-lane drought note in Rule 3; OQ-10 expanded (game-concept.md ~7 contradictions + stale pity knob — left OPEN per user for a separate pass). Cross-checked vs damage-health.md: all shared values match. Doc status → In Review. Systems-index row 4 updated; review log appended.

/consistency-check (full, 2026-06-07): CONFLICTS FOUND → resolved. (1) pity_threshold registry entry was stale — pity rule removed from upgrade-definition.md (its own source) but still active in registry + live in game-concept.md. Deprecated the registry entry (status: deprecated, replacement noted), referenced_by trimmed to game-concept.md with STALE marker. (2) Added upgrade-definition.md to referenced_by for damage_reduction_cap, base_max_hp, base_attack, base_radius (value-correct, just unrecorded consumers). All 8 entities / 4 formulas / 9 other constants verified clean. Drought params (threshold=4, mult=2×, modifier_pick_cap=3) NOT yet registered — single-GDD until Progression System GDD authored. game-concept.md left untouched (OQ-10 deferred per user). registry last_updated → 2026-06-07.

Session 2026-05-29: upgrade-definition.md revised. All 12 blockers resolved. Key decisions:
- Pity rule removed → pure flat draw pool (matches "self-knowledge, not resource management" fantasy)
- Pool expanded 16 → 24 (3 lanes × 6 + 6 Modifiers) → ~50% catalogue overlap per run
- "Survivability" lane demoted to "Modifier" type — cross-lane supplements, same flat draw pool
- C3+P3 synergy named "Iron Cyclone" — documented as mastery feature, not exploit
- 8 new upgrades added: C5 Heaven's Gate, C6 Tiger's Chain, P5 Crane's Eye, P6 Second Wind, A5 Rushing Stream, A6 Relentless Strike, S5 Battle Hunger, S6 Iron Stance
- Spec fixes: C1 baseline formula added; A4 combo-increment = 1 per swing regardless of targets; P3 trigger = successful parry only (incoming attack present); P4 momentum_reset_multiplier ownership → Parry System
- Note: game-concept.md still references "15-18 upgrades" — needs updating to 24 in a future pass
- Status: **In Revision → re-review required** (`/design-review design/gdd/upgrade-definition.md`)

Session 2026-05-28: Both re-reviews completed. Key decisions:
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
- [x] `/design-system upgrade-definition` — DESIGNED (2026-05-18); reviewed 2026-05-28 (12 blockers); **revised 2026-05-29; re-review required**
- [ ] `/design-system dodge-system` — required before Warlord implementation
- [ ] `/design-system` per remaining system (13 left)
- [x] `/create-architecture` — DRAFTED 2026-07-04 (autonomous; pending user review)
- [ ] `/architecture-decision` (×12 — ADR-0001..0007 blocking, 0008–0012 staged)
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

## Session Extract — /dev-story 2026-10-10
- Story: production/epics/bootstrap-combat-frame/story-002-game-clock-run-time.md — IGameClock with unified pause-compensated RunTime + test clock
- Files changed: src/Core/Time/IGameClock.cs, IRunClockControl.cs, RunClockState.cs (shared pure math), UnityGameClock.cs; tests/helpers/FakeGameClock.cs
- Test written: tests/unit/game-clock/game_clock_run_time_test.cs (31 tests; EditMode suite 32/32 green via `unity test`)
- Deviation: implemented while BCF-001 dependency awaits CI (AC-10) — user-directed
- Open: G-2 (InputManager access to ToRunTime on the RSM-only control interface); realtimeSinceStartup vs Input System ctx.time epoch check (input-manager epic); UnityGameClock covered via RunClockState only (play-mode check later)
- Blockers: None for BCF-002. BCF-001 AC-10: CI run 38088358551 failed — Unity error 20111 "serial has reached the maximum number of activations" (user must free an activation)
- Next: /code-review src/Core/Time/*.cs tests/helpers/FakeGameClock.cs then /story-done story-002

## Session Extract — /story-done 2026-10-10
- Verdict: COMPLETE WITH NOTES
- Story: production/epics/bootstrap-combat-frame/story-002-game-clock-run-time.md — IGameClock with unified pause-compensated RunTime + test clock
- Tech debt logged: None (3 follow-ups recorded in the story's Completion Notes, owned by RSM / input-manager / BCF-005-006)
- Next recommended: BCF-004 designer-error channel (0.25d) → BCF-003 CombatFrame orchestrator; GD-001 EnemyDataSO + IM-001 PlayerControls also unblocked

## Session Extract — /story-done BCF-001 2026-10-11
- Verdict: COMPLETE WITH NOTES (10/10; CI run 38097320963 green — EditMode 1/1, PlayMode 1/1)
- CI licence switched to Unity Personal (Pro seat is employer-owned, both activations used by home+office PCs); UNITY_SERIAL secret deleted; test job has checks:write
- Evidence: production/qa/smoke-2026-10-11.md
- Next: commit + push BCF-002 (so CI runs all 38 tests) → /dev-story BCF-004

## Session Extract — /dev-story 2026-10-11
- Story: production/epics/bootstrap-combat-frame/story-004-designer-error-channel.md — Shared [DESIGNER-ERROR] logging channel
- Files changed: src/Core/Diagnostics/IDesignerErrorLog.cs (interface + DesignerError marker/format), src/Core/Diagnostics/UnityDesignerErrorLog.cs, tests/helpers/RecordingDesignerErrorLog.cs
- Test written: tests/unit/designer-error/designer_error_channel_test.cs (14 tests; EditMode suite 52/52 green via `unity test`)
- Implemented inline (no agent spawn — 0.25d Logic story)
- Blockers: None
- Next: /code-review then /story-done story-004

## Session Extract — /story-done 2026-10-11
- Verdict: COMPLETE WITH NOTES
- Story: production/epics/bootstrap-combat-frame/story-004-designer-error-channel.md — Shared [DESIGNER-ERROR] logging channel
- Review: unity-specialist CLEAN; qa-tester GAPS → fixes applied; EditMode 57/57
- CI: run 38098780516 (BCF-002 push fb350ac) green — EditMode 38/38, PlayMode 1/1
- Tech debt logged: None
- Next recommended: BCF-003 CombatFrame orchestrator (production/epics/bootstrap-combat-frame/story-003-combat-frame-orchestrator.md); GD-001 / OP-001 / IM-001 also ready

## Session Extract — /story-done BCF-003 2026-10-11
- Story: production/epics/bootstrap-combat-frame/story-003-combat-frame-orchestrator.md
- Evidence: `tests/unit/combat-frame/combat_frame_order_test.cs` + `combat_frame_event_recorder_test.cs` (14 tests), EditMode 71/71
- Verdict: COMPLETE WITH NOTES (autonomous Sprint 1 batch; consolidated independent code review at batch end)

## Session Extract — /story-done GD-001 2026-10-11
- Story: production/epics/game-data/story-001-enemy-data-so.md
- Evidence: `tests/unit/game-data/game_data_enemy_data_test.cs` (13 tests), EditMode 84/84
- Verdict: COMPLETE WITH NOTES (autonomous Sprint 1 batch; consolidated independent code review at batch end)

## Session Extract — /story-done OP-001 2026-10-11
- Story: production/epics/object-pool/story-001-pool-core-prewarm-spawn-return.md
- Evidence: `tests/unit/object-pool/object_pool_core_test.cs` (14 tests), EditMode 98/98
- Verdict: COMPLETE WITH NOTES (autonomous Sprint 1 batch; consolidated independent code review at batch end)

## Session Extract — /story-done OP-002 2026-10-11
- Story: production/epics/object-pool/story-002-attacker-id-spawn-jitter.md
- Evidence: `tests/unit/object-pool/object_pool_spawn_identity_test.cs` (9 tests), EditMode 107/107
- Verdict: COMPLETE WITH NOTES (autonomous Sprint 1 batch; consolidated independent code review at batch end)

## Session Extract — /story-done IM-001 2026-10-11
- Story: production/epics/input-manager/story-001-player-controls-asset-and-input-manager-shell.md
- Evidence: `tests/unit/input-manager/input_manager_context_test.cs` (14 tests), EditMode 121/121
- Verdict: COMPLETE WITH NOTES (autonomous Sprint 1 batch; consolidated independent code review at batch end)

## Session Extract — /story-done IM-002 2026-10-11
- Story: production/epics/input-manager/story-002-parry-ring-buffer-liveness.md
- Evidence: `tests/unit/input-manager/input_manager_parry_buffer_test.cs` (12 cases), EditMode 151/151
- Verdict: COMPLETE WITH NOTES (autonomous Sprint 1 batch; consolidated independent code review at batch end)

## Session Extract — /story-done IM-003 2026-10-11
- Story: production/epics/input-manager/story-003-try-consume-parry-press.md
- Evidence: `tests/unit/input-manager/input_manager_parry_consume_test.cs` (10 tests × 2 implementations), EditMode 151/151
- Verdict: COMPLETE WITH NOTES (autonomous Sprint 1 batch; consolidated independent code review at batch end)

# Accessibility Requirements: Dynasty Survivors

> **Status**: Committed
> **Author**: autonomous session (ratify/adjust as desired) — 2026-07-04
> **Last Updated**: 2026-07-04
> **Accessibility Tier Target**: **Basic** (MVP) with two elevated commitments — see below. Post-MVP target: Standard.
> **Platform(s)**: PC (Steam / itch.io)
> **External Standards Targeted**: WCAG 2.1 Level A (contrast/text-size guidance used as reference); no platform certification requirements (PC only)
> **Accessibility Consultant**: None engaged
> **Linked Documents**: `design/gdd/systems-index.md`, `design/ux/interaction-patterns.md`, `design/gdd/game-concept.md` (Visual Identity Anchor)

---

## This Project's Commitment

**Target Tier**: **Basic** for MVP, with **two commitments elevated above Basic** because they are already binding design rules in the approved GDDs:

1. **Shape-first colorblind safety for all gameplay-critical signals** (normally a Standard-tier colorblind-mode feature — here solved at the design layer instead of via palette modes): every telegraph shape is distinguishable by shape alone; parry burst (ring) vs Finisher (starburst) differ by shape; no two enemy types share both shape and color. Source: game-concept Visual Identity Anchor, enemy-definition telegraph rules, vfx-audio Rule 8.
2. **Deaf/hard-of-hearing playability**: the combo counter UI pulse (1→2→FINISH) and shatter-on-reset are the *primary* signals; audio is additive, never the sole channel for gameplay-critical information. Source: game-concept deaf/HoH path.

**Rationale**: This is a fast, timing-based action game for PC with a 3–6 week solo MVP. The MVP explicitly excludes settings menus, remapping, and difficulty options (game-concept "NOT in MVP"), which caps the *feature* tier at Basic. However, the game's core readability pillar (Readable Chaos) already commits the design to the two most impactful accessibility properties for this genre — colorblind-safe telegraphs and visual-primary feedback — at zero extra feature cost. Full keyboard navigation with visible focus on all menus is additionally mandated by technical-preferences (no hover-only interactions). Moving to Standard post-MVP requires: input remapping UI (bindings already live in the Input Actions asset — pure-data add per ADR-0004), independent volume sliders, and UI scaling.

**Features explicitly in scope (MVP)**:
- Shape-first telegraphs and effects (elevated commitment #1)
- Visual-primary combat feedback; combo reset break animation (elevated commitment #2)
- Full keyboard navigation with visible focus on selection/pick/end screens; no hover-only interactions (technical-preferences, ADR-0009)
- Pause-safe input handling (parry buffer freezes during pause — no stolen inputs; input-manager)
- No photosensitivity-risk strobing: VFX hierarchy caps effect stacking; hit feedback is throttled (`hit_feedback_throttle_ms`) explicitly to prevent screen strobing at density (combat-feedback-ui)

**Features explicitly out of scope (MVP)** — see Known Intentional Limitations:
- Settings menu of any kind (remapping, volume sliders, colorblind palette modes, text scaling, motion reduction)
- Subtitles (no voiced content exists)
- Difficulty/assist modes

---

## Color-as-Only-Indicator Audit

| Location | Color Signal | What It Communicates | Non-Color Backup | Status |
|----------|-------------|---------------------|-----------------|--------|
| Enemy telegraphs (all 6 shapes) | Gold/orange/red per type | Incoming attack type + parry window | **Shape is primary**: ring / line / arrowhead / thick ring / 270° arc / filled circle — no two types share shape+color | Designed (GDD-mandated) |
| Parry burst vs Finisher flash | Reserved accent vs Finisher accent | Which player effect fired | Ring (expanding) vs starburst (radial) — shape rule | Designed (GDD-mandated) |
| Warlord slam (non-parriable) | Bright red filled circle | Dodge, don't parry | Filled circle ≠ any parry-prompt shape; no parry indicator shown | Designed (GDD-mandated) |
| Combo counter | Pulse/glow tint | Chain state 1→2→FINISH | Numeric/state display + pulse + shatter break animation on reset | Designed (GDD-mandated) |
| Momentum meter | Glow intensity | Meter value / max state | Fill level is positional; distinct full-meter state must include a non-color cue (e.g., frame change) — flag for /ux-design | Open — UX spec |
| Reduced elite hit (combo<2) | Dimmer flash / smaller number | Stagger gate not met | Number size + flash intensity differ (not hue alone) — flag for /ux-design | Open — UX spec |
| Upgrade card lane hint (if adopted; upgrade-selection-ui OQ-01) | Lane color-coding | Upgrade category | Must add lane icon or label if color-coding is adopted | Open — UX spec |
| HP bar low state | (typical red-low convention) | Near death | Numeric `ceil(hp)` display + bar position — flag low-state treatment for /ux-design | Open — UX spec |

## Gameplay-Critical SFX Audit

Design rule (game-concept): audio is additive — every entry below already has a visual primary.

| Sound Effect | What It Communicates | Visual Backup | Caption Required | Status |
|-------------|---------------------|--------------|-----------------|--------|
| hit → hit → CRASH escalation | Combo build state | Combo counter pulse 1→2→FINISH (primary) | No | Designed |
| Parry success SFX | Parry landed | Expanding ring burst + momentum fill | No | Designed |
| Counter-hit cue (louder) | Most dangerous incoming attack | 270° arc telegraph (largest, visually prioritized) | No | Designed |
| Warlord roar | Phase transition | Roar animation + movement lock (state change visible) | No | Designed |
| Elite/boss spawn stinger | Checkpoint spawn | Elite spawns on-screen from spawn ring; distinct silhouette | No | Designed |
| Level-up cue | Pick incoming | XP bar full + run pauses + pick screen appears | No | Designed |

---

## Per-Feature Accessibility Matrix

| System | Visual Concerns | Motor Concerns | Cognitive Concerns | Auditory Concerns | Addressed | Notes |
|--------|----------------|---------------|-------------------|------------------|-----------|-------|
| Combo/Parry/Dodge (active skills) | Effects must not obscure telegraphs (Pillar 1 rule) | Timing windows (telegraph-derived, 350–800ms) + 120ms input buffer; no mashing required; no hold inputs | Two active systems + positioning — bounded to ~3–4 simultaneous concerns by design (cross-review PASS) | All cues visually primary | Yes (design layer) | Timing-window extension (P5 upgrade) exists in-fiction; a global timing multiplier is a post-MVP assist candidate |
| Enemy telegraphs @200 density | Readability at density; concurrent-telegraph cap TBD | — | Reading many threats | Counter-hit audio is additive | Partial | `telegraph_concurrent_cap` empirical (prototype AOQ-03) |
| Upgrade Selection UI | One-line plain-text descriptions (Pillar 3) | Keyboard/mouse/gamepad; no timeout on pick | 3 options max; no tooltips | — | Yes | No auto-dismiss; run paused indefinitely |
| Run UI (HUD/screens) | HP/XP/timer readable at density; edge-anchored | Keyboard-first menus, visible focus | Death screen explains cause (Pillar 4) | Win/death distinct audio + visual | Yes (design layer) | Layout validation in /ux-design |
| Wave/escalation | Spawn outside view (no pop-in startle) | — | Pressure ramps on fixed 2-min schedule (learnable) | Music intensity additive | Yes | — |

---

## Known Intentional Limitations (MVP)

| Feature | Tier Required | Why Not Included | Risk / Impact | Mitigation |
|---------|--------------|-----------------|--------------|------------|
| Input remapping UI | Standard | MVP excludes settings (game-concept); solo 3–6wk scope | Players needing alternate bindings blocked | Bindings live in the Input Actions asset — remapping is a pure-data post-MVP add (ADR-0004); Steam Input offers system-level remap in the interim |
| Independent volume sliders | Basic (partial gap) | No settings menu in MVP | Players can't balance mix | OS/Steam volume as interim; first item in post-MVP settings |
| Colorblind palette modes | Standard | Solved structurally by shape-first design instead | Residual risk for low-vision players relying on color reinforcement | Shape-first audit above; validate with Coblis simulator on prototype screenshots |
| Text scaling / UI scaling | Standard | No settings menu in MVP | Small-text readability on high-DPI | UI Toolkit USS makes scaling a low-cost post-MVP add; keep base sizes generous (flag in /ux-design) |
| Motion reduction mode | Standard | No settings menu in MVP | Screen-shake sensitivity (CounterHit/Slam shake) | Shake magnitudes are tuning knobs (`counter_hit_shake_scale`, `finisher_screen_shake` on/off) — a toggle is trivial post-MVP |
| Difficulty/assist options | Standard | Fixed 20-min run design; MVP scope | Motor/cognitive difficulty floor | Auto-attack baseline means survival without active inputs is partially viable (Auto-attack lane is the designed passive path) |

## Test Plan (MVP-scope)

| Feature | Test Method | Pass Criteria | Responsible | Status |
|---------|------------|--------------|-------------|--------|
| Shape-first telegraphs | Coblis simulation on prototype screenshots (all 3 CVD types) | All telegraph types + parry/Finisher distinguishable in every mode | ux-designer | Not Started |
| Keyboard-only flows | Manual: select general, pick upgrades, retry — no mouse | All flows completable; focus always visible | qa-tester | Not Started (AC-US-04, AC-RU-07) |
| Visual-primary combat signals | Play 5 minutes muted | All threats readable and game fully playable with no audio | qa-tester | Not Started |
| Anti-strobe throttling | 200-enemy density scene | No flashing above ~3 flashes/sec from stacked hit feedback | qa-tester | Not Started |
| Playtest telegraph comprehension | Structured playtest | ≥70% identify parry telegraph unprompted (game-concept AC-13) | producer | Not Started |

## Audit History

| Date | Auditor | Type | Scope | Findings Summary | Status |
|------|---------|------|-------|-----------------|--------|
| 2026-07-04 | Internal — autonomous session | Initial commitment | Tier definition + audits seeded from approved GDDs | Tier committed: Basic + 2 elevated commitments; 4 open items routed to /ux-design | Committed |

## Open Questions

| Question | Owner | Deadline | Resolution |
|----------|-------|----------|-----------|
| Momentum-meter full state: non-color cue design | /ux-design (HUD) | Before UI stories | Unresolved |
| Upgrade card lane color-coding (OQ-01) — icon/label backup if adopted | /ux-design (pick screen) | Before UI stories | Unresolved |
| Does the 120ms parry buffer + telegraph windows meet motor-accessibility floor, or is a timing multiplier needed sooner than post-MVP? | Prototype playtest | After prototype | Unresolved |

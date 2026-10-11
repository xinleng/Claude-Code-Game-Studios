# Interaction Pattern Library: Dynasty Survivors

> **Status**: Draft (initialized — patterns fleshed out by `/ux-design` per screen)
> **Author**: autonomous session — 2026-07-04
> **Last Updated**: 2026-07-04
> **Version**: 0.1
> **Engine**: Unity 6.3 LTS
> **UI Framework**: Unity UI Toolkit (UXML/USS) — ADR-0009
> **Related Documents**:
> - `design/gdd/game-concept.md` (Visual Identity Anchor) — visual standards until the art bible exists
> - `design/accessibility-requirements.md` — accessibility commitments
> - `design/ux/ux-spec-[screen].md` — individual screen specs (to be authored via `/ux-design`)
> - `docs/architecture/adr-0009-ui-architecture.md` — binding implementation rules

> Screen specs reference patterns by name instead of re-specifying focus, input,
> and feedback behaviour. When a pattern changes, it changes here and applies
> everywhere. Do not design a new interaction without checking here first.

**Global rules (apply to every pattern — from technical-preferences + ADR-0009):**
- Keyboard-first: every interactive element reachable and activatable without a mouse; visible `:focus` style always
- No hover-only affordances — anything shown on hover must also show on focus
- Inputs arrive via `IInputAuthority` (UI action map: Navigate/Confirm/Cancel) — never raw devices
- Presenters write element properties only on changed values (diff before write)
- No auto-dismiss of actionable UI; the pick screen waits indefinitely (run is paused)

---

## Pattern Catalog Index

| Pattern Name | Category | Description | Used In (Screens) | Status |
|-------------|----------|-------------|------------------|--------|
| Card (Selectable) | Input | Focusable card in a horizontal set; navigate left/right, confirm to pick | General Selection (3 cards), Upgrade Pick (≤3 cards) | Draft |
| Button (Primary) | Input | Single most important action on a screen | End screens (Retry), Main Menu (Play) | Draft |
| Button (Secondary) | Input | Alternate/back action; maps to Cancel input | Main Menu (Quit), End screens (Menu) | Draft |
| Focus Management | Navigation | Where focus lands on open/close; no focus loss ever | All screens | Draft |
| Escape / Cancel | Navigation | Cancel = back where a back exists; no-op on the pick screen (no skip) | Selection (→ main menu), Pick screen (no-op) | Draft |
| Screen Replace | Navigation | Menu → run and run → end screens; no loading screens, no nav stack | RSM flow transitions | Draft |
| Pause Overlay | Feedback / Layout | Battlefield dims behind modal content; run frozen (`timeScale = 0`) | Upgrade Pick | Draft |
| HUD Meter (Combo Counter) | Game-Specific | 1→2→FINISH pulse; shatter break animation on reset (never a fade) | Combat HUD | Draft |
| HUD Meter (Momentum) | Game-Specific | 0–100 fill + glow; distinct full state (needs non-color cue — open); visible empty/halve on reset | Combat HUD | Draft |
| HUD Meter (HP Bar) | Game-Specific | `ceil(hp)/max`; ceiling expands on S1 without fill jump; ghost-drain on damage | Run HUD | Draft |
| HUD Meter (XP Bar) | Game-Specific | Fill on kills; reset with overflow carry on level-up; level number | Run HUD | Draft |
| Run Timer | Game-Specific | mm:ss countdown from 20:00; high-intensity state in final 5 min | Run HUD | Draft |
| Telegraph Indicator | Game-Specific | In-world attack telegraph; shape-first per enemy-definition table; counter-hit arc always visually dominant | In-world (VFX Graph) | Draft |
| Geometry Preview | Game-Specific | Top-down Finisher-shape silhouette (ring / 90° wedge / circle+outward arrows), equal scale across cards | General Selection | Draft |
| Control Card (Toast) | Feedback | One-frame dismissible "Attack: auto \| Parry: [KEY]" at run start; auto-fades ~4s; never blocks input | Run start | Draft |
| Damage Number | Game-Specific | Finisher hits large/distinct; normal hits small or none; pooled (64); reduced-elite-hit variant | Combat HUD / VFX | Draft |

---

## Pattern Notes (seed specifications)

#### Card (Selectable)
The core interactive pattern for both decision screens. Horizontal set of 1–3 cards; `Navigate` left/right moves focus (no wrap at ends); leftmost focused by default (pick screen) / Guan Yu pre-focused (selection); `Confirm` activates; mouse click also selects+confirms. Focus = scale ~1.03 + high-contrast frame (not color-only). Card content is Name (bold) + one line — no tooltips, no secondary panels (Pillar 3). Selection screen adds the Geometry Preview inside each card; pick screen may add a lane hint (icon + optional color — never color alone; accessibility audit).

#### Focus Management
On any screen open: focus lands on the default element (pick: leftmost card; selection: Guan Yu; end screens: Retry). Focus is never lost — if a focused element disappears, focus moves to its nearest sibling. While the pick overlay is open, focus cycles within it only. Gameplay HUD has no focusable elements.

#### Escape / Cancel
Selection screen: Cancel returns to main menu, no confirmation (general-characters Rule 8). Pick screen: Cancel does nothing — a level-up always grants a pick (upgrade-selection-ui Rule 5). End screens: Cancel = Menu option.

#### Pause Overlay
`Time.timeScale = 0` + Menu input context (RSM owns both). Battlefield dims (~60%) behind the cards — visible but muted so the pick reads as "mid-battle decision". Parry buffer freezes (input-manager) — a press just before pause is not stolen. `card_open_anim_ms` = 150 (snappy; Pillar 3).

#### HUD Meters
All meters are event-driven (no polling — ADR-0009) and edge-anchored so they never obscure the player or nearby telegraphs. Combo counter: pulse on increment, primed glow at 2, FINISH flash, **shatter** on reset (teaches the damage-reset rule without a tooltip — game-concept). Momentum: fill + glow, halve-visibly with P4; full-meter state needs a non-color cue (open — accessibility audit). HP: ghost-drain on damage (previous value lingers ~0.5s); ceiling expansion on S1 keeps the current fill position. Timer: whole-second updates only (1Hz).

#### Telegraph Indicator
Not a UI Toolkit element — VFX Graph in-world (ADR-0008). Shape table is owned by enemy-definition and is binding: Levy ring / Pikeman line / Archer arrowhead / Iron Captain thick ring + counter-hit 270° arc / Warlord filled circle. Counter-hit arc reserves its visual layer and is never culled. Slam (filled circle) must NOT show any parry-prompt affordance.

---

## Change Log

| Date | Change | By |
|------|--------|----|
| 2026-07-04 | Library initialized with 16 seed patterns from the 3 UI GDDs + game-concept | autonomous session |

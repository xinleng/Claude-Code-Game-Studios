# UX Spec: Combat HUD

> **Status**: Drafted — PROVISIONAL (authored autonomously 2026-10-10 under the user's standing
> proceed directive; single-pass authoring, decisions self-approved with rationale recorded.
> Validate with `/ux-review` and ratify the flagged items before implementation.)
> **Author**: ux-designer (agent) + main session
> **Last Updated**: 2026-10-10
> **Journey Phase(s)**: In-run combat (no player-journey.md exists — assumed phase: the entire
> 20-minute run loop; emotional state: high-arousal flow, 200-enemy pressure)
> **Template**: UX Spec
> **Scope**: the combat-feedback layer owned by `design/gdd/combat-feedback-ui.md` — combo
> counter, momentum meter, hit feedback, damage numbers, parry-attempt feedback, and the HUD-side
> coordination contract with in-world telegraphs. HP/XP/timer belong to `design/ux/run-hud.md`
> (not yet specced — placement is reserved here for coherence).
> **Binding inputs**: combat-feedback-ui.md UI Requirements · art bible §7 + §10 (43 UX
> constraints) · design/accessibility-requirements.md (Basic + 2 elevated commitments) ·
> interaction-patterns.md (HUD Meter Combo/Momentum, Damage Number, Telegraph Indicator,
> Control Card) · ADR-0009 (UI Toolkit, event-driven presenters).

---

## Purpose & Player Need

The player arrives at this layer wanting to **know, without looking away from the fight:
how close am I to a Finisher, how strong is my next parry burst, and did my input just count?**
The combat HUD is the skill-loop instrument panel for the two active systems (combo, parry).
If it fails, Pillar 1 (Readable Chaos) fails: combo state is the deaf/HoH-primary signal, and
momentum is the only way to judge burst value before committing to a parry. It is a pure
feedback surface — zero interactive elements.

## Player Context on Arrival

Visible from the first frame of Running state (after general select, after the control-card
toast) until Resolved. The player is mid-combat, time-pressured, eyes locked on the player
character and nearby telegraphs. Everything here must be readable in ≤200 ms peripheral
glances. The design assumes maximum cognitive load — 200 enemies, multiple telegraphs, VFX.

## Navigation Position

This layer lives at: Run (Running state) → overlay layer, above the battlefield, below nothing.
It is not a destination: no navigation reaches it, it has no focus, no cursor interaction
(`pickingMode = Ignore` on all roots). It persists through the upgrade-pick pause (dimmed with
the battlefield, behind the pick overlay) and hides on the end screens.

## Entry & Exit Points

| Entry Source | Trigger | Player carries this context |
|---|---|---|
| Run start (RSM → Running) | `RunStarted` flow event | Fresh state: combo 0, momentum 0 |
| Resume from upgrade pick | `SetContext(Gameplay)` / timeScale = 1 | Pre-pause combat state, unchanged |

| Exit Destination | Trigger | Notes |
|---|---|---|
| Upgrade pick overlay | LevelUp pause | HUD stays rendered, dimmed with the world; state frozen |
| Win / Death screen | RunResolved | HUD hides; kill-context flows to the death screen (run-ui's spec) |

No one-way exits; no state is lost on any transition (presenters re-render from events).

---

## Layout Specification

### Information Hierarchy

1. **Combo state (0 → 1 → 2/primed → FINISH moment)** — the deaf/HoH-primary signal and the
   core loop readout. Most important element in this layer.
2. **Combo reset (shatter)** — teaches the damage-reset rule with no tooltip; must never be missed.
3. **Momentum value + full state** — decides when a parry is "worth it"; continuous value.
4. **Hit-taken feedback (DamageType-keyed)** — what just hit me and how badly.
5. **Parry-attempt feedback** — a buffered press exists (`HasLiveParryPress()` peek cue).
6. **Damage numbers** — Finisher payoff confirmation; discoverable, lowest priority.

In-world telegraphs outrank everything above (art bible priority stack) but are not HUD
elements — see Coordination Contract below.

### Layout Zones

Bottom-centre cluster (RATIFY — art bible §7.1 default; final placement = this spec's OQ-01,
validate at 200-enemy density):

- **Zone BC (bottom-centre, above the XP bar's reserved strip):** combo pips with the momentum
  meter directly beneath them. Rationale: one glance covers both skill systems; bottom-centre
  is the closest edge-zone to where the player's eyes rest (the character at screen centre)
  without entering the chrome-free play zone (central ~70%×75%); it never covers telegraphs
  near the general. Alternative considered and rejected: player-following placement (would
  overlap telegraphs at the exact moment they matter).
- **Zone FX (screen-edge, non-layout):** hit-taken edge flash — a directional edge treatment,
  not a vignette ring (rings are reserved vocabulary) and never red-tinted full-screen.
- **Zone W (in-world, pooled):** damage numbers above struck enemies.
- Reserved for run-hud.md: HP top-left, timer top-centre, XP bottom edge full-width.

### Component Inventory

| Component | Pattern (library) | Content | Interactive | Notes |
|---|---|---|---|---|
| Combo pip cluster | HUD Meter (Combo Counter) | 3 chamfered pips + FINISH label | No | ≥64 px cluster height; violet only on FINISH |
| Momentum meter | HUD Meter (Momentum) | 0–100 azure fill, seal-stamp + notched outline when full | No | ≥10 px thick; azure appears nowhere else |
| Parry-ready tick | NEW pattern → add to library | Small bone-white tick/notch on the momentum frame while `HasLiveParryPress()` is true | No | Non-consuming peek; sub-200 ms lifetime typical |
| Hit-taken edge flash | NEW pattern → add to library | Directional edge flash keyed by DamageType (Standard dim / CounterHit strong + shake / Slam fullscreen impact frame / Projectile directional sliver if source known) | No | Throttled 50 ms; ≤3 luminance events/sec stacking cap |
| Damage numbers | Damage Number | Finisher hits: large violet-fringed numerals; reduced elite hit: small grey "~"-prefixed; normal hits: none (OQ-03 default) | No | Pool 64; cached digit strings (ADR-0009) |

### ASCII Wireframe (1920×1080; not to scale)

```
+----------------------------------------------------------------------+
| [HP ███ 42]        [ 12:34 ]                             (run-hud)   |
|                                                                      |
|          (chrome-free play zone — battlefield, telegraphs,           |
|           in-world damage numbers, parry ring, Finisher VFX)         |
|                                                                      |
|                         ⟨1⟩ ⟨2⟩ ⟨★⟩   ← combo pips (FINISH = ★)      |
|                      [≈≈≈≈≈≈≈——————]▌ ← momentum (▌= parry-ready)    |
| Lv 7 ███████████████ XP ████████████████████████████████  (run-hud)  |
+----------------------------------------------------------------------+
   edge flash: directional band on the struck side, outside play zone
```

---

## States & Variants

| State / Variant | Trigger | What Changes |
|---|---|---|
| Default (combo 0) | Run start / after reset settles | Pips empty, momentum at current value |
| Combo 1 / 2 | Pip fills with 120 ms pulse (scale 1.0→1.25→1.0) | 2nd pip = "primed" treatment (brighter fill + steady emphasis, no strobe) |
| FINISH moment | 3rd landing swing | ★ pip flashes Mandate Violet once (≤350 ms), then cluster returns to 0 cleanly (no shatter — a Finisher is success) |
| Combo reset (damage) | `DamageTaken ≥ threshold` with reset flag | Pips shatter into 6–8 angular shards, ~250 ms, interruptible by the next increment; never a fade |
| Momentum full | meter = 100 | Gold seal stamp + notched outline on the frame (shape change, not glow-only) |
| Momentum reset | threshold damage | Fill empties/halves (P4) in the same frame as the hit; ghost-tone trail ≤300 ms |
| Parry-ready | `HasLiveParryPress()` true | Bone-white tick visible on momentum frame |
| Paused (upgrade pick) | timeScale = 0 | Dimmed with the world; all animations freeze except none needed (values static) |
| Empty state | n/a | There is no no-data state — all values have defined zeros from run init |
| Error state | Event stream missing (dev error) | Elements render at zero; `[DESIGNER-ERROR]` logged; never hidden silently |
| Platform variant | None | PC-only; same layout at all supported aspect ratios, anchored to safe margins |

## Interaction Map

Input methods (from technical-preferences): KB/M primary, partial gamepad, no touch.
**This layer is non-interactive by design** — no clicks, no focus, no hover. The only
input-adjacent behavior is the parry-ready tick, which *reflects* buffered input state
read-only. Keyboard navigation is intentionally not applicable (HUD exempt per
accessibility commitments; nothing here can be reached or activated).

## Events Fired

This layer fires **no events** (deliberate — it is a pure subscriber). It consumes:

| Consumed signal | Publisher | Used for |
|---|---|---|
| Combo increment / reset / Finisher-resolved | ComboSystem | Pips, shatter, ★ flash |
| Momentum value (continuous) + reset | ParrySystem | Meter fill, ghost trail |
| `DamageTaken` (presentation tier, with DamageType) | PlayerHealthSystem | Edge flash keying |
| Enemy-hit events with reduced-hit snapshot flag | EnemySystemManager/CombatCore | Damage-number variants |
| `HasLiveParryPress()` (read-only peek, per frame-change) | InputManager | Parry-ready tick |

No analytics events in MVP (no analytics system exists). Nothing here writes game state.

## Transitions & Animations

- **Layer enter:** instant with run start (no fade — the run's first frame is playable).
- **Layer exit:** hides with the end-screen transition (run-ui owns that wipe).
- Pip pulse 120 ms ease-out; shatter ~250 ms, interruptible; FINISH violet flash ≤350 ms;
  momentum fill eases ≤120 ms; ghost trail ≤300 ms.
- All combat-state animations complete in ≤350 ms; nothing loops ambiently (art bible §10 r.9).
- **Anti-strobe:** combined HUD + edge-flash luminance events ≤3/sec (throttle 50 ms +
  severity priority: CounterHit > Slam > Standard).
- **Reduced motion:** no motion-sickness vectors (no parallax/camera-coupled HUD motion).
  Screen shake is world-side, knob-gated (`counter_hit_shake_scale`), never information-bearing.

## Data Requirements

| Data | Source System | Read / Write | Notes |
|---|---|---|---|
| combo_counter state + reset/Finisher signals | ComboSystem | Read (events) | Snapshot semantics — event payload carries state, UI never polls |
| momentum value + full/reset | ParrySystem | Read (events + value signal) | Same-frame reset display is a hard requirement |
| DamageTaken + DamageType | PlayerHealthSystem (presentation tier, ADR-0003) | Read | Projectile direction needs source position in payload — flagged to architecture (TR-CC-007 payload extension) |
| Reduced-hit flag (combo<2 at resolution) | EnemySystemManager | Read | Must be a snapshot on the event (TR-AI-002) |
| Live parry press | InputManager `HasLiveParryPress()` | Read (non-consuming) | The one synchronous read; per ADR-0009 diff-before-write |

No writes. No UI-owned game state.

## Accessibility

Per `design/accessibility-requirements.md` (Basic + 2 elevated commitments):
- **Deaf/HoH visual-primary:** combo pulse/shatter is THE primary combo channel; audio additive.
  Every consumed audio cue in this layer has its visual twin (fully playable muted).
- **Colorblind shape-first:** FINISH = ★ pip + label (not violet alone); momentum full =
  seal + notch (not glow alone); reduced hit = size + "~" prefix (not grey alone); edge flash
  severity = intensity + position + impact-frame (not hue alone).
- **Contrast:** pips/numerals ≥7:1 on their backing; meter fills ≥3:1 vs track; backing plates
  ≥80% opacity or outlined (validate on worst-case brown battlefield screenshots at 200 enemies).
- **Sizes (1080p):** combo cluster ≥64 px; momentum ≥10 px thick; damage numbers ≥24 px
  (Finisher ≥32 px); nothing below 20 px.
- **No keyboard path needed** (non-interactive, exempt); no screen-reader targets in MVP
  (flagged post-MVP with the settings menu).
- **Photosensitivity:** ≤3 flashes/sec enforced at the throttle layer, including Slam.

## Localization Considerations

Only two strings exist: "FINISH" (★ pip label) and the "~" prefix glyph. FINISH must fit the
pip cluster at +40% expansion (German "ABSCHLUSS" would not — **rule: the FINISH label is a
non-localized stylized mark OR swapped for the ★ glyph alone in long locales**; HIGH PRIORITY
note for the localization pass). Numerals are tabular, locale-independent (no separators at
these magnitudes). CJK decorative glyphs do not appear in this layer.

## Coordination Contract (telegraphs — in-world, not HUD)

Telegraph rendering is the VFX layer's job (ADR-0008 TelegraphRenderer; art bible §3.4). This
spec's binding interface: the HUD never overlays or duplicates telegraph shapes, never uses
rings/arcs/starbursts, and surrenders any contested pixels (telegraphs rank above all HUD).
The counter-hit arc and slam disc are never occluded by Zone FX edge flashes (flashes stay in
the outer margin band).

## Acceptance Criteria

- [ ] AC-HUD-01 (performance): HUD layer renders within its share of frame budget — zero UI
  layout invalidations during steady 200-enemy combat with combo/momentum churn (Profiler,
  AC-FB-06 companion).
- [ ] AC-HUD-02 (core purpose): combo pip state matches `combo_counter` on the same rendered
  frame for every increment, reset, and Finisher across a scripted 60-second sequence.
- [ ] AC-HUD-03 (reset legibility): on threshold damage, the shatter animation plays (never a
  fade), completes ≤300 ms, and is correctly interrupted by an immediately following increment.
- [ ] AC-HUD-04 (momentum same-frame): a threshold hit shows the emptied/halved meter in the
  same rendered frame as the hit flash (frame-step test, AC-FB-02 companion).
- [ ] AC-HUD-05 (reduced hit): elite hit at combo<2 shows the small grey "~" number and no
  flash; at combo≥2 shows the full treatment (logic seam test, AC-FB-04).
- [ ] AC-HUD-06 (navigation/exit): HUD visible in Running, frozen-dimmed under the pick
  overlay, hidden on end screens; reappears intact on resume.
- [ ] AC-HUD-07 (accessibility): muted + greyscale playthrough — a tester can report combo
  state, momentum-full, and hit severity correctly at 200-enemy density; no element below its
  minimum size/contrast; ≤3 flashes/sec measured on a worst-case hit burst.
- [ ] AC-HUD-08 (error state): with a presenter's event source disconnected, elements render
  zeros and a `[DESIGNER-ERROR]` is logged (never an invisible/missing element).

## Open Questions

| # | Question | Resolution path |
|---|---|---|
| OQ-01 | Bottom-centre cluster placement (RATIFY) — does it survive 200-enemy density with the player near the bottom edge? | Prototype screenshot test; fallback = split (combo bottom-centre, momentum left-of-centre) |
| OQ-02 | Parry-ready tick: useful skill-feedback or noise? (new pattern, not in any GDD — additive proposal from combat-feedback-ui Rule 7's peek API) | Prototype A/B; cheap to remove |
| OQ-03 | Damage numbers for normal hits: none vs small? (GDD default: Finisher-focused) | Prototype feel pass |
| OQ-04 | Projectile-direction edge flash needs attacker position in the DamageTaken presentation payload | Architecture: fold into TR-CC-007 payload extension at story time |

## Pattern Library Updates Needed

New patterns to add to `interaction-patterns.md`: **Parry-Ready Tick** (read-only input-state
reflector), **Directional Edge Flash** (DamageType-keyed, throttled, non-vignette). Flagged, not
yet added.

# ADR-0009: Runtime UI Architecture — UI Toolkit, Event-Driven Binding

## Status
Accepted
*(Proposed and Accepted 2026-07-04, autonomous session — user directive to proceed; lean review mode.)*
*(Amended 2026-10-10 by /architecture-review: navigation input route fixed to the InputSystemUIInputModule bound to InputManager's single action-asset instance — resolves blocking finding N2 (double-handling / clone-asset hazard); UI Toolkit implementation gotchas added to Risks from the independent engine-specialist audit.)*

## Date
2026-07-04

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS |
| **Domain** | UI |
| **Knowledge Risk** | MEDIUM — UI Toolkit production-ready for runtime UI in Unity 6 (post-cutoff maturity); `VisualElement.transform` deprecated 6.2+ |
| **References Consulted** | `docs/engine-reference/unity/modules/ui.md`, `breaking-changes.md`, `deprecated-apis.md` |
| **Post-Cutoff APIs Used** | UI Toolkit runtime (UIDocument/UXML/USS) as verified in `modules/ui.md` |
| **Verification Required** | HUD update cost at 60fps (no per-frame layout thrash); `VisualElement.style` transforms only (never `.transform`) |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | ADR-0003 (UI subscribes to the presentation event tier) |
| **Enables** | Combat Feedback UI, Run UI, Upgrade Selection UI implementation |
| **Blocks** | All UI stories |
| **Ordering Note** | UGUI is not used for any new screen; TextMeshPro only if a world-space text need appears (damage numbers may instead be VFX) |

## Context

### Problem Statement
Three UI systems (combat HUD, run frame + screens, pick screen) need one implementation technology and one data-binding pattern. Technical preferences mandate UI Toolkit; run-ui.md left polling-vs-events open (OQ-01); keyboard navigation with no hover-only interaction is a platform requirement.

### Constraints
- HUD readable at 200 enemies; updates must not cost layout each frame
- Keyboard/gamepad navigation with visible focus on all interactive screens
- Screen flow owned by RSM (UI renders, never decides)

### Requirements
- UXML/USS per screen; C# controllers subscribe to gameplay events and write to elements
- Event-driven binding — no per-frame polling of HP/XP/momentum
- Hit-feedback throttling and damage-number pooling at density

## Decision

**All runtime UI is UI Toolkit. One `UIDocument` per logical screen group, driven by thin C# presenter classes that subscribe to the presentation event tier (ADR-0003) and mutate elements only on change.**

1. **Structure**: `RunHud.uxml` (HP/XP/timer — Run UI), `CombatHud.uxml` (combo counter, momentum meter, hit feedback layer — Combat Feedback UI), `SelectScreen.uxml`, `PickScreen.uxml`, `EndScreen.uxml` (win/death variants). USS themes shared; final styling gated on the art bible.
2. **Binding**: presenters subscribe to events (`DamageTaken`, momentum/combo change signals, level-up, timer tick at 1Hz for mm:ss) and write element properties on change. The run timer is the only per-frame-ish update and is throttled to whole-second changes. No presenter reads gameplay state in `Update()`.
3. **Navigation (amended)**: UI Toolkit focus system + explicit focus order per screen. Navigation input arrives through the panel's `InputSystemUIInputModule`, bound by `UiInputModuleBinder` to InputManager's single `PlayerControls` instance (ADR-0004 rules 7–8). Presenters handle `NavigationMoveEvent`/`NavigationSubmitEvent`/`NavigationCancelEvent` — **`IInputAuthority` is NOT a second navigation route** (implementing both double-handles every press). All UIDocuments share one `PanelSettings` asset = one navigation scope. **Explicit initial focus is set on every `Show()`** after layout resolves (`GeometryChangedEvent` or a one-frame schedule): a panel has no focused element by default, so keyboard-only flows (AC-US-04, AC-RU-07) fail silently without it; interactive elements need `focusable` + `tabIndex`, and `display:none` drops focus. Visible focus style mandatory (`:focus` USS); no `:hover`-only affordances.
4. **Flow**: RSM calls `Show/Hide` on screen presenters; presenters return user choices (pick, retry, general) via typed callbacks to RSM/Progression. Presenters never touch `Time.timeScale` or input contexts.
5. **Density protections**: hit feedback throttled (`hit_feedback_throttle_ms` 50); damage numbers from a pooled element pool (`damage_number_pool_size` 64) — or delegated to VFX Graph if UI cost is too high (prototype call).
6. **Deprecations respected**: styling transforms via `style.translate/rotate/scale` (never `VisualElement.transform`, deprecated 6.2+); no UGUI Canvas for new UI.

### Key Interfaces
- `IScreenPresenter { void Show(context); void Hide(); event Action<TResult> Completed; }` per screen
- HUD presenters: constructor-injected event sources (testable with fake events, no UIDocument required for logic-level tests)

## Alternatives Considered

### Alternative 1: UGUI (Canvas + TextMeshPro)
- **Pros**: Most familiar; huge documentation base (pre-cutoff knowledge is reliable).
- **Cons**: Deprecated-for-new-projects in Unity 6; per-element GameObject overhead; technical preferences mandate UI Toolkit.
- **Rejection Reason**: Project standard + Unity 6 direction. (Acceptable fallback only for world-space needs UI Toolkit can't cover.)

### Alternative 2: Per-frame polling HUD (`Update()` reads CurrentHp etc.)
- **Pros**: Simplest code.
- **Cons**: Wasted work per frame; hidden coupling; run-ui OQ-01 prefers events; contradicts the event architecture.
- **Rejection Reason**: Event-driven is strictly better here and resolves the open question.

## Consequences

### Positive
- One UI technology; HUD cost proportional to state *changes*, not frame count; presenters unit-testable
### Negative
- UI Toolkit runtime idioms are post-cutoff — implementation must lean on `modules/ui.md` rather than trained knowledge (verification cost accepted)

### Risks
- **UI Toolkit world-space limitation** (floating damage numbers) → prefer VFX Graph numbers; decide in prototype
- **USS/layout perf at density** → measure; batch class toggles, avoid per-frame style writes
- **UI Toolkit runtime input routing can bypass `IInputAuthority`** *(engine-specialist finding)*: `UIDocument` panels have their own event/navigation handling which may consume raw device input in parallel unless the panel's input provider is bound to the Input System UI action map (`InputSystemUIInputModule` / PanelSettings wiring). Verify the panel input source is the same `PlayerControls` UI map — otherwise focus navigation double-handles input outside the authority invariant
- **Layout passes triggered by same-value writes** *(engine-specialist finding)*: writes to layout-affecting properties trigger recalculation even when unchanged. Presenters must diff old vs new before writing (e.g., repeated `DamageTaken` with the same displayed `ceil(hp)` must not touch the element)
- **Bar fills via scale, not width** *(audit 2026-10-10)*: HP/XP fills use `style.scale` (scaleX with `transform-origin: left`), never `style.width` — width dirties layout every change. Do not copy the `modules/ui.md` health-bar example, which writes width
- **`usageHints` required on animated elements** *(audit 2026-10-10)*: pooled damage numbers and any transform/opacity-animated element get `UsageHints.DynamicTransform` (`DynamicColor` for color), or each change can re-tessellate and re-upload the subtree
- **Per-hit string allocation** *(audit 2026-10-10)*: `label.text = $"{n}"` allocates — damage numbers use a cached number-string table or the zero-GC validation target is unachievable
- **HUD hit-testing** *(audit 2026-10-10)*: set `pickingMode = Ignore` on HUD roots so pointer hit-testing skips them
- **UITK animations run on real time** *(audit 2026-10-10)*: schedulers/USS transitions ignore `timeScale` — correct for the pick screen (which exists only at timeScale 0, per TR-UI-003) but means hit-stop will not pause damage-number animations; drive sim-time-bound visuals from events if that reads wrong
- **World-space panels may exist in 6.3** *(audit 2026-10-10, unverified)*: UI Toolkit world-space support possibly landed ~6.2/6.3 — re-check before building the VFX Graph damage-number fallback; and avoid UITK's runtime data-binding system (its polling trigger modes defeat the event-driven goal)

## GDD Requirements Addressed

| GDD System | Requirement | How This ADR Addresses It |
|------------|-------------|--------------------------|
| combat-feedback-ui.md | UI Toolkit HUD; throttling; pooling; DamageType-keyed feedback | Presenter + density protections |
| run-ui.md | OQ-01 polling vs events → event-driven preferred | Decision rule 2 |
| upgrade-selection-ui.md | Keyboard/gamepad navigation, focus visible, no hover-only | Navigation rule 3 |
| technical-preferences | UI Toolkit for runtime UI; keyboard menu navigation | The decision itself |

## Performance Implications
- **CPU**: Event-driven writes ≈ dozens/frame worst case; retained-mode rendering
- **Memory**: Pooled damage-number elements; small
- **Load Time / Network**: N/A

## Migration Plan
Greenfield. Art-bible styling lands as USS-only changes.

## Validation Criteria
- AC-RU-02 (bar ceiling expand), AC-US-04/AC-RU-07 (keyboard-only flows), AC-FB-06 (frame budget at density)
- Profiler: zero UI layout invalidation storms during steady combat

## Related Decisions
- ADR-0003 (event tier), ADR-0004 (UI action map), ADR-0008 (damage numbers may move to VFX)

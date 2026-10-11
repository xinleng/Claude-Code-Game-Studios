# ADR-0008: Telegraph and Combat VFX Rendering

## Status
Accepted
*(Proposed and Accepted 2026-07-04, autonomous session — user directive to proceed; lean review mode.)*
*(Amended 2026-10-10 by /architecture-review: Decision item 1 rewritten to the single-system-per-shape authoring pattern — the original text contradicted this ADR's own elevated risk note and implementers would have followed the wrong half; bounds/capacity/sim-clock/warm-up rules added; overlay guidance updated to render-graph-native options.)*

## Date
2026-07-04

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS (URP) |
| **Domain** | Rendering |
| **Knowledge Risk** | HIGH — RenderGraph replaces CommandBuffer passes; URP Compatibility Mode hidden + deprecated in 6.3; VFX Graph replaces legacy particles |
| **References Consulted** | `docs/engine-reference/unity/modules/rendering.md`, `breaking-changes.md` (6.3 note), `deprecated-apis.md` |
| **Post-Cutoff APIs Used** | `RecordRenderGraph` (URP render passes, 6.x) — only if/when a screen-space overlay is built; VFX Graph GPU instancing (verified current) |
| **Verification Required** | Draw-call count at 200 active telegraphs (Frame Debugger); VFX Graph instancing actually batches per shape asset |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | ADR-0001 (VFX triggered by manager/system events), ADR-0003 (presentation event tier) |
| **Enables** | VFX & Audio implementation; Combat Feedback UI telegraph rendering |
| **Blocks** | Telegraph rendering stories; any screen-space effect story |
| **Ordering Note** | The Compatibility-Mode path must never be used — it is deprecated in 6.3 |

## Context

### Problem Statement
Up to 200 simultaneous telegraph indicators must render within a ≤1500 draw-call budget while staying the most readable thing on screen (Pillar 1). Per-enemy `ParticleSystem` instances would cost ~400 draw calls for telegraphs alone (enemy-definition Contract #4). Separately, any screen-space overlay (parry flash vignette, Finisher shake post-process) touches the URP render pipeline, where Unity 6.3 has removed the legacy path.

### Constraints
- ≤1500 draw calls total; 60fps; shape-first colorblind-safe telegraphs
- One telegraph VFX asset per shape type (ring, thick ring, line, arrowhead, 270° arc, filled circle)
- Counter-hit arc must never be occluded or culled

### Requirements
- GPU-instanced telegraph rendering; per-instance transform/progress/color parameters
- Screen-space overlays only via RenderGraph API
- Priority culling: post-process throttles first, telegraphs never (VFX hierarchy: Finisher > parry burst > rest; counter-hit arc reserved)

## Decision

**Telegraphs and combat effects use VFX Graph with GPU instancing (one graph asset per telegraph shape; one per hero effect). Screen-space overlays, if built, use a `ScriptableRendererFeature` with `RecordRenderGraph` — the Compatibility Mode path is forbidden. SRP Batcher and the GPU Resident Drawer are enabled in the URP asset.**

1. **Telegraphs (amended 2026-10-10)**: one VFX Graph asset per shape, authored as **one system rendering N instances with exactly one `VisualEffect` component per shape** — never pooled per-telegraph components (200 separate components of the same asset do not auto-batch; each is its own render item). Per-instance data (position/rotation/scale, color, start time, duration) feeds through an exposed GraphicsBuffer indexed by `particleId`; a CPU slot allocator recycles slots keyed by `AttackEvent`/resolve signals (empty slot = scale 0). **Progress is computed in-graph** from `(simTime − start) / duration`: upload start+duration once at `Show`, one global sim-clock float per frame (`IGameClock.RunTime`) — never per-instance progress uploads, and never the VFX internal clock (would drift from the parry window under pause/hit-stop). **Bounds are manual and padded to the arena** with update-when-offscreen, or the whole system culls when its auto bounds leave the frustum — which would violate the never-cull rule. **VFX `capacity` ≥ `telegraph_concurrent_cap`** (overflow silently drops particles; value TBD via prototype — AOQ-03); the cap culls lowest-priority telegraphs only; the counter-hit arc and Warlord slam are never culled. Verify batching in the Frame Debugger before building all six shapes; also check whether 6.3's asset-level VFX Instancing Mode covers these graphs (if so, the pooled-component pattern becomes a viable alternative — compare both). Fallback per-shape remains `Graphics.RenderMeshInstanced` (Alternative 2).
2. **Hero effects**: per-general Finisher VFX (3 distinct assets — Ring ≠ PointBurst mandated), parry burst ring, dodge trail — VFX Graph, pooled, triggered from the presentation event tier.
3. **Screen-space overlays (amended)** (parry vignette; note the "Finisher shake" is a camera effect, not a post-process): implement only if the prototype shows they're worth budget. When built, prefer render-graph-native options that need no custom C# pass — Volume overrides (Vignette/ColorAdjustments weight) or URP's Full Screen Pass Renderer Feature with a Fullscreen Shader Graph. A custom `ScriptableRendererFeature` pass, if ever required, implements `RecordRenderGraph(RenderGraph, ContextContainer)`; any `Execute(ScriptableRenderContext, ref RenderingData)`-style pass is a build error by convention (forbidden pattern). (Context wording corrected: the Compatibility Mode path is hidden + deprecated in 6.3, not removed.)
4. **Budget allocation** (initial, tune in prototype): telegraphs ≤ ~50 draw calls (instanced per shape ≈ 6 shapes × state variants), hero effects ≤ ~40, environment/enemies/UI take the rest; overlays are the first thing throttled (vfx-audio rule).

### Key Interfaces
- `TelegraphRenderer.Show(in AttackEvent evt)` / `Resolve(attackerId)` — presentation-side, subscribes to the AttackEvent multicast
- VFX assets addressed by `FinisherShape`/telegraph-shape enums from `GameData`

## Alternatives Considered

### Alternative 1: Per-enemy ParticleSystem instances
- **Rejection Reason**: ~2 draw calls × 200 = 400 of the 1500 budget on telegraphs alone; explicitly forbidden (enemy-definition Contract #4); legacy particles deprecated in favour of VFX Graph.

### Alternative 2: Custom instanced mesh renderer (`Graphics.RenderMeshInstanced`) for telegraphs
- **Pros**: Maximum control, no VFX Graph dependency.
- **Cons**: Hand-rolled animation/progress shaders; loses VFX Graph authoring for the effects team; two systems for what VFX Graph does alone.
- **Rejection Reason**: VFX Graph already integrates with URP's render graph and gives instancing + authoring; bespoke path adds cost without need. (Falls back here only if VFX Graph instancing disappoints in the draw-call verification.)

### Alternative 3: Screen-space overlays via URP Compatibility Mode
- **Rejection Reason**: Hidden + deprecated in 6.3 (breaking-changes.md); building on it guarantees migration pain.

## Consequences

### Positive
- Telegraph cost scales with shape count, not enemy count; 6.3-proof rendering path; readability priorities encoded in the culling policy
### Negative
- VFX Graph authoring skill required (solo dev learning curve — time-boxed: game-concept allots 2 days for the two hero effects)

### Risks
- **Instancing requires single-system authoring, not pooled components** *(engine-specialist finding — elevated)*: spawning 200 separate `VisualEffect` component instances of the same asset does **not** auto-batch into one draw call; each component is its own render item. The ≤~50-draw-call telegraph budget depends on authoring each telegraph shape as **one VFX Graph system rendering N instances** (per-instance data fed via a GraphicsBuffer/point-cache/GPU-event pattern), with one `VisualEffect` instance per shape. Verify this authoring pattern in the Frame Debugger before building all six shapes; fallback per-shape is `Graphics.RenderMeshInstanced` (Alternative 2)
- **Instancing assumption fails for a shape** → Frame Debugger verification early; fallback per-shape to Alternative 2
- **Overlay budget creep** → overlays are opt-in after prototype, first-throttled by rule
- **First-use hitch on hero effects** *(audit 2026-10-10)*: pooled `VisualEffect` hero graphs can hitch on first compute/PSO compilation — warm each effect once during load
- **GPU Resident Drawer prerequisites** *(audit 2026-10-10)*: requires the Forward+ rendering path (pin it in the URP asset) and does not batch VFX Graph or skinned meshes — the 200 enemy skinned meshes draw outside this ADR's 50+40 allocation and must be counted in the prototype's draw-call baseline
- **GraphicsBuffer lifetime** *(audit 2026-10-10)*: release per-shape buffers on teardown; leaked buffers survive domain reload

## GDD Requirements Addressed

| GDD System | Requirement | How This ADR Addresses It |
|------------|-------------|--------------------------|
| enemy-definition.md | Contract #4: VFX Graph instancing, one asset per shape; RecordRenderGraph for overlays | Adopted verbatim |
| vfx-audio.md | VFX hierarchy, budgets, cull priority, shape-first accessibility | Budget allocation + cull policy |
| combat-feedback-ui.md | Telegraph rendering per shape/duration; counter-hit arc priority | TelegraphRenderer contract; never-cull list |
| game-concept.md | ≤1500 draw calls, 60fps | Instancing strategy + allocation |

## Performance Implications
- **CPU**: VFX Graph simulation on GPU; CPU cost = instance property updates (~200 writes/frame worst case)
- **Memory**: A few VFX Graph assets + pooled instances — small
- **Load Time**: Negligible
- **Network**: N/A

## Migration Plan
Greenfield. Placeholder-quality graphs first (behavioural requirements only); art-bible-conformant assets after `/art-bible` + `/asset-spec`.

## Validation Criteria
- Frame Debugger at 200 telegraphs: instanced batches per shape; total draw calls ≤1500 (AC-VA-05)
- AC-FB-05 / AC-VA-03 (arc readability) at density — manual QA

## Related Decisions
- ADR-0003 (presentation events), ADR-0011 (audio mix, deferred), AOQ-03 (`telegraph_concurrent_cap`)

# VFX & Audio

> **Status**: Approved (self-review 2026-06-19)
> **Author**: xinleng + Claude Code agents
> **Last Updated**: 2026-06-19
> **Implements Pillar**: Pillar 1 (Readable Chaos)

> **Note**: Detailed per-asset specs (dimensions, prompts, palettes) are produced by `/asset-spec system:vfx-audio` **after the art bible is approved**. This GDD defines the behavioural and quality requirements the asset pass must satisfy and the performance constraints it must respect.

## Overview

VFX & Audio is the sensory layer that turns mechanics into feel: the Finisher's hero effect, the parry burst, every enemy telegraph, the 1→2→FINISH audio escalation, the per-general Finisher signatures, the counter-hit's distinct danger cue, Lu Bu's knockback displacement, the dodge whoosh, and the wave-escalating music. It is subordinate to readability — every effect must *serve* Pillar 1, never fight it: a Finisher flourish that obscures an enemy telegraph is cut, no matter how cool (game-concept design test). It implements the visual/audio requirements mandated across all gameplay GDDs (Combat Core, Combo, Parry, Enemy Definition, Dodge, General Characters) within the performance budget (≤1500 draw calls, 60fps, ≤200 telegraph VFX via GPU instancing). It owns asset production direction and the audio mix; it owns no gameplay rules.

The player experiences VFX & Audio as the *juice* — the crunch of a Finisher, the snap of a parry, the readable storm of a 200-enemy battlefield that always sounds and looks like it makes sense.

## Player Fantasy

VFX & Audio is the feeling of a battlefield that *hits*. Two swings build with rising audio — hit, hit — and the third lands as a crash of light in the general's signature shape; a parry snaps the world with a ring of force and a crisp report; the horde telegraphs in honest, readable shapes while the music climbs with the pressure. The fantasy is **weight and clarity at once**: every action feels powerful *and* every threat stays legible. The hero moment — the first parry-into-Finisher of a run, the screen exploding in that general's specific geometry — is the anchor the whole sensory layer is built to deliver. When it works, the player doesn't notice the craft; they just feel unstoppable and in control.

## Detailed Design

### Core Rules

1. **VFX hierarchy (game-concept).** The **Finisher is the hero effect** (most prominent); the **parry burst is second**; all other VFX are subordinate. The 1→2→FINISH visual escalation must be unmistakable. No effect may obscure an enemy telegraph (the readability test that overrides "coolness").

2. **Per-general Finisher signatures.** Guan Yu (Ring) = spinning blade sweep, warm gold/red; Zhao Yun (Cone) = forward lance thrust, cool silver/blue; Lu Bu (PointBurst) = ground-slam shockwave + visible enemy displacement, earth/dark red. **Ring and PointBurst share geometry but MUST use distinct VFX + audio** (general-characters mandate) — if a player can't tell them apart, the pass failed.

3. **Parry burst.** An expanding **ring** (shape-distinguishable from the Finisher **starburst**, colorblind rule); a dedicated accent color reserved exclusively for parry feedback; a crisp, distinct success SFX; a visible momentum-meter glow that intensifies toward max.

4. **Telegraphs (shape-first, colorblind-safe).** Each enemy telegraph renders per Enemy Definition's shape/duration/color: Levy ring, Pikeman line, Archer arrowhead, Iron Captain thick ring / counter-hit 270° arc, Warlord slam filled circle. **No two types share both shape AND color.** The **counter-hit arc is the most important telegraph** and must remain readable at 200 enemies — no other VFX competes for its visual space. Telegraphs use **VFX Graph with GPU instancing**, one asset per shape (enemy-definition Contract #4) — no per-enemy ParticleSystem instances.

5. **Audio escalation.** The 3-hit combo escalates audibly: hit → hit → CRASH (game-concept core audio). The **counter-hit** (Iron Captain / Warlord) has a distinct, louder cue than standard attacks (enemy-definition). The **Warlord roar** has a unique state-change sound (not just another attack). Parry success SFX is crisp and instantly recognisable.

6. **Music.** Builds with wave intensity (escalation steps); a distinct stinger on elite spawns and a unique ominous cue on the boss spawn; a rising-tension shift in the final 5 minutes (game-concept).

7. **Hit + dodge feedback assets.** Per-`DamageType` hit responses (CounterHit shake/flash distinct from Standard; Slam fullscreen; Projectile arrow-hit) — produced here, triggered by Combat Feedback UI. Dodge = dash trail + whoosh + i-frame shimmer (Dodge System), subordinate to parry/Finisher.

8. **Accessibility.** Shape-over-color for all gameplay-critical VFX (telegraphs, parry ring, Finisher starburst). The combo-counter pulse and shatter are the primary (visual) combo signal; audio is additive, not sole (game-concept deaf/HoH path).

9. **Performance budget.** ≤1500 draw calls, 60fps / 16.6ms, ≤4GB. 200 telegraph VFX must fit via GPU instancing (≤ a small fraction of the draw-call budget). A screen-space overlay (parry flash vignette, Finisher screen-shake post-process) — if added — must use a URP `RecordRenderGraph` pass per Unity 6.3 (enemy-definition Contract #4).

10. **Presentation only.** VFX & Audio triggers on signals from gameplay systems; it owns no gameplay state, damage, or timing logic.

### States and Transitions

VFX & Audio is fully event-driven — it has no state machine. Each gameplay event maps to an effect trigger:

| Trigger (signal) | Effect |
|---|---|
| Combo increment / Finisher (Combo) | counter pulse; on 3rd, the general's Finisher VFX + CRASH audio |
| Parry success (Parry) | expanding ring + crisp SFX + momentum glow |
| `DamageTaken` + `DamageType` (Damage & Health) | per-type hit feedback |
| `AttackEvent` (Enemy AI) | telegraph VFX for `TelegraphDurationMs`; counter-hit = distinct cue |
| Dodge (Dodge System) | dash trail + whoosh + shimmer |
| Wave step / elite / boss spawn (Wave Spawner) | music intensity shift / stinger |
| Lu Bu PointBurst (Combat Core) | knockback displacement VFX |

### Interactions with Other Systems

| System | Data In (trigger) | Interface Owner |
|---|---|---|
| **Combo System** | Finisher resolved; counter transitions | Combo owns state; VFX&Audio renders |
| **Parry System** | parry success; momentum value | Parry owns state |
| **Damage & Health** | `DamageTaken` + `DamageType` | Damage & Health owns events |
| **Enemy Definition / Enemy AI** | `AttackEvent` telegraph shape/duration; roar | Enemy Definition owns shapes |
| **General Characters** | selected general's Finisher shape (asset selection) | General Characters owns geometry |
| **Dodge System** | dodge start | Dodge owns the dash |
| **Wave Spawner** | wave step / elite / boss spawn | Wave Spawner owns timeline |
| **Combat Feedback UI** | shares hit/telegraph assets | UI triggers; VFX&Audio produces |
| **Art Bible** *(pending)* | palette, style constraints | Art Bible gates asset production |

## Formulas

VFX & Audio owns no balance formulas — it is a presentation/asset layer. Its quantitative constraints are the **performance budgets** (≤1500 draw calls, ≤16.6ms frame, ≤4GB memory, ≤200 instanced telegraph VFX) enforced as acceptance criteria, not formulas.

## Edge Cases

- **If many Finishers/bursts fire in a short window:** Effects are pooled and the most prominent (Finisher > parry burst > hit feedback) takes visual priority; lower-priority VFX may be culled to protect readability and the draw-call budget.
- **If the counter-hit arc would be visually buried by infantry telegraphs:** The arc reserves a distinct visual layer/color so it is never occluded (enemy-definition; Pillar 1).
- **If draw calls approach 1500 at peak density:** Telegraph VFX instancing and effect culling keep within budget; a screen-space post-process is the first thing throttled, not gameplay-critical telegraphs.
- **If a player is colorblind:** All gameplay-critical reads (telegraph shape, parry ring, Finisher starburst) are distinguishable by shape alone; color is reinforcement only.
- **If audio cues stack (many simultaneous hits):** The mix prioritises gameplay-critical cues (counter-hit, parry success, Finisher) over ambient hit sounds; ambient hits are ducked/limited.
- **If the art bible is not yet authored when implementation begins:** Use placeholder VFX/audio that satisfy the *behavioural* requirements (shape, distinctness, timing); final assets follow art-bible approval + `/asset-spec` (game-concept: placeholder sprites/sound acceptable for MVP).

## Dependencies

| System | Direction | Nature | Interface |
|---|---|---|---|
| **Combo System** | Combo → VFX&Audio | Hard | Triggers Finisher VFX + 1→2→FINISH audio; counter pulse/shatter assets. |
| **Parry System** | Parry → VFX&Audio | Hard | Triggers parry burst ring + SFX; momentum glow. |
| **Damage & Health** | Damage & Health → VFX&Audio | Hard | Triggers per-`DamageType` hit feedback. |
| **Enemy Definition / Enemy AI** | Enemy → VFX&Audio | Hard | Telegraph shapes/durations (VFX Graph); counter-hit + roar audio. |
| **General Characters** | General Characters → VFX&Audio | Hard | Per-general Finisher signature assets (Ring ≠ PointBurst). |
| **Dodge / Wave Spawner / Combat Feedback UI** | systems → VFX&Audio | Hard | Dodge feedback; music/stingers; shared hit/telegraph assets. |
| **Art Bible** *(pending)* | Art Bible → VFX&Audio | Hard | Palette, style, and asset standards gate final asset production. |

## Tuning Knobs

| Knob | Default | Safe Range | Affects |
|---|---|---|---|
| `vfx_draw_call_budget` | 1500 | fixed (perf budget) | Total draw-call ceiling shared with all rendering |
| `telegraph_concurrent_cap` | (TBD via prototype) | — | Max concurrent telegraph VFX before culling lowest-priority (enemy-definition OQ-03) |
| `finisher_screen_shake` | on | on/off + magnitude | Finisher impact shake (subordinate to readability) |
| `music_intensity_steps` | per wave step | — | Number of music intensity tiers across the run |
| `hit_sfx_voice_limit` | 16 | 8–32 | Max simultaneous hit SFX voices (mix protection) |

*Referenced, not owned: telegraph shapes/durations (Enemy Definition); Finisher geometry (General Characters); `DamageType` (Damage & Health); performance budgets (technical-preferences); palette/style (Art Bible).*

## Visual/Audio Requirements

This system *is* the visual/audio requirements layer. It consolidates and produces the assets mandated by every gameplay GDD: Finisher (hero, per-general), parry burst (ring), telegraphs (VFX Graph, shape-first), per-`DamageType` hit feedback, dodge, Lu Bu knockback displacement, the 1→2→FINISH + counter-hit + roar audio, and wave-escalating music. All within the performance budget and the colorblind/deaf-HoH accessibility rules. Final palettes and style follow the **art bible** (not yet authored) + `/asset-spec`.

## Acceptance Criteria

| # | Criterion | Story Type | Gate |
|---|---|---|---|
| AC-VA-01 | **GIVEN** Guan Yu and Lu Bu both fire Finishers in one session, **WHEN** each plays, **THEN** they use distinct VFX assets (different GUIDs) and distinct audio — they read as different moves. | Visual | ADVISORY |
| AC-VA-02 | **GIVEN** the 3-hit combo, **WHEN** it resolves, **THEN** audio escalates hit → hit → CRASH and the Finisher VFX is the most prominent on-screen effect. | Visual | ADVISORY |
| AC-VA-03 | **GIVEN** an Iron Captain counter-hit among 200 enemies, **WHEN** its telegraph renders, **THEN** the 270° arc is shape-distinct, colorblind-safe, and not occluded by infantry telegraphs. | Visual | ADVISORY |
| AC-VA-04 | **GIVEN** the parry burst and the Finisher flash, **WHEN** both occur, **THEN** they are distinguishable by shape (ring vs starburst) without relying on color. | Visual | ADVISORY |
| AC-VA-05 | **GIVEN** 200 enemies with active telegraph VFX, **WHEN** rendered, **THEN** telegraph VFX use GPU-instanced VFX Graph (no per-enemy ParticleSystem) and total draw calls stay ≤ 1500 at 60fps. | Performance | ADVISORY |
| AC-VA-06 | **GIVEN** a colorblind player, **WHEN** any gameplay-critical effect renders (telegraph, parry ring, Finisher), **THEN** it is distinguishable by shape alone. | Visual | ADVISORY |
| AC-VA-07 | **GIVEN** the Warlord roar, **WHEN** it plays, **THEN** it has a unique state-change sound distinct from any attack cue. | Visual | ADVISORY |

## Open Questions

| # | Question | Owner | Resolution Path |
|---|---|---|---|
| OQ-01 | What is the `telegraph_concurrent_cap` before VFX readability/perf breaks at 200 enemies? | Prototype | enemy-definition OQ-03 — establish empirically; may require culling lowest-priority telegraphs. |
| OQ-02 | Final palettes, style, and asset standards depend on the art bible (not yet authored). | Art Bible | Run `/art-bible`, then `/asset-spec system:vfx-audio` for per-asset specs. |
| OQ-03 | Does a Finisher screen-shake post-process fit the draw-call/perf budget at peak density, or must it be throttled? | Prototype/Perf | Validate; the screen-space pass is the first thing throttled, never telegraphs. |
| OQ-04 | Is the 2-day VFX time-box (game-concept scope risk) enough for the Finisher + parry burst to feel good? | Production | Monitor; these two effects are the non-negotiable quality targets for MVP. |

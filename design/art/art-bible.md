# Art Bible — Dynasty Survivors

> **Status**: PROVISIONAL — authored autonomously 2026-10-10 under the user's standing proceed
> directive. Items marked **(RATIFY)** are taste calls made without user input: **ratify or veto
> them before any asset production begins** (`/asset-spec` is gated on this).
> **Art Director Sign-Off (AD-ART-BIBLE)**: skipped — Lean mode.
> **Provenance**: drafted by art-director agent from the approved Visual Identity Anchor in
> game-concept.md; hard engine constraints by technical-artist agent; 43 binding UX constraints
> by ux-designer agent (§10). Conflicts resolved per the rule "technical constraints win in
> Asset Standards"; every override is recorded in §8.7.
>
> **(RATIFY) list**: Mandate Violet #B14DFF as the Finisher accent + Parry Azure #3DD8FF ·
> Lu Bu as the gold-dominant general · inverted-hull outline on the player · square arena
> footprint · combo counter + momentum meter at bottom-centre · font picks (Cinzel / Barlow
> Semi Condensed / Noto Serif TC Black) · soft instanced blob shadows (vs hard baked disc).
>
> **Working assumptions to calibrate in the first camera prototype**: camera pitch 50–55°,
> 1 unit = 1 m, infantry ≈ 60–80 px tall at 1080p, scale ladder (infantry 1.0× ≈ 1.6u,
> generals 1.2×, Iron Captain 1.6×, Warlord 2.5×). **Min-spec pinned 2026-10-10: GTX 1650 / Ryzen 5 3600 /
> 16GB RAM** (technical-preferences.md) — §8 performance gates are measured against it.

---

## 1. Visual Identity Statement

**Anchor (approved in game-concept.md, unchanged):** *Three Kingdoms Melee Clarity. The
battlefield of legends: everything moves, everything reads.*

**The one-line rule:** *If a pixel does not help the player decide what to do next, it must be
quieter than a pixel that does.*

Every art decision is judged by this rule. Cut anything that fails it, however cool it looks
(Pillar 1: Readable Chaos).

### 1.1 Value, then hue, then detail (Pillar 1)
The general, the crowd and the floor are separated first by luminance, second by hue, third by
surface detail. A figure that only works by its detail has failed.
- The floor is the darkest, quietest, most warm-neutral layer.
- Infantry are a mid-dark, desaturated mass.
- The general is the only thing in the world carrying saturated red, jade or gold plus bright trim.
- **Design test (1-second test):** greyscale a 200-enemy frame, shrink to 25%. A new viewer must
  find the player, the nearest elite and the most urgent telegraph in one second, with no colour.

### 1.2 One hero at a time; escalation is monotonic (Pillars 3, 2)
Combat feedback has a strict rank: Finisher, then parry burst, then everything else. The
1 → 2 → FINISH chain strictly grows in screen area, brightness and duration at every step.
- Only the Finisher uses the white-hot-core starburst. Only the parry burst uses the thin azure
  ring. Nothing else may borrow either shape, colour or scale.
- **Design test (escalation test):** peak-frame stills of hit 1, hit 2, FINISH, and a parry —
  by bright-pixel area each step ≥1.5× the previous; Finisher ≥3× the parry ring; a muted-audio
  viewer must rank them correctly.

### 1.3 The telegraph is a contract (Pillars 2, 1)
Each threat has one shape that means one thing, and no other effect may imitate it. Shape
identifies the threat; colour only reinforces; brightness only tells how close the strike is.
Telegraphs always render above every other ground-plane effect.
- **Design test (stencil test):** fill every telegraph solid white on black; a first-time viewer
  must name the threat class and tell a parry prompt from a dodge prompt. Repeat under
  deuteranopia, protanopia and tritanopia filters.

### 1.4 Pillar 4 corollary: loss is legible
The defeat state always shows what killed the player: the killer and its telegraph shape stay
lit while the rest of the world goes to ink. No generic "you died" tableau.

### 1.5 Visual priority stack
When two elements compete for pixels, the higher wins (z-sorting, bloom allocation, throttling):
1. Telegraphs (counter-hit arc and Warlord slam disc first) · 2. The player general ·
3. Finisher starburst · 4. Parry ring · 5. Elite/boss bodies · 6. Infantry mass ·
7. Hit sparks and damage numbers · 8. Environment.

The Finisher is the biggest spectacle in the game — and still sorts beneath telegraphs; its
bright phase caps at ~350 ms so it can never hide an incoming attack for long.

---

## 2. Mood & Atmosphere

**Global lighting constants (all states):**
- One directional key light, fixed azimuth from screen upper-left (315°) — shadows fall lower-right,
  never onto the top-edge HUD and never hiding the player's lower body.
- Only the key's elevation, colour and intensity change by state; elevation carries time of day.
- **The world never goes cool.** Saturation and exposure carry the emotional shifts. The only
  saturated cool hues on screen are the two reserved accents (§4.3).
- Shadow tint is warm dark (#2A211C), never black, never blue.
- State changes are URP Volume weight blends (near-zero runtime cost; blend timing = technical-artist call).

### Summary matrix (every state differs on ≥3 columns)

| State | Key colour | Key elev. | Saturation | Exposure (EV) | Vignette | Haze | Energy (0–5) |
|---|---|---|---|---|---|---|---|
| General select | #FFC878 candle spot | 70°, narrow cone | 0.90 | −1.0 | 0.45 | 0 | 1 |
| Standard waves (0–5 min) | #FFE6C0 | 55° | 1.00 | 0 | 0.15 | 0.05 | 2 |
| Escalating (5–15 min) | #FFC97A | 55°→30° | 0.90→0.80 | 0→−0.2 | 0.25 | 0.15→0.35 | 3→4 |
| Elite encounter | unchanged | unchanged | 0.70 (crowd only) | −0.4 | 0.40 | unchanged | 4 |
| Boss / final (15–20 min) | #FFB060 | 22° (long shadows) | 0.85, contrast +0.15 | −0.3 | 0.35 | 0.30 | 5 |
| Upgrade pick (paused) | frozen | frozen | 0.35 | −1.2 | 0.60 + radial spot | frozen | 0.5 |
| Victory | #FFF0D0 | 65° | 1.15 | +0.5 | 0.10 | 0 | 3→1 |
| Defeat | #E8DCC8 | 80° (short shadows) | 0.0–0.1 | −0.8 | 0.80, closing | 0 | 0 |

**2.1 General select** — reverence and appetite: a night war-council; near-black ambient
(#1B1714), one candle-gold spot on the highlighted general, the other two at 40% with gold rim.
Three lacquer-black plinths, ink-wash mountain silhouette behind. The selected general's Finisher
footprint previews as a hatched Imperial-Gold fill — never a glowing ring (that would teach
"ring = parry"). Ceremonial, hushed, lacquered, weighty. The only dark, indoor, spot-lit state.

**2.2 Standard waves (0–5)** — confident mastery: clear early morning, cream key (#FFE6C0),
moderate contrast, haze near zero, arena edge crisp. Perimeter row of tall Han banners and
rammed-earth wall in full view. Open, dusty, brisk, legible, bright.

**2.3 Escalating (5–15)** — flow under pressure: a slow blend morning cream → harsh high amber
(8:00, hottest and flattest) → late-afternoon ochre with lengthening shadows. Far-field dust up
to 0.35 haze; a "sacred radius" ~6u around the player stays haze-free so the fight never goes
murky. The arena visibly wears: banners tatter, scuff/track decals accumulate (capped, §6.5),
broken spears at the perimeter. Dust-choked, relentless, ochre, surging, dense. The one state
that changes over time.

**2.4 Elite encounter (Iron Captain)** — dread and focus: key unchanged; the *crowd* desaturates
to 0.70, −0.4 EV, vignette 0.40 (no coloured spotlight — telegraph colours are never
contaminated). Grade blends in ~0.8 s on spawn, out ~1.5 s after death. The elite arrives under
a tall black ink war-banner (~6u, bone-white seal glyph) with an irregular rectilinear splash
decal — **never a ground ring** (rings are parry/Captain vocabulary). Ominous, focused, hushed, heavy.

**2.5 Boss / final (15–20)** — mythic climax at last light: key at 22°, deep amber (#FFB060),
shadows ~2.5× length, dark ambient (#3A2E28), maximum contrast. The sunset lives in the key,
shadows and far rim; the playfield ground stays warm-dark neutral so red/orange telegraphs keep
contrast — the scene never washes red. Phase 2: slow ≤1 Hz vignette throb, the Warlord's
black-crimson war-standard visibly tears, slam fissure decals (dark umber-black, never red).
Last-light, towering, scorched, climactic, hard-edged.

**2.6 Upgrade pick (paused)** — calm agency (Pillar 3): the frozen world drops to ink-wash
(sat 0.35, −1.2 EV); the general keeps colour under a soft radial UI-layer spot (gradient
texture, no extra render pass; fallback: uniform desaturation — the player's saturated hues
carry). UI frame is warm Imperial Gold on Ink Night; slow ember motes only. Suspended, hushed,
deliberate, clear, golden.

**2.7 Victory** — earned triumph: key up to 65°, pale gold (#FFF0D0), haze cleared, sat 1.15,
+0.5 EV — the only state brighter and more saturated than baseline. Survivors dissolve into
drifting ink flecks (dithered dissolve, not alpha). The general stands under their raised banner;
a large 勝 seal stamp with Latin label beside it. Golden, cleared, resonant, triumphant, exhaled.

**2.8 Defeat** — sober and instructive (Pillar 4): near-total desaturation, neutral top light
(#E8DCC8, 80°), vignette closing to a pool around the fallen general over ~1.5 s, −0.8 EV. The
killer and its last telegraph shape stay lit in bone-white outline; cause-of-death readout beside
it. **No red damage vignette** — red is the threat colour. Hushed, ashen, ink-washed, sober, still.

---

## 3. Shape Language

### 3.1 The governing rule
**Curves, rings, arcs and radial bursts on the ground plane belong to gameplay signals. The
world is orthogonal.** Architecture, ground detail, props and UI are straight edges, rectangles
and chamfered corners — the signal layer stays free of noise (Pillar 1, principle 1.3). Units
are curve-and-edge mixes so they never read as environment.

### 3.2 Character silhouette philosophy
- **Thumbnail test:** every archetype identifiable from a solid-black 64 px silhouette, no colour,
  no detail. If two fail to separate, the weaker-trait one is redesigned.
- **One dominant primitive per archetype**, echoing its telegraph shape — the player learns
  "what it is" and "what it will do" as one idea.

| Archetype | Dominant primitive | Distinguishing silhouette trait | Echoes telegraph |
|---|---|---|---|
| Guan Yu | Wide trapezoid | Heavy low wide stance; long triangular beard; guandao's top-heavy question-mark profile | n/a |
| Zhao Yun | Narrow vertical needle | Lean, tall; long spear + trailing scarf streamer (diagonal flicker in motion); jian on hip | n/a |
| Lu Bu | Forked crown | Twin pheasant plumes ("V" above the head); ji halberd with crescent side-blade; broadest pauldrons; tallest general | n/a |
| Levy Soldier | Circle / ovoid | Short, round, hunched; soft cloth cap; no tall protrusions | Ring |
| Halberd Pikeman | Vertical line | Taller/thinner than Levy; pole tilted forward ~20°; leaf-shaped blade | Line |
| Fire Arrow Archer | Triangle / diagonal | Conical bamboo hat; crouched asymmetric bow stance; quiver | Arrowhead |
| Iron Captain | Square block | Flat-topped crested helmet; heavy square pauldrons; blocky plate; 1.6× | Thick ring, 270° arc |
| Warlord | Inverted trapezoid / T | Huge shoulders; tall rectangular back-banner above the crowd; 2.5× | Thick ring, arc, filled disc |

Anti-collapse rules: Zhao Yun vs Pikeman verticals separate by scale, streamer, tilt and value.
The Warlord's back-banner is rectangular, never a radial fan (the Finisher owns radial). **No
enemy carries gold or jade on diffuse** — those are the player's.

### 3.3 Signal shapes (hero + danger vocabulary)

| Signal | Shape | Rendering rule | Rank |
|---|---|---|---|
| Finisher | Radial starburst | 12 long + 12 short alternating rays, white-hot core, violet fringe; **never a closed circular outline** — footprint implied by ray length | Hero |
| Parry burst | Expanding ring | Thin crisp azure annulus, fast, player-outward; one ring only | Second |
| Levy telegraph | Ring | Thin stroke at chest height | Danger |
| Pikeman telegraph | Line | Directional streak, weapon tip toward player | Danger |
| Archer telegraph | Arrowhead | Above the unit pointing down — not on the ground | Danger |
| Iron Captain standard | Thick ring | ~2.5× the Levy stroke | Danger |
| Counter-hit (Captain/Warlord) | 270° arc | Widest ground shape; low-alpha inner fill; **reserved — no other effect may use a ground-plane arc** | Highest danger |
| Warlord slam | Filled disc | Hard edge, 25% fill, expanding; **no parry-prompt marker** — a dodge prompt | Highest danger |
| Hit spark / dust / damage number | Short streaks, soft puffs | Small, short-lived, low contrast | Supporting |

- **Melee swing trails** are tapered airborne 3D ribbons on the weapon — never ground arcs
  (keeps the 270° arc exclusive).
- Finisher variants: Guan Yu's ring-cleave is *hitbox geometry* — its VFX is still a full 360°
  starburst. Zhao Yun's cone = 60° fan of rays. Lu Bu's point-burst = starburst at impact with
  outward displacement streaks. No Finisher VFX draws an outline circle.
- **Ring-vs-ring risk** (parry ring vs Captain's ring): separated by colour, thickness, origin
  and luminance. If playtests show confusion, thin the parry ring — never change the telegraph.

### 3.4 Telegraph rendering sheet (shape contract; gameplay values live in GDDs)

| Telegraph | Colour | Stroke / fill | Progress cue (no shape change) |
|---|---|---|---|
| Levy ring | Muted gold #C9A24A | ~0.08u stroke, ~1.2u dia., no fill | Brightness 60%→100% |
| Pikeman line | Orange-red #FF6A2B | Tapered streak ~1.8u | Same |
| Archer arrowhead | Orange #FF9A2E | Solid ~0.5u, floats above head | Same |
| Captain thick ring | Threat red #FF3030 | ~0.22u stroke | Same |
| 270° arc | Threat red #FF3030 | ~0.3u band to 1.5u radius, low-alpha fill | Same + hard leading edge |
| Slam disc | Threat red #FF3030 | Hard edge, 25% fill, 2.0u radius | Fill sweeps outward |

Brightness is only a progress cue, never identity. All telegraphs are emissive, hard-edged,
drawn above every other ground effect. Telegraphs beyond ~8u from the player draw at 50% alpha
and thinner strokes so nearest threats dominate (tunable).

### 3.5 Environment geometry
Rectilinear and orthogonal; flat floor (height detail <0.15u inside the play area — decals
only); vertical mass at the perimeter, framing but never occluding the fight.

### 3.6 UI shape grammar
Chamfered rectangles, bamboo-slip vertical strips, square seals. **No UI element uses a ring,
arc or starburst** — the HUD never duplicates world signals. The combo counter is three
chamfered pips whose shatter breaks them into angular shards. (Pillars 1, 3.)

---

## 4. Color System

### 4.1 Primary palette (hex targets; tune against the greyscale test)

| Name | Hex | Y | Role |
|---|---|---|---|
| Ink Night | #1B1714 | 0.01 | Deep shadow, UI panels, outlines |
| Dust Umber | #3A312A | 0.03 | Arena ground base (ground range Y 0.025–0.06) |
| Levy Drab | #6A6054 | 0.12 | Infantry cloth/leather (range 0.09–0.16) |
| Lacquer Red | #C23A2B | 0.15 | Player diffuse red (matte, never emissive) |
| Jade | #3FB58D | 0.36 | Player diffuse green, HP bar, restoration |
| Imperial Gold | #E8B73A | 0.51 | Player trim, XP, UI focus and frames |
| Bone White | #F0E8D6 | 0.80 | Highlights, text, hit flash, Finisher core, Zhao Yun's armour |

**Value ladder:** ground 0.03 → infantry 0.12 → player red 0.15 → jade 0.36 → gold 0.51 →
bone 0.80. Lacquer Red sits near infantry in value, so **every general carries ≥25% of visible
surface in a bright element at ≥3:1 against infantry** (gold, bone or jade) — that is why Zhao
Yun is red over bone-silver, never red alone.

### 4.2 Semantic vocabulary

| Colour | Means | Never used for |
|---|---|---|
| Lacquer Red (diffuse, dark) | The player's authority and identity | Threat |
| Threat Red (emissive #FF3030) | Lethal incoming attack | Player identity, HP, UI decoration |
| Imperial Gold | Player power, progress, XP, focus | Enemy diffuse; damage |
| Jade | Life, restoration, HP | Enemies; telegraphs |
| Bone White | Impact, highlight, text, legend | Telegraph identity colours |
| Parry Azure (reserved) | Parry feedback — nothing else | Everything else |
| Mandate Violet (reserved) | Finisher — nothing else | Everything else |

**Emissive-vs-diffuse rule:** the player's red is matte and dark; threat red is bright, emissive,
above the bloom threshold. If a test frame ever confuses them, adjust value/glow — never shape.
**HP is jade, not red** — red means "threat" everywhere on screen.

### 4.3 The two reserved accents (RATIFY)

| Accent | Hex | Y | Shape backup | Why |
|---|---|---|---|---|
| Parry Azure | #3DD8FF | 0.57 | Thin expanding ring | Cool + bright against a warm dark world; reads as light blue under red-green colorblindness |
| Mandate Violet | #B14DFF | 0.22 | 24-ray starburst, white-hot core (Y 0.8) | Imperial purple = celestial mandate, fits "heaven-sent finisher"; the only saturated violet in the game |

**Colour-blind behaviour:** protan/deutan — azure vs violet separate by luminance (0.57 vs 0.22),
shape, white core and ≥3× scale; warm telegraphs shift olive/yellow away from both. Tritan —
violet-vs-threat-red is the weakest pair: rely on shape and origin (player-centred starburst vs
enemy rings/arcs) plus the white core. Both accents are emissive past the bloom threshold;
nothing else on screen is both cool and saturated.

### 4.4 Arena colour temperature rule
The world is warm: ground/props/architecture in the 20–45° hue band at ≤25% saturation. **No
cool hue above 15% saturation in the world except the two accents** (weapon steel ≤10% slate).
Cool saturated pixels ≤5% of average screen area. Grading never pushes the world cool.

### 4.5 UI palette divergence
World = toon-lit warm browns + emissive signals. UI = flat lacquer and paper: Ink Night panels
~85% opacity, Bone White text, Imperial Gold frames/focus, Jade for HP/positives. UI never uses
emissive threat orange/red (single exception: boss bar frame in dark non-emissive crimson
#7A1F1A). Accents appear in UI only for their own state: azure on the momentum meter, violet on
the FINISH pip. Text contrast: Bone on Ink ≈14:1; HUD minimum 7:1, secondary 4.5:1.

### 4.6 Colour-blind failure pairs and backups

| Pair | Who fails | Backup |
|---|---|---|
| Warm telegraph family (gold/orange/red) | Protan, deutan | Shape (ring/line/arrowhead/arc/disc) + position (arrowhead floats) |
| Parry Azure vs Mandate Violet | Protan, deutan | Ring vs starburst, luminance, scale, white core |
| Violet vs threat red | Tritan | Shape + origin |
| Lacquer Red vs Jade | Protan, deutan | Never co-present in a run (one general); select screen uses silhouettes + labels; gold trim bands separate within a general |
| Jade vs Dust Umber | Deutan | Value (0.36 vs 0.03) |
| HP low state | All | Frame fracture glyph + hatched fill + bold numeral — never colour alone |
| Momentum full | All | Seal-stamp + notched outline, not just glow |
| Reduced elite hit | All | Smaller grey number, "~" prefix, no flash |

Validate with Coblis-type filters on prototype screenshots (all three types) + the greyscale test.

---

## 5. Character Design Direction

### 5.1 Player-general treatment
Each general owns one player hue as ≥50% of visible surface, the other two as trim — three
instantly distinct generals. Only the general gets the thin Imperial Gold rim (toon fresnel) and
an inverted-hull outline (RATIFY) — cheap for one character, forbidden on 200. Generals are the
only humans with visible, stylised, named faces.

| General | Dominant hue | Trim | Silhouette hook |
|---|---|---|---|
| Guan Yu | Jade (robe/cape) | Gold, red sash | Wide trapezoid, long beard, guandao; heaviest lowest stance |
| Zhao Yun | Lacquer Red over bone-silver (#C9CCCB) tabard | Gold | Needle, scarf streamer, spear + hip jian |
| Lu Bu (RATIFY) | Imperial Gold (gilded black armour) | Red cape, jade accent | Twin plumes, ji, broad pauldrons; brightest general |

Weapons are modelled 1.3× oversize (guandao, jian, qiang, ji — historical references).
Plume/streamer secondary motion is the one place extra animation effort goes.

### 5.2 Per-type reads at game-camera distance (infantry ≈60–80 px tall)

| Type | Must read at distance | May be lost |
|---|---|---|
| Levy | Round mass, cloth cap, raised-arms tell | Face, belts, fingers |
| Pikeman | Tall thin line, forward blade, lowered-point tell | Armour plates |
| Archer | Conical hat, bow-draw tell, flame at arrow tip | Quiver detail |
| Iron Captain | Block silhouette, crest, 1.6×, dark iron value | Rivets |
| Warlord | T silhouette, tall banner, 2.5× | Ornament |

- **Telegraph animation pose**: every attack has a one-pose silhouette-changing tell that begins
  before the VFX (Levy arms up; Pikeman lowers pole; Archer draws; Captain plants and winds;
  Warlord raises the banner) — Pillar 1 insurance if VFX is throttled.
- Enemy materials: infantry diffuse Y 0.09–0.16, ≤25% sat. Elite iron-dark (Y 0.04–0.10) with
  slate. Boss black-crimson (#4A1512) + iron + bone-white trim. **No gold, jade or bright red on
  any enemy diffuse.**

### 5.3 Pose and expression style
Stylised-heroic: ~5.5 heads infantry, 6 heads generals; hands/feet/heads 10–20% oversize; wide
planted stances. Infantry are anonymous (faces shadowed/covered — a crowd, not characters).
Generals are stoic and proud, weapons grounded at idle, strong anticipation poses and smear
frames. Elites/boss get lowered brows and a heavier, slower gait — a different threat before any
telegraph.

### 5.4 Detail-retention philosophy for 200 on screen
Keep silhouette, headgear, weapon length and 2–3 colour blocks; drop everything else early. **No
distance LOD chains ship** (camera distance is near-constant — §8.3): characters are LOD0-only.
The LOD1 spec below exists solely as the *density fallback* that activates only if the §8
performance gates trip: >150 active enemies → farthest drop to ~250-tri LOD1 at half animation
rate. What survives any fallback: silhouette, hat/helmet, weapon length, dominant colour block.
No normal maps on infantry — lighting is the toon ramp + vertex colour.

---

## 6. Environment Design Language

### 6.1 Setting
One arena: a Han-dynasty military parade ground (jiaochang), seen from above.

### 6.2 Architectural vocabulary (all rectilinear, low-poly)
Que gate towers (2–4, paired, tiled eaves) · rammed-earth hangtu walls with horizontal layer
bands (the perimeter backbone) · beacon/watch towers on far edges · palisades and juma barrier
dressing · camp dressing (tents, weapon racks, war drums, grain stores, carts, bronze ding,
signal banners). Structure palette: dark grey tile, red-lacquered posts, ochre earth — **no gold
or jade on architecture**. Tile-end roundels only above head height, never on the ground.

### 6.3 Arena layout (RATIFY)
Square or slightly rectangular footprint. **Never a circular boundary** — a giant ground ring
competes with the parry ring.

### 6.4 Texture philosophy: hand-painted stylised, not PBR
Flat-palette hand-painted albedo lit by a 3-band toon ramp; **no metallic/normal/roughness maps**
on infantry or environment. Why: PBR micro-detail is noise against the signal layer (Pillar 1);
costs texture count and bandwidth invisible at 60–80 px characters; and a solo 3D artist moves
fastest with palette atlas + vertex colour. Hero assets (player/elite/boss) may use a small
hand-painted 1024 for gradients and wear — still no normal map. Emission only for
gameplay-relevant glow (Archer arrow tip, banner seals).

### 6.5 Prop density rules
- **The combat field is sacred**: central ~70% of the arena has nothing above 0.15u — only low
  decals (scuffs, tracks, grass tufts) within ±10% of ground luminance.
- Perimeter band (outer 15–20%) holds all tall props and architecture.
- Decal cap ~40 persistent wear decals, pooled, oldest fades first.
- Ground contrast: patches ≥4u wide only; adjacent patches ≤1.3:1 luminance difference; no
  high-frequency noise at the 0.5u scale.
- Camera-near props dither-fade (opaque dither, no alpha blending).
- Interior dressing is decals-only by art default; collision policy belongs to level/AI design.

### 6.6 Environmental storytelling without text
Progressive wear through the run (§2.3) lets the player read time from the ground and banners.
Silent narrative props: empty racks, a broken cart, a cold brazier, a split drum — a
recently-fought-over camp, no names, no text. Banners are undyed/ochre with black ink seals (no
faction colour — faction blues/greens would break the cool-hue budget). **No readable world
text** and no calligraphy a viewer could mistake for UI.

---

## 7. UI/HUD Visual Direction

### 7.1 Screen-space HUD, edge-anchored (not diegetic)
A fixed top-down camera with 200 enemies cannot hold a diegetic readout steady; edge-anchored
elements sit in predictable places and never compete with telegraphs (Pillar 1); and the combo
counter is the deaf/HoH primary signal, needing fixed position, large size, high contrast. The
HUD borrows the world's material language (lacquer, bamboo, seal red) but not its placement.
The central ~70%×75% of the screen is a chrome-free play zone (§10 rule 2).

**Layout:** HP top-left (chamfered jade bar + `ceil(hp)` numeral) · timer top-centre · XP a thin
full-width Imperial Gold bar on the bottom edge with the level numeral at left · combo counter +
momentum meter bottom-centre above the XP bar (RATIFY — final placement is `/ux-design` OQ-01;
default is fixed-position, never player-following).

**State visuals (visual-side answers to the accessibility open items; `/ux-design` owns final specs):**
- **Combo pips (1, 2, FINISH):** three chamfered pips, each filling with a 120 ms pulse; FINISH
  turns violet; on reset the pips shatter into 6–8 angular shards over ~250 ms.
- **Momentum full:** azure fill + gold seal stamp on the frame + notched outline — a shape
  change, not only glow.
- **HP low (<25%):** frame fracture glyph, hatched fill, numeral bold and ~15% larger. No red.
- **Reduced elite hit (combo <2):** smaller dimmer grey number, "~" prefix, no flash.
- **Upgrade lane marking:** icon + text label + corner-notch shape — never the reserved accents,
  never colour alone.
- HP/XP bar art is 9-slice/tileable (the HP ceiling expands with S1 — no fixed-length
  ornamental frame); the ghost-drain layer uses a distinct tone/pattern, not colour alone.

### 7.2 Typography (RATIFY fonts)
Personality: inscribed, stately, clean — Han stele lettering for display, clean semi-condensed
sans for gameplay numbers.
- **Display/titles (Latin):** Cinzel (OFL) — flared capitals, generous tracking; screen titles
  and general names only.
- **HUD/body (Latin):** Barlow Semi Condensed (OFL), SemiBold; **tabular lining figures** so the
  timer never jitters.
- **CJK accent:** Noto Serif TC Black (OFL), static ~30-glyph subset (關羽, 趙雲, 呂布, 勝, 敗…).
  Decorative only, always paired with a Latin label, never carries required information.
- **Hierarchy at 1080p (minimums — no text-scaling setting exists in MVP):** screen title
  64–96 px · timer 40 px · combo numeral/FINISH 56 px (≥64 px pip cluster) · HUD numerals ≥28 px ·
  card title 32 px · card body ≥24 px (one line — overflow is a content error) · key hints
  ≥22 px · absolute floor 20 px.
- Calligraphic/brush faces never appear on numerals, stats, descriptions or the combo counter
  (reserved for the ≥48 px Victory/Defeat banner at most).
- Implementation: UI Toolkit SDF font assets; verify Latin fallback when CJK is missing.

### 7.3 Iconography
Seal-carved: solid single-weight glyphs, thick stroke, centred in a square cartouche. Bone White
on Ink Night with one Imperial Gold highlight. 128 px source, white-on-transparent, USS-tinted,
one 2048 atlas. Silhouette-first: must read at 48 px. Upgrade icons use weapon/banner/drum/scroll
motifs. Keycap glyphs are rounded-rect caps; gamepad glyphs swap in on active-device change.

### 7.4 UI animation feel (Pillar 3)
In 80–140 ms, out 60–100 ms, ease-out cubic; nothing in combat fades slower than 150 ms.
Confirm = ink-stamp (1.15→1.0, one overshoot, ~120 ms). Card reveal: 120 ms slide with 40 ms
stagger. Screen transitions: brush-wipe ≤200 ms. Combo pip pulse 1.0→1.25→1.0 over 120 ms.
Method: USS transitions on translate/scale/opacity only — never layout properties (ADR-0009).
Pick-screen animations run on **unscaled time** (the screen only exists at timeScale 0).
Anti-strobe: nothing flashes >3×/sec; hit feedback obeys `hit_feedback_throttle_ms`.
Persistent HUD chrome is static — no ambient animation (drifting clouds, flicker, scrolling).

### 7.5 Keyboard navigation and visible focus (mandatory)
- **Focus indicator:** 3 px Imperial Gold outline + 4 px Ink Night outer outline + corner
  brackets + 5% scale-up — shape-based, ≥3:1 against surroundings, identical for hover and focus.
- **Unfocused-frame rule (anti gold-on-gold):** unfocused cards/buttons carry at most a 1 px
  muted lower-value gold frame and never corner brackets or scale — no unfocused state may
  resemble the focus treatment (§10 rules 12–13).
- Explicit initial focus on every `Show()` (Guan Yu on select; leftmost card on pick; Retry on
  death). Focus order = visual left-to-right. Pick screen: keys 1/2/3 + Enter + arrows, no
  timeout, no wrap at the ends. Primary vs secondary buttons differ by shape/weight/position,
  hit areas ≥44 px tall. The HUD itself has no focusable elements and `pickingMode = Ignore`.

---

## 8. Asset Standards

**Binding order: technical constraints win over art ideals.** Both are recorded; §8.7 lists
every conflict and its resolution. Full technical derivations (frame sketch, VAT gates, memory
math) live in the technical-artist proposal — key binding numbers are reproduced here.

### 8.1 Naming (binding: PascalCase per technical-preferences.md)
`LevySoldier.fbx` · clips `LevySoldier@Move.fbx` · `LevySoldier.prefab` · materials
`CrowdToon.mat`, `CrowdToonFlash.mat` · textures `CrowdAtlasAlbedo.png` · controllers
`InfantryController.controller` · bones `UpperArmL`/`ThighR`/`WeaponR` (no dots/spaces —
Blender `.L/.R` renamed on export) · VFX `VfxTelegraphRing.vfx`, `VfxTelegraphArc270.vfx`,
`VfxParryRing.vfx`, `VfxFinisherStarburstGuanYu.vfx` · UI sprites `UiBtnPrimaryFocus.png` ·
shader `DynastyToon.shadergraph`. Canonical names: `GuanYu`, `ZhaoYun`, `LuBu`, `LevySoldier`,
`HalberdPikeman`, `FireArrowArcher`, `IronCaptain`, `Warlord`. Folders:
`assets/art/{characters,environment,props,weapons}`, `assets/shaders`, `assets/vfx`.
No underscores, spaces or dots in asset names. Numbered variants use two digits (`WeaponRack01`).

### 8.2 Poly budgets (triangles — HARD caps binding; targets are the aim)

| Category | Target | HARD cap | Notes |
|---|---|---|---|
| Infantry (each of 3 types; ≤200 combined) | 300 | **500** | Weapon/gear merged into body mesh; one renderer |
| Iron Captain (≈12 concurrent — confirm cap) | 1,000 | **1,500** | Weapon merged |
| Warlord (1) | 3,000 | **4,000** | Phase 2 = same mesh; data/material change only |
| Player general (×3, one active) | 2,200 | **3,000** | Shared skeleton across all three |
| Player weapon (separate, socketed) | 400 | **600** | Silhouette via edge loops, not detail |
| Arena environment (total) | 80k | **150k** | Static; ground is one mesh |
| Props (each; set pieces 800) | 150 | **300** | Static, instanced/GRD-batched |
| Archer projectiles (≈60) | 24 | **48** | Instanced path |

**LOD policy: LOD0 only — no LODGroup.** Camera distance is near-constant; distance LODs buy
nothing. The only LOD is the density fallback (§5.4), and only if the performance gates trip.

### 8.3 Textures (1080p target; 64 texels/m crowd+env, 128 texels/m player)

| Category | Max res | Layout |
|---|---|---|
| Infantry (all 3 types) | **512²** | ONE shared atlas, one material; per-type 256² UV rects |
| Elite | 512² | |
| Boss | 1024² | |
| Player general (each) | 1024² | Body + weapon in one atlas → shared material |
| Arena ground | 1024² tiling | |
| Arena structures | 2048² | Trim sheet shared by all structures |
| Props | 1024² | One shared atlas |
| VFX (noise/flipbook/mask) | 512² | Telegraph shapes procedural in VFX Graph, not textured |
| UI atlas | ≤2×2048² | ADR-0009 domain |

No 4096 anywhere. Formats: BC1 flat/palette albedo, BC7 gradient albedo (heroes, structures),
R8 SDF masks; **no normal maps in MVP** (tangents stripped). Mips ON for world, OFF for UI/VFX;
trilinear; aniso 2× ground only; mip streaming OFF; linear colour space. Atlas padding 4 px with
dilation; palette swatches 2 px inset. Per-unit colour variation via vertex colour / palette
rows — never duplicated textures. **Texture memory target ≤128 MB, hard ceiling 256 MB**
(estimated actual ≈52 MB); meshes ≤64 MB; clips ≤32 MB.

### 8.4 Materials, shader, shadows (binding)
- Material ceilings: infantry **1 shared** across all three types (`CrowdToon` + `CrowdToonFlash`
  for hit flash via `sharedMaterial` swap); elite 1; boss ≤2; general 1 (weapon shares it);
  arena ≤8; scene total ≤25 opaque materials, ≤3 toon-shader variants.
- **One custom Shader Graph toon-lit shader — "DynastyToon"** — for all opaque world assets:
  atlas albedo × vertex colour, 2–3 band N·L step from the single directional key, optional rim
  (art call — ON for the player only), flash colour as a material property. ≤2 texture samples
  per fragment; keywords limited to main-light shadows. (URP Lit = wasted PBR cost; Simple Lit
  can't band cleanly.) Shader authoring → unity-shader-specialist.
- **Forbidden on crowd renderers: `MaterialPropertyBlock` and `.material`** (SRP Batcher breakage
  / instance leaks). No dissolve shaders on crowd (death = clip + pooled return; victory dissolve
  is dithered clip). No per-hit realtime lights; ≤4 non-shadow hero additional lights (Forward+
  pinned per ADR-0008).
- **Shadows — hybrid:** crowd infantry cast NO realtime shadows; crowd grounding via **soft
  instanced blob discs in ONE instanced draw** (RATIFY vs hard baked disc), slightly raised,
  alpha-blended. Realtime main-light shadows only for player/elite/boss/static casters: 1
  cascade, 2048² map, ~40 m distance, hard or lowest-soft. No lightmaps in MVP. Quality tiers:
  Low = blob-only, no shadow map.
- Post stack: Bloom (threshold tuned so only emissive signals glow), Color Adjustments (per-state
  grade), Vignette. No SSAO, DoF, motion blur or chromatic aberration.

### 8.5 Rigs and animation (binding)
- Bones: infantry **≤20** (target 16–18, shared base skeleton across Levy/Pikeman/Archer, 2 skin
  influences, `quality = Bone2`); elite ≤40; boss ≤60; generals ≤60 on ONE shared skeleton
  (clip sharing via Override Controllers). **Generic rigs everywhere — Humanoid forbidden**
  (retarget cost ×200). Optimize Game Objects ON (expose weapon socket + VFX anchor only).
  No IK, no Animation Rigging (player-only, prototype-justified), no extra layers on crowd, no
  bone colliders (hit detection is ADR-0007 queries), blend shapes OFF.
- Clip budgets: Levy ≤4 · Archer ≤5 · Pikeman ≤5 · Iron Captain ≤7 · Warlord ≤10 · generals ≤14
  each (shared where the skeleton matches). 30 fps, compression Optimal, scale curves stripped,
  seamless loops.
- **Telegraph clips are authored for time-scaling** (durations are data, 400–800 ms): the
  readable anticipation pose is reached and HELD at the end; Animator speed =
  `clipLength / telegraphDuration`.
- **No animation events for gameplay** — hit timing, parry windows and damage come from the
  manager clock only (cosmetic player hooks allowed).
- **Root motion FORBIDDEN** (`applyRootMotion = false`; in-place clips; Bake Into Pose ON for
  rotation/Y/XZ). Lunges, slams, knockback = manager velocity; animation sells, never drives.
  Crowd foot-sliding accepted — tune Move speed to data speed.
- **Animator stepping mode is an open prototype A/B, NOT a standard** (TD-PHASE-GATE
  2026-10-10): (A) engine-driven Animators with pause/hit-stop via `Animator.speed` /
  update mode, vs (B) manager-stepped (`Animator.enabled = false`, manager calls
  `Animator.Update(simDt)`). Risk on (B): script-driven `Animator.Update` evaluates each
  Animator individually on the main thread instead of Unity's batched multithreaded update and
  could consume the 3.0 ms gate alone. Measure both at 200 in a player build; ratify the winner
  as an ADR-0002 amendment.

### 8.6 Import/export and prefab conventions (binding)
Blender → FBX binary 2020, forward −Z, up Y, applied transforms, scale 1, triangulated, deform
bones only, leaf bones off, 30 fps bakes, `Model@Clip.fbx` convention. Importer: scale 1,
Read/Write OFF, Optimize Mesh ON, compression Low/Med, colliders OFF, blendshapes OFF, weld ON,
16-bit index, normals imported (hard edges via split normals), **tangents None**, lightmap UVs
OFF, UV0 only, vertex colours ON, Generic rig, Optimize Game Objects ON, materials never
extracted (assigned from project). Pivot = ground centre between feet (Y 0 at sole), forward +Z.
Nominal heights: infantry ~1.75 m, Captain ~1.95 m, generals ~1.85 m, Warlord ~2.4 m; mesh
footprint ≈ collision radius in `EnemyDataSO`. Pooled prefab shape: logic on the root (no
renderer), Animator + armature + SMR on a `Model` child; no runtime material instantiation.
**All references are direct serialized refs — no Addressables, no Resources.Load (ADR-0006).**

### 8.7 Art-ideal vs constrained standard (conflict record)

| # | Topic | Art-ideal | Binding standard |
|---|---|---|---|
| 1 | Infantry detail | 2–5k tris, modeled armour/faces | 500-tri cap; silhouette + colour blocking; pauldrons/helmets as silhouette shapes |
| 2 | Crowd animation | Per-unit Animator, blending, IK | ≤20-bone Generic, ≤4–5 clips, no IK; VAT path if gates trip. *(Stepping mode NOT binding — prototype A/B, see §8.5)* |
| 3 | Hero textures | 2–4K at 4K output | 1024² for 1080p; softness at 1440p/4K accepted (flat-shaded style hides it) |
| 4 | PBR hero look | Full PBR | None in MVP; flat toon, no tangents |
| 5 | Crowd shadows | Real shadow per soldier | Instanced blob discs (1 draw); real shadows heroes-only |
| 6 | Per-unit material FX | Tint/dissolve/emissive per unit | Shared materials + vertex tint + flash-material swap; per-unit FX reserved for heroes |
| 7 | Separate weapon meshes on enemies | Yes | Merged into body; only the player weapon is separate |
| 8 | Root-motion lunges, 8-way locomotion | Yes | Forbidden; in-place + manager velocity |
| 9 | Baked GI + per-hit lights | Yes | No lightmaps; ≤4 hero lights |
| 10 | Distance LOD chains + art-director LOD1/2 tiers | Yes | LOD0 only; density fallback only on gate trip |
| 11 | Art-director §8 naming (`Char_LevySoldier_Lod0`) | Underscore categories | PascalCase per technical-preferences (`LevySoldier.fbx`) |
| 12 | Addressables grouping | By category | Direct refs only (ADR-0006) |
| 13 | Player poly 3.5–5k / Warlord 6–9k (art-director) | | Caps 3,000 / 4,000 (tech) — ideals revisit post-MVP only with profiling headroom |

### 8.8 Performance gates and verification (binding; measure in a PLAYER build on min-spec)
- 200 skinned Animator-driven infantry is **conditionally viable** inside the §8.5 envelope
  (estimate 1.5–3 ms CPU). **VAT-ready authoring is mandatory from day one** (no bone-bound
  logic, ≤5 clips, flat-shaded, merged weapons) so the switch stays ~3–4 days.
- **Switch basic infantry to vertex-animation-texture rendering if ANY gate trips at 200 enemies:**
  (a) Animator+skinning CPU > 3.0 ms; (b) design wants >~250 concurrent; (c) total draws > 1,100.
- Draw-call sketch vs the 1,500 cap: ≈876 allocated (incl. 220 crowd, 50 telegraphs + 40 hero FX
  per ADR-0008, 100 shadow pass, 250 arena, 60 UI, 80 post) → ≈42% reserve. **1,100 is the
  early-warning line.** Draw calls are a proxy — CPU milliseconds are the binding constraint.
- Verify in the Frame Debugger: telegraph batching per shape, shadow-pass count, and whether URP
  issues a depth prepass in this configuration (would double opaque draws — prefer depth copy).
- Per-asset validation tool checks: tris, bones, materials, texture size, root-motion flags, naming.
- Open: pin the min-spec GPU/CPU; confirm elite concurrent cap; camera lock (§ header).

---

## 9. Reference Direction

| Reference | Take (specific) | Avoid |
|---|---|---|
| **Vampire Survivors** | Crowd read as colour/value families; smallest things (XP gems) are the brightest; player = cleanest highest-contrast shape; big calm legible level-up screen | Layered bright projectile noise — every upgrade stays inside the §1.2 hierarchy |
| **Dynasty Warriors stylised 3D spin-offs** | Officer = oversized weapon + back-banner/crest + colour-coded costume standing in near-identical uniform mass; crowd-as-colour-block | Realistic armour-detail noise; muddy brown-green palette; ultra-thin polearms that break up at distance |
| **Hades** | Hard-edged flat ground telegraphs on a dark floor; clearly rim-lit hero; boss entry framing; warm key with long readable shadows | Late-game effect stacking; painterly surface detail |
| **Okami** | Ink-bleed / brush-wipe transition grammar (defeat vignette, wipes, victory dissolve); sumi-e monochrome for paused/defeat states | Full-screen paper filter at runtime; pale parchment playfield (washes out emissive telegraphs); cursive brush lettering for required text |
| **Han-dynasty primary sources** (Mawangdui lacquerware, Wu Liang Shrine reliefs, tomb-brick architecture) | Vermilion-over-black lacquer for the player's red; flat profile-readable relief figures; que gates + rammed-earth walls | Ming/Qing opera excess; terracotta grey as palette; historical accuracy that hurts silhouette |

---

## 10. Binding UX Constraints (from ux-designer, 2026-10-10)

The full 43-rule constraint list is binding on all UI/HUD art. Summary of the load-bearing ones
(numbers = ux-designer rule IDs; [UX-PROPOSED] thresholds validate via screenshots at 200-enemy
density before sign-off):
- Edge-anchored HUD; central ~70%×75% chrome-free play zone; telegraph layer never occluded by
  HUD; counter-hit arc never culled (1–3)
- HUD backing ≥80% opacity or outlined; text ≥4.5:1 (7:1 for timer/HP/combo); fills ≥3:1 (4–5)
- Minimum sizes at 1080p: 28 px HUD numerals, 64 px combo cluster, HP bar ≥16 px×280 px (6);
  numerals always accompany bars (7); nothing animates ambiently on persistent HUD (9)
- ≤3 flashes/sec anywhere, including timer high-intensity state and Slam impact (10, 25);
  shake is a removable knob — never information-bearing alone (26)
- Focus = ≥2 non-hue cues, visible always, on every screen; unfocused states never resemble it;
  hover ≡ focus (11–14); primary/secondary buttons differ by shape, hit areas ≥44 px (15)
- Telegraph shapes are untouchable; slam carries no parry affordance; parry accent appears
  nowhere else in the UI (17–19)
- Combo shatter ≤~300 ms, interruptible, never a fade (20); momentum-full and HP-low get
  non-colour cues (21–22); lane colour never alone (23); text floor 20 px (24); all critical
  audio has a visual primary — fully playable muted (27)
- Ornament only on non-interactive outer frames of non-combat screens, never inside card content
  areas or on persistent HUD (28); decorative faces never on numerals/descriptions; tabular
  digits (29); HUD contrast verified against worst-case battlefield screenshots, not mockups (30)
- One job per colour; gold HUD framing must stay distinct in hue+value from both accents (31)
- Damage numbers Finisher-focused, pooled, distinct from HUD numerals (32); pause dim ~60%,
  battlefield visible (33, 36); card layouts 1/2/3 all composed (37); geometry previews
  diagrammatic, same scale, shape-distinct (38); control card edge-anchored, variable-length
  keycaps, never blocks input (39); HP bar extensible with distinct ghost layer (40); win/death
  differ by more than hue, death shows cause ≥24 px (42); HUD = simple USS-friendly assets (43)

---

## Follow-ups recorded by this authoring pass
1. `design/accessibility-requirements.md` says elites spawn "from spawn ring" — rename to
   "spawn mark" (the banner drop); ground rings are reserved vocabulary.
2. general-characters.md + vfx-audio.md should state explicitly: **no Finisher VFX draws a
   closed circular outline** (Guan Yu's ring-cleave is hitbox geometry; VFX is a starburst).
3. Melee swing trails are airborne ribbons, never ground arcs — note for vfx-audio.md.
4. The §7.1 state visuals answer the accessibility doc's open items (momentum full, HP low,
   reduced hit, lane coding) — `/ux-design` owns final specs.
5. Manager-stepped Animators (§8.5) need unity-specialist + ADR-0002 sign-off before adoption.

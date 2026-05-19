# Game Concept: Dynasty Survivors

*Created: 2026-04-24*
*Updated: 2026-04-24 — renamed; added Three Kingdoms theme; added combo finisher system; added army scale + map objectives as stretch goals*
*Status: Draft*

---

## Elevator Pitch

> A Three Kingdoms-themed vampire survivors-style auto-battler where you fight as a legendary general against endless enemy armies. Chain melee strikes into devastating combo finishers, parry incoming attacks to burst with power, and survive 20 minutes as your legend grows.

---

## Core Identity

| Aspect | Detail |
|---|---|
| **Genre** | Roguelite Survivors / Action |
| **Platform** | PC (Steam / itch.io) |
| **Target Audience** | Action-game fans, musou / Three Kingdoms fans, melee preference, 18-35 |
| **Player Count** | Single-player |
| **Session Length** | 20-30 minutes |
| **Monetization** | None (practice/learning project) |
| **Estimated Scope** | Small (3-6 weeks, solo) |
| **Comparable Titles** | Vampire Survivors, 20 Minutes Till Dawn, Dynasty Warriors (musou) |

---

## Core Fantasy

You ARE a legendary Three Kingdoms general — Guan Yu, Zhao Yun, or Lu Bu — surrounded by thousands of enemy soldiers. Every two strikes builds into a third, devastating finisher. Every parry converts their aggression into your momentum. The battlefield never empties, but you are a legend who cannot be stopped. Your reputation grows with every wave you survive.

---

## Unique Hook

Like Vampire Survivors, AND ALSO you play as a Three Kingdoms general with two interlocking active systems: a combo finisher that rewards sustained attack chains (hit, hit, FINISH), and a fighting-game parry that converts near-death moments into power bursts. Skilled players chain both. New players can survive without mastering both — but the legendary general fantasy is fully felt only when both systems click. The learning curve is forgiving; the systems are discovered, not bypassed.

---

## Player Experience Analysis (MDA Framework)

### Target Aesthetics (What the player FEELS)

| Aesthetic | Priority | How We Deliver It |
|---|---|---|
| **Sensation** (sensory pleasure) | 2 | Combo finisher VFX, parry flash, screen shake, satisfying audio on each hit tier |
| **Fantasy** (make-believe, role-playing) | 2 | Three Kingdoms general power fantasy — one warrior vs. an army |
| **Narrative** (drama, story arc) | N/A | No story in MVP |
| **Challenge** (obstacle course, mastery) | 1 | Combo timing, parry windows, escalating waves, momentum meter skill ceiling |
| **Fellowship** (social connection) | N/A | Single-player |
| **Discovery** (exploration, secrets) | 4 | Weapon variants; combo vs. parry upgrade build paths |
| **Expression** (self-expression, creativity) | 3 | Build identity shaped by upgrade category choices |
| **Submission** (relaxation, comfort zone) | N/A | This game is not relaxing |

### Key Dynamics (Emergent player behaviors)
- Players will naturally try to chain parries to maintain the momentum meter
- Players will learn enemy attack patterns over repeated runs and anticipate telegraphs
- Players will position to stay in attack range long enough to complete a 3-hit combo, creating tension between "push in to finish the combo" vs. "reposition to safety"
- Players will select upgrades that amplify their preferred style: combo-focused, parry-focused, or survivability
- Players will weigh "risk staying in range to parry/combo" vs. "reposition to dodge" every wave

### Core Mechanics (Systems we build)
1. **Auto-attack system** — character attacks enemies in melee range automatically; player focuses on positioning, the parry decision, and staying in range to complete combos
2. **Combo finisher system** — landing 2 consecutive melee hits builds a combo counter (visible UI); the 3rd hit triggers a Finisher: a stronger attack with larger area, bonus damage, and a distinct visual/audio flourish. Counter resets if the player takes damage or leaves attack range too long. A dedicated upgrade category (Combo) enhances finisher power, area, or adds status effects.
3. **Parry system** — one-button input; generous timing window; successful parry triggers an area burst (damage + brief invincibility) and fills the momentum meter; missing the window has no penalty (only opportunity cost)
4. **Momentum meter** — builds on successful parries; higher meter = larger burst; resets on taking damage (not on a missed parry attempt)
5. **XP / level-up / upgrade picks** — enemies drop XP on death; level up presents 3 readable upgrade options drawn from 4 categories: **Combo** (finisher power/area/effects), **Parry** (burst size/momentum gain), **Auto-attack** (base damage/speed/range), **Survivability** (HP/regen/damage reduction); each upgrade is one line, no tooltip required
6. **Enemy wave escalation** — spawns escalate in density and speed every 2 minutes; elite enemies with exaggerated telegraphs appear at 5/10/15 min as combined combo + parry skill tests

---

## Player Motivation Profile

### Primary Psychological Needs Served

| Need | How This Game Satisfies It | Strength |
|---|---|---|
| **Autonomy** | Upgrade category choices (lean Combo vs. lean Parry vs. balanced) + positioning decisions | Supporting |
| **Competence** | Visible momentum meter + combo counter = two live skill indicators; players watch themselves improve within a run | Core |
| **Relatedness** | Three Kingdoms general identity + weapon unlocks create a meta mastery arc across sessions | Supporting |

### Player Type Appeal (Bartle Taxonomy)

- [x] **Achievers** — Run score, weapon unlocks, "beat my best run" loop. Core appeal.
- [x] **Explorers** — Weapon variants; combo-path vs. parry-path upgrade discovery; Three Kingdoms general fantasy.
- [ ] **Socializers** — Not served (solo, no social systems in MVP)
- [x] **Killers/Competitors** — Personal best records; competing against your own previous runs

### Flow State Design

- **Onboarding curve:** First 3 minutes are low-density waves. A one-frame control card is shown at run start ("Attack: auto | Parry: [KEY]") — dismissible immediately, no pause required. The combo counter appears after the first 2 hits land — players discover it through play. A slow elite enemy with an obvious telegraph at minute 5 demonstrates the parry in context. No other tutorial pop-ups.
- **Difficulty scaling:** Wave density and speed increase every 2 minutes. Elite enemies at 5/10/15 min are the main skill checkpoints — designed to require both a full combo and a parry read to defeat cleanly. Final 5 minutes are high-intensity.
- **Feedback clarity:** Momentum meter fills and glows. Combo counter pulses visibly (1 → 2 → FINISH). Finisher hit numbers are large and distinct from normal hits. Audio escalates across the 3-hit sequence (hit, hit, CRASH).
- **Recovery from failure:** Death shows stats — total finishers landed, best parry chain, wave reached, time survived. Always answers "what should I try differently?"

---

## Core Loop

### Moment-to-Moment (30 seconds)
Character auto-attacks nearby enemies continuously. After 2 consecutive hits land, the combo counter primes — staying in range triggers a Finisher on the 3rd hit (bigger, louder, more satisfying). Enemies telegraph attacks with a flash on their body and a soft ring indicator around the player. Pressing parry at the right moment triggers an area burst and fills the momentum meter. Taking damage resets both the momentum meter and the combo counter. Constant decisions: push in to complete the combo, or reposition to safety?

### Short-Term (5-15 minutes)
Enemies drop XP → level up → pick 1 of 3 upgrades from a pool of 4 categories (Combo / Parry / Auto-attack / Survivability). Run identity forms around which categories you lean into. The first elite at minute 5 is the first real "do both" moment — requires a full combo to stagger and a parry to survive its counter-hit. Micro-goals emerge naturally: "I need to land one more finisher to fill my momentum meter."

### Session-Level (20-30 minutes)
A complete run is 20 minutes. Survive to the end → win screen with stats. Die → death screen with stats and "try again" prompt. The fixed timer provides a natural stopping point. The hook back: "I almost had it — if I'd taken the Combo upgrade at level 4 instead of HP…"

### Long-Term Progression
Per-run: Choose a Three Kingdoms general (weapon type) at run start — each has a different Finisher shape (Guan Yu: wide cleave ring; Zhao Yun: forward lance cone; Lu Bu: ground slam point-burst). Post-MVP: Generals unlock through run milestones. Meta goal: unlock all generals, then optimize each.

### Retention Hooks
- **Mastery:** "I landed a 5-parry chain AND completed 3 back-to-back finishers. Next run I'm going higher."
- **Curiosity:** "What does Lu Bu's point-burst finisher do with the Combo area upgrade stacked on it?"
- **Fantasy:** "I want to try Zhao Yun's build — spear cone feels different from Guan Yu's cleave."
- **Investment:** 20-minute sessions — failure costs 20 minutes, short enough to retry immediately

---

## Game Pillars

### Pillar 1: Readable Chaos
The game is always intense, never illegible. At 200 enemies on screen, the player should always know what to do next. Telegraph design, combo counter UI, audio mix, and visual hierarchy all serve this.

*Design test:* If a new system makes the screen harder to read, it fails this pillar. A flashy finisher effect that obscures enemy telegraphs is cut — no matter how cool it looks.

### Pillar 2: Skill Has a Lane
Upgrade specialization is a real lane — a Combo-focused build and a Parry-focused build should both be viable during standard waves. Skilled players who understand both systems and chain them together accelerate dramatically. Players who specialise into one lane can still complete runs.

**Exception — elites are intentional dual-system checkpoints.** Elite enemies at 5/10/15 min are designed to require both a full combo (to stagger) and a parry (to survive their counter-hit). This is the designed tension: build into one upgrade lane during waves, be tested on both systems at skill checkpoints. Elites are where single-lane specialization is tested and found wanting — intentionally.

*Design test:* Upgrade picks should not feel forced. A player committed to a Combo lane should not feel they wasted their build during standard waves. Elites will punish one-dimensional play — that is their explicit purpose as checkpoints, not a failure of the pillar.

### Pillar 3: Snappy Decisions
Upgrade picks are fast and readable, not agonizing. The game respects the player's time. Three options, plain language, immediate payoff.

*Design test:* If a new upgrade requires a tooltip longer than one line, it doesn't belong in this game. Complexity belongs in what upgrades do, not in how they're described.

### Pillar 4: Every Run Teaches You Something
Death should feel like data, not punishment. Players leave a failed run knowing what to try next time.

*Design test:* If dying just feels bad with no insight, the run structure or death screen is failing. Every death screen must answer: "what was different about this run?"

### Anti-Pillars (What This Game Is NOT)

- **NOT a build-crafter:** No synergy spreadsheets, no multi-item interaction chains. Upgrade categories are readable lanes, not hidden combos requiring a guide.
- **NOT a bullet hell:** We will NOT add projectile spam the player must dodge constantly. Melee range, combo timing, and telegraph reads are the survival mechanics.
- **NOT a story game:** No cutscenes, no lore drops mid-run. We will NOT pause momentum for narrative. The Three Kingdoms theme is aesthetic, not story-driven.
- **NOT a prestige treadmill:** No 50-hour meta grind. We will NOT gate content behind excessive replay requirements.

---

## Visual Identity Anchor

*(Full visual direction defined in `/art-bible`. This section captures MVP-critical decisions.)*

**Direction: Three Kingdoms Melee Clarity**
> The battlefield of legends — everything moves, everything reads.

Visual principles:
1. **High-contrast silhouettes in a Three Kingdoms palette** — player character (general in armor) and enemy soldiers must be instantly distinguishable at any density. Reference: red lacquer, jade, imperial gold for player; grey/brown infantry for enemies. Never same-value contrast.
2. **Finisher is the hero effect** — the Combo Finisher is the most visually prominent hit in the game. Parry burst is second. All other VFX are subordinate. The 1→2→FINISH visual escalation must be unmistakable.
3. **Telegraph is sacred** — enemy attack telegraphs must be readable in dense crowds. Use shape + color, not just brightness (colorblind-accessible). Three Kingdoms aesthetic should not compromise readability.

Color philosophy: Warm base palette (ink-wash battlefield tones) with a single accent color reserved exclusively for parry-related feedback, and a second distinct accent for the Finisher flash. Enemies use muted infantry palette. Visual noise is the enemy.

**Shape differentiation (colorblind safety):** The parry burst and Finisher flash must be distinguishable by shape, not color alone. Parry burst = expanding ring. Finisher flash = radial starburst. These shapes must be maintained even when Combo or Parry upgrades alter size or intensity.

**Deaf / hard-of-hearing path:** The combo counter UI pulse (1 → 2 → FINISH) is the primary combo feedback signal — the audio escalation is additive, not the sole signal. The combo counter reset must play a visible break animation (counter shatters, not just disappears) to communicate the damage-reset rule without a tooltip or audio cue.

---

## Inspiration and References

| Reference | What We Take From It | What We Do Differently | Why It Matters |
|---|---|---|---|
| Vampire Survivors | Auto-attack format, XP/level-up loop, wave escalation, 20-min timer | Active combo + parry layer, momentum meter, Three Kingdoms theme, no passive item synergy chains | Proves the genre formula works; our differentiation is the active skill layer + IP aesthetic |
| Dynasty Warriors (Musou) | Three Kingdoms setting, general characters, melee crowd combat feel, combo finisher structure, momentum-through-action | Survivors format instead of mission-based levels; roguelite upgrades instead of character leveling | Validates melee-forward crowd combat AND Three Kingdoms aesthetic as proven appeal |
| Street Fighter / Virtua Fighter | Parry/counter mechanics as skill-expression | One button, generous window — accessible to non-fighting-game players | Proves timing-based defense is satisfying when it feels crisp |
| Dead Cells | Visual clarity in chaotic combat, satisfying parry feedback, escalating hit sounds | Simpler progression, no metroidvania exploration | Validates that active defense in real-time chaos can feel outstanding |

**Non-game inspirations:** Romance of the Three Kingdoms (novel + TV series) — the mythic scale of one general vs. an army; the crowd-reading instinct of musou players navigating officer spawns; the "edge of survival" tension from fighting game sets.

---

## Target Player Profile

| Attribute | Detail |
|---|---|
| **Age range** | 18-35 |
| **Gaming experience** | Mid-core — familiar with action games and musou; doesn't need hand-holding |
| **Time availability** | 20-30 minute sessions; plays in short bursts |
| **Platform preference** | PC |
| **Current games they play** | Vampire Survivors, Dynasty Warriors, Romance of the Three Kingdoms games, fighting games |
| **What they're looking for** | Musou-scale melee fantasy with skill-expression depth in a short-session format |
| **What would turn them away** | Deep build theory-crafting; bullet hell dodge-spam; slow start; no Three Kingdoms flavor |

---

## Technical Considerations

| Consideration | Assessment |
|---|---|
| **Recommended Engine** | Unity — developer is familiar; strong 2D support; C# is production-standard; asset store for particles/UI |
| **Key Technical Challenges** | (1) Combo counter state tracking across frame-rate-variable auto-attacks; (2) Parry readability at 100+ enemies; (3) Input buffering fairness at high chaos; (4) Performance with large enemy counts |
| **Art Style** | 3D — top-down/isometric camera; Three Kingdoms stylized low-poly aesthetic (Dynasty Warriors stylized 3D spin-offs as reference); developer has 3D/realtime art background; weapon designs reference historical Chinese weapons (guandao, jian, spear) |
| **Art Pipeline Complexity** | Low-Medium — developer has 3D/realtime art background; Unity URP for stylized rendering; Finisher VFX and parry burst are the two non-negotiable quality targets for MVP |
| **Audio Needs** | Moderate — 3-hit audio escalation (hit → hit → CRASH) is core to combo feel; parry SFX must feel crisp; music builds with wave intensity |
| **Networking** | None |
| **Content Volume** | MVP: 1 map, 5 enemy types, 3 general characters (weapon types), 15-18 upgrades across 4 categories |
| **Procedural Systems** | Enemy wave spawning (escalating); upgrade pool draw (weighted random per category) |

---

## Risks and Open Questions

### Design Risks
- **Combo counter readability at scale** — combo counter UI must remain visible and readable when 100+ enemies are on screen and VFX are firing.
- **Combo vs. parry priority** — with two active skill systems, players may feel overwhelmed deciding which to focus on. Upgrade categories help by letting players commit to a lane.
- **Parry balance** — too weak and players ignore it; too strong and the combo system becomes irrelevant. Both systems need distinct, meaningful payoffs.
- **Upgrade monotony** — 15-18 upgrades across 4 categories; must feel varied enough that runs don't all converge on the same build.

### Technical Risks
- **Combo state tracking** — the combo counter must track "2 consecutive hits on enemies" reliably across auto-attack timing, enemy death (hit that kills doesn't feel wasted), and reset conditions. Needs careful implementation.
- **Input buffering on parry** — in a chaotic scene, a parry that "didn't register" will feel unfair. Input handling must be bulletproof with a slightly generous buffer window.
- **Performance with enemy counts** — Unity 2D with 200+ moving enemies and particle effects. Object pooling and simple physics must be implemented from day one.

### Scope Risks
- **Weeks timeline is aggressive** — first game + two active skill systems + weeks timeline means MVP must be ruthlessly scoped. Any feature beyond the core loop below is post-MVP.
- **VFX quality** — both the Finisher and parry burst must feel good. Set a combined time-box: 2 days maximum on both effects for MVP.

### Open Questions
- **Does the combo counter reset feel fair when the player takes damage mid-combo?** → Answer with a 2-hour combat prototype before building other systems.
- **Does adding the combo system on top of parry feel like depth or overwhelm?** → Answer with first playtest session.
- **What's the right momentum meter reset penalty?** → Losing ALL momentum on a hit may feel punishing; HALF may be too forgiving. → Playtest iteration.
- **How many enemies on screen before readability breaks?** → Answer empirically during prototype.

---

## MVP Definition

**Core hypothesis:** Players find the combo-finisher + parry-momentum loop engaging together, and want to improve both systems each run.

**Required for MVP:**
1. Character auto-attacks enemies within melee range automatically
2. Combo counter: 2 consecutive hits → 3rd hit triggers Finisher (stronger hit, larger area, distinct VFX/SFX)
3. Enemies telegraph attacks (enemy flash + player ring indicator); parry button triggers burst + fills momentum meter
4. Momentum meter resets on taking damage; combo counter also resets on damage; higher momentum = larger parry burst
5. XP drops → level up → 3 upgrade options drawn from 4 categories (Combo / Parry / Auto-attack / Survivability)
6. 20-minute timer → survive = win screen with stats; die = death screen with stats + retry
7. 3 general characters at run start (different Finisher shapes: Guan Yu ring cleave / Zhao Yun cone thrust / Lu Bu ground slam)
8. 5 enemy types (3 basic infantry, 1 elite per 5-min checkpoint, 1 final boss at 18 min)

**Explicitly NOT in MVP:**
- Meta unlock system (generals unlocked by default for MVP)
- Full Three Kingdoms art (placeholder sprites acceptable)
- Full sound design (3-hit SFX escalation + parry SFX is the minimum; rest is placeholder)
- Title screen beyond "Play / Quit"
- Controller support
- Settings / accessibility options

### Scope Tiers

| Tier | Content | Features | Timeline |
|---|---|---|---|
| **MVP** | 1 map, 5 enemy types, 3 generals, 15-18 upgrades (4 categories) | Core loop: combo finisher + parry + momentum + auto-attack + XP + 20-min timer | 3-6 weeks |
| **Vertical Slice** | 1 map, 5 enemy types, 3 generals, 24 upgrades | Core loop + run score screen + basic menus + 3-hit audio escalation | +2-4 weeks |
| **Expanded** | 2 maps, 7 enemy types, 5 generals, 36 upgrades | Meta unlocks, 5th general with unique Finisher mechanic, settings, Three Kingdoms art | +1-2 months |
| **Full Vision** | 3 maps, 10+ enemy types, 8+ generals | All features, polish, full Three Kingdoms aesthetic, Steam release candidate | Multi-month |
| **Stretch / Army Scale** | See Expansion Concepts section | Army + supply mechanics, squad-based enemies, map objectives (forts/villages/cities) | Post-launch |

---

## Expansion Concepts (Stretch Goals — Post-MVP)

These ideas are captured here so they aren't lost, but they are **explicitly out of scope for MVP and all current tiers**. Revisit after the core loop is validated.

---

### Expansion A: Army Scale

**Concept:** Instead of a lone general vs. individual enemies, the player commands a small army *led* by the general. The player's "health" is partly the general's HP and partly army size — losing soldiers weakens the force. Enemies are formations and squads, not solo units.

**New mechanics this enables:**
- **Army size as a resource** — soldiers are lost when the army takes hits. A depleted army deals less damage and covers less area. Replenishing soldiers becomes a mid-run priority.
- **Replenishment** — scattered surviving soldiers can be rescued on the map; supply convoys appear as timed objectives; certain upgrades let you recruit soldiers from defeated enemy groups.
- **Supply mechanics** — a supply resource fuels replenishment. Players must balance fighting (spending soldiers) with resupply (leaving combat range briefly). Creates the same risk/reward tension the base game has, but at army scale.
- **Formation-based enemies** — enemy groups behave as squads: a commander unit that buffs the formation, flankers that spread out, shield-bearers that block frontal attacks. The parry/counter system still applies — the general parries on behalf of the army, and a successful Finisher can break an enemy formation's cohesion.

**Why the parry still works at this scale:** The general is always the focal point. The parry window represents the general personally intercepting a dangerous enemy officer's strike before it reaches the army — thematically strong, mechanically identical to the MVP implementation.

**Design questions to answer before building:**
- Does army size make the core loop feel more strategic or just add a second health bar to manage?
- How do you telegraph an enemy squad's "attack" vs. an individual unit's attack? Does the parry window target the commander of the formation?
- Does replenishment feel like exciting resource management or annoying bookkeeping?

---

### Expansion B: Map Objectives — Forts, Villages, Cities

**Concept:** The arena map gains static locations — forts, villages, walled cities — that the player can move through, capture, or defend. These break the flat-arena survivors format with spatial objectives and create narrative beats within a run.

**New mechanics this enables:**
- **Forts** — defensive structures that slow enemy waves while the army holds them. Capturing a fort provides a brief breathing room and a bonus upgrade pick. Enemies will siege the fort if the player stays too long.
- **Villages** — neutral locations that can be defended for a replenishment reward (recruit soldiers) or lost to enemy raiding parties if ignored.
- **Cities** — major objectives that, if held until a timer expires, grant a powerful one-run buff (e.g., "Imperial Mandate" — all Finishers deal double damage for 3 minutes). High-value, high-risk targets that pull the player into dangerous territory.

**Ties to army scale:** Map objectives make the most sense once army size is a resource — fighting to hold a village to replenish soldiers is a meaningful choice. Without army scale, forts/cities are purely cosmetic objectives.

**Design questions to answer before building:**
- Does navigating between objectives conflict with the survivors formula (players expect enemies to come to them, not vice versa)?
- How does the map generate — procedurally placed, or hand-authored layouts?
- Does this require a camera system that shows more of the map, or stay tightly focused on the general?

---

## Formulas

All values are tuning-time constants unless marked runtime-dynamic.

### Combo Finisher

| Variable | Definition | Value |
|----------|-----------|-------|
| `base_attack` | Standard auto-attack damage | Tuning constant (set in Combat System GDD) |
| `finisher_damage` | Finisher hit damage | `base_attack × 2.5` |
| `base_radius` | Standard auto-attack hit radius | Tuning constant (set in Combat System GDD) |
| `finisher_radius` | Finisher AoE radius | `base_radius × 2.0` |

*Example:* If `base_attack = 10`, Finisher deals 25 damage. If `base_radius = 1.5 units`, Finisher AoE = 3.0 units.

### Momentum Burst

| Variable | Definition | Value |
|----------|-----------|-------|
| `base_burst` | Burst damage at 0 momentum | Tuning constant |
| `momentum` | Current meter value | Range: 0–100 |
| `burst_damage` | Burst damage at current momentum | `base_burst × (0.25 + momentum/100 × 1.75)` |

At `momentum = 0`: `burst = base_burst × 0.25` (floor — parrying is never worthless)
At `momentum = 100`: `burst = base_burst × 2.0` (ceiling)
Relationship: linear. Burst radius scales identically to burst damage.

### XP and Level-Up Pacing

Target: **12 level-up events per 20-minute run** (roughly one every 100 seconds on average, weighted toward the first 10 minutes). Specific XP-per-enemy values defined in the Progression System GDD.

### Wave Escalation

Formula: `density(step) = min(base_density × 1.2^step, base_density × 5)` | `speed(step) = base_speed × 1.1^step`

| Step | Time | Density Multiplier | Speed Multiplier |
|------|------|--------------------|------------------|
| 0 | 0:00 | 1.0× | 1.0× |
| 1 | 2:00 | 1.2× | 1.1× |
| 2 | 4:00 | 1.44× | 1.21× |
| … | … | ×1.2 per step | ×1.1 per step |
| Cap | 18:00 | 5.0× max | — |

### Upgrade Pool Draw

- Pool: 15–18 one-time-pick upgrades across 4 categories (~3–4 per category)
- Draw: 3 options per level-up, without replacement from eligible pool
- Pity rule: No more than 2 consecutive offers without at least one upgrade from the player's most-picked category (if that category is not exhausted)
- Category exhaustion: When a category is fully picked, it drops from the draw pool
- Stacking: Not implemented in MVP — each upgrade is a unique one-time pick

---

## Edge Cases

### Combo Counter

| Situation | Rule |
|-----------|------|
| 2nd hit kills the enemy | Counter advances (the kill counts as the hit); Finisher triggers on the next hit against any enemy |
| No enemies in range after 2nd hit | Counter resets after timeout `T` without a hit (specific timeout set in Combat System GDD) |
| Player takes damage during the 3rd hit animation | Finisher fires (already triggered); counter resets to 0 after the animation |
| Player presses parry during a combo chain | Parry resolves independently; combo counter is NOT reset by a parry input or a successful parry |

### Parry System

| Situation | Rule |
|-----------|------|
| Parry input and damage arrive in the same frame | Parry takes priority if input is within the timing window |
| Parry at 0 momentum | Burst fires at 25% of base burst (floor — never worthless) |
| Missed parry window | No penalty — player takes the hit normally; momentum meter unchanged |
| Second parry during post-parry invincibility frames | Second parry does not stack; input is ignored during active burst window |

### Upgrade System

| Situation | Rule |
|-----------|------|
| Category fully exhausted | Category drops from draw pool; draws continue from remaining categories |
| Level-up occurs during Finisher animation | Upgrade UI queues until Finisher animation completes; player input is not frozen |

### Session State

| Situation | Rule |
|-----------|------|
| Player presses Retry on death screen | Full state reset: combo counter → 0, momentum meter → 0, XP → 0, timer → 20:00, all enemy spawns cleared |
| Boss spawns at 18 min with active elites on screen | Boss spawn clears all active elite enemies; only the boss and standard infantry remain |

---

## Dependencies

This concept doc is the parent authority for the following system GDDs:

| System | GDD File | Notes |
|--------|----------|-------|
| Combo System | `design/gdd/combo-system.md` | Combo counter, Finisher trigger, reset conditions, Combo upgrade category |
| Parry System | `design/gdd/parry-system.md` | Parry window, momentum meter, burst damage, Parry upgrade category |
| Progression System | `design/gdd/progression-system.md` | XP curve, level-up pacing, upgrade pool draw rules |
| Enemy Definition | `design/gdd/enemy-definition.md` | Enemy types, telegraph behavior, elite and boss behavior |
| Wave Spawner | `design/gdd/wave-spawner.md` | Escalation formula, spawn timing, elite/boss event triggers |
| General Characters | `design/gdd/general-characters.md` | Finisher shapes per general, stat definitions |

Each system GDD must reference this concept doc as its parent and must not contradict values defined here without a design change request.

---

## Tuning Knobs

| Knob | Default | Safe Range | Affects |
|------|---------|------------|---------|
| `finisher_damage_multiplier` | 2.5× | 1.5×–4.0× | Combo power; below 1.5× makes Finisher feel irrelevant |
| `finisher_radius_multiplier` | 2.0× | 1.5×–3.0× | AoE coverage; above 3.0× trivializes dense packs |
| `burst_floor` | 0.25 | 0.1–0.5 | Parry value at 0 momentum; below 0.1 feels worthless |
| `burst_ceiling_multiplier` | 2.0× | 1.5×–3.0× | Parry value at full momentum; above 3.0× makes combo irrelevant |
| `target_level_ups_per_run` | 12 | 8–18 | Run upgrade pacing; drives XP curve definition |
| `wave_density_step` | 1.2× | 1.1×–1.4× | Escalation pace; above 1.4× produces difficulty spikes |
| `wave_speed_step` | 1.1× | 1.05×–1.2× | Enemy movement escalation |
| `wave_density_cap` | 5.0× | 3.0×–8.0× | Maximum late-run density |
| `pity_threshold` | **3 draws** | 2–5 | Consecutive draws without the player's most-picked category before pity triggers. Updated from 2 → 3 in upgrade-definition.md: threshold of 2 was too aggressive (railroaded lane identity). Authoritative value now owned by upgrade-definition.md. |
| `combo_counter_timeout` | TBD | — | Seconds without a hit before counter resets; set in Combat System GDD |
| `parry_window_ms` | TBD | — | Parry timing window duration in ms; set in Parry System GDD |

---

## Acceptance Criteria

All of the following must pass before MVP is considered shippable:

| # | Criterion | Test Type | Gate |
|---|-----------|-----------|------|
| AC-01 | Character auto-attacks the nearest enemy within `melee_range` units. No attack fires if no enemy is within range. | Unit test | BLOCKING |
| AC-02 | Combo counter increments to 1 on 1st hit, 2 on 2nd hit. On the 3rd consecutive hit, a Finisher fires dealing `base_attack × 2.5` damage in radius `base_radius × 2.0`. Counter resets to 0 after Finisher. | Unit test | BLOCKING |
| AC-03 | Combo counter resets to 0 when player takes damage. Counter does NOT reset on a successful parry. | Unit test | BLOCKING |
| AC-04 | Parry input within the window triggers a burst dealing `base_burst × (0.25 + momentum/100 × 1.75)`. At `momentum = 0`, burst = `base_burst × 0.25`. | Unit test | BLOCKING |
| AC-05 | Successful parry fills the momentum meter. Taking damage resets momentum to 0 in the same frame that combo counter resets. | Unit test | BLOCKING |
| AC-06 | Enemies drop XP. Sufficient XP accumulation triggers a level-up presenting 3 upgrade options drawn from the eligible pool without replacement. | Integration test | BLOCKING |
| AC-07 | 20-minute timer counts down. At 0:00 with player alive: win screen with stats. On player death: death screen with stats and retry. Retry resets all state (counter=0, meter=0, XP=0, timer=20:00, no enemies). | Integration test | BLOCKING |
| AC-08 | 3 general characters are selectable at run start. Each Finisher fires with the correct AoE geometry (ring / cone / point-burst). | Smoke check | ADVISORY |
| AC-09 | Elite enemy spawns at t=300s, t=600s, t=900s (±5s tolerance). Boss spawns at t=1080s and clears all active elite enemies. | Integration test | BLOCKING |
| AC-10 | Wave density multiplies by `wave_density_step` and speed by `wave_speed_step` every 120 seconds. Density is capped at `wave_density_cap` × base. | Integration test | BLOCKING |
| AC-11 | Parry burst (ring) and Finisher flash (starburst) are shape-distinguishable without relying on color. | Manual QA | ADVISORY |
| AC-12 | Combo counter UI pulse (1 → 2 → FINISH) is visible and readable at 100+ enemies on screen. Counter reset plays a visible break animation. | Manual QA | ADVISORY |
| AC-13 | In a structured playtest, ≥70% of participants correctly identify the parry telegraph without verbal instruction. | Playtest | ADVISORY |
| AC-14 | A run started via Retry enters a fully clean state: combo counter = 0, momentum = 0, XP = 0, timer = 20:00, no enemies present. | Integration test | BLOCKING |

---

- [ ] Run `/setup-engine` to configure Unity and populate version-aware reference docs
- [ ] Run `/art-bible` to create the visual identity spec — do BEFORE writing system GDDs
- [ ] Run `/design-review design/gdd/game-concept.md` to validate concept completeness
- [ ] Run `/map-systems` to decompose the concept into individual systems with dependencies
- [ ] Run `/design-system` per system identified by `/map-systems`
- [ ] Run `/create-architecture` to produce the master architecture blueprint
- [ ] Run `/architecture-decision` (×N) for each key technical decision
- [ ] Run `/gate-check` to validate readiness before committing to production
- [ ] Run `/prototype combo-and-parry-system` to validate both active systems together before full implementation
- [ ] Run `/playtest-report` after prototype to validate the core hypothesis
- [ ] Run `/sprint-plan new` to plan the first sprint

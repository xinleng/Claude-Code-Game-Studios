# Systems Index: Dynasty Survivors

> **Status**: Approved
> **Created**: 2026-05-10
> **Last Updated**: 2026-05-10
> **Source Concept**: design/gdd/game-concept.md

---

## Overview

Dynasty Survivors is a 20-minute roguelite survivors game with two interlocking active skill systems — a combo finisher (2-hit → Finisher on 3rd) and a parry (timing window → burst + momentum fill) — layered on top of an auto-attack baseline. The mechanical scope is deliberately narrow: one arena, five enemy types, three generals, and 15-18 upgrades across four categories. The systems that matter most are the ones that make the two active skill systems feel fair, readable, and rewarding. Everything else (wave spawner, progression, UI) exists to support those two systems in a 20-minute run container. Pillar 1 ("Readable Chaos") governs every UI and VFX decision; Pillar 2 ("Skill Has a Lane") governs every balance decision.

---

## Systems Enumeration

| # | System Name | Category | Priority | Status | Design Doc | Depends On |
|---|-------------|----------|----------|--------|------------|------------|
| 1 | Enemy Definition | Gameplay | MVP | In Review | design/gdd/enemy-definition.md | — |
| 2 | General Characters | Core | MVP | Approved | design/gdd/general-characters.md | — |
| 3 | Damage & Health | Core | MVP | In Review | design/gdd/damage-health.md | — |
| 4 | Upgrade Definition | Progression | MVP | Designed | design/gdd/upgrade-definition.md | — |
| 5 | Input Manager | Core | MVP | Not Started | — | — |
| 6 | Combat Core | Core | MVP | Not Started | — | — |
| 7 | Combo System | Gameplay | MVP | Not Started | — | Combat Core, General Characters, Damage & Health |
| 8 | Parry System | Gameplay | MVP | Not Started | — | Input Manager, Damage & Health, Enemy Definition |
| 9 | Enemy AI *(inferred)* | Gameplay | MVP | Not Started | — | Enemy Definition |
| 10 | Progression System | Progression | MVP | Not Started | — | Enemy Definition, Upgrade Definition |
| 11 | Wave Spawner | Gameplay | MVP | Not Started | — | Enemy Definition, Enemy AI |
| 12 | Run Session Manager *(inferred)* | Core | MVP | Not Started | — | Damage & Health, Combo System, Parry System |
| 13 | Combat Feedback UI *(inferred)* | UI | MVP | Not Started | — | Combo System, Parry System, Damage & Health, Enemy AI |
| 14 | Upgrade Selection UI *(inferred)* | UI | MVP | Not Started | — | Progression System, Upgrade Definition |
| 15 | Run UI *(inferred)* | UI | MVP | Not Started | — | Run Session Manager, Damage & Health, General Characters |
| 16 | VFX & Audio *(inferred)* | Audio/VFX | MVP | Not Started | — | Combo System, Parry System, Damage & Health |
| 17 | Dodge System *(inferred)* | Core | MVP | Not Started | — | Input Manager |

*(inferred) = system not explicitly named in the concept doc but required for the described mechanics to function.*

---

## Categories

| Category | Description | Systems in This Game |
|----------|-------------|----------------------|
| **Core** | Foundation systems everything depends on | General Characters, Damage & Health, Input Manager, Combat Core, Run Session Manager |
| **Gameplay** | The systems that make the game fun | Enemy Definition, Combo System, Parry System, Enemy AI, Wave Spawner |
| **Progression** | How the player grows over time | Upgrade Definition, Progression System |
| **UI** | Player-facing information displays | Combat Feedback UI, Upgrade Selection UI, Run UI |
| **Audio/VFX** | Sound and visual effects | VFX & Audio |

---

## Priority Tiers

| Tier | Definition | Target Milestone | Design Urgency |
|------|------------|------------------|----------------|
| **MVP** | Required for the core loop to function. Without these, you can't test "is this fun?" | First playable prototype | Design FIRST |
| **Vertical Slice** | Required for a complete, polished experience. | +2-4 weeks | Design SECOND |
| **Expanded** | Additional maps, generals, enemies, meta unlocks. | +1-2 months | Design THIRD |
| **Full Vision** | Polish, edge cases, content-complete. | Multi-month | Design as needed |

All 16 systems are **MVP tier**. The game has no non-MVP systems in scope. Post-MVP systems (meta progression, additional generals/maps) are captured in the concept doc's Expansion Concepts section.

---

## Dependency Map

### Foundation Layer (no dependencies — design these first)

1. **Enemy Definition** — Defines the full enemy cast: 5 types (3 basic infantry, 1 elite, 1 boss), stats, telegraph behavior, attack events. Four systems block on this.
2. **General Characters** — Defines Guan Yu / Zhao Yun / Lu Bu: Finisher AoE geometry (ring/cone/point-burst), stat definitions. Combo System cannot specify Finisher behavior without this.
3. **Damage & Health** — Player HP, damage taken, death event. The stakes of every positioning decision. Four dependents.
4. **Upgrade Definition** — 15-18 upgrade entries across 4 categories: name, one-line description, stat modified, magnitude. Progression System draws from this pool.
5. **Input Manager** — Unity new Input System setup, parry button binding, input buffering. Parry fairness risk lives here.
6. **Combat Core** — Melee range boundary, auto-attack fire rate, hit detection, enemy targeting (nearest in range). Sets the 30-second loop tempo.

### Core Layer (depends on Foundation only)

1. **Combo System** — depends on: Combat Core, General Characters, Damage & Health
2. **Parry System** — depends on: Input Manager, Damage & Health, Enemy Definition (attack event contract)
3. **Enemy AI** — depends on: Enemy Definition
4. **Progression System** — depends on: Enemy Definition, Upgrade Definition

### Feature Layer (depends on Core)

1. **Wave Spawner** — depends on: Enemy Definition, Enemy AI
2. **Run Session Manager** — depends on: Damage & Health, Combo System, Parry System

### Presentation Layer (depends on Features)

1. **Combat Feedback UI** — depends on: Combo System, Parry System, Damage & Health, Enemy AI
2. **Upgrade Selection UI** — depends on: Progression System, Upgrade Definition
3. **Run UI** — depends on: Run Session Manager, Damage & Health, General Characters

### Polish Layer (depends on everything)

1. **VFX & Audio** — depends on: Combo System, Parry System, Damage & Health

---

## Recommended Design Order

| Order | System | Priority | Layer | Primary Agent | Est. Effort |
|-------|--------|----------|-------|---------------|-------------|
| 1 | Enemy Definition | MVP | Foundation | game-designer | M |
| 2 | General Characters | MVP | Foundation | game-designer | S |
| 3 | Damage & Health | MVP | Foundation | systems-designer | S |
| 4 | Upgrade Definition | MVP | Foundation | economy-designer | M |
| 5 | Input Manager | MVP | Foundation | gameplay-programmer | S |
| 6 | Combat Core | MVP | Foundation | gameplay-programmer | S |
| 7 | Combo System | MVP | Core | game-designer | M |
| 8 | Parry System | MVP | Core | game-designer | M |
| 9 | Enemy AI | MVP | Core | ai-programmer | M |
| 10 | Progression System | MVP | Core | economy-designer | M |
| 11 | Wave Spawner | MVP | Feature | game-designer | M |
| 12 | Run Session Manager | MVP | Feature | gameplay-programmer | S |
| 13 | Combat Feedback UI | MVP | Presentation | ux-designer | M |
| 14 | Upgrade Selection UI | MVP | Presentation | ux-designer | S |
| 15 | Run UI | MVP | Presentation | ui-programmer | S |
| 16 | VFX & Audio | MVP | Polish | technical-artist | M |

*Effort: S = 1 session, M = 2-3 sessions. Sessions 1-6 (Foundation) can be designed in parallel if multiple sessions are available — they have no inter-dependencies.*

---

## Circular Dependencies

None detected. All dependency chains are acyclic.

---

## High-Risk Systems

| System | Risk Type | Risk Description | Mitigation |
|--------|-----------|-----------------|------------|
| **Combo System** | Design + Technical | State machine edge cases: combo counter on kill hit, reset during Finisher animation, interaction with parry invincibility. Frame-rate-dependent auto-attack timing. | Prototype before full GDD authoring (`/prototype combo-and-parry-system`). Resolve all edge cases in the GDD's Edge Cases section before implementation. |
| **Parry System** | Technical + Design | Input buffering fairness at 100-200 enemies. Parry window timing must feel consistent under chaotic load. Window too narrow = feels broken; too wide = trivialized. | Prototype alongside Combo System. Define `parry_window_ms` empirically via prototype before locking in GDD. |
| **Wave Spawner** | Technical | Performance with 200+ moving enemies + VFX. Object pooling must be implemented from day 1; without it, the escalation formula cannot be stress-tested. | Implement object pool as the very first piece of code. Establish a performance baseline at 50/100/200 enemy counts before tuning escalation. |

---

## Progress Tracker

| Metric | Count |
|--------|-------|
| Total systems identified | 17 |
| Design docs started | 2 |
| Design docs reviewed | 0 |
| Design docs approved | 0 |
| MVP systems designed | 2 / 17 |

---

## Next Steps

- [ ] Run `/prototype combo-and-parry-system` before designing the Combo and Parry GDDs — answers the four open questions from the concept doc
- [ ] Run `/design-system enemy-definition` — first in design order, biggest bottleneck
- [ ] Run `/design-system general-characters` — second in order, can be done in parallel with Enemy Definition
- [ ] Run `/map-systems next` to always pick the highest-priority undesigned system automatically
- [ ] Run `/gate-check pre-production` when all 16 MVP GDDs are authored and reviewed

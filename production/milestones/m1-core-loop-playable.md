# Milestone M1 — Core Loop Playable

> **Status**: Planned
> **Decided**: 2026-10-10 (user chose the producer's "M1 cut line first" option at the Pre-Production gate)
> **Start**: 2026-10-12 · **Target**: 2026-11-20 (~6 weeks, solo)
> **Followed by**: M2 — MVP Complete (full 17-system scope from game-concept.md)
> **Min-spec for all perf gates**: GTX 1650 / Ryzen 5 3600 / 16GB (technical-preferences.md)

## Why this milestone exists

The producer estimated ~55–60 dev-days (11–12 weeks) for the full 17-system MVP against the
original 3–6 week target. Rather than cut systems permanently or slip the whole MVP, M1 proves
the thing that matters most first: **is hit-hit-FINISH + parry fun at 200-enemy density?**
Everything not needed to answer that moves to M2. No GDD is cut — M2 restores full scope.

## In scope (M1)

| Area | M1 scope |
|---|---|
| Generals | **1** — Guan Yu (Ring Finisher; default select). Proposed — confirm at sprint planning |
| Enemies | The **3 basic infantry** types: Levy Soldier, Halberd Pikeman, Fire Arrow Archer |
| Upgrades | **~12** of the 24 (subset chosen at sprint planning; must cover both Combo and Parry lanes so lane viability can be tested) |
| Run length | **Shortened timer** — `run_duration_s` = 600 (provisional; the GDD's minimum tuning value) |
| UI | **Combat HUD only** (design/ux/combat-hud.md) + bare-minimum run start/death/retry flow |
| Systems | Foundation (all 5 epics) · Combat Core · Damage & Health · Combo · Parry · Enemy AI (infantry states) · Wave Spawner (density refill + escalation, no scripted elite/boss events) · Progression · Run Session Manager (core flow) · placeholder VFX/audio |
| Art | **Greybox only** — capsules/primitives + flat palette roles (art bible §3.2 primitives). DynastyToon shader, VAT pipeline, validation tool deferred |

## Moved to M2

- Elites (Iron Captain) and the Warlord boss, incl. scripted 300/600/900/1080s events
- Dodge System (its MVP purpose is the Warlord slam)
- Generals 2–3 (Zhao Yun, Lu Bu)
- Full 24-upgrade catalogue
- Run UI selection screen + geometry previews, Upgrade Selection polish, final VFX/audio, ADR-0011 audio mix
- Art-bible-conformant assets (gated on the RATIFY list + /asset-spec)

## Exit criteria (M1 is done when)

1. A full M1 run is playable start → death/timeout → retry, on the min-spec target
2. The combo-and-parry prototype has reported and its findings are folded into the GDDs (4 balance + 4 feel questions; dodge question deferred with Dodge to M2)
3. Creative-director sign-off criteria from the gate report hold for the M1 scope:
   - a base Finisher feels like the best moment in a standard wave (and kills a Levy)
   - ≥70% of playtesters spot the parry telegraph unprompted (AC-13)
   - a Combo-lane and a Parry-lane build can both survive to the 10-minute mark
4. Performance at 200 infantry on min-spec: enemy tick ≤4ms, Animator+skinning ≤3.0ms, draws ≤1,100 — or a documented escalation decision (VAT / TransformAccessArray)
5. Art-director greybox validation pass done (greyscale, stencil, 3 colourblind filters at density)
6. All Logic stories in scope have passing tests in CI

## Risks specific to M1

- Wave escalation and XP pacing were tuned for 20 minutes — a 600s run needs a compressed curve (tuning-knob change, not a design change)
- Lane-viability testing with only ~12 upgrades may under-represent build variety
- Removing elites removes Pillar 2's "dual-system checkpoint" — M1 can't validate that pillar; M2 must

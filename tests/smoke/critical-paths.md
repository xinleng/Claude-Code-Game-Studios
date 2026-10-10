# Smoke Test: Critical Paths — Dynasty Survivors

**Purpose**: Run these checks in under 15 minutes before any QA hand-off.
**Run via**: `/smoke-check` (which reads this file)
**Update**: Add new entries when new core systems are implemented.

## Core Stability (always run)

1. Game launches to the general-selection screen without crash
2. A run can be started with each of the three generals (Guan Yu / Zhao Yun / Lu Bu)
3. Selection screen responds to keyboard navigation (no mouse required)

## Core Mechanic (update per sprint)

<!-- Update as systems land. Initial set mirrors the MVP core loop. -->
4. Auto-attack fires against the nearest enemy in melee range; combo counter shows 1 → 2 → FINISH
5. A parry press inside a telegraph window triggers the burst and fills the momentum meter
6. Taking a real hit (≥ reset threshold) resets combo counter and momentum meter; a graze does not
7. Dodge dashes and grants i-frames (visible shimmer); dodge is unavailable during cooldown
8. XP drops accumulate; a level-up pauses the run and presents 3 upgrade cards; picking resumes play

## Run Container

9. Elite (Iron Captain) spawns at ~5:00; boss (Warlord) at ~18:00; boss spawn clears active elites
10. Death shows the death screen with cause-of-death + stats; Retry produces a fully clean run state
11. Surviving to 20:00 shows the win screen with stats

## Data Integrity

12. GameDataValidator reports zero `[DESIGNER-ERROR]` entries at bootstrap

## Performance

13. No visible frame drops at peak density on target hardware (60fps target)
14. No memory growth over 5 minutes of continuous play

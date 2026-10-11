# Pending tests (not compiled)

Tests parked here reference production classes that do not exist yet. This folder is
deliberately NOT linked into any Unity test assembly (no `.asmref`), so it cannot break the
EditMode/PlayMode builds.

| Test | Waiting on | Restore to |
|------|-----------|------------|
| `damage-health/damage_health_effective_damage_test.cs` | `PlayerHealthSystem`, `TestConfigs`, `FakeGameClock` (Damage & Health epic + game-data 004 + bootstrap 002) | `tests/unit/damage-health/` |

Moved 2026-10-10 with the user's approval (bootstrap-combat-frame Story 001 prerequisite).

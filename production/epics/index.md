# Epics Index

Last Updated: 2026-10-10
Engine: Unity 6.3 LTS
Manifest Version: 2026-10-10

| Epic | Layer | System | GDD | Stories | Status |
|------|-------|--------|-----|---------|--------|
| [bootstrap-combat-frame](bootstrap-combat-frame/EPIC.md) | Foundation | CombatFrame + Bootstrap + IGameClock + project setup | damage-health / input-manager / run-session-manager (contracts) | 6 (6 Ready) | Ready |
| [object-pool](object-pool/EPIC.md) | Foundation | ObjectPool | enemy-definition, wave-spawner | 6 (6 Ready) | Ready |
| [input-manager](input-manager/EPIC.md) | Foundation | InputManager | input-manager.md | 11 (11 Ready) | Ready |
| [game-data](game-data/EPIC.md) | Foundation | GameData | enemy-definition, general-characters, upgrade-definition | 8 (8 Ready — flag #4 resolved 10-10) | Ready |
| [run-session-manager](run-session-manager/EPIC.md) | Foundation | RunSessionManager | run-session-manager.md | 8 (7 Ready, 1 Blocked — needs Combo/Parry signals) | Ready |

**Foundation total: 39 stories — 38 Ready, 1 Blocked** (RSM stats aggregation, waits on Combo/Parry signals). Build order starts with
bootstrap-combat-frame Story 001 (Unity project setup), which every other story chains back to.

### Open items surfaced by story authoring (resolve at `/story-readiness`)
- **Unity project location** — no ADR covers it; story bootstrap-001 recommends project at repo root with `src/` + `tests/` as local packages / `.asmref` links
- **Existing example test won't compile** once `tests/` is wired in (references unbuilt classes) — move out of the compiled tree (needs user OK)
- **Undefined values**: `arena_bounds` size, dodge `reaction_margin`, enemy `SpawnWeight`, per-type pool capacities, Warlord P2 telegraph/damage values (game-data 003/004)
- **Generated `PlayerControls.cs`** is a third InputSystem-touching file — ADR-0004 rule 6 needs a generated-code exemption (input-manager 009)
- **Input GDD AC-IM-11 vs AC-IM-08 contradiction** (Paused capture); unified run clock means frozen presses no longer age during pause (OQ-02) (input-manager 004/005)
- **No Dodge accessor on `IInputAuthority`** (input-manager 001)
- **Double ReturnToPool / projectile-pool exhaustion / projectiles of a dead archer** — ADR-0005 silent (object-pool 001/005)
- **"Finisher in progress" signal** has no defined source (run-session-manager 005)
- Stale "static counter" / `runStartTime = Time.time` wording in enemy-definition, damage-health Contract #6, run-session-manager Rule 2 (registry already corrected)

## Not yet created (deliberately)

**Core / Feature / Presentation epics are held until the combo-and-parry prototype reports**
(CD + PR + TD agreement at the 2026-10-10 Pre-Production gate): the prototype answers the 4
deferred balance concerns, 4 feel questions, dodge-vs-parry usage, and the Animator stepping A/B —
writing Combo/Parry/Progression stories first would mean rework. Core layer (CombatCore,
PlayerHealthSystem, DodgeSystem) may be epiced as soon as the prototype's feel pass confirms the
GDD values, or earlier for PlayerHealthSystem (least prototype-sensitive).

Unity project creation is Story 1 of `bootstrap-combat-frame`.

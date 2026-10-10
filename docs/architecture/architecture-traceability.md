# Architecture Traceability Index

> Last Updated: 2026-10-10 (by /architecture-review, full mode, independent session)
> Engine: Unity 6.3 LTS
> Registry: `tr-registry.yaml` v2 (stable IDs — never renumber)

## Coverage Summary

- Total requirements: **72** (58 baseline + 14 appended 2026-10-10)
- ✅ Covered: **69** (96%)
- ⚠️ Partial: **3**
- ❌ Gaps: **0 Foundation/Core layer** (ADR-0012 authored 2026-10-10 closed TR-GC0-002 + TR-RS-006; remaining partial = staged ADR-0011 audio half + 2 story-level payload extensions)

## Full Matrix

"Structural" = the requirement is design-complete in its GDD and the named ADR(s) fix the
pattern it is implemented under (per architecture.md's Required-ADRs closing rule).

| Requirement ID | GDD | Requirement (short) | ADR Coverage | Status |
|---|---|---|---|---|
| TR-GC0-001 | game-concept | 60fps/200 enemies, ≤1500 draws, ≤4GB | ADR-0001, 0007, 0008 | ✅ |
| TR-GC0-002 | game-concept | 20-min timer drives win + scheduled events | ADR-0012 (authored 2026-10-10) | ✅ |
| TR-GC0-003 | game-concept | Full state reset on retry | ADR-0006 + IRunResettable (arch.) | ✅ |
| TR-ED-001 | enemy-definition | Single manager, two-phase tick, −100 anchor | ADR-0001, 0002 | ✅ |
| TR-ED-002 | enemy-definition | AttackEvent struct contract | ADR-0003 | ✅ |
| TR-ED-003 | enemy-definition | AttackerId unique per spawn + test seam | ADR-0005 (amended: pool field) | ✅ |
| TR-ED-004 | enemy-definition | Flush-before-deactivate pool contract (+P2 transition) | ADR-0005 | ✅ |
| TR-ED-005 | enemy-definition | No O(n²); NonAlloc or grid | ADR-0007 (amended: grid primary) | ✅ |
| TR-ED-006 | enemy-definition | Telegraph VFX Graph instancing; RecordRenderGraph overlays | ADR-0008 (amended) | ✅ |
| TR-ED-007 | enemy-definition | Warlord P1/P2 serialized SO refs, atomic swap | ADR-0006 (swap point: GDD flag #3) | ✅ |
| TR-ED-008 | enemy-definition | Deferred registration staging | ADR-0001, 0005 | ✅ |
| TR-ED-009 | enemy-definition | Spawn cooldown jitter | ADR-0005 (amended: injected RNG) | ✅ |
| TR-ED-010 | enemy-definition | Unified run-clock timestamps | ADR-0004, 0010 (amended — C1 fix) | ✅ |
| TR-ED-011 | enemy-definition | IsPhaseTransitionLocked + roar watchdog | ADR-0001 (structural) | ✅ |
| TR-ED-012 | enemy-definition | Shared 8-state data-driven FSM | ADR-0001 | ✅ |
| TR-ED-013 | enemy-definition | Load/receipt-time validation + designer errors | ADR-0006 | ✅ |
| TR-DH-001 | damage-health | Struct-delegate events; no UnityEvent | ADR-0003 | ✅ |
| TR-DH-002 | damage-health | Entity ADR before combat code | ADR-0001 | ✅ |
| TR-DH-003 | damage-health | Unscaled iframe timestamp via IGameClock | ADR-0010 | ✅ |
| TR-DH-004 | damage-health | Same-frame parry priority; orchestrator | ADR-0002 | ✅ |
| TR-DH-005 | damage-health | OpenInvulnerability single owner, latest-end-wins | ADR-0002, 0003 (structural; IPlayerHealth) | ✅ |
| TR-DH-006 | damage-health | Guarded Reset + ForceState seam | ADR-0010 | ✅ |
| TR-DH-007 | damage-health | PlayerDead kill context (nullable), pre-reset capture | ADR-0003 | ✅ |
| TR-DH-008 | damage-health | effective_damage formula; caps at apply time | ADR-0006 (structural) | ✅ |
| TR-DH-009 | damage-health | Threshold gate; S6 inside damage formula; ordered dispatch | ADR-0003 | ✅ |
| TR-GC-001 | general-characters | GeneralData assets; nullable accessor guards; final stored values | ADR-0006 | ✅ |
| TR-GC-002 | general-characters | Finisher geometry mapping + deterministic fallback | ADR-0001, 0007 (structural) | ✅ |
| TR-GC-003 | general-characters | Screen-forward fallback Vector3(0,0,1) | ADR-0004 (structural convention) | ✅ |
| TR-IM-001 | input-manager | Single input authority (asmdef gate) | ADR-0004 (amended) | ✅ |
| TR-IM-002 | input-manager | One asset, two maps, RSM-driven context | ADR-0004 | ✅ |
| TR-IM-003 | input-manager | Parry ring buffer consumer-pull API | ADR-0004 | ✅ |
| TR-IM-004 | input-manager | Run-clock stamps; freeze outside Gameplay | ADR-0004, 0010 (amended — C1 fix) | ✅ |
| TR-IM-005 | input-manager | Input → Parry → Combat/D&H order | ADR-0002, 0004 | ✅ |
| TR-CC-001 | combat-core | Auto-attack tick, tie-broken targeting, NonAlloc | ADR-0007 | ✅ |
| TR-CC-002 | combat-core | SwingLanded signal | ADR-0003 | ✅ |
| TR-CC-003 | combat-core | Locomotion + dash override + A5 timer | ADR-0002 (structural) | ✅ |
| TR-CC-004 | combat-core | Knockback elite gate, spy seam, steering impulse | ADR-0002, 0010 (structural) | ✅ |
| TR-CC-005 | combat-core | A1–A6 resolved deltas at pick | ADR-0006 | ✅ |
| TR-CC-006 | combat-core | Finisher-replaces-3rd-swing handshake | ADR-0002 (amended 2026-10-10) | ✅ |
| TR-CC-007 | combat-core | Damage/kill source taxonomy + attack-instance id | ADR-0003 (extend payload structs at story time) | ⚠️ Partial |
| TR-CB-001 | combo-system | Counter FSM, Finisher, delta caps, reset inputs | ADR-0002, 0003 (structural) | ✅ |
| TR-CB-002 | combo-system | Cone orientation fallback chain | ADR-0004 (structural) | ✅ |
| TR-CB-003 | combo-system | C1–C6 hooks; pre-reset gate reads | ADR-0003, 0006 (structural) | ✅ |
| TR-PR-001 | parry-system | Windows from telegraphs; most-recent-event rule | ADR-0003, 0004 (structural) | ✅ |
| TR-PR-002 | parry-system | Momentum meter + burst formula + reset dispatch | ADR-0003 | ✅ |
| TR-PR-003 | parry-system | Elite gate read; suppression before D&H | ADR-0002, 0003 | ✅ |
| TR-PR-004 | parry-system | P/S6 hooks; S6 in-formula ordering | ADR-0003 | ✅ |
| TR-PR-005 | parry-system | Projectile pool keyed by AttackerId; parry cancels in-flight | ADR-0005 (cancel linkage = story detail) | ⚠️ Partial |
| TR-DG-001 | dodge-system | Dash FSM, invuln request, load-time checks, arena clamp | ADR-0002, 0006 (structural) | ✅ |
| TR-AI-001 | enemy-ai | FSM executor + grid steering + movement locks | ADR-0007 | ✅ |
| TR-AI-002 | enemy-ai | DamageType assignment; Slam bypass; reduced-hit flag | ADR-0003 | ✅ |
| TR-AI-003 | enemy-ai | Aggro jitter; Cooldown shortcut; Archer fallback | ADR-0001 (structural) | ✅ |
| TR-AI-004 | enemy-ai | Enemy HP ledger ownership; stagger/counter-hit; XP event | ADR-0001 (module ownership) | ✅ |
| TR-PG-001 | progression | XP curve, overflow, sequential level-ups | ADR-0006, 0010 (structural) | ✅ |
| TR-PG-002 | progression | Draw-of-3, drought, C3 condition, caps (injected RNG) | ADR-0006, 0010 (structural) | ✅ |
| TR-UD-001 | upgrade-definition | UpgradeCatalogue data asset | ADR-0006 | ✅ |
| TR-UD-002 | upgrade-definition | total_catalogue vs eligible_pool run state | ADR-0006 (structural) | ✅ |
| TR-UD-003 | upgrade-definition | Typed effect routing; caps at receivers; S4 regen gate | ADR-0003, 0006 | ✅ |
| TR-WS-001 | wave-spawner | Pre-warmed pools; no Instantiate/Destroy | ADR-0005 | ✅ |
| TR-WS-002 | wave-spawner | Pure-function escalation; live-count cap; speed multiplier | ADR-0006 (structural) | ✅ |
| TR-WS-003 | wave-spawner | Spawn ring; scripted elites/boss; silent elite clear | ADR-0005 (structural) | ✅ |
| TR-WS-004 | wave-spawner | Spawner events + lifecycle/halt/reset | ADR-0003 (structural) | ✅ |
| TR-RS-001 | run-session-manager | runStartTime owner; data-driven duration; flow FSM | ADR-0010 (clock) + ADR-0012 (flow) | ✅ |
| TR-RS-002 | run-session-manager | Pause orchestration; deferred pick; death priority | ADR-0004, 0009 | ✅ |
| TR-RS-003 | run-session-manager | Stats aggregation from signals | ADR-0003 (structural) | ✅ |
| TR-RS-004 | run-session-manager | Guarded retry reset orchestration | ADR-0006 + IRunResettable (structural) | ✅ |
| TR-RS-005 | run-session-manager | UNIFIED RUN CLOCK (C1 resolution) | ADR-0010, 0004 (amended 2026-10-10) | ✅ |
| TR-RS-006 | run-session-manager | Init order; single-scene flow; arena bounds | ADR-0012 (authored 2026-10-10) | ✅ |
| TR-UI-001 | ui (3 GDDs) | UI Toolkit; event binding; kb/gamepad/mouse nav | ADR-0009 | ✅ |
| TR-UI-002 | combat-feedback-ui | Throttling/pooling; DamageType keyed; VFX-layer telegraphs | ADR-0009, 0008 | ✅ |
| TR-UI-003 | upgrade-selection-ui | Pick screen: unscaled anims, card-count seam | ADR-0009 (amended) | ✅ |
| TR-VA-001 | vfx-audio | VFX hierarchy; shape-first; overlays; voice limit | VFX half ADR-0008; audio half → staged ADR-0011 | ⚠️ Partial |
| TR-VA-002 | vfx-audio | Telegraph lifecycle/cap owner; never-cull; asset swap | ADR-0008 (amended) | ✅ |
| TR-TP-001 | technical-preferences | Data-driven, DI, deterministic tests | ADR-0006, 0010 | ✅ |
| TR-TP-002 | cross-cutting | [DESIGNER-ERROR] channel assertable by tests | ADR-0006, 0010 | ✅ |

## Known Gaps
- ~~TR-GC0-002, TR-RS-006~~ → **CLOSED 2026-10-10** by ADR-0012 (scene organisation & boot flow)
- ⚠️ TR-VA-001 audio half → author **ADR-0011 — Audio mix architecture** before VFX & Audio implementation (Presentation layer — does not gate pre-production)
- ⚠️ TR-CC-007, TR-PR-005 → payload/linkage details to be specified in their stories (extend ADR-0003 structs; no new ADR needed)

## Superseded Requirements
None. All 58 baseline IDs remain active; 12 had their `requirement` text revised 2026-10-10
(same intent, corrected or enriched wording — see `revised` fields in tr-registry.yaml).

## History
| Date | Coverage | Notes |
|---|---|---|
| 2026-07-03 | 58 TRs, 0 covered | architecture.md baseline, pre-ADR |
| 2026-10-10 | 72 TRs, 66✅/4⚠️/2❌ | First independent review; 3 blocking conflicts found + resolved; registry v2 |

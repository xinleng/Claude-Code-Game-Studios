# Dynasty Survivors — Master Architecture

## Document Status

- **Version**: 1.1 (v1.0 2026-07-03; v1.1 amendment 2026-10-10 — see "v1.1 Amendments" below)
- **Last Updated**: 2026-10-10
- **v1.1 Amendments (2026-10-10, from the independent /architecture-review)**: where this document conflicts with the amended ADRs, **the ADRs win**. (1) **Unified run clock** — all combat timestamps (AttackStartTime, parry presses, steer_time) use `IGameClock.RunTime`; any `Time.time − runStartTime` or `Time.unscaledTime − runStartTime` wording below is superseded (conflict C1). (2) **Spatial queries** — the uniform grid serves ALL queries; enemies carry no physics colliders; `OverlapSphereNonAlloc` recommendations below are superseded (ADR-0007, finding N1). (3) **Input** — single `PlayerControls` instance shared with the UI input module via `UiInputModuleBinder`; asmdef gate (ADR-0004/0009, finding N2). (4) **ADR-0012 authored** (single scene, Bootstrap composition root, RSM-owned run clock + arena bounds) — no longer deferred. (5) Finisher-replaces-3rd-swing handshake (ADR-0002). (6) TR baseline now 72 entries in `tr-registry.yaml` v2; traceability in `architecture-traceability.md`.
- **Engine**: Unity 6.3 LTS (URP, PC / Steam / itch.io)
- **GDDs Covered**: game-concept, enemy-definition, general-characters, damage-health, upgrade-definition, input-manager, combat-core, combo-system, parry-system, enemy-ai, progression-system, dodge-system, wave-spawner, run-session-manager, combat-feedback-ui, upgrade-selection-ui, run-ui, vfx-audio (17/17 approved)
- **ADRs Referenced**: ADR-0001…ADR-0010 (Accepted 2026-07-04; amended 2026-10-10 after an independent review found 3 blocking conflicts — all resolved) and ADR-0012 scene/boot (Accepted 2026-10-10). ADR-0011 (audio mix) remains staged before VFX & Audio implementation.
- **Technical Director Sign-Off**: 2026-07-04 — APPROVED WITH CONDITIONS (conditions met: ADR-0001..0007 Accepted; user ratified the draft by directing the session to proceed)
- **Lead Programmer Feasibility**: skipped — Lean review mode

---

## Engine Knowledge Gap Summary

**Engine**: Unity 6.3 LTS (Dec 2025). **LLM training covers**: ~Unity 2022 LTS. **Post-cutoff**: entire Unity 6 series (6.0–6.3). All engine API usage must be verified against `docs/engine-reference/unity/` before implementation.

### HIGH RISK domains (verify before every decision)

| Domain | Post-cutoff change | Impact on this project |
|---|---|---|
| **Input** | Legacy `Input` class deprecated; Input System package is the default | Input Manager is a Foundation module. All input goes through `InputAction` / generated wrapper classes. `Input.GetKeyDown` etc. is forbidden project-wide. |
| **Rendering / URP** | RenderGraph API replaces CommandBuffer passes; URP Compatibility Mode hidden + deprecated in 6.3 | Any screen-space overlay (parry flash vignette, Finisher post-process shake) must use `RecordRenderGraph` from day one. Telegraph VFX avoid the issue entirely via VFX Graph (auto-integrates with render graph). |
| **DOTS / Entities** | Complete API overhaul (Entities 1.3, `ISystem`, production-ready in 6.3) | The 200-enemy workload is the one candidate for DOTS. Decision deferred to ADR-0001 — this architecture specifies the MonoBehaviour + single-manager path as the default (see Module Ownership), with DOTS as the profiled fallback. Any DOTS code must use `ISystem`/`IJobEntity`, never `ComponentSystem`. |

### MEDIUM RISK domains

| Domain | Change | Impact |
|---|---|---|
| **Physics** | PhysX 5.1; default solver iterations 6 → 8 | Combat uses overlap queries only (no rigidbody-driven gameplay); low exposure. `RaycastAll`/allocating `OverlapSphere` forbidden — NonAlloc only (already mandated by enemy-definition Contract #3). |
| **UI Toolkit** | Production-ready for runtime UI; `VisualElement.transform` deprecated (6.2+) | All four UI systems build on UI Toolkit (UXML/USS) per technical preferences. Use `style` transform properties, never `VisualElement.transform`. |
| **Addressables** | Load failures throw (6.2+) instead of returning null | MVP does not require Addressables (see ADR-0006 rationale) — content volume is 1 map / 5 enemies / 3 generals, all direct serialized references. If adopted post-MVP, wrap loads in try/catch. |

### LOW RISK domains

C# 9 scripting, Unity Test Framework (NUnit), overlap/raycast query APIs, ScriptableObjects, AudioSource mixing — stable since 2022 LTS.

### Systems touching HIGH/MEDIUM risk domains

| System | Domain | Risk |
|---|---|---|
| Input Manager | Input System package | HIGH |
| VFX & Audio, Combat Feedback UI (telegraphs, overlays) | URP RenderGraph / VFX Graph | HIGH |
| Enemy AI, Damage & Health (200-entity tick) | DOTS candidacy | HIGH (decision), LOW (if MonoBehaviour path confirmed) |
| Combat Core, Enemy AI (overlap queries) | Physics | MEDIUM |
| Run UI, Upgrade Selection UI, Combat Feedback UI | UI Toolkit | MEDIUM |

---

## Technical Requirements Baseline

Extracted from 17 GDDs + technical preferences. 58 requirements. IDs are stable — do not renumber; append only. (These seed `docs/architecture/tr-registry.yaml`, created by `/architecture-review`.)

| Req ID | GDD | Requirement | Domain |
|---|---|---|---|
| TR-GC0-001 | game-concept | 60 fps (16.6ms) at 200 concurrent enemies + VFX; ≤1500 draw calls; ≤4GB memory | Performance |
| TR-GC0-002 | game-concept | 20-minute run timer drives win condition and all scheduled events | Core |
| TR-GC0-003 | game-concept | Full state reset on retry across every system (counter, meter, XP, timer, enemies, upgrades) | Core |
| TR-ED-001 | enemy-definition | Single `EnemySystemManager` ticks all enemies; two sequential phases per frame (damage → state-transition); `[DefaultExecutionOrder(-100)]` before Parry/UI | Core |
| TR-ED-002 | enemy-definition | `AttackEvent` struct contract (AttackerId, AttackStartTime, TelegraphDurationMs, AttackDamage, AttackerPosition, Parriable, IsCounterHit, DamageType); one emission per attack | Events |
| TR-ED-003 | enemy-definition | `AttackerId` unique per spawn instance (static counter, not pool slot); test-resettable via `ResetInstanceIdCounter()` | Core |
| TR-ED-004 | enemy-definition | Pool contract: `ReturnToPool` calls `ParrySystem.FlushEventsFor(id)` before deactivation; `SetActive(false)` forbidden outside the pool; Parry System keys events by `AttackerId` dictionary (O(1) flush) | Lifecycle |
| TR-ED-005 | enemy-definition | Range checks via layer-filtered `Physics.OverlapSphereNonAlloc` with pre-allocated buffer sized to `max_enemy_count`, or a custom spatial grid — never O(n²) brute force, never allocating overloads | Physics/Perf |
| TR-ED-006 | enemy-definition | Telegraph VFX: VFX Graph with GPU instancing, one asset per telegraph shape; per-enemy ParticleSystem instances forbidden | Rendering |
| TR-ED-007 | enemy-definition | Warlord Phase 1/2 data both loaded at spawn via serialized `EnemyDataSO` references; atomic data swap at Roar exit | Data |
| TR-ED-008 | enemy-definition | Deferred registration: `OnEnable` stages to `RegisterPending`; manager flushes at tick start (no list mutation mid-iteration) | Core |
| TR-ED-009 | enemy-definition | Spawn-time cooldown jitter (`Random.Range(0, cooldown)`) to prevent synchronized attack spikes | Core |
| TR-ED-010 | enemy-definition | All timestamps run-local: `Time.time − runStartTime` (float precision at 350ms windows over long sessions) | Core |
| TR-ED-011 | enemy-definition | `IsPhaseTransitionLocked` observable property + 1700ms roar watchdog that unblocks the Dead path | Core |
| TR-ED-012 | enemy-definition | Shared 8-state enemy state machine (Idle/Chase/Telegraph/Attack/Cooldown/Staggered/Roar/Dead), data-driven per type — no per-type state classes | Core |
| TR-ED-013 | enemy-definition | Load-time validation + clamping of tuning values with designer-error logging (`stagger_resist_multiplier`, `phase_2_threshold_ratio`, etc.) | Data |
| TR-DH-001 | damage-health | `DamageTaken` as C# delegate with `readonly struct` payload; `UnityEvent` forbidden on combat hot paths (GC pressure) | Events |
| TR-DH-002 | damage-health | Entity-architecture ADR (MonoBehaviour vs DOTS) accepted before any combat code | Core |
| TR-DH-003 | damage-health | iframe timer = timestamp pattern on `Time.unscaledTime` (correct under `timeScale = 0`) | Core |
| TR-DH-004 | damage-health | Same-frame parry priority: Parry evaluates before Damage & Health — CombatFrame orchestrator preferred over hidden Script Execution Order settings | Core |
| TR-DH-005 | damage-health | `OpenInvulnerability(duration_ms)` — Damage & Health is single owner of invulnerability; one timer; latest-end-time overlap resolution; callers: Parry (300ms), Dodge (400ms) | Core |
| TR-DH-006 | damage-health | Reset API guarded (no-op + designer error unless Dead); `ForceState()` test seam | Testing |
| TR-DH-007 | damage-health | `PlayerDead` payload carries kill context (KillerAttackerId, KillerDamageType, ComboCounterAtDeath, MomentumAtDeath, TimeOfDeath); FinalHP clamped to 0 | Events |
| TR-DH-008 | damage-health | `effective_damage = max(1, floor(raw × (1 − reduction)))`; reduction values restricted to exact binary fractions; caps (`damage_reduction_cap`, `regen_rate_max`) enforced at upgrade-apply time | Core |
| TR-DH-009 | damage-health | `reset_damage_threshold` gate: combo/momentum resets fire only at `effective_damage ≥ 3`; subscriber ordering on `DamageTaken` must be deterministic (S6 reads momentum before Parry reset applies) | Events |
| TR-GC-001 | general-characters | `GeneralData` profiles as data assets; nullable fields (`FinisherAngle`, `KnockbackForce/Radius`) readable only for the matching shape — no 0.0 sentinels | Data |
| TR-GC-002 | general-characters | Finisher geometry mapping: Ring/PointBurst = full-circle overlap; Cone = 45° half-angle wedge oriented by directional input; PointBurst adds radial knockback via Combat Core | Core |
| TR-GC-003 | general-characters | Canonical screen-forward fallback `Vector3(0,0,1)` shared by cone fallback and dodge direction | Core |
| TR-IM-001 | input-manager | Single input authority: only Input Manager touches devices/InputActions — architectural invariant, statically checkable | Input |
| TR-IM-002 | input-manager | One Input Actions asset, two maps (Gameplay / UI); context switching (`SetContext`) driven by Run Session Manager | Input |
| TR-IM-003 | input-manager | Parry press ring buffer (capacity 4, 120ms life) with `TryConsumeParryPress(notBefore)` / `HasLiveParryPress()` consumer-pull API; one press one consumption | Input |
| TR-IM-004 | input-manager | Input timestamps run-local unscaled; buffer frozen (not pruned) while context ≠ Gameplay | Input |
| TR-IM-005 | input-manager | Frame order: Input Manager (capture/prune) → Parry (query/consume) → Combat Core / Damage & Health | Core |
| TR-CC-001 | combat-core | Auto-attack tick: nearest-target selection (XZ), interval gating, `max_targets` hit resolution via NonAlloc overlap | Core |
| TR-CC-002 | combat-core | `SwingLanded` signal (≥1 hit per swing → exactly +1 combo advance) consumed by Combo System | Events |
| TR-CC-003 | combat-core | Locomotion integrates `CurrentMoveDirection`; Dodge System overrides locomotion and suppresses auto-attack during dash | Core |
| TR-CC-004 | combat-core | Knockback application with elite gate (suppress for Iron Captain/Warlord at `combo_counter < 2`) — spy-testable `ApplyKnockback` seam | Core |
| TR-CC-005 | combat-core | Upgrade deltas (A1–A6) received as resolved values at pick time — Combat Core applies, never reads the catalogue | Data |
| TR-CB-001 | combo-system | Combo counter state machine {0,1,2} + Finisher execution consuming general geometry; `FinisherUpgradeDelta` accumulation with caps | Core |
| TR-CB-002 | combo-system | Zhao Yun cone orientation: `LastSteerDirection` + 300ms staleness window → nearest-enemy → screen-forward fallback chain | Core |
| TR-CB-003 | combo-system | C1–C6 upgrade hooks (damage/radius overrides, C3 reset-to-1, C4 infantry stagger, C5 heal w/ `finisher_max_targets` cap, C6 chain classified as non-Finisher hit) | Core |
| TR-PR-001 | parry-system | Per-attack parry windows derived from `TelegraphDurationMs`; open-window set management; `Parriable = false` opens no window | Core |
| TR-PR-002 | parry-system | Momentum meter [0,100]; burst formula `base_burst × (0.25 + m/100 × 1.75)`; reset via `momentum_reset_multiplier` on threshold damage | Core |
| TR-PR-003 | parry-system | Elite burst gate reads `combo_counter` from Combo System at burst resolution; parried attack suppressed before Damage & Health sees it | Core |
| TR-PR-004 | parry-system | P1–P6 + S6 hooks; S6 momentum exposure at DamageTaken time with fixed subscriber ordering (ADR) | Events |
| TR-DG-001 | dodge-system | Dash state machine (Ready/Dashing/Cooldown); direction fallback chain; locomotion override; `OpenInvulnerability(400ms)`; dash clears 2.0u slam radius (load-time constraint check) | Core |
| TR-AI-001 | enemy-ai | State-machine executor + steering (pursuit + separation force, weight 0.4) with movement locks in Staggered/Roar | Core |
| TR-AI-002 | enemy-ai | Deterministic `DamageType`/`IsCounterHit` assignment at AttackEvent emission; Slam routes directly to Damage & Health bypassing Parry | Events |
| TR-AI-003 | enemy-ai | Aggro jitter (1–3 frames); Cooldown→Telegraph shortcut (non-Archer); Archer chase-fallback timer rules | Core |
| TR-PG-001 | progression-system | XP accumulation + level curve `round(xp_base × xp_growth^(level−1))`; overflow carry; sequential multi-level-up resolution (never two hands at once) | Core |
| TR-PG-002 | progression-system | Draw-of-3 without replacement from eligible pool; drought weighting (2× after 4 dry draws/lane); C3 draw condition; Modifier cap (3); category exhaustion | Core |
| TR-WS-001 | wave-spawner | Mandatory pre-warmed object pool sized to 200; `Instantiate`/`Destroy` per enemy forbidden | Lifecycle/Perf |
| TR-WS-002 | wave-spawner | Escalation as pure function of run time (`density(step)`, `speed(step)`); target-concurrent refill on 0.5s ticks capped by `max_spawns_per_tick` | Core |
| TR-WS-003 | wave-spawner | Spawn ring (14u) with arena-bounds rejection; scripted elite events (300/600/900s ±5s) + boss event (1080s) clearing active elites | Core |
| TR-RS-001 | run-session-manager | RSM owns `runStartTime` (single source), 1200s countdown, and run flow state machine (Selection/Running/Paused/Resolved-Win/Resolved-Death) | Core |
| TR-RS-002 | run-session-manager | Pause orchestration: `Time.timeScale = 0` + `InputManager.SetContext` on level-up pick; death-before-win priority on the final frame | Core |
| TR-RS-003 | run-session-manager | Stats aggregation (finishers, best parry chain, wave, time, cause-of-death) from system signals — Damage & Health stays a ledger | Core |
| TR-RS-004 | run-session-manager | Retry reset orchestration: calls every system's reset API (Damage & Health, Progression, Upgrade pool, enemy pool clear, timer) | Core |
| TR-UI-001 | combat-feedback-ui, run-ui, upgrade-selection-ui | All UI in UI Toolkit (UXML/USS); event-driven binding (no per-frame polling of HP); keyboard navigation with visible focus; no hover-only interaction | UI |
| TR-UI-002 | combat-feedback-ui | Hit-feedback throttling/pooling at density; DamageType-keyed responses; reduced-elite-hit distinction driven by `combo_counter < 2` | UI |
| TR-VA-001 | vfx-audio | VFX hierarchy (Finisher > parry burst > rest); shape-first colorblind-safe effects; screen-space overlays via `RecordRenderGraph` only; audio voice limiting (16) with priority ducking | Rendering/Audio |
| TR-TP-001 | technical-preferences / coding standards | All gameplay values data-driven (external config, never hardcoded); public APIs doc-commented; DI over singletons for testability; deterministic tests (manual tick pump, injected time, no random seeds) | Data/Testing |

**No save/load domain exists in MVP** — there is no persistence (no meta progression, no settings, no rebinding). The first persistent data arrives post-MVP; a save-architecture ADR is deferred until then.

---

## System Layer Map

```
┌────────────────────────────────────────────────────────────────────────┐
│ PRESENTATION                                                           │
│   CombatFeedbackUI · RunUI · UpgradeSelectionUI · VFXAudio             │
│   (read events/state; render; never mutate gameplay)                   │
├────────────────────────────────────────────────────────────────────────┤
│ FEATURE                                                                │
│   ComboSystem · ParrySystem · EnemySystemManager (Enemy AI) ·          │
│   WaveSpawner · ProgressionSystem                                      │
├────────────────────────────────────────────────────────────────────────┤
│ CORE                                                                   │
│   CombatCore (auto-attack + locomotion) · PlayerHealthSystem           │
│   (Damage & Health) · DodgeSystem                                      │
├────────────────────────────────────────────────────────────────────────┤
│ FOUNDATION                                                             │
│   RunSessionManager (run clock/flow) · InputManager · CombatFrame      │
│   (tick orchestrator) · ObjectPool · GameData (SO configs: EnemyDataSO,│
│   GeneralData, UpgradeCatalogue, TuningConfig)                         │
├────────────────────────────────────────────────────────────────────────┤
│ PLATFORM                                                               │
│   Unity 6.3 LTS · URP + RenderGraph · Input System pkg · UI Toolkit ·  │
│   VFX Graph · PhysX 5.1 · Unity Test Framework                         │
└────────────────────────────────────────────────────────────────────────┘
```

Rule: a module may depend downward only. Presentation reads Feature/Core state via events; Feature systems coordinate through Foundation-owned contracts (events, the frame orchestrator, pooled lifecycles). No upward references, no cross-layer mutation.

### Mapping the 17 GDD systems to modules

The three **pure data-definition systems** (Enemy Definition, General Characters, Upgrade Definition) do not become runtime modules — they become **ScriptableObject data assets** in the Foundation `GameData` module, consumed by runtime systems. Their GDD contracts (AttackEvent shape, stagger gates, catalogue rules) are enforced by the consuming modules.

| GDD system | Architecture module | Layer | Notes |
|---|---|---|---|
| Input Manager | `InputManager` | Foundation | HIGH-risk engine domain (Input System pkg) |
| Run Session Manager | `RunSessionManager` | Foundation | Owns `runStartTime`, flow, pause, resets |
| — (implied by contracts) | `CombatFrame` | Foundation | New infrastructure module: deterministic per-frame tick order (TR-DH-004, TR-IM-005) |
| — (implied by contracts) | `ObjectPool` | Foundation | New infrastructure module: enemy/projectile/VFX pooling + flush contract (TR-ED-004, TR-WS-001) |
| Enemy Definition / General Characters / Upgrade Definition | `GameData` (SO assets) | Foundation | Load-time validation + clamping (TR-ED-013); mirrors `design/registry/entities.yaml` |
| Combat Core | `CombatCore` | Core | Auto-attack, locomotion, knockback application |
| Damage & Health | `PlayerHealthSystem` | Core | HP ledger, iframes, `OpenInvulnerability` |
| Dodge System | `DodgeSystem` | Core | Dash + invuln request + locomotion override |
| Combo System | `ComboSystem` | Feature | Counter + Finisher execution |
| Parry System | `ParrySystem` | Feature | Windows, momentum, burst, event suppression |
| Enemy AI | `EnemySystemManager` | Feature | Single-manager tick for all enemies (TR-ED-001) |
| Wave Spawner | `WaveSpawner` | Feature | Scheduling + escalation + elite/boss timeline |
| Progression System | `ProgressionSystem` | Feature | XP curve + draw logic |
| Combat Feedback UI | `CombatFeedbackUI` | Presentation | HUD combat state + in-world telegraphs (with VFXAudio) |
| Upgrade Selection UI | `UpgradeSelectionUI` | Presentation | Pick screen |
| Run UI | `RunUI` | Presentation | HP/XP/timer HUD + selection/win/death screens |
| VFX & Audio | `VFXAudio` | Presentation | Effect/audio triggers, mix, budgets |

**Engine-risk flags on Core/Foundation modules**: `InputManager` → Input System package (HIGH — verified against `modules/input.md`: generated wrapper class + action maps pattern confirmed current). `EnemySystemManager`/`PlayerHealthSystem` → DOTS candidacy (HIGH — ADR-0001 decides; the default is MonoBehaviour + central manager, which uses no post-cutoff APIs). `CombatFrame`, `ObjectPool`, `GameData` → LOW (plain C# + ScriptableObjects, stable APIs).

---

## Module Ownership

For each module: what it exclusively owns, what it exposes, what it consumes, and the engine APIs it touches.

### Foundation layer

| Module | Owns (exclusive) | Exposes | Consumes | Engine APIs (risk) |
|---|---|---|---|---|
| **RunSessionManager** | `runStartTime`, run clock/countdown, run flow state machine, pause state, run statistics, reset orchestration | `run_time`/`time_remaining`; flow events (RunStarted, BossSpawnedWindow, RunResolved); `IRunResettable` registry it invokes on retry | `PlayerDead` (PlayerHealthSystem); level-up pause requests (Progression); Finisher/parry-chain stat signals (Combo/Parry); selected `GeneralId` (RunUI) | `Time.time`, `Time.timeScale` (LOW) |
| **InputManager** | Raw device access (sole subscriber), Input Actions asset, action-map context, parry press ring buffer, `CurrentMoveDirection`, `LastSteerDirection` | `TryConsumeParryPress(notBefore)`, `HasLiveParryPress()`, `CurrentMoveDirection`, `LastSteerDirection`, `SetContext()`, active-binding query (control card) | `runStartTime` (RSM) | Input System package: generated `PlayerControls`, `InputAction` callbacks (HIGH — verified `modules/input.md`) |
| **CombatFrame** | The canonical per-frame combat tick order (single `Update()` calling subsystems sequentially) | Registration points for ordered ticks | Ticks: ParrySystem → ComboSystem/CombatCore/DodgeSystem → PlayerHealthSystem → ProgressionSystem | `[DefaultExecutionOrder]`, MonoBehaviour lifecycle (LOW) |
| **ObjectPool** | Enemy/projectile lifecycle: pre-warm, spawn, `ReturnToPool` (the only legal `SetActive(false)` site), flush-before-deactivate contract | `Spawn(type, position)`, `ReturnToPool(controller)` | `ParrySystem.FlushEventsFor(id)` (called during return); pool sizes from TuningConfig | `GameObject.SetActive`, instantiation at pre-warm only (LOW) |
| **GameData** | All ScriptableObject configs: `EnemyDataSO` (per type + Warlord P1/P2), `GeneralData` ×3, `UpgradeCatalogue` (24 entries + draw rules metadata), `TuningConfig` (all tuning knobs); load-time validation/clamps + designer-error logging | Read-only typed access to every tuning value and data profile | `design/registry/entities.yaml` is the design-side source of truth; SO values must match it | ScriptableObject (LOW) |

### Core layer

| Module | Owns | Exposes | Consumes | Engine APIs (risk) |
|---|---|---|---|---|
| **CombatCore** | Auto-attack loop (interval, targeting, hit resolution), player locomotion, knockback application (incl. elite gate), A1–A6 applied values | `SwingLanded` signal; `ApplyKnockback(force, radius)` (spy-testable); melee geometry reuse for Finisher | `CurrentMoveDirection` (InputManager); `BaseAttack`/knockback params (GameData); enemy positions/HP/elite type (EnemySystemManager); dash override (DodgeSystem); upgrade deltas (Progression pick) | `Physics.OverlapSphereNonAlloc` + LayerMask (MEDIUM — verified `modules/physics.md`) |
| **PlayerHealthSystem** | `current_hp`/`max_hp`, effective-damage formula, iframe timer, `DamageTaken`/`PlayerDead` events, reset-threshold gate, upgrade caps enforcement | `DamageTaken` (struct delegate), `PlayerDead` (kill context), `OpenInvulnerability(ms)`, `Reset()` (guarded), `ForceState()` (test seam) | Resolved damage + DamageType (ParrySystem / EnemySystemManager slam path); S1–S5 deltas at pick; C5/P6/S5 heals | `Time.unscaledTime` (LOW) |
| **DodgeSystem** | Dash state machine, dodge cooldown, dash direction resolution | Dash-active flag (CombatCore reads to suppress attack/locomotion) | `Dodge` events + directions (InputManager); `OpenInvulnerability(400)` (PlayerHealthSystem) | none beyond transforms (LOW) |

### Feature layer

| Module | Owns | Exposes | Consumes | Engine APIs (risk) |
|---|---|---|---|---|
| **ComboSystem** | `combo_counter`, Finisher trigger + AoE execution, `FinisherUpgradeDelta`, C1–C6 applied values, cone orientation logic | `combo_counter` (read by Parry/CombatCore for gates); Finisher-resolved signal (VFX/stats); stagger requests to EnemySystemManager | `SwingLanded` (CombatCore); `GeneralData` geometry; `LastSteerDirection` (InputManager); `DamageTaken` (reset); P3 advance signal (ParrySystem) | overlap queries via CombatCore's buffer (LOW) |
| **ParrySystem** | Open-window set, momentum meter, burst execution, `momentum_reset_multiplier`, P1–P6/S6 applied values, `AttackerId → events` dictionary | `FlushEventsFor(id)`, `HasOpenWindowFor(id)`; parry-success signal (P3/VFX/stats); momentum value (S6/UI); suppression of parried AttackEvents | `AttackEvent` (EnemySystemManager); `TryConsumeParryPress` (InputManager); `combo_counter` (ComboSystem); `DamageTaken` reset event + `OpenInvulnerability` (PlayerHealthSystem) | none direct (LOW) |
| **EnemySystemManager** | All enemy state/HP, two-phase tick, AttackEvent emission, steering, stagger/roar/dead handling, per-enemy registration | `AttackEvent` stream; enemy positions/HP/elite type queries; XP-drop emission on Dead | `EnemyDataSO` (GameData); spawn registrations (WaveSpawner via ObjectPool); stagger requests (Combo/Parry); auto-attack damage (CombatCore); `runStartTime` (RSM) | `Physics.OverlapSphereNonAlloc`, `[DefaultExecutionOrder(-100)]` (MEDIUM/LOW); DOTS only if ADR-0001 flips (HIGH, gated) |
| **WaveSpawner** | Spawn schedule, escalation multipliers, elite/boss timeline, spawn positions | Wave-step / elite-spawn / boss-spawn events (music, stats); active speed multiplier (EnemySystemManager reads) | Run clock (RSM); type IDs + spawn weights (GameData); `ObjectPool.Spawn` | `Random` angle rolls (LOW) |
| **ProgressionSystem** | XP total, level, threshold curve, eligible pool state, drought streaks, Modifier pick count, draw execution | Level-up event (pause request to RSM); 3 drawn options (UpgradeSelectionUI); pick-applied routing to owning systems; XP-bar values (RunUI) | XP drops (EnemySystemManager); `UpgradeCatalogue` + draw rules (GameData); pick (UpgradeSelectionUI) | `Random` weighted draw (LOW) |

### Presentation layer

| Module | Owns | Consumes | Engine APIs (risk) |
|---|---|---|---|
| **CombatFeedbackUI** | Combat HUD (combo counter, momentum meter, hit feedback, damage numbers), telegraph rendering coordination | Combo/Parry state events; `DamageTaken` + DamageType; `AttackEvent` telegraphs; `HasLiveParryPress` peek | UI Toolkit (MEDIUM); VFX Graph (HIGH domain, but standard usage — verified) |
| **RunUI** | HP/XP/timer HUD, selection screen + geometry previews, control card, win/death screens | HP events (PlayerHealthSystem); XP (Progression); timer/stats/flow (RSM); roster (GameData); bindings (InputManager) | UI Toolkit (MEDIUM) |
| **UpgradeSelectionUI** | Pick screen (3 cards, focus navigation) | Drawn options (Progression); Navigate/Confirm (InputManager); show/hide (RSM) | UI Toolkit (MEDIUM) |
| **VFXAudio** | Effect assets + triggers, audio mix (voice limits, ducking), music intensity, screen-space overlays | All gameplay signals (Finisher, parry, DamageTaken, AttackEvent, dodge, wave events) | VFX Graph; `RecordRenderGraph` for overlays only (HIGH — verified `modules/rendering.md` + breaking-changes 6.3 note) |

### Dependency diagram (compile-time references, downward only)

```
 CombatFeedbackUI  RunUI  UpgradeSelectionUI  VFXAudio
        │(events/state subscriptions only)│
        ▼          ▼           ▼           ▼
 ComboSystem ◄──► ParrySystem      ProgressionSystem
      │  ▲            │  ▲                │
      │  └─SwingLanded│  └─AttackEvents   │ XP
      ▼               ▼                   │
 CombatCore      EnemySystemManager ◄── WaveSpawner
      │               │                   │
      ▼               ▼                   ▼
 DodgeSystem   PlayerHealthSystem    ObjectPool
      └──────┬────────┴───────┬──────────┘
             ▼                ▼
     InputManager      RunSessionManager ── CombatFrame ── GameData
```

(Combo↔Parry and Combo↔CombatCore bidirectional pairs are event/read-only in one direction — see API Boundaries for which side owns the reference.)

---

## Data Flow

### 1. Frame update path (the canonical tick order)

This order is a hard contract assembled from TR-IM-005, TR-ED-001, and TR-DH-004. It is enforced by two mechanisms: `[DefaultExecutionOrder]` for the managers that must precede everything, and the `CombatFrame` orchestrator for the combat sub-order (preferred over hidden Script Execution Order settings — damage-health Contract #3 option b).

```
[-200] InputManager.Tick        prune stale presses → capture new input →
                                update CurrentMoveDirection / LastSteerDirection
[-100] EnemySystemManager.Tick  flush RegisterPending →
                                PHASE 1 damage: apply damage, HP clamp,
                                  phase-2 check, stagger triggers
                                PHASE 2 state: Dead checks, Staggered exits
                                  (emit counter-hit AttackEvents), Cooldown
                                  expiry, Telegraph → Attack resolution,
                                  steering/movement
[   0] CombatFrame.Update       1. ParrySystem.Evaluate()   — match buffered
                                     presses to open windows; fire bursts;
                                     suppress parried events
                                2. DodgeSystem.Tick()        — dash state
                                3. CombatCore.Tick()         — locomotion,
                                     auto-attack, SwingLanded
                                4. ComboSystem.Tick()        — counter/timeout,
                                     Finisher on 3rd landing swing
                                5. PlayerHealthSystem.Apply()— unsuppressed
                                     damage → DamageTaken → resets/iframes
                                6. ProgressionSystem.Tick()  — XP thresholds,
                                     level-up requests
[+100] Presentation             UI Toolkit HUD updates, VFX triggers, audio
                                (LateUpdate or explicit late order)
```

Parry-before-damage in the same frame is what makes "parry input and damage arrive in the same frame → parry wins" true (game-concept edge case). Enemy AttackEvents emitted at `[-100]` are visible to the Parry System at `[0]` in the same frame.

### 2. Event/signal path

All gameplay events are **C# delegates with `readonly struct` payloads, owned by their publisher** (TR-DH-001). There is no global event bus, no string-keyed messaging, and no `UnityEvent` on any per-hit path. Subscription happens at run initialisation; unsubscription on teardown.

| Event | Publisher | Payload | Subscribers (ordered where it matters) |
|---|---|---|---|
| `AttackEvent` | EnemySystemManager | struct per enemy-definition contract | ParrySystem (window mgmt), CombatFeedbackUI (telegraph render) |
| `SwingLanded` | CombatCore | swing metadata | ComboSystem |
| `DamageTaken` | PlayerHealthSystem | `DamageTakenData` struct | **ordered**: 1. S6 evaluation (reads momentum pre-reset) 2. ParrySystem momentum reset 3. ComboSystem counter reset 4. UI/VFX (order fixed by ADR-0003; the S6-before-reset rule is upgrade-definition's required ADR) |
| `PlayerDead` | PlayerHealthSystem | kill-context struct | RunSessionManager |
| `ParrySucceeded` | ParrySystem | press time, burst values | ComboSystem (P3), VFXAudio, RSM (chain stats) |
| `FinisherResolved` | ComboSystem | general, targets hit | VFXAudio, RSM (stats), PlayerHealthSystem (C5 heal) |
| `LevelUp` / `OptionsDrawn` | ProgressionSystem | 3 option IDs | RSM (pause), UpgradeSelectionUI |
| Wave/elite/boss events | WaveSpawner | step / spawn type | VFXAudio (music/stingers), RSM (wave stat) |

Synchronous calls (not events): `TryConsumeParryPress`, `OpenInvulnerability`, `FlushEventsFor`, `ApplyKnockback`, stagger requests, upgrade-delta application, `SetContext`. These are ordinary method calls across module references wired at scene setup.

### 3. Initialisation and reset order

```
Boot → MainMenu → RSM.Selection (general select)
  → on confirm:
     1. RSM records runStartTime (first frame, before any game logic)
     2. RSM passes runStartTime → InputManager, EnemySystemManager, WaveSpawner
     3. GameData validation pass (clamps + designer-error log)  [once at load]
     4. Systems reset/initialise via IRunResettable (HP, XP, pool, meters)
     5. InputManager.SetContext(Gameplay); WaveSpawner starts at step 0
  → Running … → Resolved (win/death)
  → on retry: RSM invokes every IRunResettable.ResetForNewRun(), clears the
    enemy pool, resets the timer, re-enters Selection
```

Input capture and enemy spawning are **inert until `runStartTime` is set** (input-manager / enemy-definition contracts). Retry reset is guarded — only valid from a Resolved state.

### 4. Pause path (upgrade pick)

```
ProgressionSystem.LevelUp → RSM: queue until Finisher animation completes →
Time.timeScale = 0 → InputManager.SetContext(Menu) [parry buffer freezes] →
UpgradeSelectionUI.Show(options) → pick → Progression routes deltas to owning
system → RSM: SetContext(Gameplay), timeScale = 1 [buffer pruning resumes]
```

*(v1.1)* All combat timestamps (input presses, AttackStartTime, steer_time) use the unified pause-compensated `IGameClock.RunTime` so they stay mutually comparable across pauses. Iframes use `IGameClock.UnscaledTime`; regen uses scaled `deltaTime` and correctly pauses. This asymmetry is documented in damage-health Formula 2 as a design choice.

### 5. Threading

Everything is main-thread. No data flow crosses a thread boundary in the MonoBehaviour path. If ADR-0001 later flips the enemy tick to DOTS/Jobs, the AttackEvent stream and damage application become the sync points and that ADR must specify the boundary — until then, no threading contracts exist.

---

## API Boundaries

Public contracts between modules, in project C# style. These are the interfaces programmers implement against; invariants are binding.

### InputManager (Foundation)

```csharp
public interface IInputAuthority {
    Vector2 CurrentMoveDirection { get; }                       // may be zero
    SteerSample LastSteerDirection { get; }                     // direction + steer_time
    bool TryConsumeParryPress(float notBefore, out float pressTime);
    bool HasLiveParryPress();                                   // non-consuming peek
    void SetContext(InputContext context);                      // Gameplay | Menu | Paused
    void SetRunStartTime(float runStartTime);                   // RSM only, at run init
    string GetActiveBindingDisplay(GameAction action);          // control card
}
public readonly struct SteerSample { public readonly Vector2 Direction; public readonly float SteerTime; }
```

Invariants: only InputManager references `UnityEngine.InputSystem` device APIs (statically checkable — AC-IM-13); a consumed press is never returned again; capture is inert before `SetRunStartTime`; buffer freezes outside Gameplay context.

### PlayerHealthSystem (Core)

```csharp
public readonly struct DamageTakenData { public readonly float Amount, RawAmount; public readonly int AttackerId; public readonly DamageType Type; }
public readonly struct PlayerDeadData  { public readonly float FinalHP, TimeOfDeath, MomentumAtDeath; public readonly int KillerAttackerId, ComboCounterAtDeath; public readonly DamageType KillerDamageType; }

public interface IPlayerHealth : IRunResettable {
    event Action<DamageTakenData> DamageTaken;   // struct delegate — never UnityEvent
    event Action<PlayerDeadData>  PlayerDead;
    float CurrentHp { get; } int MaxHp { get; }
    void ApplyDamage(float rawDamage, int attackerId, DamageType type); // callers: ParrySystem (unparried), EnemySystemManager (Slam only)
    void OpenInvulnerability(int durationMs);    // single invuln owner; latest-end-time-wins
    void ApplyHeal(float amount);                // C5/P6/S5; clamped to MaxHp
}
```

Guarantees: `DamageTaken` fires before `PlayerDead` in the same frame; damage during iframes/Dead is discarded silently; `raw_damage ≤ 0` is rejected with a designer error; `Amount` is always a whole-number float. Callers must respect: nothing but Parry (unparried path) and EnemySystemManager (Slam) calls `ApplyDamage`.

### ParrySystem (Feature)

```csharp
public interface IParrySystem : IRunResettable {
    void ReceiveAttackEvent(in AttackEvent evt);   // Parriable=false → no window
    void FlushEventsFor(int attackerId);           // O(1); called by ObjectPool.ReturnToPool and Warlord phase transition
    bool HasOpenWindowFor(int attackerId);         // test observable (AC-06, AC-19)
    float MomentumMeter { get; }                   // S6 + UI read
    event Action<ParrySuccessData> ParrySucceeded;
}
```

Invariants: a parried AttackEvent never reaches `IPlayerHealth.ApplyDamage`; elite burst gate reads `IComboSystem.ComboCounter` at resolution time; one press satisfies at most one window (`notBefore` guard).

### ComboSystem / CombatCore (Feature / Core)

```csharp
public interface IComboSystem : IRunResettable {
    int ComboCounter { get; }                      // {0,1,2}; read by Parry + CombatCore gates
    void AdvanceFromParry();                       // P3 path; clamps at 2
    event Action<FinisherData> FinisherResolved;
}
public interface ICombatCore : IRunResettable {
    event Action<SwingData> SwingLanded;           // exactly +1 combo per landing swing
    void ApplyKnockback(float force, float radius, Vector3 origin); // elite gate inside; spy-testable
    void SetDashOverride(bool active);             // DodgeSystem only
}
```

### EnemySystemManager (Feature)

```csharp
public readonly struct AttackEvent {               // enemy-definition contract, verbatim
    public readonly int AttackerId; public readonly float AttackStartTime;
    public readonly int TelegraphDurationMs, AttackDamage;
    public readonly Vector3 AttackerPosition;
    public readonly bool Parriable, IsCounterHit;
    public readonly DamageType Type;               // Standard | CounterHit | Slam | Projectile
}
public interface IEnemyWorld {
    void RequestStagger(int attackerId, int comboCounterAtResolution); // Combo/Parry; gate applied inside
    void ApplyEnemyDamage(int attackerId, float damage, bool isFinisher, int comboCounter);
    EnemyQueryView NearestEnemies(Vector3 origin, float radius, Span<EnemyHit> buffer); // NonAlloc
    event Action<AttackEvent> AttackEmitted;
    event Action<XpDrop> EnemyDied;
}
```

Invariants: two-phase tick (damage before state transitions); `AttackerId` unique per spawn; movement locked in Staggered/Roar; Slam damage routes directly to `IPlayerHealth`, never through ParrySystem.

### Lifecycle + reset (Foundation)

```csharp
public interface IRunResettable { void ResetForNewRun(); }     // registered with RSM; invoked only from Resolved state
public interface IObjectPool {
    EnemyController Spawn(EnemyTypeId type, Vector3 position); // assigns fresh AttackerId, initial cooldown jitter
    void ReturnToPool(EnemyController enemy);                  // flushes parry events BEFORE SetActive(false); sole legal deactivation site
}
```

### Upgrade application (Progression → owning systems)

Upgrades are applied as **typed setter calls on the owning system at pick time** — no reflection, no string lookups, no upgrade logic in receivers. `UpgradeCatalogue` entries carry a delta descriptor; `ProgressionSystem` routes each to its owner (C→Combo, A→CombatCore, P/S6→Parry, S1–S5→PlayerHealthSystem). Receivers validate against caps (`damage_reduction_cap`, `regen_rate_max`, delta caps) and log designer errors on violation.

### Engine-type exposure rule

Public cross-module interfaces expose only: primitives, project structs/enums, `Vector2`/`Vector3`, and `Span<T>` buffers. No `GameObject`, `Transform`, `Collider`, or UI Toolkit types cross module boundaries — engine types stay inside the module that touches the engine. This keeps every Feature/Core interface testable in EditMode with a manual tick pump (TR-TP-001).

---

## ADR Audit

`docs/architecture/` contained **zero ADRs** at the time of this session. There is nothing to audit for quality or conflicts; every architectural decision made in this document is currently unrecorded, and every Technical Requirement is a coverage gap by definition.

**Traceability coverage: 0 of 58 requirements covered by an Accepted ADR.** All 58 map onto the twelve Required ADRs below. Per project rules (`docs/CLAUDE.md`), stories referencing a `Proposed` ADR are auto-blocked — the "must have" group below therefore gates the start of implementation.

---

## Required ADRs

Grouped by priority. Each lists the decision to record, the recommended position this architecture assumes, and the TR-IDs it covers. Author via `/architecture-decision`, Foundation group first.

### Must have before any coding starts

1. **ADR-0001 — Entity architecture: MonoBehaviour + central manager vs DOTS for the 200-enemy tick**
   Recommended: MonoBehaviour with the single `EnemySystemManager` (no per-enemy `Update()`), plain C# state structs, pre-allocated buffers. DOTS (`ISystem`/`IJobEntity`) only if profiling at 200 enemies breaks the 16.6ms budget — it is production-ready in 6.3 but its API churn is the project's highest knowledge-gap risk, and every GDD implementation contract is already written against the manager pattern.
   Covers: TR-DH-002, TR-ED-001, TR-ED-008, TR-ED-012, TR-GC0-001.

2. **ADR-0002 — Canonical frame execution order and the CombatFrame orchestrator**
   Recommended: `[-200] InputManager → [-100] EnemySystemManager → [0] CombatFrame (Parry → Dodge → CombatCore → Combo → Health → Progression) → [+100] Presentation`, with the combat sub-order as explicit sequential calls in one `Update()` rather than Script Execution Order entries.
   Covers: TR-IM-005, TR-DH-004, TR-ED-001 (ordering half), TR-PR-003.

3. **ADR-0003 — Event architecture and DamageTaken subscriber ordering**
   Recommended: publisher-owned C# delegates with `readonly struct` payloads; `UnityEvent` forbidden on combat paths; explicit ordered dispatch for `DamageTaken` (S6 evaluation → momentum reset → combo reset → presentation). This is the S6 event-ordering ADR that upgrade-definition.md names as mandatory.
   Covers: TR-DH-001, TR-DH-009, TR-PR-004, TR-CC-002 (signal form).

4. **ADR-0004 — Single input authority and the parry input buffer**
   Recommended: Input System package with one generated wrapper; only InputManager subscribes to devices; timestamped ring buffer with consumer-pull (`TryConsumeParryPress`); run-local unscaled timestamps.
   Covers: TR-IM-001…005.

5. **ADR-0005 — Object pooling and enemy lifecycle**
   Recommended: pre-warmed pools sized to `max_enemy_count`; `ReturnToPool` as the sole `SetActive(false)` site with flush-before-deactivate; per-spawn `AttackerId`; deferred registration; spawn cooldown jitter.
   Covers: TR-ED-003, TR-ED-004, TR-ED-008, TR-ED-009, TR-WS-001.

6. **ADR-0006 — Data-driven configuration via ScriptableObjects (no Addressables in MVP)**
   Recommended: `EnemyDataSO` / `GeneralData` / `UpgradeCatalogue` / `TuningConfig` as directly-referenced SOs; load-time validation with clamps + designer-error logs; `design/registry/entities.yaml` remains the design source of truth that SO values must mirror. Addressables deferred — MVP content is one scene of direct references; `Resources.Load` forbidden either way.
   Covers: TR-ED-007, TR-ED-013, TR-GC-001, TR-CC-005, TR-TP-001 (data half), TR-GC0-003 (config reset).

7. **ADR-0007 — Spatial queries and enemy steering**
   *(v1.1: superseded — the Accepted ADR-0007 uses the per-frame uniform grid for ALL queries, no enemy colliders.)* Original recommendation: layer-filtered `Physics.OverlapSphereNonAlloc` with pre-allocated 200-slot buffers for aggro/attack/melee checks; custom separation-vector steering (no NavMesh — a flat open arena with 200 agents doesn't justify NavMesh agent cost). Benchmark both steering options at 50/100/200 in the prototype (enemy-ai OQ-01) before locking.
   Covers: TR-ED-005, TR-CC-001, TR-AI-001, TR-AI-003.

### Should have before the relevant system is built

8. **ADR-0008 — Telegraph and combat VFX rendering** (before VFX & Audio / Combat Feedback UI)
   Recommended: VFX Graph + GPU instancing, one asset per telegraph shape; screen-space overlays via `RecordRenderGraph` only; draw-call budget allocation (telegraphs capped, post-process first to throttle).
   Covers: TR-ED-006, TR-VA-001, TR-UI-002 (telegraph half), TR-GC0-001 (rendering half).

9. **ADR-0009 — Runtime UI architecture** (before any UI system)
   Recommended: UI Toolkit (UXML/USS) for all screens/HUD; event-driven data binding (no per-frame polling — resolves run-ui OQ-01); RSM drives screen flow; focus-visible keyboard navigation baseline.
   Covers: TR-UI-001, TR-RS-002 (UI half).

10. **ADR-0010 — Deterministic test harness** (before the first gameplay story)
    Recommended: EditMode-first testing with manual `Tick()` pump and injected clock; test seams (`ForceState`, `ResetInstanceIdCounter`) compiled in editor only; Unity Test Framework + `game-ci/unity-test-runner@v4` in CI (also run `/test-setup`).
    Covers: TR-DH-006, TR-ED-003 (test half), TR-TP-001.

### Can defer to implementation of the specific system

11. **ADR-0011 — Audio mix architecture** (voice limits, priority ducking, music intensity layers) — before VFX & Audio implementation. Covers: TR-VA-001 (audio half).
12. **ADR-0012 — Scene organisation and boot flow** (single scene vs additive; menu→run transition without loading screen) — before Run Session Manager implementation. Covers: TR-RS-001 (flow half), TR-GC0-002.

Remaining TRs (combat rules: TR-CB-*, TR-PR-001/002, TR-DG-001, TR-AI-002, TR-PG-*, TR-WS-002/003, TR-RS-003/004, TR-DH-003/005/007/008, TR-GC-002/003) are **design-complete in their GDDs and structurally covered by ADR-0001/0002/0003** — they need stories, not further architectural decisions.

---

## Architecture Principles

1. **One owner per fact.** Every value, event, and timer has exactly one owning module (HP → PlayerHealthSystem, invulnerability → `OpenInvulnerability`, combo → ComboSystem, clock → RSM, input → InputManager). Consumers read or subscribe; they never duplicate state. The GDD set was written this way — the code must preserve it.
2. **Deterministic simulation core.** All combat logic is tick-driven with injected time and explicit ordering (CombatFrame), testable in EditMode without the engine loop, animation system, or wall-clock waits. Determinism is a coding-standard requirement, not a nice-to-have.
3. **Data-driven everything.** Every tuning knob across 17 GDDs lives in ScriptableObject config validated at load. Balance changes never require a code change (coding standard: gameplay values external, never hardcoded).
4. **Allocation-free hot path.** At 200 enemies: struct events, NonAlloc queries, pre-allocated buffers, pooled objects, no per-enemy `Update()`, no `UnityEvent`/boxing per hit. The 16.6ms budget is a design pillar dependency (Readable Chaos requires the density).
5. **Presentation is subordinate and one-way.** UI/VFX/audio subscribe and render; they never mutate gameplay state. Readability (Pillar 1) outranks spectacle — an effect that hides a telegraph is cut regardless of quality.

---

## Open Questions

Deferred decisions that must be resolved before the relevant layer is built:

| # | Question | Blocking | Resolution path |
|---|---|---|---|
| AOQ-01 | MonoBehaviour manager vs DOTS if 200-enemy profiling fails budget | ADR-0001 final status | Prototype performance baseline at 50/100/200 enemies (systems-index high-risk mitigation) |
| AOQ-02 | Custom separation steering vs NavMesh avoidance | ADR-0007 | Benchmark in prototype (enemy-ai OQ-01) |
| AOQ-03 | `telegraph_concurrent_cap` value before readability/perf breaks | ADR-0008 tuning | Empirical, prototype (enemy-definition OQ-03) |
| AOQ-04 | Balance concerns deferred from the 2026-06-19 cross-review: parry burst ceiling (80) vs Finisher (25–40) dominance; base Finisher 25 under-killing Levy 30 HP; C5 aggregate-heal ceiling (OQ-11); no player-power scaling vs density | none (tuning-knob values, not structure) | `/prototype combo-and-parry-system` + `/balance-check` |
| AOQ-05 | Pause asymmetry: iframes on unscaled time vs regen on scaled time — fair or confusing? | none | Playtest; align both to unscaled via ADR amendment if unfair (damage-health note) |
| AOQ-06 | Art bible not yet authored — gates final VFX/UI asset production (not architecture) | asset production only | `/art-bible`, then `/asset-spec` |

---

## Technical Director Self-Review (Gate TD-ARCHITECTURE, Lean mode)

| Criterion | Assessment |
|---|---|
| 1. Every TR covered by an architectural decision? | **Yes** — 58/58 TRs map to the 12 Required ADRs or are design-complete GDD rules structurally covered by ADR-0001/0002/0003. No orphan requirements found. |
| 2. HIGH-risk engine domains addressed or flagged? | **Yes** — Input (ADR-0004, verified against modules/input.md), RenderGraph/VFX (ADR-0008, 6.3 Compatibility-Mode deprecation explicitly handled), DOTS (ADR-0001 defers with a recommended default and a profiling trigger). |
| 3. API boundaries clean, minimal, implementable? | **Yes with one watch-item** — interfaces are small, engine-type-free, and match GDD contracts verbatim. Watch-item: the Combo↔Parry↔CombatCore bidirectional trio needs care at wiring time to avoid reference cycles; the interface split (events one way, reads the other) resolves it but ADR-0002 should show the wiring diagram. |
| 4. Foundation ADR gaps resolved before implementation? | **Not yet — by design.** ADRs 0001–0007 must be authored and Accepted before the first story. This is the named next step. |

**Verdict: APPROVED WITH CONDITIONS** — conditions: (a) author ADRs 0001–0007 before implementation begins; (b) this document was produced in an autonomous session — the user should review the layer map and the 12 ADR recommendations before `/architecture-decision` runs lock them in.

LP-FEASIBILITY review skipped — Lean mode (`production/review-mode.txt`).


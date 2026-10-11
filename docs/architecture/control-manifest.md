# Control Manifest

> **Engine**: Unity 6.3 LTS (URP, Forward+)
> **Last Updated**: 2026-10-10
> **Manifest Version**: 2026-10-10
> **ADRs Covered**: ADR-0001, ADR-0002, ADR-0003, ADR-0004, ADR-0005, ADR-0006, ADR-0007, ADR-0008, ADR-0009, ADR-0010 (all as amended 2026-10-10), ADR-0012
> **Also covers**: binding asset rules from `design/art/art-bible.md` §8 (provisional art bible; asset rules are technical, not taste)
> **Status**: Active — regenerate with `/create-control-manifest update` when ADRs change
> **Supersedes**: Manifest Version 2026-07-04 — stories embedding that version were written against superseded rules (collider-based queries, split clocks, static AttackerId). None exist yet.

`Manifest Version` is the date this manifest was generated. Story files embed
this date when created. `/story-readiness` compares a story's embedded version
to this field to detect stories written against stale rules.

This manifest is a programmer's quick-reference extracted from all Accepted ADRs,
technical preferences, and engine reference docs. For the reasoning behind each
rule, see the referenced ADR.

---

## Foundation Layer Rules

*Applies to: Bootstrap, CombatFrame, InputManager, RunSessionManager, ObjectPool, GameData*

### Required Patterns
- **One scene (`MainArena.unity`) for the whole MVP; `Bootstrap` is the composition root** — boot order: GameData validation → construct + inject all systems (incl. `UiInputModuleBinder.Bind`) → ObjectPool pre-warm → RSM `Selection`. Menu/select/end screens are UI Toolkit states, not scenes — source: ADR-0012
- **Run-start order is binding (same frame, before any game logic)**: RSM records the run origin → `SetRunStartTime` on InputManager/EnemySystemManager/WaveSpawner → every `IRunResettable.ResetForNewRun()` → `SetContext(Gameplay)` → spawner unfreezes. All three consumers stay inert until then — source: ADR-0012
- **ONE run clock for all combat timestamps: `IGameClock.RunTime`** (pause-compensated, run-local, origin owned by RSM). `AttackStartTime`, parry press stamps, `steer_time`, and every parry-window comparison use it — source: ADR-0010, ADR-0004 (TR-RS-005)
- **RSM owns the run countdown** (`run_duration_s`, default 1200s from TuningConfig) and derives every scheduled trigger (wave steps, elites, boss, win) from the run clock; no other system keeps a run timer — source: ADR-0012
- **Arena bounds live in `TuningConfig.arena_bounds`** (rectangle), exposed read-only via GameData to WaveSpawner and DodgeSystem — source: ADR-0012
- **InputManager creates the single `PlayerControls` instance**; `UiInputModuleBinder` hands that same instance's action references to `InputSystemUIInputModule`. Project-wide default actions asset removed/unassigned — source: ADR-0004
- **Parry presses go into the ring buffer** (capacity 4, 120ms life), stamped from the event's own clock (`ctx.time`) converted to `IGameClock.RunTime`; consumers use `TryConsumeParryPress(notBefore)` / `HasLiveParryPress()`; one press = one consumption — source: ADR-0004
- **UI action map is a superset**: Navigate, Submit, Cancel, Point, Click, ScrollWheel — source: ADR-0004
- **Pin `Active Input Handling` = "Input System Package (New)", `updateMode` = Dynamic Update, and an exact `com.unity.inputsystem` version** in the manifest; assert at bootstrap — source: ADR-0004
- **Frame order is fixed**: `[-200] InputManager → [-100] EnemySystemManager → [0] CombatFrame (Parry → Dodge → CombatCore → Combo → PlayerHealth → Progression) → [+100] Presentation`; combat sub-order is explicit sequential calls in `CombatFrame.RunFrame(dt)` — source: ADR-0002
- **Wire dependencies by constructor/initializer injection at Bootstrap** — interfaces only, no singletons, no `FindObjectOfType`/`FindFirstObjectByType` lookups — source: ADR-0002, ADR-0012
- **All enemy/projectile lifecycles go through `ObjectPool`**: `Spawn()` assigns `AttackerId` from the pool's **instance field** `_nextInstanceId++` (never static, never per slot) and jitter from the **injected `System.Random`** via `Initialize(in SpawnParams)` — source: ADR-0005
- **Pre-warm under an inactive container**; `OnEnable` stages registration only when `_initialized` is set (set by `Initialize`, cleared by `ReturnToPool`) — prevents phantom registrations — source: ADR-0005
- **`ReturnToPool` order is binding**: `ParrySystem.FlushEventsFor(id)` → deregister → `SetActive(false)` → push — source: ADR-0005
- **All gameplay values live in `GameData` ScriptableObjects** (`EnemyDataSO`, `GeneralData`, `UpgradeCatalogueSO`, `TuningConfig`), direct serialized references — source: ADR-0006
- **Run `GameDataValidator` at Bootstrap**: every GDD clamp rule, `[DESIGNER-ERROR]` logging, assert in editor / clamp-and-continue in builds — source: ADR-0006
- **Gameplay logic reads time via injected `IGameClock`** and randomness via injected `System.Random` — source: ADR-0010
- **`IRunResettable.ResetForNewRun()`** is the reset contract; RSM invokes it only from a Resolved state — source: ADR-0002, ADR-0012

### Forbidden Approaches
- **Never reference `Unity.InputSystem` outside the `Dynasty.Input` assembly** (`InputManager.cs` + `UiInputModuleBinder.cs` only) — compile-time asmdef gate; advisory grep `\bInput\.(Get|mouse|anyKey|touch)|Pointer\.current|Touchscreen\.current|Joystick\.current|Pen\.current|\bInputSystem\.` — source: ADR-0004
- **Never mix `Time.time`-derived and `Time.unscaledTime`-derived run-local values** — they diverge by total paused duration and silently break parry matching after the first level-up — source: ADR-0010 (conflict C1)
- **Never call `SceneManager.LoadScene` in MVP** — single-scene flow — source: ADR-0012
- **Never let a gameplay object self-initialise or touch another system in `Awake/Start`** — Bootstrap constructs and wires everything — source: ADR-0012
- **Never use a static `AttackerId` counter** — source: ADR-0005
- **Never call `SetActive(false)` on an enemy (or `Destroy` any pooled object) outside `ObjectPool.ReturnToPool`** — source: ADR-0005
- **Never register with a manager directly from `OnEnable`** — stage on `RegisterPending` — source: ADR-0001, ADR-0005
- **Never use `Resources.Load` or Addressables in MVP** — direct serialized references only — source: ADR-0006
- **Never hardcode a gameplay/tuning value** (exception: boundary-value tests) — source: ADR-0006
- **Never rely on `OnTriggerExit` for deactivation cleanup** — source: ADR-0005

### Performance Guardrails
- **Zero `Instantiate`/`Destroy` during a run** (Profiler-verified) — source: ADR-0005
- **Bootstrap validation is the only load-time pass** — source: ADR-0006

---

## Core Layer Rules

*Applies to: CombatCore, PlayerHealthSystem, DodgeSystem*

### Required Patterns
- **`DamageTaken` dispatch order is binding** (inside PlayerHealthSystem): S6 reduction evaluated inside the damage formula (pre-reset momentum) → apply damage → threshold-gated resets (Parry then Combo, only if `effective_damage ≥ reset_damage_threshold`) → presentation multicast → `PlayerDead` (same frame, once, kill context captured pre-reset) — source: ADR-0003
- **Events are publisher-owned `System.Action<T>` with `readonly struct` payloads**, subscribed once at init with cached delegates — source: ADR-0003
- **Invulnerability has one owner**: `PlayerHealthSystem.OpenInvulnerability(ms)`, single timer, latest-end-time-wins, on `IGameClock.UnscaledTime` — source: ADR-0003, ADR-0010
- **Finisher handshake**: `CombatCore` reads `combo.ComboCounter` BEFORE applying swing damage; a primed 3rd landing swing (`== 2`) applies no normal damage and delegates to the Finisher in the same tick — source: ADR-0002 (TR-CC-006)
- **Melee/AoE hit detection queries the shared uniform grid via `IEnemyWorld.NearestEnemies`** (sphere-vs-cell, centre-distance semantics) — source: ADR-0007
- **Nearest-target selection ties break by lower entity index** — source: ADR-0007
- **Only ParrySystem (unparried path) and EnemySystemManager (Slam) call `IPlayerHealth.ApplyDamage`** — source: ADR-0003
- **Locomotion and dash are transform integration**; the dash clamps to `ArenaBounds` — source: ADR-0007, ADR-0012
- **Doc comments on all public APIs; PascalCase public / `_camelCase` private fields** — source: technical-preferences

### Forbidden Approaches
- **Never use `UnityEvent`/`SendMessage` for combat events** — source: ADR-0003
- **Never implement `Update()`/`FixedUpdate()` in a combat system** — source: ADR-0002
- **Never allocate a closure lambda in a hot dispatch path** — source: ADR-0003
- **Never write state owned by another system** (`docs/registry/architecture.yaml` state_ownership) — source: ADR-0003
- **Never read `Time.time`/`Time.unscaledTime` or use `UnityEngine.Random` inside testable gameplay logic** — source: ADR-0010

### Performance Guardrails
- **Zero GC allocation per event dispatch** — verify under 200-enemy load — source: ADR-0003
- **Payload structs ≤ ~48 bytes; pass by `in`** where profiling warrants — source: ADR-0003

---

## Feature Layer Rules

*Applies to: ComboSystem, ParrySystem, EnemySystemManager, WaveSpawner, ProgressionSystem*

### Required Patterns
- **One `EnemySystemManager` ticks all enemies** in two sequential phases per frame (damage → state-transition), `[DefaultExecutionOrder(-100)]`, no per-enemy `Update()` — source: ADR-0001
- **AttackEvents deliver via direct call** `parry.ReceiveAttackEvent(in evt)` plus a presentation multicast; ParrySystem keys events by `AttackerId` (O(1) `FlushEventsFor`) — source: ADR-0003
- **The uniform spatial grid (cell 1u) is rebuilt once per frame after the enemy move phase and serves ALL queries** — separation neighbours and player-centric AoE; enemy→player range is plain distance-squared — source: ADR-0007
- **Enemies carry NO physics colliders in the primary path** and move by direct transform integration in the manager tick — source: ADR-0007
- **Knockback is a steering-velocity impulse applied by EnemySystemManager**, never a Rigidbody force — source: ADR-0007 (TR-CC-004)
- **Spawn scheduling is a pure function of the run clock**; boss spawn returns active elites to the pool WITHOUT death/XP signals — source: ADR-0005, ADR-0012
- **Draw logic (drought, C3 condition, Modifier cap) uses the injected seedable RNG** — source: ADR-0010

### Forbidden Approaches
- **Never brute-force O(n²) range checks** — source: ADR-0007
- **Never add colliders/Rigidbodies to enemies for queries** unless the grid benchmark fails at 200 AND a superseding amendment adopts the documented fallback (kinematic Rigidbody + one `Physics.SyncTransforms()` after the move phase + Enemy×Enemy layer off + explicit `QueryTriggerInteraction`) — source: ADR-0007 (finding N1)
- **Never use allocating physics queries** (`OverlapSphere`, `RaycastAll`) anywhere — source: ADR-0007
- **Never use NavMesh/NavMeshAgent in MVP** — source: ADR-0007
- **Never let DOTS/ECS code in without a superseding ADR**; escalation order is (a) `TransformAccessArray` + `IJobParallelForTransform` + Burst, then (b) Entities 1.3 `ISystem`/`IJobEntity` — source: ADR-0001

### Performance Guardrails
- **Enemy tick (all 200) ≤ ~4ms**, with **queries + steering ≤ ~2ms as a NESTED subset** (not additive) — source: ADR-0001, ADR-0007
- **Benchmark in a player build (record Mono/IL2CPP) on the pinned min-spec — never in the Editor** — source: ADR-0001

---

## Presentation Layer Rules

*Applies to: CombatFeedbackUI, RunUI, UpgradeSelectionUI, VFXAudio*

### Required Patterns
- **All runtime UI is UI Toolkit**; presenters subscribe to the presentation event tier and write elements only on changed values (diff before write) — source: ADR-0009
- **UI navigation arrives through `InputSystemUIInputModule` bound to InputManager's instance**; presenters handle `Navigation*Event`; `IInputAuthority` is NOT a navigation route; one shared `PanelSettings` — source: ADR-0009, ADR-0004
- **Set explicit initial focus on every `Show()`** after layout resolves; interactive elements `focusable` + `tabIndex` — source: ADR-0009
- **Visible `:focus` style on every interactive screen; hover ≡ focus; no hover-only affordances** — source: ADR-0009, art bible §7.5
- **Bar fills use `style.scale` (scaleX, origin left), never `style.width`**; animated elements set `usageHints` (`DynamicTransform`/`DynamicColor`); damage numbers use a cached number-string table; HUD roots `pickingMode = Ignore` — source: ADR-0009
- **Telegraphs: one VFX Graph system per shape rendering N instances (exactly one `VisualEffect` per shape)**; per-instance data via GraphicsBuffer + CPU slot allocator; progress computed in-graph from `IGameClock.RunTime`; manual padded arena bounds; `capacity ≥ telegraph_concurrent_cap` — source: ADR-0008
- **Screen-space overlays (if built) prefer Volume overrides / Full Screen Pass Renderer Feature**; any custom pass implements `RecordRenderGraph` — source: ADR-0008
- **Cull priority**: post-process throttles first; counter-hit arc and Warlord slam never culled — source: ADR-0008
- **Warm each hero VFX graph once at load**; release GraphicsBuffers on teardown — source: ADR-0008
- **Hit feedback throttled (`hit_feedback_throttle_ms` 50); damage numbers pooled (64)** — source: ADR-0009

### Forbidden Approaches
- **Never pool per-telegraph `VisualEffect` components or per-enemy `ParticleSystem`s** — source: ADR-0008
- **Never write a URP Compatibility Mode pass** (`Execute(ScriptableRenderContext, ref RenderingData)`) — source: ADR-0008
- **Never use UGUI Canvas for a new screen** — source: ADR-0009
- **Never poll gameplay state per-frame from UI**; never use UI Toolkit runtime data-binding polling modes — source: ADR-0009
- **Never use `VisualElement.transform`** (deprecated 6.2+) — source: ADR-0009
- **Presentation never mutates gameplay state** — source: architecture principles

### Performance Guardrails
- **Telegraph draw calls ≤ ~50; hero effects ≤ ~40; total ≤ 1500 with 1,100 as the early-warning line** — source: ADR-0008, art bible §8.8
- **HUD cost proportional to state changes; timer throttled to 1Hz** — source: ADR-0009

---

## Asset Rules (from art bible §8 — binding technical constraints)

- **Shader**: one custom Shader Graph toon shader (`DynastyToon`) for all opaque world assets; ≤2 texture samples/fragment
- **Crowd materials**: one shared material across infantry; hit flash = `sharedMaterial` swap to `CrowdToonFlash`. **Never `MaterialPropertyBlock` or `.material` on crowd renderers**
- **Rigs**: Generic only (Humanoid forbidden); infantry ≤20 bones, 2 skin influences; Optimize Game Objects ON; no bone colliders, no IK on crowd
- **Root motion forbidden** (`applyRootMotion = false`; in-place clips); **no gameplay animation events** — timing comes from the manager clock
- **Polys (hard caps)**: infantry 500, elite 1,500, boss 4,000, general 3,000 (+600 weapon); LOD0 only
- **Shadows**: crowd casts none (instanced blob discs, one draw); realtime shadows for player/elite/boss/static only
- **Import**: Read/Write OFF, tangents None, materials never extracted, scale 1, pivot at feet, forward +Z

---

## Global Rules (All Layers)

### Naming Conventions
| Element | Convention | Example |
|---------|-----------|---------|
| Classes | PascalCase | `ComboSystem`, `PlayerGeneral` |
| Public properties/fields | PascalCase | `MoveSpeed`, `MaxHealth` |
| Private fields | _camelCase | `_comboCounter`, `_momentumMeter` |
| Methods | PascalCase | `TakeDamage()`, `TriggerFinisher()` |
| Events/Delegates | PascalCase + EventHandler suffix (project events use `Action<T>` fields named for the event) | `ParrySuccessEventHandler` / `DamageTaken` |
| Files | PascalCase matching class | `ComboSystem.cs` |
| Scenes/Prefabs | PascalCase | `MainArena.unity`, `PlayerGeneral.prefab` |
| Assets | PascalCase, no underscores/spaces/dots | `LevySoldier@Move.fbx`, `CrowdToon.mat` |
| Constants | PascalCase | `MaxComboCount` |
| Tests | `[system]_[feature]_test.cs`; `test_[scenario]_[expected]` | `parry_window_test.cs` |

### Performance Budgets
| Target | Value |
|--------|-------|
| Framerate | 60 fps |
| Frame budget | 16.6 ms |
| Draw calls | ≤ 1500 (early warning 1,100) |
| Memory ceiling | 4 GB (texture target ≤128 MB) |
| Enemy tick share | ~4 ms at 200 enemies (ADR-0001) |
| Queries + steering | ~2 ms at 200 — nested inside the enemy tick (ADR-0007) |
| Animator + skinning | ≤3.0 ms at 200 — VAT escalation gate (art bible §8.8) |

### Approved Libraries / Addons
- Input System package (exact version pinned) — input (ADR-0004)
- UI Toolkit — runtime UI (ADR-0009)
- VFX Graph — telegraphs/effects (ADR-0008)
- Unity Test Framework + `game-ci/unity-test-runner@v4` — testing/CI (ADR-0010)
- *(No third-party libraries approved yet — additions require a technical-preferences update)*

### Forbidden APIs (Unity 6.3 LTS)
- `UnityEngine.Input.*` (legacy input) → Input System package (inside `Dynasty.Input` only)
- `Resources.Load()` / Addressables → direct serialized references in MVP
- `Physics.RaycastAll()` / allocating `OverlapSphere` → grid queries (NonAlloc only on the fallback path)
- `SceneManager.LoadScene` → forbidden in MVP (single scene, ADR-0012)
- `Rigidbody.velocity` → renamed `linearVelocity` in Unity 6 (N/A in MVP — no rigidbody movement)
- `FindObjectOfType` → superseded by `FindFirstObjectByType` — and both are forbidden for wiring (DI at Bootstrap)
- UGUI `Canvas`/`Text`/`Image` for new UI → UI Toolkit
- `VisualElement.transform` → `style` transform properties
- Legacy Particle System → VFX Graph
- `ComponentSystem` / `GameObjectEntity` → `ISystem`/`IJobEntity` (only if the DOTS ADR ever fires)
- `CommandBuffer.DrawMesh()` / `OnPreRender`/`OnPostRender` / Compatibility Mode passes → RenderGraph API
- `WWW` → `UnityWebRequest`

### Cross-Cutting Constraints
- **Tests first** for gameplay systems; every Logic AC is a deterministic EditMode test on the manual pump — source: coding standards, ADR-0010
- **Never disable or skip a failing test to make CI pass** — source: coding standards
- **Commits reference the relevant design doc or task ID** — source: coding standards
- **Check `docs/engine-reference/unity/` before using any engine API** — source: docs/CLAUDE.md
- **Check `docs/registry/architecture.yaml` before claiming state ownership or defining a cross-system contract** — source: registry rules
- **C# `record`/`init` require a project-defined `System.Runtime.CompilerServices.IsExternalInit` shim in Unity** — source: engine-reference correction 2026-10-10

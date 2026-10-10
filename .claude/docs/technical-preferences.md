# Technical Preferences

<!-- Populated by /setup-engine. Updated as the user makes decisions throughout development. -->
<!-- All agents reference this file for project-specific standards and conventions. -->

## Engine & Language

- **Engine**: Unity 6.3 LTS
- **Language**: C#
- **Rendering**: Unity URP (Universal Render Pipeline) — stylized 3D on PC
- **Physics**: Unity PhysX (3D) — standard for action games

## Input & Platform

<!-- Written by /setup-engine. Read by /ux-design, /ux-review, /test-setup, /team-ui, and /dev-story -->
<!-- to scope interaction specs, test helpers, and implementation to the correct input methods. -->

- **Target Platforms**: PC (Steam / itch.io)
- **Input Methods**: Keyboard/Mouse, Gamepad
- **Primary Input**: Keyboard/Mouse
- **Gamepad Support**: Partial
- **Touch Support**: None
- **Platform Notes**: PC-first; menus must support keyboard navigation; no hover-only interactions

## Naming Conventions

- **Classes**: PascalCase (e.g., `PlayerGeneral`, `ComboSystem`)
- **Public Properties/Fields**: PascalCase (e.g., `MoveSpeed`, `MaxHealth`)
- **Private Fields**: _camelCase (e.g., `_comboCounter`, `_momentumMeter`)
- **Methods**: PascalCase (e.g., `TakeDamage()`, `TriggerFinisher()`)
- **Events/Delegates**: PascalCase + EventHandler suffix (e.g., `ParrySuccessEventHandler`)
- **Files**: PascalCase matching class (e.g., `ComboSystem.cs`, `PlayerGeneral.cs`)
- **Scenes/Prefabs**: PascalCase (e.g., `PlayerGeneral.prefab`, `MainArena.unity`)
- **Constants**: PascalCase (e.g., `MaxComboCount`, `DefaultMomentumDecay`)

## Performance Budgets

<!-- Generous 3D budgets for PC — performance is not a chief concern during initial development -->
- **Target Framerate**: 60fps
- **Frame Budget**: 16.6ms
- **Draw Calls**: ≤1500
- **Memory Ceiling**: 4GB
- **Min-Spec (benchmark reference, pinned 2026-10-10)**: NVIDIA GTX 1650 (4GB VRAM) · AMD Ryzen 5 3600 (6C/12T) · 16GB RAM · 1080p. All performance gates (enemy tick ≤4ms, Animator+skinning ≤3.0ms, ≤1,100-draw early warning) are measured in a player build on this spec — never in the Editor.

## Testing

- **Framework**: Unity Test Framework (NUnit)
- **Minimum Coverage**: Balance formulas, gameplay systems
- **Required Tests**: Combo counter state transitions, parry timing window logic, momentum meter reset conditions

## Forbidden Patterns

<!-- Add patterns that should never appear in this project's codebase -->
Full list with sources: `docs/architecture/control-manifest.md` (Manifest Version 2026-10-10). Headline rules:

- Referencing `Unity.InputSystem` outside the `Dynasty.Input` assembly (ADR-0004)
- Mixing `Time.time`- and `Time.unscaledTime`-derived timestamps — use `IGameClock.RunTime` (ADR-0010)
- `SceneManager.LoadScene`, `Resources.Load`, Addressables in MVP (ADR-0012, ADR-0006)
- `Update()`/`FixedUpdate()` in combat systems; per-enemy `Update()` (ADR-0001/0002)
- `UnityEvent`/`SendMessage` on gameplay paths (ADR-0003)
- Physics colliders on enemies for queries; allocating physics queries; O(n²) range checks (ADR-0007)
- `SetActive(false)`/`Destroy` on pooled objects outside `ObjectPool.ReturnToPool`; static `AttackerId` (ADR-0005)
- Singletons / `FindObjectOfType` wiring — DI at Bootstrap only (ADR-0002/0012)
- Hardcoded gameplay values (ADR-0006)
- `MaterialPropertyBlock`/`.material` on crowd renderers; Humanoid rigs; root motion; gameplay animation events (art bible §8)
- UGUI for new screens; `VisualElement.transform`; URP Compatibility Mode passes (ADR-0008/0009)

## Allowed Libraries / Addons

<!-- Add approved third-party dependencies here. Only add when actively integrating — not speculatively. -->
- Input System package (exact version pinned in `Packages/manifest.json`) — ADR-0004
- UI Toolkit — ADR-0009
- VFX Graph — ADR-0008
- Unity Test Framework + `game-ci/unity-test-runner@v4` — ADR-0010
- No third-party libraries approved yet

## Architecture Decisions Log

<!-- Quick reference linking to full ADRs in docs/architecture/ -->
All Accepted. ADR-0001…0010 amended 2026-10-10 by the independent `/architecture-review`.

- ADR-0001 — Entity architecture: MonoBehaviour + central manager (DOTS deferred, two-step escalation)
- ADR-0002 — Frame execution order + CombatFrame orchestrator (+ Finisher handshake)
- ADR-0003 — Struct-delegate events + ordered DamageTaken dispatch
- ADR-0004 — Single input authority, parry buffer, single action-asset instance
- ADR-0005 — Object pooling + enemy lifecycle
- ADR-0006 — ScriptableObject data configuration (no Addressables in MVP)
- ADR-0007 — Uniform-grid spatial queries + separation steering (no enemy colliders)
- ADR-0008 — Telegraph/combat VFX rendering (VFX Graph single-system instancing)
- ADR-0009 — UI Toolkit runtime UI, event-driven presenters
- ADR-0010 — Deterministic test harness + unified run clock (`IGameClock.RunTime`)
- ADR-0011 — Audio mix architecture — *staged, author before VFX & Audio implementation*
- ADR-0012 — Single-scene boot flow, Bootstrap composition root, RSM-owned run clock + arena bounds

## Engine Specialists

<!-- Written by /setup-engine when engine is configured. -->
<!-- Read by /code-review, /architecture-decision, /architecture-review, and team skills -->
<!-- to know which specialist to spawn for engine-specific validation. -->

- **Primary**: unity-specialist
- **Language/Code Specialist**: unity-specialist (C# review — primary covers it)
- **Shader Specialist**: unity-shader-specialist (Shader Graph, HLSL, URP/HDRP materials)
- **UI Specialist**: unity-ui-specialist (UI Toolkit UXML/USS, UGUI Canvas, runtime UI)
- **Additional Specialists**: unity-dots-specialist (ECS, Jobs system, Burst compiler — invoke if performance optimisation with ECS is needed), unity-addressables-specialist (asset loading, memory management, content catalogs)
- **Routing Notes**: Invoke primary for architecture and general C# code review. Invoke shader specialist for rendering and visual effects. Invoke UI specialist for all interface implementation. Invoke DOTS specialist only if ECS/Jobs/Burst code is introduced. Invoke Addressables specialist for asset management systems.

### File Extension Routing

<!-- Skills use this table to select the right specialist per file type. -->

| File Extension / Type | Specialist to Spawn |
|-----------------------|---------------------|
| Game code (.cs files) | unity-specialist |
| Shader / material files (.shader, .shadergraph, .mat) | unity-shader-specialist |
| UI / screen files (.uxml, .uss, Canvas prefabs) | unity-ui-specialist |
| Scene / prefab / level files (.unity, .prefab) | unity-specialist |
| Native extension / plugin files (.dll, native plugins) | unity-specialist |
| General architecture review | unity-specialist |

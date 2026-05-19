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

## Testing

- **Framework**: Unity Test Framework (NUnit)
- **Minimum Coverage**: Balance formulas, gameplay systems
- **Required Tests**: Combo counter state transitions, parry timing window logic, momentum meter reset conditions

## Forbidden Patterns

<!-- Add patterns that should never appear in this project's codebase -->
- [None configured yet — add as architectural decisions are made]

## Allowed Libraries / Addons

<!-- Add approved third-party dependencies here. Only add when actively integrating — not speculatively. -->
- [None configured yet — add as dependencies are approved]

## Architecture Decisions Log

<!-- Quick reference linking to full ADRs in docs/architecture/ -->
- [No ADRs yet — use /architecture-decision to create one]

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

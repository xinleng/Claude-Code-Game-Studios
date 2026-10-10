# Story 001: Day-0 Unity 6.3 LTS Project Setup & Assembly Layout

> **Epic**: Bootstrap, CombatFrame & Run Clock
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Config/Data
> **Manifest Version**: 2026-10-10

## Context

**GDD**: `design/gdd/input-manager.md` (Core Rule 1 single authority; AC-IM-13), `.claude/docs/technical-preferences.md` (engine, testing, CI)
**Requirement**: `TR-IM-001` (assembly-gate portion only), `TR-TP-001` (deterministic-test / CI infrastructure portion)
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0004: Single Input Authority (assembly-gate + project-settings portion, amended 2026-10-10); ADR-0010: Deterministic Test Harness (structure & CI, rule 6); ADR-0012: Scene Organisation & Boot Flow (single scene)
**ADR Decision Summary**: Only the `Dynasty.Input` asmdef may reference `Unity.InputSystem` (compile-time gate); Active Input Handling, Input System `updateMode` and the exact `com.unity.inputsystem` version are pinned. Tests live in `tests/unit|integration/[system]/` and run EditMode + PlayMode in CI via `game-ci/unity-test-runner@v4` as a blocking gate. Exactly one scene (`MainArena.unity`) is in the build.

**Engine**: Unity 6.3 LTS | **Risk**: HIGH (package pinning — ADR-0004)
**Engine Notes**: Legacy `Input` class is deprecated in Unity 6; the Input System package is the only supported path. "Dynamic Update is the default `updateMode`" is a *package* default, not an engine one — hence the exact-version pin. If Active Input Handling is left at "Both", legacy `Input.*` silently works and bypasses the authority invariant. Unity 6 templates may create and assign a project-wide default actions asset — ADR-0004 §7 requires it removed/unassigned. Verify the exact Input System version that resolves under 6.3 LTS (engine-reference docs cite the 1.11 manual — confirm, do not assume). URP Compatibility Mode is hidden/deprecated in 6.3 — do not enable it.

**Control Manifest Rules (this layer)**:
- Required: Pin `Active Input Handling` = "Input System Package (New)", `updateMode` = Dynamic Update, and an exact `com.unity.inputsystem` version in the manifest (ADR-0004)
- Required: One scene (`MainArena.unity`) for the whole MVP (ADR-0012)
- Forbidden: Never reference `Unity.InputSystem` outside the `Dynasty.Input` assembly (`InputManager.cs` + `UiInputModuleBinder.cs` only) — compile-time asmdef gate (ADR-0004)
- Forbidden: Never use `Resources.Load` or Addressables in MVP (ADR-0006) — do not install Addressables
- Guardrail: Approved packages only — Input System (exact pin), UI Toolkit, VFX Graph, Unity Test Framework; no third-party libraries

---

## Acceptance Criteria

*From ADR-0004 / ADR-0010 / ADR-0012 and the epic Definition of Done, scoped to this story:*

- [ ] A Unity 6.3 LTS project exists in the repository, opens with zero console errors, and `ProjectSettings/ProjectVersion.txt` is discoverable by the CI detect step (`find . -maxdepth 3 … ProjectVersion.txt`)
- [ ] URP is the active render pipeline, with the Universal Renderer's rendering path set to **Forward+** (technical-preferences; control manifest header "URP, Forward+")
- [ ] `Packages/manifest.json` pins `com.unity.inputsystem` to an **exact** version (no range); VFX Graph and Unity Test Framework packages are installed; UI Toolkit is available; Addressables is NOT installed
- [ ] Player Settings → Active Input Handling = **Input System Package (New)** (not "Both")
- [ ] Input System settings `updateMode` = **Dynamic Update** (explicit, not left to the package default)
- [ ] The project-wide default Input Actions asset is removed/unassigned (ADR-0004 §7)
- [ ] `MainArena.unity` exists and is the **only** scene in Build Settings (ADR-0012 §1)
- [ ] Assembly definitions exist: `Dynasty.Input` is the **only** project asmdef listing `Unity.InputSystem` in its references; all other project asmdefs (incl. gameplay/core/bootstrap) do not reference it; no game code compiles into `Assembly-CSharp` (so the gate cannot be bypassed by an un-asmdef'd script)
- [ ] `tests/` EditMode and PlayMode asmdefs are wired so that suites under `tests/unit/` and `tests/integration/` compile into the EditMode test assembly and `tests/PlayMode/` into the PlayMode test assembly; at least one trivial sanity test referencing a `Dynasty.*` runtime assembly passes in both the Test Runner and CLI (`-runTests -testPlatform EditMode`)
- [ ] CI (`.github/workflows/tests.yml`) detects the project and runs the EditMode suite green on push — **requires the `UNITY_LICENSE` repository secret (manual user step, see below)**

---

## Implementation Notes

*Derived from ADR-0004 (amended 2026-10-10), ADR-0010 rule 6, ADR-0012 §1:*

1. **Project location — DECISION REQUIRED AT STORY START (not covered by any ADR; confirm with user / technical-director before creating the project).** Constraint: `tests/unit/[system]/…` and `tests/integration/[system]/…` are the binding test paths in every story file, and Unity only compiles code under `Assets/` or `Packages/`. Options:
   - **(A, recommended)** Unity project at the repo root; `src/` and `tests/` exposed to the compiler as local packages (`"file:../src"`, `"file:../tests"` in `Packages/manifest.json`, each with a `package.json`). `tests/unit/` and `tests/integration/` join the EditMode test assembly via `.asmref` files pointing at `EditModeTests` (an asmdef only covers its own folder subtree). Verify whether a `testables` entry in `manifest.json` is needed for package-hosted tests to appear in the Test Runner.
   - **(B)** Code and tests under `Assets/` — breaks the documented `tests/` paths used by every story; not recommended.
2. **Create the project** with the Unity 6.3 LTS URP (3D) template. Record the exact editor version (`6000.3.x`) in the story completion notes for `docs/engine-reference/unity/VERSION.md` follow-up.
3. **URP**: confirm the URP asset + Universal Renderer data are assigned in Graphics and Quality settings; set Rendering Path = Forward+.
4. **Input System**: pin the exact version in `manifest.json`; set Active Input Handling = Input System Package (New) (editor restart required); set `updateMode` = Dynamic Update in the Input System settings asset; unassign/remove the project-wide actions asset the template creates. The `PlayerControls` actions asset and `InputManager.cs` are **not** part of this story (input-manager epic).
5. **Assemblies (minimal Day-0 set; later epics add their own)** — names other than `Dynasty.Input` are proposals:
   - `Dynasty.Input` — references `Unity.InputSystem`. Holds exactly two files once the input-manager epic lands (`InputManager.cs`, `UiInputModuleBinder.cs`); its public surface must not expose InputSystem types, so `Dynasty.Bootstrap` can construct it without referencing `Unity.InputSystem`.
   - `Dynasty.Core` — `IGameClock`, `CombatFrame`, `ITickable`, `ExecutionOrder` constants, designer-error channel (Stories 002–004). No `Unity.InputSystem` reference.
   - `Dynasty.Bootstrap` — composition root (Story 005); references the runtime assemblies, never `Unity.InputSystem`.
   - `EditModeTests` (Editor platform only; references `UnityEngine.TestRunner`, `UnityEditor.TestRunner`, `nunit.framework.dll` precompiled ref, `Dynasty.*`) and `PlayModeTests` — names per `tests/EditMode/README.md` and `tests/PlayMode/README.md`.
   - Custom asmdefs do not auto-reference package assemblies, so the gate holds as long as no gameplay asmdef lists `Unity.InputSystem` and nothing lives in `Assembly-CSharp`.
6. **Existing scaffold test** `tests/unit/damage-health/damage_health_effective_damage_test.cs` references `PlayerHealthSystem`, `TestConfigs`, `FakeGameClock`, which do not exist yet — it will break compilation of the whole EditMode assembly once `tests/` is wired. **Resolve at story start with user confirmation** (e.g. relocate it out of the compiled test tree as a reference sample until the damage-health epic lands). This is not "skipping a failing test" — it has never compiled. Do not delete it without approval.
7. **Sanity test**: add one trivial EditMode test (e.g. `tests/unit/project-setup/project_setup_assembly_test.cs`, `test_runtime_assembly_reference_compiles`) that references a `Dynasty.Core` type, proving asmdef wiring end-to-end. Story 002's clock types are the first real content.
8. **Version control**: ensure `.gitignore` excludes `Library/`, `Temp/`, `Obj/`, `Logs/`, `UserSettings/`, `Build*/`; commit `.meta` files; `Packages/packages-lock.json` must be committed (CI cache key hashes it).
9. **CI license — MANUAL USER STEP**: the `UNITY_LICENSE` secret must be added to the GitHub repository by the user (game-ci activation docs). Verify against current game-ci v4 docs whether a Personal license additionally needs `UNITY_EMAIL` / `UNITY_PASSWORD` secrets. Claude never handles license files or credentials — hand this step to the user with the exact secret names.

---

## Out of Scope

*Handled by neighbouring stories / epics — do not implement here:*

- Story 002: `IGameClock` + test clock (first real `Dynasty.Core` content)
- Story 003: `CombatFrame` + `ExecutionOrder` anchor constants
- Story 005: `Bootstrap` GameObject in `MainArena.unity` and the boot sequence
- Story 006: automated static checks proving the asmdef gate / forbidden patterns (this story only configures the asmdefs)
- input-manager epic: `PlayerControls` asset, `InputManager.cs`, `UiInputModuleBinder.cs`, the bootstrap-time `updateMode` assertion (must live inside `Dynasty.Input`), the "deliberate InputSystem reference fails to compile" verification
- UI epics: shared `PanelSettings`, UI Toolkit screens
- game-data epic: ScriptableObject configs

---

## QA Test Cases

*Written by qa-lead at story creation (lean mode — QL-STORY-READY skipped). Config/Data story → smoke-check verification steps.*

- **AC-1**: Project opens clean and is CI-discoverable
  - Setup: fresh clone; open the project in Unity 6.3 LTS (exact version recorded in completion notes)
  - Verify: Console after first import/compile; run `find . -maxdepth 3 -type f -name ProjectVersion.txt -path '*/ProjectSettings/*'` from repo root
  - Pass condition: zero errors in Console; the find command returns exactly one path; `ProjectVersion.txt` shows a `6000.3.*` version
- **AC-2**: URP Forward+
  - Setup: Project Settings → Graphics and → Quality
  - Verify: assigned render pipeline asset is URP; its Universal Renderer data shows Rendering Path = Forward+
  - Pass condition: every quality level in use points at a URP asset whose renderer is Forward+
- **AC-3**: Package pins
  - Setup: open `Packages/manifest.json`
  - Verify: `com.unity.inputsystem` value; presence of VFX Graph and Test Framework; absence of Addressables
  - Pass condition: inputsystem version is an exact `x.y.z` string (no `^`, `~`, range, or git ref); VFX Graph + Test Framework present; no `com.unity.addressables`
- **AC-4**: Active Input Handling
  - Setup: Project Settings → Player → Other Settings
  - Verify: Active Input Handling value (and `activeInputHandler` in `ProjectSettings/ProjectSettings.asset`)
  - Pass condition: "Input System Package (New)" — not "Both", not "Input Manager (Old)"
- **AC-5**: Input System update mode
  - Setup: Project Settings → Input System Package → Settings
  - Verify: Update Mode field
  - Pass condition: "Process Events In Dynamic Update", explicitly saved in a settings asset under version control
- **AC-6**: Project-wide actions unassigned
  - Setup: Project Settings → Input System Package
  - Verify: Project-wide Actions field
  - Pass condition: field is empty (None); no template `InputSystem_Actions` asset is referenced by settings
- **AC-7**: Single scene
  - Setup: File → Build Profiles / Build Settings
  - Verify: scene list
  - Pass condition: exactly one enabled scene, `MainArena.unity`
- **AC-8**: Assembly gate configuration
  - Setup: inspect every `*.asmdef` in the project (Inspector or JSON)
  - Verify: `references` arrays; check `Assembly-CSharp` contents in the Project window / `Library/ScriptAssemblies`
  - Pass condition: `Unity.InputSystem` appears only in `Dynasty.Input.asmdef`; `Assembly-CSharp` contains no project scripts
- **AC-9**: Test assemblies wired
  - Setup: Window → General → Test Runner; then CLI `Unity -runTests -batchmode -projectPath <path> -testPlatform EditMode`
  - Verify: the sanity test is listed under the EditMode assembly and passes in both runners; a PlayMode tab exists with its assembly
  - Pass condition: sanity test green in Editor and CLI result XML; no compile errors from any file under `tests/`
- **AC-10**: CI green
  - Setup: user has added the `UNITY_LICENSE` secret (and any additional game-ci-required secrets); push to a PR branch targeting `main`
  - Verify: GitHub Actions "Automated Tests" run — detect job output and "Run Edit Mode Tests" step
  - Pass condition: detect reports `has-project=true`; EditMode step completes green with the sanity test counted as passed

---

## Test Evidence

**Story Type**: Config/Data
**Required evidence**:
- Config/Data: smoke check pass (`production/qa/smoke-[date].md`) recording AC-1…AC-10 with the exact editor and Input System versions; link to the green CI run
- Supporting: `tests/unit/project-setup/project_setup_assembly_test.cs` (sanity test) passing

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: None (first story of the first epic). External: user adds `UNITY_LICENSE` secret before AC-10 can pass.
- Unlocks: Story 002, Story 003, Story 004 (and transitively every story in every epic — nothing else can be compiled or tested until this lands); input-manager, object-pool, game-data, run-session-manager epics

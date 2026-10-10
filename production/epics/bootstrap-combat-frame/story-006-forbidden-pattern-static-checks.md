# Story 006: Forbidden-Pattern Static Checks (Assembly Gate, LoadScene, Self-Updating Systems, Lookups, Raw Time)

> **Epic**: Bootstrap, CombatFrame & Run Clock
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Integration
> **Manifest Version**: 2026-10-10

## Context

**GDD**: `design/gdd/input-manager.md` (Core Rule 1; AC-IM-13 static-scan assertion), `design/gdd/damage-health.md` (Implementation Contract #3 — no hidden ordering), `design/gdd/run-session-manager.md` (no-loading-screen single-scene flow)
**Requirement**: `TR-IM-001` (single input authority — statically checkable), `TR-RS-006` (no `SceneManager.LoadScene`), `TR-DH-004` (ordering only via CombatFrame — no self-updating combat systems), `TR-RS-005` (no raw `Time.*` run-local values outside the clock), `TR-TP-001` (DI over singletons/lookups)
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0012 (Validation Criteria — static LoadScene check); ADR-0004 (amended 2026-10-10 — §6 assembly-scoped gate + widened advisory grep); ADR-0002 (Validation Criteria — no combat system contains `Update()`; registry pattern `self_updating_combat_system`); ADR-0010 (no direct `Time.*` reads in gameplay logic)
**ADR Decision Summary**: The epic's architectural invariants are enforced mechanically, not only by review: zero `SceneManager.LoadScene` in MVP; only `Dynasty.Input` references `Unity.InputSystem` (plus the advisory raw-device grep); no system ticks itself outside the execution-order anchors; wiring never uses `Find*` lookups; only the clock binding reads `UnityEngine.Time`.

**Engine**: Unity 6.3 LTS | **Risk**: LOW
**Engine Notes**: Reflection over loaded assemblies (`AppDomain.CurrentDomain.GetAssemblies()`, `Assembly.GetReferencedAssemblies()`, `Type.GetMethod(..., DeclaredOnly)`, `DefaultExecutionOrder.order`) is stable and needs no file I/O. Unity 6 adds `FindFirstObjectByType` / `FindAnyObjectByType` / `FindObjectsByType` alongside the deprecated `FindObjectOfType` — the lookup check must cover all spellings.

**Control Manifest Rules (this layer)**:
- Forbidden: Never reference `Unity.InputSystem` outside the `Dynasty.Input` assembly — compile-time asmdef gate; advisory grep `\bInput\.(Get|mouse|anyKey|touch)|Pointer\.current|Touchscreen\.current|Joystick\.current|Pen\.current|\bInputSystem\.` (ADR-0004)
- Forbidden: Never call `SceneManager.LoadScene` in MVP (ADR-0012)
- Forbidden: Never implement `Update()`/`FixedUpdate()` in a combat system (ADR-0002)
- Forbidden: `FindObjectOfType` / `FindFirstObjectByType` — forbidden for wiring (DI at Bootstrap) (manifest Forbidden APIs; ADR-0002, ADR-0012)
- Forbidden: Never mix `Time.time`- and `Time.unscaledTime`-derived run-local values; never read `Time.time`/`Time.unscaledTime` inside testable gameplay logic (ADR-0010)
- Cross-cutting: Never disable or skip a failing test to make CI pass (coding standards)

---

## Acceptance Criteria

*From ADR-0012 / ADR-0002 / ADR-0004 Validation Criteria, input-manager AC-IM-13, epic DoD ("Static checks pass: no `SceneManager.LoadScene`; no InputSystem reference outside `Dynasty.Input`"), scoped to this story:*

- [ ] **Assembly gate (reflection)**: among all loaded project assemblies (`Dynasty.*` and `Assembly-CSharp*`), only `Dynasty.Input` lists `Unity.InputSystem` in its referenced assemblies
- [ ] **Self-updating ban (reflection)**: every `MonoBehaviour` type in project runtime assemblies that declares `Update`, `FixedUpdate` or `LateUpdate` carries `[DefaultExecutionOrder]` with one of the named `ExecutionOrder` anchor values; any other declaring type fails the check with its type name in the message
- [ ] **LoadScene ban (CI source scan)**: zero occurrences of `SceneManager.LoadScene` (incl. `LoadSceneAsync`) in project runtime source
- [ ] **Raw device / legacy input (CI source scan)**: the ADR-0004 advisory regex matches only files inside the `Dynasty.Input` assembly folder (AC-IM-13)
- [ ] **Lookup ban (CI source scan)**: zero occurrences of `FindObjectOfType`, `FindObjectsOfType`, `FindFirstObjectByType`, `FindAnyObjectByType`, `FindObjectsByType` in project runtime source
- [ ] **Raw time reads (CI source scan)**: `UnityEngine.Time` time-value reads (`Time.time`, `Time.unscaledTime`, `Time.deltaTime`, `Time.unscaledDeltaTime`, `Time.realtimeSinceStartup*`, `Time.*AsDouble`) appear only in the Unity-backed `IGameClock` implementation; writes to `Time.timeScale` are not flagged
- [ ] Source-scan checks run as a CI job on every push/PR to `main` and fail the workflow on any match (blocking gate, alongside the test jobs)
- [ ] Each check is demonstrated **red** against a deliberately planted violation (local throwaway change, never committed) and **green** on the clean tree; both outcomes recorded in the story completion notes

---

## Implementation Notes

*Derived from ADR-0004 §6 (amended), ADR-0002 Validation Criteria + Risks, ADR-0012 Validation Criteria + Risks, ADR-0010 Decision 1:*

- **Two mechanisms, chosen by what each can see**:
  1. **Reflection checks (EditMode integration test)** for assembly references and declared Unity messages — no file I/O, so they comply with the testing standards' independence rule. Enumerate project assemblies by name prefix (`Dynasty.`) plus `Assembly-CSharp*`; exclude test assemblies and Unity/package assemblies.
  2. **Source-text scan (CI job)** for call-site patterns that reflection cannot see in method bodies (`LoadScene`, `Find*`, `Time.*` reads, the ADR-0004 advisory regex). Implement as a `static-checks` job in `.github/workflows/tests.yml` running `grep -rnE` (or `rg`) over the runtime source root chosen in Story 001, excluding `tests/`. It does not need the Unity license, so it should run even when the Unity jobs are skipped.
- **Allow-lists are explicit and minimal**: input regex → `Dynasty.Input` folder only; time reads → the single clock-binding file; `Update`/`FixedUpdate`/`LateUpdate` → only types whose `DefaultExecutionOrder.order` equals an `ExecutionOrder` constant (Story 003). Any allow-list change requires code review, mirroring ADR-0002's "changing order requires a code review, by design".
- **Compile-time gate stays primary** (ADR-0004 §6): the reflection check catches a mis-edited asmdef; the input-manager epic separately verifies that a deliberate InputSystem reference in a gameplay assembly fails to compile.
- **Not mechanically checkable here** (remain review-checklist items, per ADR-0012 Risks): "no gameplay `Awake/Start` touches another system". Do not attempt heuristics for it.
- **Failure messages** must name the file/type and the violated rule + source ADR so a red CI run is self-explanatory.
- Keep the patterns in one place (e.g. a small script under `tools/ci/`) so the CI job and local runs use identical rules.

---

## Out of Scope

*Handled by neighbouring stories / epics — do not implement here:*

- Story 001: the asmdef configuration this story verifies
- input-manager epic: compile-failure proof for the asmdef gate; `GetActiveBindingDisplay` / binder exemption semantics
- Later candidates (not required by this epic's ADRs/DoD; add via their own stories): `UnityEngine.Random` in testable logic (ADR-0010 RNG), `UnityEvent`/`SendMessage` on combat paths (ADR-0003), `Resources.Load` (ADR-0006), `SetActive(false)` outside the pool (ADR-0005), allocating physics queries (ADR-0007)

---

## QA Test Cases

*Written by qa-lead at story creation (lean mode — QL-STORY-READY skipped).*

- **AC-1**: Only `Dynasty.Input` references `Unity.InputSystem`
  - Given: the EditMode domain with all project assemblies loaded
  - When: the reflection check collects `GetReferencedAssemblies()` for every `Dynasty.*` / `Assembly-CSharp*` assembly
  - Then: the set of assemblies referencing `Unity.InputSystem` is exactly `{ Dynasty.Input }` (or empty if `Dynasty.Input` has no code yet — document which)
  - Edge cases: planted violation — add `Unity.InputSystem` to `Dynasty.Core.asmdef` plus one usage → check fails naming `Dynasty.Core` (local only, reverted)
- **AC-2**: No unanchored self-updating MonoBehaviours
  - Given: all `MonoBehaviour` types in project runtime assemblies
  - When: the check looks for declared (`DeclaredOnly`, any visibility) `Update`/`FixedUpdate`/`LateUpdate`
  - Then: every declaring type has `[DefaultExecutionOrder]` whose `order` is one of the `ExecutionOrder` constants; `CombatFrame` passes; `Bootstrap` declares none
  - Edge cases: planted violation — a test MonoBehaviour in a runtime assembly with a private `Update()` and no attribute → fails naming the type; same type with `[DefaultExecutionOrder(5)]` (non-anchor value) → still fails
- **AC-3**: No `SceneManager.LoadScene`
  - Given: the runtime source tree
  - When: the CI scan runs
  - Then: zero matches for `SceneManager\.LoadScene` (covers `LoadSceneAsync`)
  - Edge cases: planted `SceneManager.LoadScene("X")` in a runtime file → job fails with file:line
- **AC-4**: Advisory raw-device regex confined to `Dynasty.Input`
  - Given: the ADR-0004 regex `\bInput\.(Get|mouse|anyKey|touch)|Pointer\.current|Touchscreen\.current|Joystick\.current|Pen\.current|\bInputSystem\.`
  - When: the CI scan runs over runtime source excluding the `Dynasty.Input` folder
  - Then: zero matches
  - Edge cases: planted unqualified `Input.GetKeyDown(KeyCode.Space)` in a `Dynasty.Core` file → fails (this is the case the old `UnityEngine.Input\b` pattern missed)
- **AC-5**: No `Find*` lookups
  - Given: runtime source
  - When: the CI scan runs for `FindObjectOfType|FindObjectsOfType|FindFirstObjectByType|FindAnyObjectByType|FindObjectsByType`
  - Then: zero matches
  - Edge cases: test code under `tests/` is excluded (PlayMode smoke may count objects); a planted call in Bootstrap → fails
- **AC-6**: Raw time reads only in the clock binding
  - Given: runtime source
  - When: the CI scan runs for `\bTime\.(time|unscaledTime|deltaTime|unscaledDeltaTime|realtimeSinceStartup|timeAsDouble|unscaledTimeAsDouble|realtimeSinceStartupAsDouble)\b`
  - Then: matches occur only in the Unity-backed `IGameClock` implementation file
  - Edge cases: `Time.timeScale = 0f` in any file → not flagged; planted `Time.time` read in `CombatFrame` → fails
- **AC-7**: CI blocking job
  - Given: a PR targeting `main`
  - When: the workflow runs
  - Then: a `static-checks` job runs (even if the Unity project/license step is skipped) and its failure marks the workflow failed
  - Edge cases: none
- **AC-8**: Red/green demonstration
  - Given: the clean tree
  - When: each planted violation from AC-1…AC-6 is applied one at a time locally, then reverted
  - Then: each check goes red with a message naming file/type + rule + ADR; the clean tree is green; outcomes recorded in completion notes
  - Edge cases: none

---

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- Integration: `tests/integration/bootstrap/bootstrap_forbidden_patterns_test.cs` (AC-1, AC-2 — reflection, EditMode) — must exist and pass
- CI: `static-checks` job in `.github/workflows/tests.yml` (AC-3…AC-7) green on the clean tree; red/green log for AC-8 in completion notes

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 003 (`ExecutionOrder` anchors) and Story 005 (Bootstrap + wired tree to scan) must be DONE
- Unlocks: Epic Definition of Done ("Static checks pass"); every later epic inherits these gates on every push

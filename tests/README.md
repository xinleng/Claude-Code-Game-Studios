# Test Infrastructure

**Engine**: Unity 6.3 LTS
**Test Framework**: Unity Test Framework (NUnit)
**CI**: `.github/workflows/tests.yml`
**Setup date**: 2026-07-04
**Governing ADR**: `docs/architecture/adr-0010-test-harness.md` (deterministic harness — injected `IGameClock`, manual `CombatFrame.RunFrame(dt)` pump, seedable `System.Random`)

## Directory Layout

```
tests/
  unit/           # Isolated unit tests (formulas, state machines, logic) — EditMode
  integration/    # Cross-system tests — EditMode via the CombatFrame pump where possible
  EditMode/       # Unity asmdef home for EditMode suites (unit/ + integration/ compile here)
  PlayMode/       # Scene-wiring smoke tests only (per ADR-0010, PlayMode is minimal)
  smoke/          # Critical path test list for /smoke-check gate
  evidence/       # Screenshot logs and manual test sign-off records
  helpers/        # TestConfigs factories, EventRecorder, fakes (generate via /test-helpers)
```

> **Note — Unity project pending**: The Unity project itself has not been created
> yet (pre-implementation). These directories and the CI workflow are scaffolding;
> the `.asmdef` files are added when the Unity project is initialised (first
> implementation story / prototype). CI skips gracefully until a Unity project
> (`ProjectSettings/ProjectVersion.txt`) exists on the branch.

## Running Tests

- Editor: `Window → General → Test Runner` (Unity Test Framework is built in)
- CLI/CI: `game-ci/unity-test-runner@v4` (see workflow) — EditMode then PlayMode
- Local CLI: `Unity -runTests -batchmode -projectPath <path> -testPlatform EditMode`

## Test Naming

- **Files**: `[system]_[feature]_test.cs`
- **Functions**: `test_[scenario]_[expected]`
- **Example**: `parry_window_test.cs` → `test_press_within_buffer_front_extension_consumes()`

## Determinism Rules (ADR-0010 — binding)

- No `WaitForSeconds` / `WaitForFixedUpdate` in logic tests — advance the injected `IGameClock` instead
- Drive frames via `CombatFrame.RunFrame(dt)` / `EnemySystemManager.Tick()` — the production order
- Inject seeded `System.Random`; never `UnityEngine.Random` in testable logic
- Call `ResetInstanceIdCounter()` in every `[SetUp]` that asserts AttackerIds
- Fixtures via `TestConfigs.*` factories mirroring GDD defaults — no inline magic numbers
  (exception: boundary-value tests where the number IS the point)

## Story Type → Test Evidence

| Story Type | Required Evidence | Location | Gate |
|---|---|---|---|
| Logic | Automated unit test — must pass | `tests/unit/[system]/` | BLOCKING |
| Integration | Integration test OR documented playtest | `tests/integration/[system]/` | BLOCKING |
| Visual/Feel | Screenshot + lead sign-off | `production/qa/evidence/` | ADVISORY |
| UI | Manual walkthrough OR interaction test | `production/qa/evidence/` | ADVISORY |
| Config/Data | Smoke check pass | `production/qa/smoke-[date].md` | ADVISORY |

## CI

Tests run automatically on every push to `main` and on every pull request.
A failed test suite blocks merging. Never disable or skip a failing test to
make CI pass — fix the underlying issue.

**Setup required once**: add the `UNITY_LICENSE` secret to the GitHub repository
(see game-ci activation docs) before the first real CI run.

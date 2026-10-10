# Edit Mode Tests

Unit + integration tests that run without entering Play Mode — the primary tier
per ADR-0010. Pure logic: formulas, state machines, buffers, draw logic, data
validation, and cross-system integration driven through `CombatFrame.RunFrame(dt)`.

Assembly definition required once the Unity project exists:
`tests/EditMode/EditModeTests.asmdef` (references the gameplay assemblies + NUnit).

Suites in `tests/unit/` and `tests/integration/` compile under this asmdef.

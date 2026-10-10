# Play Mode Tests

Scene-wiring smoke tests ONLY (per ADR-0010): scene loads, bootstrap injection
completes, one frame runs without exceptions, UIDocuments resolve. All gameplay
logic belongs in EditMode via the deterministic pump — if a test needs
`WaitForSeconds`, it is designed wrong.

Assembly definition required once the Unity project exists:
`tests/PlayMode/PlayModeTests.asmdef`.

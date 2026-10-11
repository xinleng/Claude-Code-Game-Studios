using System.Runtime.CompilerServices;

// Test seams (ADR-0010 rule 5): InputManagerTestSeam reports Input System state as plain strings/ints/bools,
// so the test assemblies never reference Unity.InputSystem (ADR-0004 asmdef gate).
[assembly: InternalsVisibleTo("EditModeTests")]
[assembly: InternalsVisibleTo("PlayModeTests")]

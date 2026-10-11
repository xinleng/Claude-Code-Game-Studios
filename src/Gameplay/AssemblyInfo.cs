using System.Runtime.CompilerServices;

// Test seams (ADR-0010): internal members are reachable from the test assemblies only.
[assembly: InternalsVisibleTo("EditModeTests")]
[assembly: InternalsVisibleTo("PlayModeTests")]
[assembly: InternalsVisibleTo("Dynasty.TestHelpers")]

using System.Runtime.CompilerServices;

// Test seams: internal diagnostics are visible to the test assemblies only.
[assembly: InternalsVisibleTo("EditModeTests")]
[assembly: InternalsVisibleTo("PlayModeTests")]

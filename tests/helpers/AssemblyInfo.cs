using System.Runtime.CompilerServices;

// Fixtures expose internal construction specs (e.g. EnemyDataSpec) to the test assemblies only.
[assembly: InternalsVisibleTo("EditModeTests")]
[assembly: InternalsVisibleTo("PlayModeTests")]

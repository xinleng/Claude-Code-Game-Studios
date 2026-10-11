using System.Runtime.CompilerServices;

// Test-only construction paths (ADR-0006 / ADR-0010 Decision 3): tests build configs in memory and never load assets.
[assembly: InternalsVisibleTo("EditModeTests")]
[assembly: InternalsVisibleTo("PlayModeTests")]

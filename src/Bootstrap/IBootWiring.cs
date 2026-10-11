namespace Dynasty.Bootstrap
{
    /// <summary>
    /// Boot phase (b) participant: constructs systems, injects interfaces, and finishes with the UI
    /// input-module bind (ADR-0012 Decision 2). Implemented by <see cref="CompositionRoot"/>; tests fake it.
    /// </summary>
    /// <example><code>wiring.Wire();   // between validation and pool pre-warm</code></example>
    public interface IBootWiring
    {
        /// <summary>Constructs and wires every system, then binds the UI input module.</summary>
        void Wire();
    }
}

namespace Dynasty.Core
{
    /// <summary>
    /// Boot phase (c): object-pool pre-warm (ADR-0005 §1, ADR-0012 Decision 2). Implemented by the
    /// object pool; the only place the pool instantiates.
    /// </summary>
    /// <example><code>poolPrewarm.Prewarm();   // after wiring, before RSM enters Selection</code></example>
    public interface IPoolPrewarm
    {
        /// <summary>Instantiates every pool to its configured count, inactive.</summary>
        void Prewarm();
    }
}

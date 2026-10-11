namespace Dynasty.Core
{
    /// <summary>
    /// Boot phase (d): RunSessionManager enters the <c>Selection</c> state (ADR-0012 Decision 2).
    /// Implemented by RunSessionManager (run-session-manager epic).
    /// </summary>
    /// <example><code>runSession.EnterSelection();   // final boot phase</code></example>
    public interface IRunSessionEntry
    {
        /// <summary>Enters the general-selection state; the run clock origin is marked later, at run start.</summary>
        void EnterSelection();
    }
}

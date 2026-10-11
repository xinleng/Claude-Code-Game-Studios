namespace Dynasty.Core
{
    /// <summary>
    /// Boot phase (a): load-time GameData validation (ADR-0006 §5, ADR-0012 Decision 2). Implemented by
    /// <c>GameDataValidator</c> (game-data epic); reports through the shared designer-error channel and
    /// clamps-and-continues — boot proceeds either way.
    /// </summary>
    /// <example><code>validation.ValidateAll();   // first boot phase, before any system is constructed</code></example>
    public interface IGameDataValidation
    {
        /// <summary>Applies every GDD clamp rule to the loaded data, reporting violations as designer errors.</summary>
        void ValidateAll();
    }
}

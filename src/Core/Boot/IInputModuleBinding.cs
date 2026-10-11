namespace Dynasty.Core
{
    /// <summary>
    /// Last step of boot phase (b): binds the UI input module to the single PlayerControls instance
    /// (ADR-0004 rule 7, ADR-0009). Implemented by <c>UiInputModuleBinder</c> (input-manager Story 008).
    /// </summary>
    /// <example><code>inputModuleBinding.Bind();   // end of construction + wiring</code></example>
    public interface IInputModuleBinding
    {
        /// <summary>Hands the shared PlayerControls UI action references to the UI input module.</summary>
        void Bind();
    }
}

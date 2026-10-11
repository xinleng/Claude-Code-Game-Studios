namespace Dynasty.Core
{
    /// <summary>
    /// Shared designer-error channel (ADR-0006 Decision 5, TR-TP-002). Validation passes and runtime
    /// guards report out-of-range or invalid designer data here, then clamp and continue; the channel
    /// only reports and never applies policy. Every emitted message begins with
    /// <see cref="DesignerError.Marker"/>. Consumed by constructor injection only (ADR-0002/0010):
    /// Bootstrap builds one Unity-backed instance; tests inject a recording sink.
    /// <para>Rare-path API: do not call it per frame from hot paths in normal operation.</para>
    /// </summary>
    /// <example>
    /// <code>
    /// if (reduction > MaxReduction)
    /// {
    ///     _designerErrors.Report("DamageHealth", $"damage_reduction {reduction} clamped to {MaxReduction}");
    ///     reduction = MaxReduction;   // caller clamps and continues
    /// }
    /// </code>
    /// </example>
    public interface IDesignerErrorLog
    {
        /// <summary>
        /// Reports one designer error as <c>[DESIGNER-ERROR] &lt;system&gt;: &lt;message&gt;</c>.
        /// Never throws and always returns control to the caller; null or empty arguments are
        /// still reported with the marker.
        /// </summary>
        /// <param name="system">Tag of the reporting system (e.g. <c>"DamageHealth"</c>, <c>"Validator"</c>).</param>
        /// <param name="message">Human-readable description of the bad value and the action taken.</param>
        void Report(string system, string message);
    }

    /// <summary>
    /// Marker and message format shared by every <see cref="IDesignerErrorLog"/> implementation, so
    /// the Unity console and the test recording sink produce identical text.
    /// </summary>
    public static class DesignerError
    {
        /// <summary>Exact prefix of every designer-error message (ADR-0006 Decision 5). Ordinal, case-sensitive.</summary>
        public const string Marker = "[DESIGNER-ERROR]";

        /// <summary>
        /// Formats a designer-error message as <c>[DESIGNER-ERROR] &lt;system&gt;: &lt;message&gt;</c>.
        /// Null arguments format as empty strings; never throws.
        /// </summary>
        /// <param name="system">Reporting system tag; null becomes empty.</param>
        /// <param name="message">Message body; null becomes empty.</param>
        /// <returns>The prefixed message.</returns>
        public static string Format(string system, string message)
        {
            return Marker + " " + (system ?? string.Empty) + ": " + (message ?? string.Empty);
        }
    }
}

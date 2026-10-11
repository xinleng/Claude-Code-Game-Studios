namespace Dynasty.Core
{
    /// <summary>
    /// Unity-backed <see cref="IDesignerErrorLog"/>: writes each report to the Unity console via
    /// <c>Debug.LogError</c> (one log type for every reporter, so EditMode tests can match it with
    /// <c>LogAssert.Expect(LogType.Error, new Regex(@"^\[DESIGNER-ERROR\]"))</c>; an unexpected report
    /// fails the test instead of passing silently). Stateless plain C# class; Bootstrap (Story BCF-005)
    /// constructs the single instance and injects it. Thread-safe (stateless; <c>Debug.LogError</c> is
    /// thread-safe).
    /// </summary>
    /// <example>
    /// <code>
    /// IDesignerErrorLog errors = new UnityDesignerErrorLog();   // Bootstrap only
    /// var validator = new GameDataValidator(errors);              // consumers take the interface
    /// </code>
    /// </example>
    public sealed class UnityDesignerErrorLog : IDesignerErrorLog
    {
        /// <inheritdoc />
        public void Report(string system, string message)
        {
            UnityEngine.Debug.LogError(DesignerError.Format(system, message));
        }
    }
}

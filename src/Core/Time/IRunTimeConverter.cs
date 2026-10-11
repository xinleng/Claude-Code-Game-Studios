namespace Dynasty.Core
{
    /// <summary>
    /// Read-only stamp conversion into the <see cref="IGameClock.RunTime"/> domain (ADR-0004 rule 1,
    /// ADR-0010; resolves QA gap G-2). InputManager needs to convert Input System <c>ctx.time</c> press
    /// stamps but must not hold the RSM-only <see cref="IRunClockControl"/> (origin/pause control), so
    /// the conversion is split out as this narrow seam. Implemented by the same clock objects;
    /// Bootstrap hands it to InputManager. Main thread only.
    /// </summary>
    /// <example>
    /// <code>
    /// float pressTime = converter.ToRunTime(ctx.time);   // realtimeSinceStartup basis → RunTime
    /// </code>
    /// </example>
    public interface IRunTimeConverter
    {
        /// <summary>
        /// Converts a <c>realtimeSinceStartup</c>-basis stamp to run-local seconds. Same semantics as
        /// <see cref="IRunClockControl.ToRunTime"/>: before origin → 0; inside a pause → the frozen value;
        /// always finite, within [0, current RunTime], monotonic in the stamp. A stamp later than the
        /// frame's cached sample clamps to the frame's RunTime, so presses in one frame may collapse to
        /// the same value (bounded to a few ms; harmless at 120 ms buffer / 350 ms windows).
        /// </summary>
        /// <param name="realtimeStamp">Seconds on the <c>Time.realtimeSinceStartupAsDouble</c> basis.</param>
        /// <returns>Run-local seconds.</returns>
        float ToRunTime(double realtimeStamp);
    }
}

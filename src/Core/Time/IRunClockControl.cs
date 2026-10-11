namespace Dynasty.Core
{
    /// <summary>
    /// Mutating control surface for the run clock behind <see cref="IGameClock.RunTime"/>
    /// (ADR-0010: "RSM owns the run origin and the paused-duration accumulator"). The
    /// RunSessionManager is the sole intended holder; Bootstrap hands <see cref="IGameClock"/> to
    /// everyone else and this interface to RSM only. Main thread only.
    /// <para>
    /// OPEN ITEM (QA plan gap G-2): InputManager needs <see cref="ToRunTime"/> to convert Input System
    /// <c>ctx.time</c> press stamps, but this conversion lives on the RSM-only control interface per
    /// Story BCF-002. How InputManager obtains it (narrower read-only converter interface, or other)
    /// is unresolved and owned by the input-manager epic; the interface split is deliberately not
    /// redesigned here.
    /// </para>
    /// </summary>
    /// <example>
    /// <code>
    /// control.MarkRunOrigin();           // run start / retry: RunTime = 0, accumulator cleared
    /// control.BeginPause();              // level-up pause: RunTime freezes
    /// control.EndPause();                // RunTime resumes from the frozen value
    /// float t = control.ToRunTime(ctx.time);
    /// </code>
    /// </example>
    public interface IRunClockControl
    {
        /// <summary>
        /// Marks the run origin at the current realtime instant. Resets <see cref="IGameClock.RunTime"/>
        /// to 0, clears the paused-duration accumulator and any in-progress pause (a new run starts
        /// un-paused). Call at run start and on retry (ADR-0012 section 6).
        /// </summary>
        void MarkRunOrigin();

        /// <summary>
        /// Starts compensating for a pause: <see cref="IGameClock.RunTime"/> freezes. Idempotent
        /// (no-op if already paused or if no origin has been marked).
        /// </summary>
        void BeginPause();

        /// <summary>
        /// Ends the current pause and adds its duration to the accumulator. Idempotent (no-op if not
        /// paused).
        /// </summary>
        void EndPause();

        /// <summary>
        /// Converts a <c>realtimeSinceStartup</c>-basis stamp (e.g. Input System <c>ctx.time</c>) to
        /// the <see cref="IGameClock.RunTime"/> domain. A stamp taken at the instant the clock reports
        /// <c>RunTime = R</c> converts to <c>R</c>. Stamp before origin returns 0; a stamp inside a
        /// pause interval returns the frozen value at that pause's start; the result is always finite,
        /// within [0, current RunTime], and monotonic in <paramref name="realtimeStamp"/>. A stamp later
        /// than the frame's cached sample clamps to the frame's RunTime, so presses in one frame may
        /// collapse to the same value (bounded to a few ms; harmless at 120 ms buffer / 350 ms windows).
        /// </summary>
        /// <param name="realtimeStamp">Seconds on the <c>Time.realtimeSinceStartupAsDouble</c> basis.</param>
        /// <returns>Run-local seconds.</returns>
        float ToRunTime(double realtimeStamp);
    }
}

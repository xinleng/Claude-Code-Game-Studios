namespace Dynasty.Core
{
    /// <summary>
    /// Mutating control surface for the run clock behind <see cref="IGameClock.RunTime"/>
    /// (ADR-0010: "RSM owns the run origin and the paused-duration accumulator"). The
    /// RunSessionManager is the sole intended holder; Bootstrap hands <see cref="IGameClock"/> to
    /// everyone else and this interface to RSM only. Main thread only.
    /// <para>
    /// The stamp conversion is also exposed alone as the read-only <see cref="IRunTimeConverter"/>
    /// (QA gap G-2, resolved in input-manager Story 004): InputManager receives that, never this.
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
    public interface IRunClockControl : IRunTimeConverter
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
    }
}

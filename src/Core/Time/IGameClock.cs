namespace Dynasty.Core
{
    /// <summary>
    /// Read-only time source injected into all gameplay systems (ADR-0010 Decision 1, TR-TP-001).
    /// Gameplay logic never reads <c>UnityEngine.Time</c> directly; it reads this interface so that
    /// tests can advance time explicitly (<c>FakeGameClock.Advance</c>). Consumers cannot mutate the
    /// clock: run origin and pause control live on the separate <see cref="IRunClockControl"/>.
    /// <para>
    /// Threading: main thread only (the play-mode implementation reads <c>UnityEngine.Time</c>).
    /// </para>
    /// </summary>
    /// <example>
    /// <code>
    /// float stamp = _clock.RunTime;                    // combat timestamp (e.g. AttackStartTime)
    /// bool windowOpen = _clock.RunTime - stamp &lt; windowSeconds;
    /// </code>
    /// </example>
    public interface IGameClock
    {
        /// <summary>
        /// Scaled game time in seconds (<c>Time.time</c> domain). Stops advancing while
        /// <c>timeScale == 0</c>. Do NOT mix with <see cref="RunTime"/> or <see cref="UnscaledTime"/>
        /// for run-local timestamps (conflict C1). Intended consumers: scaled gameplay timers
        /// (regen, cooldowns that must pause with the game).
        /// </summary>
        float Time { get; }

        /// <summary>
        /// Unscaled time in seconds (<c>Time.unscaledTime</c> domain). Keeps advancing while paused.
        /// Intended consumers: explicitly-unscaled timers only, e.g. i-frame expiry
        /// (damage-health Formula 2 / Contract #2). Not for combat timestamps.
        /// </summary>
        float UnscaledTime { get; }

        /// <summary>
        /// Scaled frame delta in seconds (<c>Time.deltaTime</c> domain); exactly 0 while
        /// <c>timeScale == 0</c>. Intended consumers: <c>CombatFrame.RunFrame(dt)</c>, regen,
        /// movement integration.
        /// </summary>
        float DeltaTime { get; }

        /// <summary>
        /// Unified pause-compensated, run-local clock in seconds (TR-RS-005): elapsed un-paused
        /// seconds since the run origin. 0 before the origin is marked. The single domain for ALL
        /// combat timestamps: enemy <c>AttackStartTime</c>, parry press stamps (converted from Input
        /// System <c>ctx.time</c>, ADR-0004 section 1), <c>steer_time</c>, and parry-window comparisons.
        /// Constant within a frame (all consumers in one frame read the same value).
        /// Realtime-based: it keeps advancing while the player loop is suspended (focus loss with
        /// <c>runInBackground</c> off, debugger breaks) unless RunSessionManager pauses it, and after a
        /// hitch longer than <c>Time.maximumDeltaTime</c> it advances more than <see cref="DeltaTime"/>.
        /// </summary>
        float RunTime { get; }
    }
}

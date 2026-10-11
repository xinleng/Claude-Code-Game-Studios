using System;
using Dynasty.Core;

namespace Dynasty.Tests.Helpers
{
    /// <summary>
    /// Deterministic test double for <see cref="IGameClock"/> and <see cref="IRunClockControl"/>
    /// (ADR-0010 Decision 1). <see cref="Advance"/> is the ONLY way time moves; no real time is read
    /// and there is no static state, so each test constructs its own instance. The run-clock math is
    /// the production <see cref="RunClockState"/> fed with this fake's own <see cref="Realtime"/>.
    /// </summary>
    /// <example>
    /// <code>
    /// var clock = new FakeGameClock();
    /// clock.MarkRunOrigin();
    /// clock.Advance(0.501f);              // AC-DH-07 boundary
    /// float r = clock.RunTime;            // 0.501
    /// </code>
    /// </example>
    public sealed class FakeGameClock : IGameClock, IRunClockControl
    {
        private readonly RunClockState _state = new RunClockState();

        /// <summary>Emulated <c>Time.timeScale</c> (default 1). Set to 0 to emulate an RSM pause.</summary>
        public float TimeScale { get; set; } = 1f;

        /// <summary>Simulated <c>realtimeSinceStartup</c> in seconds; advances by exactly dt per <see cref="Advance"/>.</summary>
        public double Realtime { get; private set; }

        /// <inheritdoc />
        public float Time { get; private set; }

        /// <inheritdoc />
        public float UnscaledTime { get; private set; }

        /// <inheritdoc />
        public float DeltaTime { get; private set; }

        /// <inheritdoc />
        public float RunTime => _state.GetRunTime(Realtime);

        /// <summary>
        /// Advances the clock: UnscaledTime += dt; Time += dt * TimeScale; DeltaTime = dt * TimeScale;
        /// Realtime += dt (RunTime follows from the run-clock state, advancing only while un-paused).
        /// </summary>
        /// <param name="dt">Real seconds elapsed; must be finite and &gt;= 0.</param>
        /// <exception cref="ArgumentOutOfRangeException">dt is negative, NaN or infinite.</exception>
        public void Advance(float dt)
        {
            if (!(dt >= 0f) || float.IsInfinity(dt))
            {
                throw new ArgumentOutOfRangeException(nameof(dt), dt, "FakeGameClock.Advance requires a finite, non-negative dt.");
            }

            float scaled = dt * TimeScale;
            UnscaledTime += dt;
            Time += scaled;
            DeltaTime = scaled;
            Realtime += dt;
        }

        /// <inheritdoc />
        public void MarkRunOrigin()
        {
            _state.MarkOrigin(Realtime);
        }

        /// <inheritdoc />
        public void BeginPause()
        {
            _state.BeginPause(Realtime);
        }

        /// <inheritdoc />
        public void EndPause()
        {
            _state.EndPause(Realtime);
        }

        /// <inheritdoc />
        public float ToRunTime(double realtimeStamp)
        {
            return _state.ToRunTime(realtimeStamp, Realtime);
        }
    }
}

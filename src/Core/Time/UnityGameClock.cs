namespace Dynasty.Core
{
    /// <summary>
    /// Play-mode <see cref="IGameClock"/> + <see cref="IRunClockControl"/> backed by Unity time. This is
    /// the ONLY runtime class permitted to read <c>UnityEngine.Time</c> time values (ADR-0010; static
    /// gate lands in Story BCF-006). Plain C# class (not a MonoBehaviour); Bootstrap (Story BCF-005)
    /// constructs the single instance, hands <see cref="IGameClock"/> to all systems and
    /// <see cref="IRunClockControl"/> to RunSessionManager only. Main thread only.
    /// <para>
    /// Basis: RunTime is derived from <c>Time.realtimeSinceStartupAsDouble</c> (the same basis as Input
    /// System <c>ctx.time</c>, ADR-0004 section 1), never from scaled <c>Time.time</c> (conflict C1).
    /// Frame coherence: realtime is sampled lazily on the first clock access in a frame (keyed on
    /// <c>Time.frameCount</c>) and cached for the rest of that frame, so every consumer (and the
    /// origin/pause calls) in one frame sees the same instant. No <c>[DefaultExecutionOrder]</c> is used.
    /// Math lives in <see cref="RunClockState"/>; see its remarks for stamp-conversion semantics.
    /// </para>
    /// </summary>
    /// <example>
    /// <code>
    /// var clock = new UnityGameClock();
    /// IGameClock read = clock;            // everyone
    /// IRunClockControl control = clock;   // RunSessionManager only
    /// control.MarkRunOrigin();
    /// float t = read.RunTime;
    /// </code>
    /// </example>
    public sealed class UnityGameClock : IGameClock, IRunClockControl
    {
        private readonly RunClockState _state = new RunClockState();
        private int _cachedFrame = -1;
        private double _cachedRealtime;

        /// <inheritdoc />
        public float Time => UnityEngine.Time.time;

        /// <inheritdoc />
        public float UnscaledTime => UnityEngine.Time.unscaledTime;

        /// <inheritdoc />
        public float DeltaTime => UnityEngine.Time.deltaTime;

        /// <inheritdoc />
        public float RunTime => _state.GetRunTime(FrameRealtime());

        /// <inheritdoc />
        /// <remarks>
        /// Re-samples realtime instead of using the frame cache: <c>Time.frameCount</c> does not
        /// advance during synchronous work (scene load, pool pre-warm), so a cached sample taken earlier
        /// in a hitched frame would stamp the origin early and the first real frame would read the
        /// hitch length instead of ~0. Pause calls keep the cached sample (same-frame coherence).
        /// </remarks>
        public void MarkRunOrigin()
        {
            _cachedFrame = UnityEngine.Time.frameCount;
            _cachedRealtime = UnityEngine.Time.realtimeSinceStartupAsDouble;
            _state.MarkOrigin(_cachedRealtime);
        }

        /// <inheritdoc />
        public void BeginPause()
        {
            _state.BeginPause(FrameRealtime());
        }

        /// <inheritdoc />
        public void EndPause()
        {
            _state.EndPause(FrameRealtime());
        }

        /// <inheritdoc />
        public float ToRunTime(double realtimeStamp)
        {
            return _state.ToRunTime(realtimeStamp, FrameRealtime());
        }

        private double FrameRealtime()
        {
            int frame = UnityEngine.Time.frameCount;
            if (frame != _cachedFrame)
            {
                _cachedFrame = frame;
                _cachedRealtime = UnityEngine.Time.realtimeSinceStartupAsDouble;
            }

            return _cachedRealtime;
        }
    }
}

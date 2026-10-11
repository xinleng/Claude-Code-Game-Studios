using System;

namespace Dynasty.Core
{
    /// <summary>
    /// Pure run-clock math shared by <see cref="UnityGameClock"/> and the test fake. Holds no engine
    /// references: every method takes the current realtime (seconds, <c>realtimeSinceStartup</c>
    /// basis) as a parameter, which makes the production formula directly unit-testable.
    /// <para>
    /// Formula: <c>RunTime = (now - origin) - pausedTotal</c>, where pausedTotal includes the
    /// in-progress pause (<c>now - pauseStart</c>) while paused. All state is held as double and
    /// exposed as float. Allocation-free after construction. Not thread-safe (main thread only).
    /// </para>
    /// <para>
    /// Stamp conversion semantics (<see cref="ToRunTime"/>): exact, using a fixed record of completed
    /// pause intervals (capacity <see cref="MaxRecordedPauses"/>); result =
    /// clamp((stamp - origin) - pausedDurationBefore(stamp), 0, RunTime(now)). If more than
    /// <see cref="MaxRecordedPauses"/> pauses complete in one run, the oldest are folded into a single
    /// archived total: stamps older than the oldest archived pause lose exactness (they over-subtract
    /// and clamp to &gt;= 0) but remain finite, non-negative and monotonic. Such stamps are far outside
    /// any parry window. A stamp later than <c>now</c> clamps to the current RunTime.
    /// </para>
    /// </summary>
    /// <example>
    /// <code>
    /// var s = new RunClockState();
    /// s.MarkOrigin(100.0);
    /// s.BeginPause(110.0); s.EndPause(115.0);
    /// float r = s.GetRunTime(120.0);          // 15
    /// float c = s.ToRunTime(112.0, 120.0);    // 10 (stamp inside pause -> frozen value)
    /// </code>
    /// </example>
    public sealed class RunClockState
    {
        /// <summary>Capacity of the completed-pause record before the oldest is archived.</summary>
        public const int MaxRecordedPauses = 64;

        private readonly double[] _pauseStarts = new double[MaxRecordedPauses];
        private readonly double[] _pauseEnds = new double[MaxRecordedPauses];
        private int _pauseCount;
        private double _archivedPaused;
        private double _origin;
        private double _pauseStart;
        private double _pausedTotal;
        private bool _hasOrigin;
        private bool _isPaused;

        /// <summary>True once <see cref="MarkOrigin"/> has been called.</summary>
        public bool HasOrigin => _hasOrigin;

        /// <summary>True while a pause is in progress.</summary>
        public bool IsPaused => _isPaused;

        /// <summary>Total completed paused seconds this run (excludes any in-progress pause).</summary>
        public double CompletedPausedTotal => _pausedTotal;

        /// <summary>Marks a new run origin at <paramref name="now"/>; clears accumulator and paused state.</summary>
        /// <param name="now">Realtime seconds.</param>
        public void MarkOrigin(double now)
        {
            _origin = now;
            _hasOrigin = true;
            _isPaused = false;
            _pauseStart = 0.0;
            _pausedTotal = 0.0;
            _pauseCount = 0;
            _archivedPaused = 0.0;
        }

        /// <summary>Begins a pause at <paramref name="now"/>. No-op if already paused or no origin.</summary>
        /// <param name="now">Realtime seconds.</param>
        public void BeginPause(double now)
        {
            if (!_hasOrigin || _isPaused)
            {
                return;
            }

            _isPaused = true;
            _pauseStart = now;
        }

        /// <summary>Ends the pause at <paramref name="now"/>. No-op if not paused.</summary>
        /// <param name="now">Realtime seconds.</param>
        public void EndPause(double now)
        {
            if (!_isPaused)
            {
                return;
            }

            _isPaused = false;
            double duration = now - _pauseStart;
            if (duration <= 0.0)
            {
                return; // zero-length (or clock went backwards): never accumulate negative time
            }

            _pausedTotal += duration;
            Record(_pauseStart, now);
        }

        /// <summary>Elapsed un-paused seconds since the origin at <paramref name="now"/>; 0 before the origin.</summary>
        /// <param name="now">Realtime seconds.</param>
        /// <returns>Run-local seconds, never negative.</returns>
        public float GetRunTime(double now)
        {
            return (float)RunTimeDouble(now);
        }

        /// <summary>Converts a realtime-basis stamp to run-local seconds (see class remarks for semantics).</summary>
        /// <param name="stamp">Realtime seconds (e.g. Input System <c>ctx.time</c>).</param>
        /// <param name="now">Current realtime seconds.</param>
        /// <returns>Value in [0, GetRunTime(now)]; never NaN.</returns>
        public float ToRunTime(double stamp, double now)
        {
            double current = RunTimeDouble(now);
            if (!_hasOrigin || double.IsNaN(stamp) || stamp <= _origin)
            {
                return 0f;
            }

            if (stamp >= now)
            {
                return (float)current;
            }

            double paused = _archivedPaused;
            for (int i = 0; i < _pauseCount; i++)
            {
                paused += Overlap(_pauseStarts[i], _pauseEnds[i], stamp);
            }

            if (_isPaused)
            {
                paused += Overlap(_pauseStart, now, stamp);
            }

            double result = (stamp - _origin) - paused;
            if (result < 0.0)
            {
                result = 0.0;
            }
            else if (result > current)
            {
                result = current;
            }

            return (float)result;
        }

        private double RunTimeDouble(double now)
        {
            if (!_hasOrigin)
            {
                return 0.0;
            }

            double paused = _pausedTotal;
            if (_isPaused)
            {
                paused += Math.Max(0.0, now - _pauseStart);
            }

            double run = (now - _origin) - paused;
            return run > 0.0 ? run : 0.0;
        }

        private static double Overlap(double start, double end, double stamp)
        {
            double upper = end < stamp ? end : stamp;
            double d = upper - start;
            return d > 0.0 ? d : 0.0;
        }

        private void Record(double start, double end)
        {
            if (_pauseCount == MaxRecordedPauses)
            {
                _archivedPaused += _pauseEnds[0] - _pauseStarts[0];
                Array.Copy(_pauseStarts, 1, _pauseStarts, 0, MaxRecordedPauses - 1);
                Array.Copy(_pauseEnds, 1, _pauseEnds, 0, MaxRecordedPauses - 1);
                _pauseCount--;
            }

            _pauseStarts[_pauseCount] = start;
            _pauseEnds[_pauseCount] = end;
            _pauseCount++;
        }
    }
}

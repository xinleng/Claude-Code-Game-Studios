using System;
using Dynasty.GameData;

namespace Dynasty.Input
{
    /// <summary>
    /// Engine-free parry press ring buffer (ADR-0004 rules 1–3; input-manager Core Rules 5–7, Formulas
    /// 1–2; TR-IM-003). Pre-allocated, fixed capacity, zero allocation. Every time is a
    /// <c>IGameClock.RunTime</c> value passed in by InputManager — the buffer never reads <c>Time.*</c>
    /// and references no Input System type (ADR-0004 rule 6), so it runs in EditMode with injected times.
    /// <para>
    /// Liveness (Formula 1): <c>press_time + window ≥ current_time</c> and not consumed — the inclusive
    /// boundary is evaluated in this sum form on purpose: in single precision <c>1.12f − 1.0f</c> exceeds
    /// <c>0.12f</c>, so the naive <c>now − press ≤ window</c> wrongly kills the exact-boundary press, while
    /// <c>1.0f + 0.12f</c> rounded to single precision is <c>1.12f</c> (the sum is explicitly cast to
    /// <c>float</c> so no extended-precision intermediate survives). Entries are kept oldest → newest.
    /// </para>
    /// <para>
    /// No combat semantics (GDD Core Rule 10): the Parry System owns windows and composes
    /// <c>notBefore</c> (Formula 2); this buffer only answers "is a press ≥ notBefore still live?".
    /// Pruning is explicit (<see cref="Prune"/>, called at the start of <c>InputManager.Tick</c>) and never a
    /// side effect of a query. Main thread only.
    /// </para>
    /// </summary>
    /// <example>
    /// <code>
    /// var buffer = new ParryPressBuffer(tuning);
    /// buffer.Record(clock.RunTime);                       // Parry.performed (Story 004)
    /// buffer.Prune(clock.RunTime);                        // start of InputManager.Tick
    /// if (buffer.TryConsume(clock.RunTime, notBefore, out float pressTime)) { /* parry */ }
    /// </code>
    /// </example>
    public sealed class ParryPressBuffer
    {
        private readonly float[] _pressTime;
        private readonly bool[] _consumed;
        private readonly float _windowSeconds;
        private int _head;
        private int _count;

        /// <summary>Creates an empty buffer sized and timed from the input tuning view.</summary>
        /// <param name="tuning">Read-only input section of TuningConfig.</param>
        /// <exception cref="ArgumentNullException"><paramref name="tuning"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Capacity below 1 (the validator clamps real configs to 2–8).</exception>
        public ParryPressBuffer(IInputTuning tuning)
        {
            if (tuning == null)
            {
                throw new ArgumentNullException(nameof(tuning));
            }

            if (tuning.ParryBufferCapacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(tuning), tuning.ParryBufferCapacity, "parry_buffer_capacity must be >= 1.");
            }

            _pressTime = new float[tuning.ParryBufferCapacity];
            _consumed = new bool[tuning.ParryBufferCapacity];
            _windowSeconds = tuning.ParryInputBufferMs / 1000f;
        }

        /// <summary>Fixed capacity (<c>parry_buffer_capacity</c>).</summary>
        public int Capacity => _pressTime.Length;

        /// <summary>Entries currently held (live or consumed, not yet pruned).</summary>
        public int Count => _count;

        /// <summary>Records a press. When full, the oldest entry is overwritten (mashing cannot bank presses).</summary>
        /// <param name="pressTime">Press stamp (RunTime domain).</param>
        public void Record(float pressTime)
        {
            int slot;
            if (_count == _pressTime.Length)
            {
                slot = _head;
                _head = Next(_head);
            }
            else
            {
                slot = Index(_count);
                _count++;
            }

            _pressTime[slot] = pressTime;
            _consumed[slot] = false;
        }

        /// <summary>
        /// Removes entries whose age exceeds the window at <paramref name="currentTime"/>. Kept entries
        /// retain their stamp, consumed state and order.
        /// </summary>
        /// <param name="currentTime">Current RunTime.</param>
        public void Prune(float currentTime)
        {
            int kept = 0;
            for (int i = 0; i < _count; i++)
            {
                int src = Index(i);
                if (ExpiresAt(_pressTime[src]) < currentTime)
                {
                    continue;
                }

                int dst = Index(kept);
                _pressTime[dst] = _pressTime[src];
                _consumed[dst] = _consumed[src];
                kept++;
            }

            _count = kept;
        }

        /// <summary>Non-consuming peek: true if any unconsumed press is live at <paramref name="currentTime"/>.</summary>
        /// <param name="currentTime">Current RunTime.</param>
        public bool HasLive(float currentTime)
        {
            for (int i = 0; i < _count; i++)
            {
                if (IsLive(Index(i), currentTime))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Consumes the most recent live press with <c>press_time ≥ notBefore</c> (most-recent-wins; a
        /// newer press filtered out by <paramref name="notBefore"/> never falls through to an older one
        /// because older presses are filtered too). A consumed press is never returned again.
        /// </summary>
        /// <param name="currentTime">Current RunTime (liveness).</param>
        /// <param name="notBefore">Earliest acceptable stamp, composed by the Parry System.</param>
        /// <param name="pressTime">Consumed stamp, or 0 when none qualifies.</param>
        /// <returns>True if a press was consumed.</returns>
        public bool TryConsume(float currentTime, float notBefore, out float pressTime)
        {
            int best = -1;
            for (int i = 0; i < _count; i++)
            {
                int slot = Index(i);
                if (IsLive(slot, currentTime) && _pressTime[slot] >= notBefore && (best < 0 || _pressTime[slot] > _pressTime[best]))
                {
                    best = slot;
                }
            }

            if (best < 0)
            {
                pressTime = 0f;
                return false;
            }

            _consumed[best] = true;
            pressTime = _pressTime[best];
            return true;
        }

        /// <summary>Reads entry <paramref name="index"/> (0 = oldest) for tests and diagnostics.</summary>
        /// <param name="index">0-based position, oldest first.</param>
        /// <param name="pressTime">Entry stamp.</param>
        /// <param name="consumed">Entry consumed flag.</param>
        /// <returns>False if <paramref name="index"/> is outside [0, Count).</returns>
        public bool TryGetEntry(int index, out float pressTime, out bool consumed)
        {
            if (index < 0 || index >= _count)
            {
                pressTime = 0f;
                consumed = false;
                return false;
            }

            int slot = Index(index);
            pressTime = _pressTime[slot];
            consumed = _consumed[slot];
            return true;
        }

        /// <summary>Empties the buffer (run reset).</summary>
        public void Clear()
        {
            _head = 0;
            _count = 0;
        }

        private bool IsLive(int slot, float currentTime)
        {
            return !_consumed[slot] && ExpiresAt(_pressTime[slot]) >= currentTime;
        }

        // The explicit (float) cast forces rounding to single precision: the runtime may otherwise keep
        // the sum at higher precision, where 1.0 + 0.12f (= 1.1199999973) is below 1.12f and the
        // inclusive boundary press would read as expired.
        private float ExpiresAt(float pressTime)
        {
            return (float)(pressTime + _windowSeconds);
        }

        private int Index(int offset)
        {
            return (_head + offset) % _pressTime.Length;
        }

        private int Next(int slot)
        {
            return (slot + 1) % _pressTime.Length;
        }
    }
}

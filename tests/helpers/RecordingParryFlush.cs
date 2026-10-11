using System;
using System.Collections.Generic;
using Dynasty.Core;

namespace Dynasty.Tests.Helpers
{
    /// <summary>
    /// Spy for <see cref="IParryEventFlush"/> (stands in for ParrySystem until the Parry epic). Records the
    /// flushed ids, optionally logs "FlushEventsFor" into a shared <see cref="EventRecorder"/>, and can run
    /// <see cref="OnFlush"/> at the moment of the call (e.g. to sample the enemy's active state).
    /// </summary>
    /// <example>
    /// <code>
    /// var flush = new RecordingParryFlush(recorder) { OnFlush = id => activeAtFlush = enemy.gameObject.activeSelf };
    /// </code>
    /// </example>
    public sealed class RecordingParryFlush : IParryEventFlush
    {
        private readonly EventRecorder _recorder;

        /// <summary>Creates the spy; <paramref name="recorder"/> (optional) receives "FlushEventsFor".</summary>
        public RecordingParryFlush(EventRecorder recorder = null)
        {
            _recorder = recorder;
        }

        /// <summary>Every flushed AttackerId, in call order.</summary>
        public List<int> Flushed { get; } = new List<int>();

        /// <summary>Optional hook invoked inside the flush call.</summary>
        public Action<int> OnFlush { get; set; }

        /// <inheritdoc />
        public void FlushEventsFor(int attackerId)
        {
            Flushed.Add(attackerId);
            _recorder?.Record("FlushEventsFor");
            OnFlush?.Invoke(attackerId);
        }
    }
}

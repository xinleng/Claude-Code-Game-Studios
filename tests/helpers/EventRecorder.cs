using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Dynasty.Tests.Helpers
{
    /// <summary>
    /// Ordered event/call log for tests (ADR-0010 Decision 5, AC-DH-09 pattern). Subscribers and fake
    /// frame steps call <see cref="Record"/> in callback order; tests assert on <see cref="Log"/>
    /// positions and <see cref="Count"/>. No static state; each test constructs its own instance.
    /// </summary>
    /// <example>
    /// <code>
    /// var recorder = new EventRecorder();
    /// health.DamageTaken += _ => recorder.Record("DamageTaken");
    /// health.PlayerDead += () => recorder.Record("PlayerDead");
    /// // ... pump frames ...
    /// Assert.That(recorder.Log[0], Is.EqualTo("DamageTaken"));
    /// Assert.That(recorder.Count("PlayerDead"), Is.EqualTo(1));
    /// </code>
    /// </example>
    public sealed class EventRecorder
    {
        private readonly List<string> _log = new List<string>();
        private readonly ReadOnlyCollection<string> _readOnlyLog;

        /// <summary>Creates an empty recorder.</summary>
        public EventRecorder()
        {
            _readOnlyLog = _log.AsReadOnly();
        }

        /// <summary>Entries in emission order; a live read-only view (reflects later records and <see cref="Reset"/>).</summary>
        public IReadOnlyList<string> Log => _readOnlyLog;

        /// <summary>Appends <paramref name="name"/> to the log.</summary>
        /// <param name="name">Event or call name, e.g. <c>"DamageTaken"</c> or <c>"Parry.Evaluate"</c>.</param>
        public void Record(string name)
        {
            _log.Add(name);
        }

        /// <summary>Number of entries equal to <paramref name="name"/> (ordinal); 0 if never recorded.</summary>
        /// <param name="name">Entry to count.</param>
        public int Count(string name)
        {
            int count = 0;
            foreach (string entry in _log)
            {
                if (string.Equals(entry, name, StringComparison.Ordinal))
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>Clears the log.</summary>
        public void Reset()
        {
            _log.Clear();
        }
    }
}

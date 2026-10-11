using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Dynasty.Core;

namespace Dynasty.Tests.Helpers
{
    /// <summary>
    /// Test double for <see cref="IDesignerErrorLog"/> (TR-TP-002): records formatted messages in
    /// order, with no Unity dependency, so unit tests assert designer errors without touching the
    /// console. No static state; each test constructs its own instance. Not thread-safe.
    /// </summary>
    /// <example>
    /// <code>
    /// var errors = new RecordingDesignerErrorLog();
    /// var system = new DamageHealth(..., errors);
    /// system.ApplyDamage(-5f);
    /// Assert.That(errors.Count, Is.EqualTo(1));
    /// Assert.That(errors.ContainsFromSystem("DamageHealth"), Is.True);
    /// </code>
    /// </example>
    public sealed class RecordingDesignerErrorLog : IDesignerErrorLog
    {
        private readonly List<string> _messages = new List<string>();
        private readonly ReadOnlyCollection<string> _readOnlyMessages;

        /// <summary>Creates an empty recording sink.</summary>
        public RecordingDesignerErrorLog()
        {
            _readOnlyMessages = _messages.AsReadOnly();
        }

        /// <summary>Number of reports since construction or the last <see cref="Reset"/>.</summary>
        public int Count => _messages.Count;

        /// <summary>
        /// Recorded messages in report order, each formatted by <see cref="DesignerError.Format"/>. A live
        /// read-only view: it reflects later reports and <see cref="Reset"/>, and cannot be mutated.
        /// </summary>
        public IReadOnlyList<string> Messages => _readOnlyMessages;

        /// <inheritdoc />
        public void Report(string system, string message)
        {
            _messages.Add(DesignerError.Format(system, message));
        }

        /// <summary>Clears all recorded messages.</summary>
        public void Reset()
        {
            _messages.Clear();
        }

        /// <summary>
        /// True if any recorded message contains <paramref name="fragment"/> anywhere (ordinal substring,
        /// tag or body). Null or empty fragments return false, so a negative assertion never passes
        /// vacuously on an empty search term. Use <see cref="ContainsFromSystem"/> to match the tag only.
        /// </summary>
        /// <param name="fragment">Text to look for, e.g. <see cref="DesignerError.Marker"/> or a value.</param>
        public bool Contains(string fragment)
        {
            if (string.IsNullOrEmpty(fragment))
            {
                return false;
            }

            foreach (string m in _messages)
            {
                if (m.IndexOf(fragment, StringComparison.Ordinal) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// True if any recorded message was reported with exactly this system tag, i.e. starts with
        /// <c>[DESIGNER-ERROR] &lt;system&gt;: </c>. Unlike <see cref="Contains"/>, a message whose body merely
        /// mentions <paramref name="system"/> does not match. Null or empty returns false.
        /// </summary>
        /// <param name="system">The reporting system tag, e.g. <c>"DamageHealth"</c>.</param>
        public bool ContainsFromSystem(string system)
        {
            if (string.IsNullOrEmpty(system))
            {
                return false;
            }

            string prefix = DesignerError.Format(system, string.Empty);
            foreach (string m in _messages)
            {
                if (m.StartsWith(prefix, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}

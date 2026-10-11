using System;
using Dynasty.Tests.Helpers;
using NUnit.Framework;

namespace Dynasty.Tests.CombatFrames
{
    /// <summary>Story BCF-003 AC-8: EventRecorder preserves emission order, counts per name, resets.</summary>
    public class CombatFrameEventRecorderTest
    {
        private const string DamageTaken = "DamageTaken";
        private const string PlayerDead = "PlayerDead";

        [Test]
        public void test_event_recorder_two_publishers_log_in_emission_order_then_reset()
        {
            // Arrange
            var recorder = new EventRecorder();
            Action damagePublisher = () => recorder.Record(DamageTaken);
            Action deathPublisher = () => recorder.Record(PlayerDead);

            // Act
            damagePublisher();
            deathPublisher();
            string first = recorder.Log[0];
            string second = recorder.Log[1];
            int deadBeforeReset = recorder.Count(PlayerDead);
            recorder.Reset();
            damagePublisher();

            // Assert
            Assert.That(first, Is.EqualTo(DamageTaken));
            Assert.That(second, Is.EqualTo(PlayerDead));
            Assert.That(deadBeforeReset, Is.EqualTo(1));
            Assert.That(recorder.Log.Count, Is.EqualTo(1));
            Assert.That(recorder.Count(PlayerDead), Is.EqualTo(0));
            Assert.That(recorder.Count(DamageTaken), Is.EqualTo(1));
        }

        [Test]
        public void test_event_recorder_new_recorder_is_empty_and_unknown_name_counts_zero()
        {
            // Arrange
            var recorder = new EventRecorder();

            // Act
            int unknown = recorder.Count("NeverRecorded");

            // Assert
            Assert.That(recorder.Log, Is.Empty);
            Assert.That(unknown, Is.EqualTo(0));
        }

        [Test]
        public void test_event_recorder_counts_repeated_entries_ordinally()
        {
            // Arrange
            var recorder = new EventRecorder();

            // Act
            recorder.Record(DamageTaken);
            recorder.Record(DamageTaken);
            recorder.Record("damagetaken");

            // Assert
            Assert.That(recorder.Count(DamageTaken), Is.EqualTo(2));
            Assert.That(recorder.Log.Count, Is.EqualTo(3));
        }
    }
}

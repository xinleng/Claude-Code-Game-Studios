using Dynasty.Input;
using Dynasty.Tests.Helpers;
using NUnit.Framework;
using UnityEngine.TestTools.Constraints;
using Is = NUnit.Framework.Is;

namespace Dynasty.Tests.InputManagers
{
    /// <summary>
    /// Story IM-002 tests: ParryPressBuffer liveness + pruning (AC-IM-01), inclusive boundary under float
    /// precision, capacity overwrite, prune keeps live state, non-consuming peek, zero allocation.
    /// Times are injected run-clock values (boundary-value literals are the point here).
    /// </summary>
    public class InputManagerParryBufferTest
    {
        private static ParryPressBuffer NewBuffer()
        {
            return new ParryPressBuffer(TestInputTuning.Default());
        }

        private static float[] Stamps(ParryPressBuffer buffer)
        {
            var stamps = new float[buffer.Count];
            for (int i = 0; i < buffer.Count; i++)
            {
                buffer.TryGetEntry(i, out stamps[i], out _);
            }

            return stamps;
        }

        // ---------------------------------------------------------------- AC-1 (AC-IM-01)

        [Test]
        public void test_parry_buffer_press_live_at_100ms_then_pruned_at_121ms()
        {
            // Arrange
            ParryPressBuffer buffer = NewBuffer();
            buffer.Record(1.000f);

            // Act
            buffer.Prune(1.100f);
            bool liveAt100 = buffer.HasLive(1.100f);
            buffer.Prune(1.121f);
            bool liveAt121 = buffer.HasLive(1.121f);

            // Assert
            Assert.That(liveAt100, Is.True);
            Assert.That(liveAt121, Is.False);
            Assert.That(buffer.Count, Is.EqualTo(0));
        }

        [Test]
        public void test_parry_buffer_liveness_does_not_depend_on_pruning()
        {
            // Arrange
            ParryPressBuffer buffer = NewBuffer();
            buffer.Record(1.000f);

            // Act (no Prune)
            bool live = buffer.HasLive(1.121f);

            // Assert
            Assert.That(live, Is.False);
            Assert.That(buffer.Count, Is.EqualTo(1));
        }

        // ---------------------------------------------------------------- AC-2 inclusive boundary

        [TestCase(1.000f, 1.120f)]
        [TestCase(0.000f, 0.120f)]
        [TestCase(1199.000f, 1199.120f)]
        public void test_parry_buffer_exact_window_boundary_is_live(float press, float now)
        {
            // Arrange
            ParryPressBuffer buffer = NewBuffer();
            buffer.Record(press);

            // Act
            buffer.Prune(now);
            bool live = buffer.HasLive(now);

            // Assert
            Assert.That(live, Is.True);
            Assert.That(buffer.Count, Is.EqualTo(1), "boundary press survives the prune on that frame");
        }

        // ---------------------------------------------------------------- AC-3 capacity overwrite

        [Test]
        public void test_parry_buffer_fifth_press_overwrites_oldest()
        {
            // Arrange
            ParryPressBuffer buffer = NewBuffer();
            buffer.Record(1.00f);
            buffer.Record(1.01f);
            buffer.Record(1.02f);
            buffer.Record(1.03f);

            // Act
            buffer.Record(1.04f);

            // Assert
            Assert.That(buffer.Count, Is.EqualTo(4));
            Assert.That(Stamps(buffer), Is.EqualTo(new[] { 1.01f, 1.02f, 1.03f, 1.04f }));
        }

        [Test]
        public void test_parry_buffer_capacity_two_overwrites_consumed_oldest()
        {
            // Arrange
            var buffer = new ParryPressBuffer(TestInputTuning.WithCapacity(2));
            buffer.Record(1.00f);
            buffer.TryConsume(1.00f, 0f, out _);
            buffer.Record(1.01f);

            // Act
            buffer.Record(1.02f);

            // Assert
            Assert.That(buffer.Capacity, Is.EqualTo(2));
            Assert.That(Stamps(buffer), Is.EqualTo(new[] { 1.01f, 1.02f }));
        }

        // ---------------------------------------------------------------- AC-4 prune keeps live state

        [Test]
        public void test_parry_buffer_prune_removes_only_expired_and_keeps_state()
        {
            // Arrange
            ParryPressBuffer buffer = NewBuffer();
            buffer.Record(1.000f);
            buffer.TryConsume(1.000f, 0f, out _);
            buffer.Record(1.100f);

            // Act
            buffer.Prune(1.150f);

            // Assert
            Assert.That(buffer.Count, Is.EqualTo(1));
            Assert.That(buffer.TryGetEntry(0, out float stamp, out bool consumed), Is.True);
            Assert.That(stamp, Is.EqualTo(1.100f));
            Assert.That(consumed, Is.False);
        }

        [Test]
        public void test_parry_buffer_empty_prune_is_safe()
        {
            // Arrange
            ParryPressBuffer buffer = NewBuffer();

            // Act / Assert
            Assert.DoesNotThrow(() => buffer.Prune(5f));
            Assert.That(buffer.Count, Is.EqualTo(0));
        }

        // ---------------------------------------------------------------- AC-5 non-consuming peek

        [Test]
        public void test_parry_buffer_peek_ten_times_does_not_consume()
        {
            // Arrange
            ParryPressBuffer buffer = NewBuffer();
            buffer.Record(1.000f);

            // Act
            bool all = true;
            for (int i = 0; i < 10; i++)
            {
                all &= buffer.HasLive(1.050f);
            }

            // Assert
            Assert.That(all, Is.True);
            buffer.TryGetEntry(0, out _, out bool consumed);
            Assert.That(consumed, Is.False);
        }

        [Test]
        public void test_parry_buffer_only_consumed_presses_peek_false()
        {
            // Arrange
            ParryPressBuffer buffer = NewBuffer();
            buffer.Record(1.000f);
            buffer.TryConsume(1.010f, 0f, out _);

            // Act / Assert
            Assert.That(buffer.HasLive(1.020f), Is.False);
        }

        // ---------------------------------------------------------------- AC-6 zero allocation

        [Test]
        public void test_parry_buffer_record_prune_peek_consume_allocate_nothing()
        {
            // Arrange
            ParryPressBuffer buffer = NewBuffer();

            // Act / Assert
            Assert.That(() =>
            {
                for (int i = 0; i < 8; i++)
                {
                    buffer.Record(1f + (i * 0.01f));
                }

                buffer.Prune(1.1f);
                buffer.HasLive(1.1f);
                buffer.TryConsume(1.1f, 0f, out _);
            }, UnityEngine.TestTools.Constraints.Is.Not.AllocatingGCMemory());
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Dynasty.Core;
using Dynasty.Tests.Helpers;
using NUnit.Framework;

namespace Dynasty.Tests.GameClock
{
    /// <summary>
    /// Story BCF-002 tests: IGameClock / IRunClockControl contract, pause-compensated RunTime,
    /// stamp conversion, and the deterministic FakeGameClock. Uses only FakeGameClock and the pure
    /// RunClockState core with explicit realtime inputs (no real time).
    /// AC-9 (only UnityGameClock reads UnityEngine.Time) is a code-review check; the automated gate
    /// lands in Story BCF-006.
    /// </summary>
    public class GameClockRunTimeTest
    {
        private const float Tolerance = 1e-5f;

        // ---------------------------------------------------------------- AC-1

        [Test]
        public void test_game_clock_interface_exposes_exactly_four_readonly_float_properties()
        {
            // Arrange
            var type = typeof(IGameClock);
            var expected = new[] { "DeltaTime", "RunTime", "Time", "UnscaledTime" };

            // Act
            PropertyInfo[] props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            MethodInfo[] methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance);

            // Assert
            Assert.That(props.Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal), Is.EqualTo(expected));
            Assert.That(props.All(p => p.PropertyType == typeof(float)), Is.True);
            Assert.That(props.All(p => p.CanRead && !p.CanWrite), Is.True);
            Assert.That(methods.Length, Is.EqualTo(4), "only the four getters may exist - no other methods");
            Assert.That(type.GetEvents().Length, Is.EqualTo(0));
        }

        // ---------------------------------------------------------------- AC-2

        [Test]
        public void test_game_clock_run_time_before_origin_is_zero_and_after_origin_is_elapsed()
        {
            // Arrange
            var clock = new FakeGameClock();
            clock.Advance(3.0f);

            // Act / Assert (before origin)
            Assert.That(clock.RunTime, Is.EqualTo(0f));

            // Act (after origin)
            clock.MarkRunOrigin();
            clock.Advance(2.0f);

            // Assert
            Assert.That(clock.RunTime, Is.EqualTo(2.0f).Within(Tolerance));
        }

        [Test]
        public void test_game_clock_run_time_read_immediately_after_mark_origin_is_exactly_zero()
        {
            // Arrange
            var clock = new FakeGameClock();
            clock.Advance(3.0f);

            // Act
            clock.MarkRunOrigin();

            // Assert
            Assert.That(clock.RunTime, Is.EqualTo(0f));
        }

        [Test]
        public void test_game_clock_pause_calls_before_origin_are_noops_and_run_time_stays_zero()
        {
            // Arrange
            var clock = new FakeGameClock();

            // Act
            clock.BeginPause();
            clock.Advance(4f);
            clock.EndPause();

            // Assert
            Assert.That(clock.RunTime, Is.EqualTo(0f));
        }

        // ---------------------------------------------------------------- AC-3

        [Test]
        public void test_game_clock_pause_holds_run_time_never_decreases_and_resumes_in_place()
        {
            // Arrange
            var clock = new FakeGameClock();
            clock.MarkRunOrigin();
            clock.Advance(10f);
            Assert.That(clock.RunTime, Is.EqualTo(10f).Within(Tolerance));
            var samples = new List<float> { clock.RunTime };

            // Act
            clock.BeginPause();
            clock.TimeScale = 0f;
            samples.Add(clock.RunTime);
            clock.Advance(5f);
            samples.Add(clock.RunTime);
            float duringPause = clock.RunTime;
            clock.EndPause();
            clock.TimeScale = 1f;
            samples.Add(clock.RunTime);
            clock.Advance(1f);
            samples.Add(clock.RunTime);

            // Assert
            Assert.That(duringPause, Is.EqualTo(10f).Within(Tolerance));
            Assert.That(samples[1], Is.EqualTo(10f).Within(Tolerance));
            Assert.That(samples[2], Is.EqualTo(10f).Within(Tolerance));
            Assert.That(samples[3], Is.EqualTo(10f).Within(Tolerance));
            Assert.That(clock.RunTime, Is.EqualTo(11f).Within(Tolerance));
            for (int i = 1; i < samples.Count; i++)
            {
                Assert.That(samples[i], Is.GreaterThanOrEqualTo(samples[i - 1]), "sample " + i);
            }
        }

        [Test]
        public void test_game_clock_two_consecutive_pauses_accumulate_eight_seconds_of_compensation()
        {
            // Arrange
            var clock = new FakeGameClock();
            clock.MarkRunOrigin();
            clock.Advance(10f);

            // Act
            clock.BeginPause();
            clock.TimeScale = 0f;
            clock.Advance(5f);
            clock.EndPause();
            clock.TimeScale = 1f;
            clock.BeginPause();
            clock.TimeScale = 0f;
            clock.Advance(3f);
            clock.EndPause();
            clock.TimeScale = 1f;
            clock.Advance(1f);

            // Assert: realtime elapsed 19 s, 8 s compensated
            Assert.That(clock.RunTime, Is.EqualTo(11f).Within(Tolerance));
        }

        [Test]
        public void test_game_clock_zero_length_pause_changes_nothing()
        {
            // Arrange
            var clock = new FakeGameClock();
            clock.MarkRunOrigin();
            clock.Advance(4f);

            // Act
            clock.BeginPause();
            clock.EndPause();
            clock.Advance(1f);

            // Assert
            Assert.That(clock.RunTime, Is.EqualTo(5f).Within(Tolerance));
        }

        [Test]
        public void test_game_clock_begin_pause_while_paused_and_end_pause_while_running_are_noops()
        {
            // Arrange
            var clock = new FakeGameClock();
            clock.MarkRunOrigin();
            clock.Advance(2f);

            // Act
            clock.EndPause();          // not paused: no-op
            clock.Advance(1f);
            clock.BeginPause();
            clock.Advance(2f);
            clock.BeginPause();        // already paused: must not restart the pause
            clock.Advance(2f);
            clock.EndPause();
            clock.EndPause();          // not paused again: no-op
            clock.Advance(1f);

            // Assert: 2 + 1 un-paused before, 4 s paused, 1 after
            Assert.That(clock.RunTime, Is.EqualTo(4f).Within(Tolerance));
        }

        // ---------------------------------------------------------------- AC-4

        [Test]
        public void test_game_clock_during_pause_unscaled_time_advances_and_delta_time_and_time_hold()
        {
            // Arrange
            var clock = new FakeGameClock();
            clock.MarkRunOrigin();
            clock.Advance(1f);
            clock.BeginPause();
            clock.TimeScale = 0f;
            float unscaledBefore = clock.UnscaledTime;
            float timeBefore = clock.Time;

            // Act
            clock.Advance(0.5f);

            // Assert
            Assert.That(clock.UnscaledTime, Is.EqualTo(unscaledBefore + 0.5f).Within(Tolerance));
            Assert.That(clock.DeltaTime, Is.EqualTo(0f));
            Assert.That(clock.Time, Is.EqualTo(timeBefore));
        }

        [Test]
        public void test_game_clock_time_scale_one_delta_time_equals_dt_exactly()
        {
            // Arrange
            var clock = new FakeGameClock();
            const float dt = 0.016f;

            // Act
            clock.Advance(dt);

            // Assert
            Assert.That(clock.DeltaTime, Is.EqualTo(dt));
            Assert.That(clock.Time, Is.EqualTo(dt));
            Assert.That(clock.UnscaledTime, Is.EqualTo(dt));
        }

        [Test]
        public void test_game_clock_fractional_time_scale_scales_time_and_delta_but_not_unscaled()
        {
            // Arrange
            var clock = new FakeGameClock { TimeScale = 0.5f };

            // Act
            clock.Advance(2f);

            // Assert
            Assert.That(clock.UnscaledTime, Is.EqualTo(2f));
            Assert.That(clock.Time, Is.EqualTo(1f));
            Assert.That(clock.DeltaTime, Is.EqualTo(1f));
        }

        // ---------------------------------------------------------------- AC-5

        [Test]
        public void test_game_clock_control_members_live_only_on_the_control_interface()
        {
            // Arrange
            var clockMethods = typeof(IGameClock).GetMethods(BindingFlags.Public | BindingFlags.Instance);
            // IRunClockControl inherits ToRunTime from the read-only IRunTimeConverter seam (G-2, IM-004).
            var controlMethods = typeof(IRunClockControl).GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Concat(typeof(IRunClockControl).GetInterfaces().SelectMany(i => i.GetMethods()));
            var controlNames = controlMethods.Select(m => m.Name).ToArray();
            Assert.That(typeof(IRunTimeConverter).GetMethods().Select(m => m.Name), Is.EqualTo(new[] { "ToRunTime" }));

            // Act / Assert
            Assert.That(clockMethods.All(m => m.IsSpecialName && m.Name.StartsWith("get_", StringComparison.Ordinal)),
                Is.True, "IGameClock may declare property getters only");
            Assert.That(controlNames, Is.SupersetOf(new[] { "MarkRunOrigin", "BeginPause", "EndPause", "ToRunTime" }));
            Assert.That(clockMethods.Select(m => m.Name).Intersect(controlNames), Is.Empty);
            Assert.That(typeof(IRunClockControl).IsAssignableFrom(typeof(IGameClock)), Is.False);
            Assert.That(typeof(IGameClock).IsAssignableFrom(typeof(IRunClockControl)), Is.False);
            Assert.That(typeof(IRunClockControl).GetProperties().Length, Is.EqualTo(0));
        }

        // ---------------------------------------------------------------- AC-6

        [Test]
        public void test_game_clock_remarking_origin_resets_run_time_and_clears_pause_accumulator()
        {
            // Arrange: RunTime == 30 with 5 s accumulated pause
            var clock = new FakeGameClock();
            clock.MarkRunOrigin();
            clock.Advance(30f);
            clock.BeginPause();
            clock.Advance(5f);
            clock.EndPause();
            Assert.That(clock.RunTime, Is.EqualTo(30f).Within(Tolerance));

            // Act
            clock.MarkRunOrigin();
            float immediately = clock.RunTime;
            clock.Advance(1f);

            // Assert
            Assert.That(immediately, Is.EqualTo(0f));
            Assert.That(clock.RunTime, Is.EqualTo(1f).Within(Tolerance));
        }

        [Test]
        public void test_game_clock_remarking_origin_while_paused_starts_new_run_unpaused()
        {
            // Arrange
            var clock = new FakeGameClock();
            clock.MarkRunOrigin();
            clock.Advance(7f);
            clock.BeginPause();
            clock.Advance(2f);

            // Act
            clock.MarkRunOrigin();
            float immediately = clock.RunTime;
            clock.Advance(1.5f);

            // Assert: new run is not paused, so time advances; old pause does not leak
            Assert.That(immediately, Is.EqualTo(0f));
            Assert.That(clock.RunTime, Is.EqualTo(1.5f).Within(Tolerance));
            clock.EndPause(); // stale EndPause from the old run must be a no-op
            clock.Advance(1f);
            Assert.That(clock.RunTime, Is.EqualTo(2.5f).Within(Tolerance));
        }

        // ---------------------------------------------------------------- AC-7

        [Test]
        public void test_game_clock_stamp_at_current_realtime_converts_to_current_run_time()
        {
            // Arrange: origin at T0, 4 s elapsed including a 1 s pause that has ended
            FakeGameClock clock = BuildClockWithEndedOneSecondPause(out double t0);

            // Act
            float converted = clock.ToRunTime(clock.Realtime);

            // Assert
            Assert.That(clock.Realtime, Is.EqualTo(t0 + 4.0).Within(1e-9));
            Assert.That(clock.RunTime, Is.EqualTo(3.0f).Within(Tolerance));
            Assert.That(converted, Is.EqualTo(clock.RunTime));
        }

        [Test]
        public void test_game_clock_stamp_before_origin_converts_to_zero()
        {
            // Arrange
            FakeGameClock clock = BuildClockWithEndedOneSecondPause(out double t0);

            // Act
            float converted = clock.ToRunTime(t0 - 1.0);

            // Assert
            Assert.That(converted, Is.EqualTo(0f));
        }

        [Test]
        public void test_game_clock_stamp_inside_ended_pause_is_finite_nonnegative_and_not_above_run_time()
        {
            // Arrange: pause occupied [T0 + 1, T0 + 2]
            FakeGameClock clock = BuildClockWithEndedOneSecondPause(out double t0);

            // Act
            float converted = clock.ToRunTime(t0 + 1.5);

            // Assert
            Assert.That(float.IsNaN(converted) || float.IsInfinity(converted), Is.False);
            Assert.That(converted, Is.GreaterThanOrEqualTo(0f));
            Assert.That(converted, Is.LessThanOrEqualTo(clock.RunTime));
            Assert.That(converted, Is.EqualTo(1.0f).Within(Tolerance), "frozen at the pause-start value");
        }

        [Test]
        public void test_game_clock_stamp_conversion_is_monotonic_across_origin_pause_and_now()
        {
            // Arrange
            FakeGameClock clock = BuildClockWithEndedOneSecondPause(out double t0);
            float previous = 0f;

            // Act / Assert: sweep from before the origin to after now
            for (double stamp = t0 - 1.0; stamp <= clock.Realtime + 1.0; stamp += 0.125)
            {
                float converted = clock.ToRunTime(stamp);
                Assert.That(float.IsNaN(converted), Is.False);
                Assert.That(converted, Is.GreaterThanOrEqualTo(previous), "stamp " + stamp);
                Assert.That(converted, Is.LessThanOrEqualTo(clock.RunTime));
                previous = converted;
            }
        }

        [Test]
        public void test_game_clock_stamp_taken_before_a_pause_converts_exactly_after_the_pause()
        {
            // Arrange: origin at T0, stamp at T0+1 (run 1), then 2 s pause, then now = T0+4
            var clock = new FakeGameClock();
            clock.Advance(3f);
            clock.MarkRunOrigin();
            double stamp = clock.Realtime + 1.0;
            clock.Advance(1f);
            clock.BeginPause();
            clock.Advance(2f);
            clock.EndPause();
            clock.Advance(1f);

            // Act
            float converted = clock.ToRunTime(stamp);

            // Assert: earlier pauses must not be subtracted from earlier stamps
            Assert.That(converted, Is.EqualTo(1f).Within(Tolerance));
            Assert.That(clock.RunTime, Is.EqualTo(2f).Within(Tolerance));
        }

        [Test]
        public void test_game_clock_stamp_during_in_progress_pause_is_frozen_value_not_above_run_time()
        {
            // Arrange
            var clock = new FakeGameClock();
            clock.MarkRunOrigin();
            clock.Advance(2f);
            clock.BeginPause();
            double pauseStamp = clock.Realtime + 1.0;
            clock.Advance(3f);

            // Act
            float converted = clock.ToRunTime(pauseStamp);

            // Assert
            Assert.That(converted, Is.EqualTo(2f).Within(Tolerance));
            Assert.That(converted, Is.LessThanOrEqualTo(clock.RunTime));
        }

        [Test]
        public void test_game_clock_stamp_conversion_before_origin_is_marked_is_zero()
        {
            // Arrange
            var clock = new FakeGameClock();
            clock.Advance(5f);

            // Act
            float converted = clock.ToRunTime(clock.Realtime);

            // Assert
            Assert.That(converted, Is.EqualTo(0f));
        }

        [Test]
        public void test_game_clock_non_finite_stamp_after_origin_converts_to_finite_value_within_run_time()
        {
            // Arrange: origin marked, so the NaN guard (not the no-origin guard) is what's exercised
            var clock = new FakeGameClock();
            clock.MarkRunOrigin();
            clock.Advance(4f);

            // Act
            float nan = clock.ToRunTime(double.NaN);
            float positiveInfinity = clock.ToRunTime(double.PositiveInfinity);
            float negativeInfinity = clock.ToRunTime(double.NegativeInfinity);

            // Assert
            Assert.That(nan, Is.EqualTo(0f));
            Assert.That(positiveInfinity, Is.EqualTo(clock.RunTime));
            Assert.That(negativeInfinity, Is.EqualTo(0f));
        }

        // ---------------------------------------------------------------- AC-7 / AC-3: production core, explicit realtime

        [Test]
        public void test_run_clock_state_run_time_follows_formula_with_explicit_realtime()
        {
            // Arrange
            var state = new RunClockState();
            const double origin = 1000.0;

            // Act
            float beforeOrigin = state.GetRunTime(990.0);
            state.MarkOrigin(origin);
            float atOrigin = state.GetRunTime(origin);
            state.BeginPause(1010.0);
            float inPause = state.GetRunTime(1013.0);
            state.EndPause(1015.0);
            float afterPause = state.GetRunTime(1020.0);

            // Assert: RunTime == (now - origin) - totalPaused
            Assert.That(beforeOrigin, Is.EqualTo(0f));
            Assert.That(atOrigin, Is.EqualTo(0f));
            Assert.That(inPause, Is.EqualTo(10f).Within(Tolerance));
            Assert.That(afterPause, Is.EqualTo(15f).Within(Tolerance));
        }

        [Test]
        public void test_run_clock_state_in_progress_pause_is_included_in_compensation()
        {
            // Arrange
            var state = new RunClockState();
            state.MarkOrigin(50.0);
            state.BeginPause(60.0);

            // Act
            float runTime = state.GetRunTime(75.0);

            // Assert
            Assert.That(state.IsPaused, Is.True);
            Assert.That(runTime, Is.EqualTo(10f).Within(Tolerance));
        }

        [Test]
        public void test_run_clock_state_pause_api_is_idempotent_and_never_accumulates_negative()
        {
            // Arrange
            var state = new RunClockState();
            state.MarkOrigin(0.0);

            // Act
            state.EndPause(5.0);          // not paused
            state.BeginPause(10.0);
            state.BeginPause(12.0);       // already paused: ignored
            state.EndPause(14.0);
            state.EndPause(20.0);         // not paused: ignored
            state.BeginPause(30.0);
            state.EndPause(25.0);         // clock went backwards: no negative accumulation

            // Assert: only the 4 s pause counts
            Assert.That(state.CompletedPausedTotal, Is.EqualTo(4.0).Within(1e-12));
            Assert.That(state.GetRunTime(40.0), Is.EqualTo(36f).Within(Tolerance));
        }

        [Test]
        public void test_run_clock_state_mark_origin_clears_accumulator_and_paused_flag()
        {
            // Arrange
            var state = new RunClockState();
            state.MarkOrigin(0.0);
            state.BeginPause(1.0);
            state.EndPause(6.0);
            state.BeginPause(7.0);

            // Act
            state.MarkOrigin(100.0);

            // Assert
            Assert.That(state.IsPaused, Is.False);
            Assert.That(state.CompletedPausedTotal, Is.EqualTo(0.0));
            Assert.That(state.GetRunTime(101.0), Is.EqualTo(1f).Within(Tolerance));
        }

        [Test]
        public void test_run_clock_state_stamp_conversion_handles_before_origin_inside_pause_and_now()
        {
            // Arrange: origin 100; pauses [110,115] and [120,123]; now 130 -> RunTime 30 - 8 = 22
            var state = new RunClockState();
            state.MarkOrigin(100.0);
            state.BeginPause(110.0);
            state.EndPause(115.0);
            state.BeginPause(120.0);
            state.EndPause(123.0);
            const double now = 130.0;

            // Act / Assert
            Assert.That(state.GetRunTime(now), Is.EqualTo(22f).Within(Tolerance));
            Assert.That(state.ToRunTime(now, now), Is.EqualTo(22f).Within(Tolerance));
            Assert.That(state.ToRunTime(99.0, now), Is.EqualTo(0f));
            Assert.That(state.ToRunTime(105.0, now), Is.EqualTo(5f).Within(Tolerance));
            Assert.That(state.ToRunTime(112.0, now), Is.EqualTo(10f).Within(Tolerance), "inside first pause");
            Assert.That(state.ToRunTime(118.0, now), Is.EqualTo(13f).Within(Tolerance), "between pauses");
            Assert.That(state.ToRunTime(121.0, now), Is.EqualTo(15f).Within(Tolerance), "inside second pause (frozen at its start)");
            Assert.That(state.ToRunTime(127.0, now), Is.EqualTo(19f).Within(Tolerance), "after both pauses");
            Assert.That(state.ToRunTime(500.0, now), Is.EqualTo(22f).Within(Tolerance), "future stamp clamps to now");
        }

        [Test]
        public void test_run_clock_state_stamp_conversion_stays_valid_after_pause_record_overflows()
        {
            // Arrange: more pauses than the fixed record holds; each 1 s pause every 2 s
            var state = new RunClockState();
            state.MarkOrigin(0.0);
            int pauses = RunClockState.MaxRecordedPauses + 10;
            double t = 0.0;
            for (int i = 0; i < pauses; i++)
            {
                t += 1.0;
                state.BeginPause(t);
                t += 1.0;
                state.EndPause(t);
            }

            double now = t + 5.0;
            float previous = 0f;

            // Act / Assert
            Assert.That(state.GetRunTime(now), Is.EqualTo((float)(pauses + 5)).Within(Tolerance));
            Assert.That(state.ToRunTime(t, now), Is.EqualTo((float)pauses).Within(Tolerance), "recent stamp stays exact");
            for (double stamp = -1.0; stamp <= now; stamp += 0.5)
            {
                float converted = state.ToRunTime(stamp, now);
                Assert.That(float.IsNaN(converted), Is.False);
                Assert.That(converted, Is.GreaterThanOrEqualTo(previous));
                previous = converted;
            }
        }

        [Test]
        public void test_run_clock_state_overflow_keeps_retained_stamps_exact_and_archived_stamps_clamped()
        {
            // Arrange: pause i occupies [2i+1, 2i+2]; a stamp at 2k+0.5 (run segment k) has exact RunTime s - k.
            var state = new RunClockState();
            state.MarkOrigin(0.0);
            int pauses = RunClockState.MaxRecordedPauses + 10;
            int archived = pauses - RunClockState.MaxRecordedPauses;
            for (int i = 0; i < pauses; i++)
            {
                state.BeginPause(2.0 * i + 1.0);
                state.EndPause(2.0 * i + 2.0);
            }

            double now = 2.0 * pauses + 5.0;

            // Act / Assert: retained region (k >= archived) is exact
            for (int k = archived; k < pauses; k++)
            {
                double stamp = 2.0 * k + 0.5;
                Assert.That(state.ToRunTime(stamp, now), Is.EqualTo((float)(stamp - k)).Within(Tolerance), "retained k=" + k);
            }

            // Act / Assert: archived region over-subtracts but stays in [0, exact]
            for (int k = 0; k < archived; k++)
            {
                double stamp = 2.0 * k + 0.5;
                float converted = state.ToRunTime(stamp, now);
                Assert.That(converted, Is.InRange(0f, (float)(stamp - k) + Tolerance), "archived k=" + k);
            }
        }

        [Test]
        public void test_run_clock_state_remark_after_overflow_clears_archive_and_record()
        {
            // Arrange: overflow the record, then start a new run
            var state = new RunClockState();
            state.MarkOrigin(0.0);
            for (int i = 0; i < RunClockState.MaxRecordedPauses + 10; i++)
            {
                state.BeginPause(2.0 * i + 1.0);
                state.EndPause(2.0 * i + 2.0);
            }

            const double newOrigin = 1000.0;
            state.MarkOrigin(newOrigin);

            // Act
            float converted = state.ToRunTime(newOrigin + 2.0, newOrigin + 3.0);

            // Assert: no archived or recorded pause leaks into the new run
            Assert.That(state.CompletedPausedTotal, Is.EqualTo(0.0));
            Assert.That(converted, Is.EqualTo(2f).Within(Tolerance));
            Assert.That(state.GetRunTime(newOrigin + 3.0), Is.EqualTo(3f).Within(Tolerance));
        }

        [Test]
        public void test_run_clock_state_large_realtime_keeps_sub_frame_precision()
        {
            // Arrange: ~11.6 days of uptime; float math at 1e6 has ~0.06 s ULP, so this fails loudly
            // if the internal state ever regresses from double to float.
            const double origin = 1_000_000.0;
            const float subFrameTolerance = 1e-6f;
            var state = new RunClockState();
            state.MarkOrigin(origin);
            state.BeginPause(origin + 0.25);
            state.EndPause(origin + 0.375);

            // Act
            float runTime = state.GetRunTime(origin + 0.501);
            float converted = state.ToRunTime(origin + 0.125, origin + 0.501);

            // Assert
            Assert.That(runTime, Is.EqualTo(0.376f).Within(subFrameTolerance));
            Assert.That(converted, Is.EqualTo(0.125f).Within(subFrameTolerance));
        }

        [Test]
        public void test_run_clock_state_now_before_origin_reads_zero()
        {
            // Arrange
            var state = new RunClockState();
            state.MarkOrigin(50.0);

            // Act
            float runTime = state.GetRunTime(40.0);

            // Assert
            Assert.That(runTime, Is.EqualTo(0f));
        }

        [Test]
        public void test_game_clock_fake_advance_rejects_negative_or_non_finite_dt()
        {
            // Arrange
            var clock = new FakeGameClock();

            // Act / Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => clock.Advance(-0.001f));
            Assert.Throws<ArgumentOutOfRangeException>(() => clock.Advance(float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => clock.Advance(float.PositiveInfinity));
            Assert.That(clock.Realtime, Is.EqualTo(0.0), "rejected calls must not move time");
        }

        // ---------------------------------------------------------------- AC-8

        [Test]
        public void test_game_clock_fixed_script_is_bit_identical_across_ten_runs()
        {
            // Arrange
            List<int> reference = RunDeterminismScript();

            // Act / Assert
            for (int run = 1; run < 10; run++)
            {
                Assert.That(RunDeterminismScript(), Is.EqualTo(reference), "run " + run);
            }

            Assert.That(reference, Is.Not.Empty);
        }

        [Test]
        public void test_game_clock_advance_zero_changes_nothing_but_delta_time_becomes_zero()
        {
            // Arrange
            var clock = new FakeGameClock();
            clock.MarkRunOrigin();
            clock.Advance(1.25f);
            float time = clock.Time;
            float unscaled = clock.UnscaledTime;
            float run = clock.RunTime;
            double realtime = clock.Realtime;

            // Act
            clock.Advance(0f);

            // Assert
            Assert.That(clock.Time, Is.EqualTo(time));
            Assert.That(clock.UnscaledTime, Is.EqualTo(unscaled));
            Assert.That(clock.RunTime, Is.EqualTo(run));
            Assert.That(clock.Realtime, Is.EqualTo(realtime));
            Assert.That(clock.DeltaTime, Is.EqualTo(0f));
        }

        [Test]
        public void test_game_clock_advance_iframe_boundary_value_is_exact_in_all_scaled_domains()
        {
            // Arrange
            var clock = new FakeGameClock();
            clock.MarkRunOrigin();
            const float boundary = 0.501f; // AC-DH-07 boundary

            // Act
            clock.Advance(boundary);

            // Assert
            Assert.That(clock.UnscaledTime, Is.EqualTo(boundary));
            Assert.That(clock.Time, Is.EqualTo(boundary));
            Assert.That(clock.DeltaTime, Is.EqualTo(boundary));
            Assert.That(clock.RunTime, Is.EqualTo(boundary).Within(Tolerance));
        }

        [Test]
        public void test_game_clock_instances_share_no_state()
        {
            // Arrange
            var first = new FakeGameClock();
            var second = new FakeGameClock();
            first.MarkRunOrigin();

            // Act
            first.Advance(5f);

            // Assert
            Assert.That(second.Time, Is.EqualTo(0f));
            Assert.That(second.UnscaledTime, Is.EqualTo(0f));
            Assert.That(second.RunTime, Is.EqualTo(0f));
        }

        // ---------------------------------------------------------------- helpers

        private static FakeGameClock BuildClockWithEndedOneSecondPause(out double originRealtime)
        {
            var clock = new FakeGameClock();
            clock.Advance(3f);
            clock.MarkRunOrigin();
            originRealtime = clock.Realtime;
            clock.Advance(1f);
            clock.BeginPause();
            clock.TimeScale = 0f;
            clock.Advance(1f);
            clock.EndPause();
            clock.TimeScale = 1f;
            clock.Advance(2f);
            return clock;
        }

        private static List<int> RunDeterminismScript()
        {
            var log = new List<int>();
            var clock = new FakeGameClock();

            void Sample()
            {
                log.Add(BitConverter.SingleToInt32Bits(clock.Time));
                log.Add(BitConverter.SingleToInt32Bits(clock.UnscaledTime));
                log.Add(BitConverter.SingleToInt32Bits(clock.DeltaTime));
                log.Add(BitConverter.SingleToInt32Bits(clock.RunTime));
            }

            clock.Advance(0.016f); Sample();
            clock.MarkRunOrigin(); Sample();
            clock.Advance(0.501f); Sample();
            clock.Advance(0f); Sample();
            clock.BeginPause(); clock.TimeScale = 0f; Sample();
            clock.Advance(5f); Sample();
            clock.EndPause(); clock.TimeScale = 1f; Sample();
            clock.Advance(0.016f); Sample();
            clock.TimeScale = 0.5f;
            clock.Advance(0.25f); Sample();
            clock.BeginPause(); clock.Advance(3f); clock.EndPause(); Sample();
            clock.MarkRunOrigin(); Sample();
            clock.TimeScale = 1f;
            clock.Advance(0.501f); Sample();
            return log;
        }
    }
}

using System;
using System.Linq;
using System.Reflection;
using Dynasty.Core;
using Dynasty.Input;
using Dynasty.Tests.Helpers;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Dynasty.Tests.InputManagers
{
    /// <summary>
    /// Story IM-004 tests: Parry.performed capture at the device-free seam, ctx.time → RunTime stamping
    /// through IRunTimeConverter, one press per hold, inert before run start, stamp coherence while
    /// paused (scoped AC-IM-11), capture never suppressed by i-frames.
    /// </summary>
    public class InputManagerParryCaptureTest
    {
        private const double RealtimeAtOrigin = 100.0;
        private const float FrameSeconds = 1f / 60f;

        private GameObject _go;
        private InputManager _input;
        private FakeGameClock _clock;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("InputManagerCaptureTest");
            _input = _go.AddComponent<InputManager>();
            _clock = new FakeGameClock();
            _input.Initialize(_clock, _clock, TestInputTuning.Default());
            _clock.Advance((float)RealtimeAtOrigin);
            _clock.MarkRunOrigin();
        }

        [TearDown]
        public void TearDown()
        {
            _input.Dispose();
            Object.DestroyImmediate(_go);
        }

        private void StartRun()
        {
            _input.SetRunStartTime(_clock.RunTime);
            _input.SetContext(InputContext.Gameplay);
        }

        private int BufferCount()
        {
            return _input.ParryBuffer.Count;
        }

        // ---------------------------------------------------------------- AC-1

        [Test]
        public void test_parry_capture_performed_press_is_stamped_on_run_clock()
        {
            // Arrange: event time 101.000 on the realtime basis = RunTime 1.000
            StartRun();
            _clock.Advance(1.05f);

            // Act
            _input.OnParryPerformed(RealtimeAtOrigin + 1.000);

            // Assert
            Assert.That(BufferCount(), Is.EqualTo(1));
            Assert.That(_input.TryConsumeParryPress(0f, out float pressTime), Is.True);
            Assert.That(pressTime, Is.EqualTo(1.000f).Within(1e-4f));
        }

        [Test]
        public void test_parry_capture_sub_frame_event_time_is_not_snapped()
        {
            // Arrange
            StartRun();
            _clock.Advance(1.05f);

            // Act
            _input.OnParryPerformed(RealtimeAtOrigin + 1.0083);

            // Assert
            _input.ParryBuffer.TryGetEntry(0, out float pressTime, out _);
            Assert.That(pressTime, Is.EqualTo(1.0083f).Within(1e-4f));
        }

        // ---------------------------------------------------------------- AC-2 (AC-IM-09)

        [Test]
        public void test_parry_capture_held_button_records_exactly_one_press()
        {
            // Arrange
            StartRun();
            _clock.Advance(0.5f);

            // Act: one performed edge, then 0.5 s of held frames with no further performed events
            _input.OnParryPerformed(RealtimeAtOrigin + 0.5);
            for (int frame = 0; frame < 30; frame++)
            {
                _clock.Advance(FrameSeconds);
            }

            // Assert
            Assert.That(BufferCount(), Is.EqualTo(1));
        }

        [Test]
        public void test_parry_capture_parry_action_has_no_interactions()
        {
            // Act
            string interactions = InputManagerTestSeam.ActionInteractions(_input, "Gameplay", "Parry");

            // Assert (Hold/Tap/MultiTap would change when performed fires)
            Assert.That(interactions, Is.Empty);
        }

        // ---------------------------------------------------------------- AC-3 (AC-IM-10)

        [Test]
        public void test_parry_capture_inert_before_set_run_start_time()
        {
            // Arrange
            _clock.Advance(1f);

            // Act: no context, then Gameplay context, both without SetRunStartTime
            _input.OnParryPerformed(RealtimeAtOrigin + 0.9);
            _input.SetContext(InputContext.Gameplay);
            _input.OnParryPerformed(RealtimeAtOrigin + 0.95);

            // Assert
            Assert.That(BufferCount(), Is.EqualTo(0));
            Assert.That(_input.HasLiveParryPress(), Is.False);
            Assert.That(_input.IsCaptureArmed, Is.False);
        }

        [Test]
        public void test_parry_capture_records_after_run_start_and_gameplay_context()
        {
            // Arrange
            _clock.Advance(1f);
            _input.SetRunStartTime(_clock.RunTime);
            _input.OnParryPerformed(RealtimeAtOrigin + 0.98);   // run started, context not Gameplay yet
            int beforeGameplay = BufferCount();
            _input.SetContext(InputContext.Gameplay);

            // Act
            _input.OnParryPerformed(RealtimeAtOrigin + 0.99);

            // Assert
            Assert.That(beforeGameplay, Is.EqualTo(0));
            Assert.That(BufferCount(), Is.EqualTo(1));
            _input.ParryBuffer.TryGetEntry(0, out float pressTime, out _);
            Assert.That(pressTime, Is.EqualTo(0.99f).Within(1e-4f));
        }

        // ---------------------------------------------------------------- AC-4 (AC-IM-11, scoped)

        [Test]
        public void test_parry_capture_stamp_during_pause_is_finite_frozen_and_not_buffered()
        {
            // Arrange: run to RunTime 5.000, stamp a press, then pause (timeScale 0, RunTime frozen)
            StartRun();
            _clock.Advance(5f);
            _input.OnParryPerformed(RealtimeAtOrigin + 4.9);
            _input.ParryBuffer.TryGetEntry(0, out float lastPrePause, out _);
            _clock.BeginPause();
            _clock.TimeScale = 0f;
            _input.SetContext(InputContext.Paused);
            _clock.Advance(2f);

            // Act
            float stampInsidePause = _clock.ToRunTime(RealtimeAtOrigin + 6.0);
            _input.OnParryPerformed(RealtimeAtOrigin + 6.0);

            // Assert
            Assert.That(float.IsNaN(stampInsidePause) || float.IsInfinity(stampInsidePause), Is.False);
            Assert.That(stampInsidePause, Is.EqualTo(5f).Within(1e-4f), "frozen at the pause start");
            Assert.That(stampInsidePause, Is.GreaterThanOrEqualTo(lastPrePause));
            Assert.That(_clock.DeltaTime, Is.EqualTo(0f));
            Assert.That(BufferCount(), Is.EqualTo(1), "no press enters the buffer while Paused (AC-IM-08 / States table)");
        }

        // ---------------------------------------------------------------- AC-5

        [Test]
        public void test_parry_capture_has_no_health_or_iframe_dependency_and_always_records()
        {
            // Arrange
            StartRun();
            _clock.Advance(1f);
            const BindingFlags All = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

            // Act: a player-invulnerable state exists only outside Input Manager; capture just records
            _input.OnParryPerformed(RealtimeAtOrigin + 0.95);
            string[] healthDeps = typeof(InputManager).GetFields(All)
                .Select(f => f.FieldType.Name)
                .Where(n => n.IndexOf("Health", StringComparison.Ordinal) >= 0 || n.IndexOf("Invuln", StringComparison.Ordinal) >= 0)
                .ToArray();

            // Assert
            Assert.That(BufferCount(), Is.EqualTo(1));
            Assert.That(healthDeps, Is.Empty);
        }
    }
}

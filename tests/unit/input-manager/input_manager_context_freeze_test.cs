using Dynasty.Core;
using Dynasty.Input;
using Dynasty.Tests.Helpers;
using NUnit.Framework;
using UnityEngine;

namespace Dynasty.Tests.InputManagers
{
    /// <summary>
    /// Story IM-005 tests: the parry buffer freezes outside Gameplay (no add, no prune, never cleared),
    /// Paused ≡ Menu, same-frame switch, prune on the first Gameplay tick in the RunTime domain (including
    /// the pause-compensated OQ-02 case), queries still work while frozen.
    /// </summary>
    [TestFixture(InputContext.Menu)]
    [TestFixture(InputContext.Paused)]
    public class InputManagerContextFreezeTest
    {
        private const double RealtimeAtOrigin = 100.0;

        private readonly InputContext _frozenContext;
        private GameObject _go;
        private InputManager _input;
        private FakeGameClock _clock;

        public InputManagerContextFreezeTest(InputContext frozenContext)
        {
            _frozenContext = frozenContext;
        }

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("InputManagerFreezeTest");
            _input = _go.AddComponent<InputManager>();
            _clock = new FakeGameClock();
            _input.Initialize(_clock, _clock, TestInputTuning.Default());
            _clock.Advance((float)RealtimeAtOrigin);
            _clock.MarkRunOrigin();
            _input.SetRunStartTime(_clock.RunTime);
            _input.SetContext(InputContext.Gameplay);
            _clock.Advance(1.000f);
            _input.OnParryPerformed(RealtimeAtOrigin + 1.000);   // press at RunTime 1.000
        }

        [TearDown]
        public void TearDown()
        {
            _input.Dispose();
            Object.DestroyImmediate(_go);
        }

        private int Count => _input.ParryBuffer.Count;

        private void AdvanceRunTimeTo(float runTime)
        {
            _clock.Advance(runTime - _clock.RunTime);
        }

        // ---------------------------------------------------------------- AC-1 / AC-2 (AC-IM-08)

        [Test]
        public void test_freeze_blocks_new_presses_keeps_old_and_resumes_on_gameplay()
        {
            // Act: freeze, inject a press, tick
            _input.SetContext(_frozenContext);
            AdvanceRunTimeTo(1.010f);
            _input.OnParryPerformed(RealtimeAtOrigin + 1.010);
            _input.Tick();
            int frozenCount = Count;
            _input.ParryBuffer.TryGetEntry(0, out float kept, out bool consumed);

            _input.SetContext(InputContext.Gameplay);
            AdvanceRunTimeTo(1.020f);
            _input.OnParryPerformed(RealtimeAtOrigin + 1.020);

            // Assert
            Assert.That(frozenCount, Is.EqualTo(1));
            Assert.That(kept, Is.EqualTo(1.000f).Within(1e-4f));
            Assert.That(consumed, Is.False);
            Assert.That(Count, Is.EqualTo(2), "capture resumed");
        }

        [Test]
        public void test_freeze_tick_far_past_window_prunes_nothing()
        {
            // Arrange
            _input.SetContext(_frozenContext);
            AdvanceRunTimeTo(2.000f);

            // Act
            _input.Tick();

            // Assert
            Assert.That(Count, Is.EqualTo(1));
        }

        [Test]
        public void test_freeze_menu_then_paused_keeps_buffer_until_first_gameplay_tick()
        {
            // Arrange
            _input.SetContext(InputContext.Menu);
            _input.SetContext(InputContext.Paused);
            AdvanceRunTimeTo(1.500f);
            _input.Tick();
            int beforeGameplayTick = Count;

            // Act
            _input.SetContext(InputContext.Gameplay);
            int afterSwitch = Count;
            _input.Tick();

            // Assert
            Assert.That(beforeGameplayTick, Is.EqualTo(1));
            Assert.That(afterSwitch, Is.EqualTo(1), "switching back alone never prunes or clears");
            Assert.That(Count, Is.EqualTo(0), "first Gameplay tick prunes the aged-out press");
        }

        // ---------------------------------------------------------------- AC-3 same-frame switch

        [Test]
        public void test_freeze_same_frame_switch_keeps_earlier_press_drops_later_one()
        {
            // Arrange: second press in the same frame as the first, after the switch
            _input.SetContext(_frozenContext);

            // Act
            _input.OnParryPerformed(RealtimeAtOrigin + 1.000);

            // Assert
            Assert.That(Count, Is.EqualTo(1));
        }

        // ---------------------------------------------------------------- AC-4 prune on first Gameplay tick

        [Test]
        public void test_freeze_first_gameplay_tick_prunes_press_aged_past_window()
        {
            // Arrange: unpaused time advanced during the menu (RunTime 1.200 on resume)
            _input.SetContext(_frozenContext);
            AdvanceRunTimeTo(1.200f);
            _input.SetContext(InputContext.Gameplay);

            // Act
            _input.Tick();

            // Assert
            Assert.That(Count, Is.EqualTo(0));
            Assert.That(_input.HasLiveParryPress(), Is.False);
        }

        [Test]
        public void test_freeze_pause_compensated_press_is_still_live_after_resume_oq_02()
        {
            // Arrange: RunTime pause-compensated — stays at 1.050 across a 5 s level-up pick
            AdvanceRunTimeTo(1.050f);
            _clock.BeginPause();
            _input.SetContext(_frozenContext);
            _clock.Advance(5f);
            _clock.EndPause();
            _input.SetContext(InputContext.Gameplay);

            // Act
            _input.Tick();

            // Assert (documents the RunTime behaviour flagged against GDD OQ-02)
            Assert.That(_clock.RunTime, Is.EqualTo(1.050f).Within(1e-4f));
            Assert.That(Count, Is.EqualTo(1));
            Assert.That(_input.HasLiveParryPress(), Is.True);
        }

        // ---------------------------------------------------------------- AC-5 queries while frozen

        [Test]
        public void test_freeze_queries_work_while_frozen()
        {
            // Arrange
            _input.SetContext(_frozenContext);
            AdvanceRunTimeTo(1.050f);

            // Act
            bool live = _input.HasLiveParryPress();
            bool consumed = _input.TryConsumeParryPress(0f, out float pressTime);

            // Assert
            Assert.That(live, Is.True);
            Assert.That(consumed, Is.True);
            Assert.That(pressTime, Is.EqualTo(1.000f).Within(1e-4f));
        }
    }
}

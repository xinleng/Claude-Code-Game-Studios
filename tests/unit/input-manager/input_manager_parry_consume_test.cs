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
    /// Story IM-003 tests: TryConsumeParryPress consume-once (AC-IM-02), notBefore guard (AC-IM-03),
    /// Formula 2 front-extension composition (AC-IM-04), most-recent-wins (AC-IM-05), peek/consume order
    /// independence, and a shared contract run against the real InputManager and FakeInputAuthority.
    /// </summary>
    [TestFixture("InputManager")]
    [TestFixture("Fake")]
    public class InputManagerParryConsumeTest
    {
        private readonly string _kind;
        private IHarness _harness;

        public InputManagerParryConsumeTest(string kind)
        {
            _kind = kind;
        }

        /// <summary>Uniform driver over the two IInputAuthority implementations.</summary>
        private interface IHarness : IDisposable
        {
            IInputAuthority Authority { get; }

            void Press(float t);

            void SetNow(float t);

            void Prune();
        }

        private sealed class FakeHarness : IHarness
        {
            private readonly FakeInputAuthority _fake = new FakeInputAuthority();

            public IInputAuthority Authority => _fake;

            public void Press(float t) => _fake.InjectParryPress(t);

            public void SetNow(float t) => _fake.Now = t;

            public void Prune() => _fake.Prune();

            public void Dispose()
            {
            }
        }

        private sealed class InputManagerHarness : IHarness
        {
            private readonly GameObject _go = new GameObject("InputManagerConsumeTest");
            private readonly FakeGameClock _clock = new FakeGameClock();
            private readonly InputManager _input;

            public InputManagerHarness()
            {
                _input = _go.AddComponent<InputManager>();
                _input.Initialize(_clock, _clock, TestInputTuning.Default());
                _clock.MarkRunOrigin();
            }

            public IInputAuthority Authority => _input;

            public void Press(float t) => InputManagerTestSeam.RecordParryPress(_input, t);

            public void SetNow(float t)
            {
                // RunTime only moves forward: advance by the remaining delta.
                float delta = t - _clock.RunTime;
                if (delta > 0f)
                {
                    _clock.Advance(delta);
                }
            }

            public void Prune() => _input.ParryBuffer.Prune(_clock.RunTime);

            public void Dispose()
            {
                _input.Dispose();
                Object.DestroyImmediate(_go);
            }
        }

        [SetUp]
        public void SetUp()
        {
            _harness = _kind == "Fake" ? (IHarness)new FakeHarness() : new InputManagerHarness();
        }

        [TearDown]
        public void TearDown()
        {
            _harness.Dispose();
        }

        private IInputAuthority Input => _harness.Authority;

        // ---------------------------------------------------------------- AC-1 (AC-IM-02)

        [Test]
        public void test_parry_consume_one_press_consumed_exactly_once()
        {
            // Arrange
            _harness.SetNow(1.000f);
            _harness.Press(1.000f);
            _harness.SetNow(1.050f);

            // Act
            bool first = Input.TryConsumeParryPress(0f, out float firstTime);
            bool second = Input.TryConsumeParryPress(0f, out float secondTime);

            // Assert
            Assert.That(first, Is.True);
            Assert.That(firstTime, Is.EqualTo(1.000f));
            Assert.That(second, Is.False);
            Assert.That(secondTime, Is.EqualTo(0f));
            Assert.That(Input.HasLiveParryPress(), Is.False, "a consumed press is not live");
        }

        // ---------------------------------------------------------------- AC-2 (AC-IM-03)

        [Test]
        public void test_parry_consume_not_before_guard_is_inclusive()
        {
            // Arrange
            _harness.SetNow(1.000f);
            _harness.Press(1.000f);
            _harness.SetNow(1.050f);

            // Act
            bool tooOld = Input.TryConsumeParryPress(1.5f, out _);
            bool ok = Input.TryConsumeParryPress(0.9f, out float pressTime);

            // Assert
            Assert.That(tooOld, Is.False);
            Assert.That(ok, Is.True);
            Assert.That(pressTime, Is.EqualTo(1.000f));
        }

        [Test]
        public void test_parry_consume_not_before_equal_to_press_time_consumes()
        {
            // Arrange
            _harness.SetNow(1.000f);
            _harness.Press(1.000f);
            _harness.SetNow(1.050f);

            // Act / Assert
            Assert.That(Input.TryConsumeParryPress(1.000f, out _), Is.True);
        }

        // ---------------------------------------------------------------- AC-3 (AC-IM-04)

        [Test]
        public void test_parry_consume_front_extension_window_at_1080_consumes()
        {
            // Arrange
            _harness.SetNow(1.000f);
            _harness.Press(1.000f);
            _harness.SetNow(1.080f);
            _harness.Prune();

            // Act (Parry composes notBefore = 1.080 − 0.120)
            bool ok = Input.TryConsumeParryPress(0.960f, out float pressTime);

            // Assert
            Assert.That(ok, Is.True);
            Assert.That(pressTime, Is.EqualTo(1.000f));
        }

        [Test]
        public void test_parry_consume_window_at_1200_finds_press_pruned()
        {
            // Arrange
            _harness.SetNow(1.000f);
            _harness.Press(1.000f);
            _harness.SetNow(1.200f);
            _harness.Prune();

            // Act
            bool ok = Input.TryConsumeParryPress(1.080f, out _);

            // Assert
            Assert.That(ok, Is.False);
        }

        [Test]
        public void test_parry_consume_api_takes_no_window_close()
        {
            // Act
            MethodInfo consume = typeof(IInputAuthority).GetMethod(nameof(IInputAuthority.TryConsumeParryPress));

            // Assert (no back-edge extension in Input Manager)
            Assert.That(consume.GetParameters().Select(p => p.Name), Is.EqualTo(new[] { "notBefore", "pressTime" }));
        }

        // ---------------------------------------------------------------- AC-4 (AC-IM-05)

        [Test]
        public void test_parry_consume_most_recent_wins_then_older_in_turn()
        {
            // Arrange
            _harness.SetNow(1.00f);
            _harness.Press(1.00f);
            _harness.Press(1.02f);
            _harness.Press(1.04f);
            _harness.SetNow(1.05f);

            // Act
            Input.TryConsumeParryPress(0f, out float first);
            Input.TryConsumeParryPress(0f, out float second);
            Input.TryConsumeParryPress(0f, out float third);

            // Assert
            Assert.That(new[] { first, second, third }, Is.EqualTo(new[] { 1.04f, 1.02f, 1.00f }));
        }

        [Test]
        public void test_parry_consume_filtered_newer_press_never_falls_through_to_older()
        {
            // Arrange
            _harness.SetNow(1.00f);
            _harness.Press(1.00f);
            _harness.Press(1.02f);
            _harness.Press(1.04f);
            _harness.SetNow(1.05f);

            // Act
            bool first = Input.TryConsumeParryPress(1.03f, out float pressTime);
            bool second = Input.TryConsumeParryPress(1.03f, out _);

            // Assert
            Assert.That(first, Is.True);
            Assert.That(pressTime, Is.EqualTo(1.04f));
            Assert.That(second, Is.False);
        }

        // ---------------------------------------------------------------- AC-5 order independence

        [Test]
        public void test_parry_consume_peek_then_consume_matches_consume_then_peek()
        {
            // Arrange: this harness runs peek → consume; a second fake runs consume → peek
            _harness.SetNow(1.000f);
            _harness.Press(1.000f);
            _harness.SetNow(1.050f);
            var other = new FakeInputAuthority { Now = 1.050f };
            other.InjectParryPress(1.000f);

            // Act
            bool peekA = Input.HasLiveParryPress();
            bool consumeA = Input.TryConsumeParryPress(0f, out float timeA);
            bool consumeB = other.TryConsumeParryPress(0f, out float timeB);
            bool peekB = other.HasLiveParryPress();

            // Assert
            Assert.That(consumeA, Is.True);
            Assert.That(consumeB, Is.True);
            Assert.That(timeA, Is.EqualTo(timeB));
            Assert.That(peekA, Is.True);
            Assert.That(peekB, Is.False);
        }
    }
}

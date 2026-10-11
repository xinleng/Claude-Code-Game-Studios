using System;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Dynasty.Core;
using Dynasty.Tests.Helpers;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Dynasty.Tests.DesignerErrors
{
    /// <summary>
    /// Story BCF-004 tests: the shared [DESIGNER-ERROR] channel (TR-TP-002, ADR-0006 Decision 5).
    /// Recording-sink tests never touch the Unity console; the Unity-backed sink is matched with
    /// LogAssert.Expect.
    /// </summary>
    public class DesignerErrorChannelTest
    {
        private const string DamageHealthTag = "DamageHealth";
        private const string ValidatorTag = "Validator";
        private const float MaxReduction = 0.75f;
        private const float OverMaxReduction = 0.90f;
        private static readonly Regex MarkerPattern = new Regex(@"^\[DESIGNER-ERROR\]");

        [SetUp]
        public void SetUp()
        {
            // The safety net AC-5 relies on: an unexpected designer error must fail the test.
            Assert.That(LogAssert.ignoreFailingMessages, Is.False);
        }

        /// <summary>Consumer stub: takes the channel by constructor injection, as real systems do.</summary>
        private sealed class GuardStub
        {
            private readonly IDesignerErrorLog _errors;

            public GuardStub(IDesignerErrorLog errors)
            {
                _errors = errors;
            }

            public float ClampReduction(float value, float max)
            {
                if (value > max)
                {
                    _errors.Report(DamageHealthTag, $"damage_reduction {value} clamped to {max}");
                    return max;
                }

                return value;
            }
        }

        // ---------------------------------------------------------------- AC-1

        [Test]
        public void test_designer_error_report_message_starts_with_exact_marker()
        {
            // Arrange
            var errors = new RecordingDesignerErrorLog();

            // Act
            errors.Report(DamageHealthTag, "raw_damage <= 0 rejected");

            // Assert
            Assert.That(errors.Messages[0].StartsWith("[DESIGNER-ERROR]", StringComparison.Ordinal), Is.True);
            Assert.That(errors.Messages[0], Is.EqualTo("[DESIGNER-ERROR] DamageHealth: raw_damage <= 0 rejected"));
            Assert.That(DesignerError.Marker, Is.EqualTo("[DESIGNER-ERROR]"));
        }

        [Test]
        public void test_designer_error_empty_and_null_arguments_are_still_prefixed_without_throwing()
        {
            // Arrange
            var errors = new RecordingDesignerErrorLog();

            // Act
            Assert.DoesNotThrow(() => errors.Report(DamageHealthTag, string.Empty));
            Assert.DoesNotThrow(() => errors.Report(DamageHealthTag, null));
            Assert.DoesNotThrow(() => errors.Report(null, "x"));
            Assert.DoesNotThrow(() => errors.Report(null, null));

            // Assert
            Assert.That(errors.Count, Is.EqualTo(4));
            Assert.That(errors.Messages.All(m => m.StartsWith(DesignerError.Marker, StringComparison.Ordinal)), Is.True);
            Assert.That(errors.Messages[0], Is.EqualTo("[DESIGNER-ERROR] DamageHealth: "));
            Assert.That(errors.Messages[1], Is.EqualTo("[DESIGNER-ERROR] DamageHealth: "));
            Assert.That(errors.Messages[2], Is.EqualTo("[DESIGNER-ERROR] : x"));
            Assert.That(errors.Messages[3], Is.EqualTo("[DESIGNER-ERROR] : "));
        }

        // ---------------------------------------------------------------- AC-2

        [Test]
        public void test_designer_error_recording_report_returns_control_to_caller()
        {
            // Arrange
            var errors = new RecordingDesignerErrorLog();
            bool continued = false;

            // Act
            errors.Report(DamageHealthTag, "raw_damage <= 0 rejected");
            continued = true;

            // Assert
            Assert.That(continued, Is.True);
        }

        [Test]
        public void test_designer_error_unity_report_returns_control_to_caller()
        {
            // Arrange
            IDesignerErrorLog errors = new UnityDesignerErrorLog();
            bool continued = false;
            LogAssert.Expect(LogType.Error, MarkerPattern);

            // Act
            errors.Report(DamageHealthTag, "raw_damage <= 0 rejected");
            continued = true;

            // Assert
            Assert.That(continued, Is.True);
        }

        [Test]
        public void test_designer_error_hundred_consecutive_reports_do_not_throw_and_all_are_counted()
        {
            // Arrange
            var errors = new RecordingDesignerErrorLog();

            // Act
            Assert.DoesNotThrow(() =>
            {
                for (int i = 0; i < 100; i++)
                {
                    errors.Report(ValidatorTag, "rule " + i);
                }
            });

            // Assert
            Assert.That(errors.Count, Is.EqualTo(100));
        }

        [Test]
        public void test_designer_error_guard_clamps_and_continues_after_reporting()
        {
            // Arrange
            var errors = new RecordingDesignerErrorLog();
            var guard = new GuardStub(errors);

            // Act
            float result = guard.ClampReduction(OverMaxReduction, MaxReduction);

            // Assert
            Assert.That(result, Is.EqualTo(MaxReduction));
            Assert.That(errors.Count, Is.EqualTo(1));
        }

        // ---------------------------------------------------------------- AC-3

        [Test]
        public void test_designer_error_recorded_message_contains_marker_and_system_tag()
        {
            // Arrange
            var errors = new RecordingDesignerErrorLog();
            errors.Reset();

            // Act
            errors.Report(DamageHealthTag, "damage_reduction 0.90 clamped to 0.75");

            // Assert
            Assert.That(errors.Count, Is.EqualTo(1));
            Assert.That(errors.Contains(DesignerError.Marker), Is.True);
            Assert.That(errors.Contains(DamageHealthTag), Is.True);
            Assert.That(errors.Contains(ValidatorTag), Is.False);
            Assert.That(errors.Contains(null), Is.False);
        }

        [Test]
        public void test_designer_error_reset_clears_count_and_messages()
        {
            // Arrange
            var errors = new RecordingDesignerErrorLog();
            errors.Report(DamageHealthTag, "a");
            errors.Report(ValidatorTag, "b");

            // Act
            errors.Reset();

            // Assert
            Assert.That(errors.Count, Is.EqualTo(0));
            Assert.That(errors.Messages, Is.Empty);
            Assert.That(errors.Contains(DesignerError.Marker), Is.False);
        }

        [Test]
        public void test_designer_error_messages_are_recorded_in_report_order()
        {
            // Arrange
            var errors = new RecordingDesignerErrorLog();

            // Act
            errors.Report(DamageHealthTag, "first");
            errors.Report(ValidatorTag, "second");

            // Assert
            Assert.That(errors.Messages[0], Does.Contain("first"));
            Assert.That(errors.Messages[1], Does.Contain("second"));
        }

        [Test]
        public void test_designer_error_contains_empty_fragment_returns_false()
        {
            // Arrange
            var errors = new RecordingDesignerErrorLog();
            errors.Report(DamageHealthTag, "raw_damage <= 0 rejected");

            // Act
            bool emptyMatch = errors.Contains(string.Empty);
            bool nullMatch = errors.Contains(null);

            // Assert
            Assert.That(emptyMatch, Is.False);
            Assert.That(nullMatch, Is.False);
        }

        [Test]
        public void test_designer_error_contains_from_system_matches_tag_not_body()
        {
            // Arrange
            var errors = new RecordingDesignerErrorLog();

            // Act: a Validator report whose body mentions DamageHealth
            errors.Report(ValidatorTag, "DamageHealth damage_reduction out of range");

            // Assert
            Assert.That(errors.ContainsFromSystem(ValidatorTag), Is.True);
            Assert.That(errors.ContainsFromSystem(DamageHealthTag), Is.False);
            Assert.That(errors.Contains(DamageHealthTag), Is.True, "substring Contains matches the body");
            Assert.That(errors.ContainsFromSystem(string.Empty), Is.False);
            Assert.That(errors.ContainsFromSystem(null), Is.False);
        }

        [Test]
        public void test_designer_error_messages_view_is_live_and_not_mutable()
        {
            // Arrange
            var errors = new RecordingDesignerErrorLog();
            var view = errors.Messages;

            // Act
            errors.Report(DamageHealthTag, "a");
            int afterReport = view.Count;
            errors.Reset();
            int afterReset = view.Count;

            // Assert
            Assert.That(afterReport, Is.EqualTo(1));
            Assert.That(afterReset, Is.EqualTo(0));
            Assert.That(view, Is.Not.InstanceOf<System.Collections.Generic.List<string>>());
        }

        // ---------------------------------------------------------------- AC-4

        [Test]
        public void test_designer_error_consumer_that_does_not_report_leaves_count_zero()
        {
            // Arrange
            var errors = new RecordingDesignerErrorLog();
            var guard = new GuardStub(errors);

            // Act (0.75 is the boundary: in range, no report)
            float result = guard.ClampReduction(MaxReduction, MaxReduction);

            // Assert
            Assert.That(result, Is.EqualTo(MaxReduction));
            Assert.That(errors.Count, Is.EqualTo(0));
        }

        // ---------------------------------------------------------------- AC-5

        [Test]
        public void test_designer_error_unity_sink_is_matchable_with_log_assert_marker_pattern()
        {
            // Arrange
            IDesignerErrorLog errors = new UnityDesignerErrorLog();
            LogAssert.Expect(LogType.Error, MarkerPattern);

            // Act
            errors.Report(ValidatorTag, "stagger_resist_multiplier out of range");

            // Assert: the expectation is satisfied (no unexpected-log failure)
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void test_designer_error_unity_sink_logs_exact_formatted_text()
        {
            // Arrange
            IDesignerErrorLog errors = new UnityDesignerErrorLog();
            LogAssert.Expect(LogType.Error, "[DESIGNER-ERROR] Validator: stagger_resist_multiplier out of range");

            // Act
            errors.Report(ValidatorTag, "stagger_resist_multiplier out of range");

            // Assert
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void test_designer_error_unity_sink_null_arguments_are_prefixed_without_throwing()
        {
            // Arrange
            IDesignerErrorLog errors = new UnityDesignerErrorLog();
            LogAssert.Expect(LogType.Error, "[DESIGNER-ERROR] : ");

            // Act / Assert
            Assert.DoesNotThrow(() => errors.Report(null, null));
        }

        // ---------------------------------------------------------------- AC-6

        [Test]
        public void test_designer_error_injected_sinks_do_not_share_state()
        {
            // Arrange
            var firstErrors = new RecordingDesignerErrorLog();
            var secondErrors = new RecordingDesignerErrorLog();
            var first = new GuardStub(firstErrors);
            var second = new GuardStub(secondErrors);

            // Act
            first.ClampReduction(OverMaxReduction, MaxReduction);
            second.ClampReduction(MaxReduction, MaxReduction);

            // Assert
            Assert.That(firstErrors.Count, Is.EqualTo(1));
            Assert.That(secondErrors.Count, Is.EqualTo(0));
        }

        [Test]
        public void test_designer_error_implementations_hold_no_static_fields_except_constants()
        {
            // Arrange
            const BindingFlags StaticFields = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            Type[] types = { typeof(UnityDesignerErrorLog), typeof(RecordingDesignerErrorLog), typeof(DesignerError) };

            // Act (static readonly counts too: a static readonly list would still be shared state)
            string[] nonConstStatics = types
                .SelectMany(t => t.GetFields(StaticFields))
                .Where(f => !f.IsLiteral)
                .Select(f => f.DeclaringType.Name + "." + f.Name)
                .ToArray();

            // Assert
            Assert.That(nonConstStatics, Is.Empty);
        }

        [Test]
        public void test_designer_error_interface_exposes_report_with_system_and_message()
        {
            // Arrange
            Type type = typeof(IDesignerErrorLog);

            // Act
            MethodInfo report = type.GetMethod("Report", new[] { typeof(string), typeof(string) });

            // Assert
            Assert.That(report, Is.Not.Null);
            Assert.That(report.ReturnType, Is.EqualTo(typeof(void)));
        }
    }
}

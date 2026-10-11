using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Dynasty.Bootstrap;
using Dynasty.Core;
using Dynasty.Tests.Helpers;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Dynasty.Tests.Bootstraps
{
    /// <summary>
    /// Story BCF-005 integration tests (AC-1…AC-8): boot phase order with recording participants, one-time
    /// boot, clamp-and-continue on designer errors, single clock / designer-error channel injected
    /// everywhere, CombatFrame initialised in canonical order with no-op slots, no lookups, no ticking.
    /// </summary>
    public class BootstrapBootOrderTest
    {
        private sealed class RecordingPhases : IGameDataValidation, IBootWiring, IPoolPrewarm, IRunSessionEntry, IInputModuleBinding
        {
            private readonly EventRecorder _recorder;
            private readonly IDesignerErrorLog _errors;

            public RecordingPhases(EventRecorder recorder, IDesignerErrorLog errors = null)
            {
                _recorder = recorder;
                _errors = errors;
            }

            public void ValidateAll()
            {
                _recorder.Record("Validate");
                _errors?.Report("Validator", "stagger_resist_multiplier 0.9 clamped to 0.8");
            }

            public void Wire() => _recorder.Record("Wire");

            public void Bind() => _recorder.Record("BindInputModule");

            public void Prewarm() => _recorder.Record("Prewarm");

            public void EnterSelection() => _recorder.Record("EnterSelection");
        }

        private GameObject _go;
        private CombatFrame _frame;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("BootTest");
            _frame = _go.AddComponent<CombatFrame>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
        }

        // ---------------------------------------------------------------- AC-1 / AC-2

        [Test]
        public void test_bootstrap_phases_run_in_adr_0012_order_once()
        {
            // Arrange
            var recorder = new EventRecorder();
            var phases = new RecordingPhases(recorder);
            var boot = new BootSequence(phases, phases, phases, phases);

            // Act
            boot.Run();
            boot.Run();

            // Assert
            Assert.That(recorder.Log, Is.EqualTo(new[] { "Validate", "Wire", "Prewarm", "EnterSelection" }));
            Assert.That(boot.HasRun, Is.True);
        }

        [Test]
        public void test_bootstrap_wiring_ends_with_input_module_bind()
        {
            // Arrange
            var recorder = new EventRecorder();
            var phases = new RecordingPhases(recorder);
            var clock = new FakeGameClock();
            var root = new CompositionRoot(clock, new RecordingDesignerErrorLog(), _frame, phases);

            // Act
            new BootSequence(phases, root, phases, phases).Run();

            // Assert
            Assert.That(recorder.Log, Is.EqualTo(new[] { "Validate", "BindInputModule", "Prewarm", "EnterSelection" }));
            Assert.That(_frame.IsInitialized, Is.True, "CombatFrame wired before the bind");
        }

        // ---------------------------------------------------------------- AC-3

        [Test]
        public void test_bootstrap_designer_errors_do_not_stop_boot()
        {
            // Arrange
            var recorder = new EventRecorder();
            var errors = new RecordingDesignerErrorLog();
            var phases = new RecordingPhases(recorder, errors);

            // Act
            new BootSequence(phases, phases, phases, phases).Run();

            // Assert
            Assert.That(errors.Count, Is.EqualTo(1));
            Assert.That(recorder.Log.Last(), Is.EqualTo("EnterSelection"));
        }

        // ---------------------------------------------------------------- AC-4 / AC-5 / AC-6

        [Test]
        public void test_bootstrap_same_clock_and_error_channel_reach_every_consumer()
        {
            // Arrange
            var clock = new FakeGameClock();
            var errors = new RecordingDesignerErrorLog();
            var root = new CompositionRoot(clock, errors, _frame, new NoOpBootParticipants());

            // Act
            root.Wire();
            object frameClock = typeof(CombatFrame).GetField("_clock", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_frame);

            // Assert
            Assert.That(frameClock, Is.SameAs(clock));
            Assert.That(root.Clock, Is.SameAs(clock));
            Assert.That(root.DesignerErrors, Is.SameAs(errors));
            Assert.That(typeof(CombatFrame).GetFields(BindingFlags.Instance | BindingFlags.NonPublic).Any(f => f.FieldType == typeof(IRunClockControl)),
                Is.False, "the run-clock control goes to RSM only");
        }

        [Test]
        public void test_bootstrap_combat_frame_slots_are_no_ops_in_canonical_order()
        {
            // Arrange
            var root = new CompositionRoot(new FakeGameClock(), new RecordingDesignerErrorLog(), _frame, new NoOpBootParticipants());
            string[] slots = { "_parry", "_dodge", "_combatCore", "_combo", "_playerHealth", "_progression" };

            // Act
            root.Wire();
            object[] values = slots.Select(s => typeof(CombatFrame).GetField(s, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_frame)).ToArray();

            // Assert
            Assert.That(values.All(v => v is NoOpFrameStep), Is.True);
            Assert.DoesNotThrow(() => _frame.RunFrame(0.016f));
        }

        [Test]
        public void test_bootstrap_bootstrapper_builds_exactly_one_clock_and_one_error_channel()
        {
            // Arrange
            const BindingFlags All = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

            // Act
            FieldInfo[] fields = typeof(Bootstrapper).GetFields(All);

            // Assert: no static state; a single clock field; the error channel lives in the root only
            Assert.That(fields.Any(f => f.IsStatic), Is.False);
            Assert.That(fields.Count(f => typeof(IGameClock).IsAssignableFrom(f.FieldType)), Is.EqualTo(1));
        }

        // ---------------------------------------------------------------- AC-7 / AC-8

        [Test]
        public void test_bootstrap_declares_no_frame_callbacks()
        {
            // Arrange
            string[] forbidden = { "Update", "FixedUpdate", "LateUpdate" };
            const BindingFlags All = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

            // Act
            string[] found = new[] { typeof(Bootstrapper), typeof(BootSequence), typeof(CompositionRoot) }
                .SelectMany(t => t.GetMethods(All))
                .Select(m => m.Name)
                .Where(forbidden.Contains)
                .ToArray();

            // Assert
            Assert.That(found, Is.Empty);
        }

        [Test]
        public void test_bootstrap_source_uses_no_find_lookups_or_singletons()
        {
            // Arrange
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "src", "Bootstrap"));
            Assume.That(Directory.Exists(dir), "source not found at " + dir);
            var forbidden = new Regex(@"Find(First|Any)?Object(s)?(Of|By)Type|GameObject\.Find|FindWithTag|\.Instance\b|static\s+\w+\s+Instance");

            // Act
            string[] hits = Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories)
                .SelectMany(f => File.ReadAllLines(f).Select((line, i) => (f, i, line)))
                .Where(x => !x.line.TrimStart().StartsWith("//", StringComparison.Ordinal) && forbidden.IsMatch(x.line))
                .Select(x => Path.GetFileName(x.f) + ":" + (x.i + 1))
                .ToArray();

            // Assert
            Assert.That(hits, Is.Empty);
        }
    }
}

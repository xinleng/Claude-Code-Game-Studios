using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Dynasty.Core;
using Dynasty.Tests.Helpers;
using NUnit.Framework;
using UnityEngine;

namespace Dynasty.Tests.CombatFrames
{
    /// <summary>
    /// Story BCF-003 tests (AC-1…AC-7): CombatFrame canonical order, dt passthrough, Update() == RunFrame,
    /// fixed step list, execution-order anchors, injection-only fields, pre-init safety.
    /// </summary>
    public class CombatFrameOrderTest
    {
        private static readonly string[] CanonicalOrder =
        {
            "Parry.Evaluate", "Dodge.Tick", "CombatCore.Tick", "Combo.Tick", "PlayerHealth.Apply", "Progression.Tick",
        };

        private GameObject _go;
        private CombatFrame _frame;
        private FakeGameClock _clock;
        private EventRecorder _recorder;
        private RecordingStep _parry;
        private RecordingStep _dodge;
        private RecordingStep _combatCore;
        private RecordingStep _combo;
        private RecordingStep _playerHealth;
        private RecordingStep _progression;

        /// <summary>Fake frame step: records "&lt;Name&gt;.&lt;Method&gt;" and every dt it receives.</summary>
        private sealed class RecordingStep : IParryFrameStep, ITickable, IPlayerHealthFrameStep, IProgressionFrameStep
        {
            private readonly string _name;
            private readonly EventRecorder _recorder;

            public RecordingStep(string name, EventRecorder recorder)
            {
                _name = name;
                _recorder = recorder;
            }

            public List<float> ReceivedDt { get; } = new List<float>();

            public void Evaluate() => _recorder.Record(_name + ".Evaluate");

            public void Tick(float dt)
            {
                ReceivedDt.Add(dt);
                _recorder.Record(_name + ".Tick");
            }

            public void Apply(float dt)
            {
                ReceivedDt.Add(dt);
                _recorder.Record(_name + ".Apply");
            }

            void IProgressionFrameStep.Tick() => _recorder.Record(_name + ".Tick");
        }

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("CombatFrameTest");
            _frame = _go.AddComponent<CombatFrame>();
            _clock = new FakeGameClock();
            _recorder = new EventRecorder();
            _parry = new RecordingStep("Parry", _recorder);
            _dodge = new RecordingStep("Dodge", _recorder);
            _combatCore = new RecordingStep("CombatCore", _recorder);
            _combo = new RecordingStep("Combo", _recorder);
            _playerHealth = new RecordingStep("PlayerHealth", _recorder);
            _progression = new RecordingStep("Progression", _recorder);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_go);
        }

        private void InitializeFrame()
        {
            _frame.Initialize(_clock, _parry, _dodge, _combatCore, _combo, _playerHealth, _progression);
        }

        private void InvokeUpdate()
        {
            MethodInfo update = typeof(CombatFrame).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(update, Is.Not.Null, "CombatFrame must declare a private Update()");
            update.Invoke(_frame, null);
        }

        private IEnumerable<RecordingStep> DtSteps()
        {
            yield return _dodge;
            yield return _combatCore;
            yield return _combo;
            yield return _playerHealth;
        }

        // ---------------------------------------------------------------- AC-1

        [Test]
        public void test_combat_frame_run_frame_calls_six_steps_in_canonical_order_once_each()
        {
            // Arrange
            InitializeFrame();

            // Act
            _frame.RunFrame(0.016f);

            // Assert
            Assert.That(_recorder.Log, Is.EqualTo(CanonicalOrder));
        }

        [Test]
        public void test_combat_frame_three_frames_repeat_sequence_without_interleaving()
        {
            // Arrange
            InitializeFrame();

            // Act
            _frame.RunFrame(0.016f);
            _frame.RunFrame(0.016f);
            _frame.RunFrame(0.016f);

            // Assert
            string[] expected = CanonicalOrder.Concat(CanonicalOrder).Concat(CanonicalOrder).ToArray();
            Assert.That(_recorder.Log, Is.EqualTo(expected));
        }

        // ---------------------------------------------------------------- AC-2

        [Test]
        public void test_combat_frame_dt_reaches_every_dt_step_unchanged_including_zero()
        {
            // Arrange
            InitializeFrame();

            // Act
            _frame.RunFrame(0.25f);
            _frame.RunFrame(0f);

            // Assert
            foreach (RecordingStep step in DtSteps())
            {
                Assert.That(step.ReceivedDt, Is.EqualTo(new[] { 0.25f, 0f }));
            }

            Assert.That(_recorder.Log.Count, Is.EqualTo(12), "the zero-dt frame still runs all six steps");
            Assert.That(_recorder.Log.Skip(6), Is.EqualTo(CanonicalOrder));
        }

        // ---------------------------------------------------------------- AC-3

        [Test]
        public void test_combat_frame_update_runs_same_path_as_run_frame_with_clock_delta()
        {
            // Arrange
            InitializeFrame();
            _clock.Advance(0.02f);

            // Act
            InvokeUpdate();

            // Assert
            Assert.That(_recorder.Log, Is.EqualTo(CanonicalOrder));
            foreach (RecordingStep step in DtSteps())
            {
                Assert.That(step.ReceivedDt, Is.EqualTo(new[] { 0.02f }));
            }
        }

        [Test]
        public void test_combat_frame_update_with_paused_clock_passes_zero_dt()
        {
            // Arrange
            InitializeFrame();
            _clock.TimeScale = 0f;
            _clock.Advance(0.02f);

            // Act
            InvokeUpdate();

            // Assert
            Assert.That(_recorder.Log, Is.EqualTo(CanonicalOrder));
            foreach (RecordingStep step in DtSteps())
            {
                Assert.That(step.ReceivedDt, Is.EqualTo(new[] { 0f }));
            }
        }

        // ---------------------------------------------------------------- AC-4

        [Test]
        public void test_combat_frame_exposes_no_api_to_change_steps_beyond_initialize()
        {
            // Arrange
            const BindingFlags Declared = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.DeclaredOnly;

            // Act
            string[] publicMethods = typeof(CombatFrame).GetMethods(Declared)
                .Where(m => !m.IsSpecialName)
                .Select(m => m.Name)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToArray();
            PropertyInfo[] writable = typeof(CombatFrame).GetProperties(Declared).Where(p => p.CanWrite).ToArray();
            FieldInfo[] publicFields = typeof(CombatFrame).GetFields(Declared);

            // Assert
            Assert.That(publicMethods, Is.EqualTo(new[] { "Initialize", "RunFrame" }));
            Assert.That(writable, Is.Empty);
            Assert.That(publicFields, Is.Empty);
        }

        [Test]
        public void test_combat_frame_second_initialize_is_rejected_and_order_unchanged()
        {
            // Arrange
            InitializeFrame();
            var other = new EventRecorder();
            var stray = new RecordingStep("Stray", other);

            // Act
            Assert.Throws<InvalidOperationException>(() =>
                _frame.Initialize(_clock, stray, stray, stray, stray, stray, stray));
            _frame.RunFrame(0.016f);

            // Assert
            Assert.That(_recorder.Log, Is.EqualTo(CanonicalOrder));
            Assert.That(other.Log, Is.Empty);
        }

        [Test]
        public void test_combat_frame_initialize_with_null_step_throws_and_stays_uninitialized()
        {
            // Act / Assert
            Assert.Throws<ArgumentNullException>(() =>
                _frame.Initialize(_clock, _parry, _dodge, null, _combo, _playerHealth, _progression));
            Assert.That(_frame.IsInitialized, Is.False);
        }

        // ---------------------------------------------------------------- AC-5

        [Test]
        public void test_combat_frame_execution_order_anchors_are_ordered_and_applied()
        {
            // Arrange
            var attribute = typeof(CombatFrame).GetCustomAttribute<DefaultExecutionOrder>();

            // Assert (boundary values are the point here)
            Assert.That(ExecutionOrder.Input, Is.EqualTo(-200));
            Assert.That(ExecutionOrder.EnemyManager, Is.EqualTo(-100));
            Assert.That(ExecutionOrder.CombatFrame, Is.EqualTo(0));
            Assert.That(ExecutionOrder.Presentation, Is.EqualTo(100));
            Assert.That(ExecutionOrder.Input, Is.LessThan(ExecutionOrder.EnemyManager));
            Assert.That(ExecutionOrder.EnemyManager, Is.LessThan(ExecutionOrder.CombatFrame));
            Assert.That(ExecutionOrder.CombatFrame, Is.LessThan(ExecutionOrder.Presentation));
            Assert.That(attribute, Is.Not.Null);
            Assert.That(attribute.order, Is.EqualTo(ExecutionOrder.CombatFrame));
        }

        // ---------------------------------------------------------------- AC-6

        [Test]
        public void test_combat_frame_fields_are_only_clock_six_steps_and_init_flag()
        {
            // Arrange
            const BindingFlags All = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            var allowed = new HashSet<Type>
            {
                typeof(IGameClock), typeof(IParryFrameStep), typeof(ITickable),
                typeof(IPlayerHealthFrameStep), typeof(IProgressionFrameStep), typeof(bool),
            };

            // Act
            FieldInfo[] fields = typeof(CombatFrame).GetFields(All);

            // Assert
            Assert.That(fields.Length, Is.EqualTo(8));
            Assert.That(fields.All(f => !f.IsStatic), Is.True, "no static state");
            Assert.That(fields.All(f => allowed.Contains(f.FieldType)), Is.True);
        }

        // ---------------------------------------------------------------- AC-7

        [Test]
        public void test_combat_frame_run_frame_and_update_before_initialize_do_nothing()
        {
            // Act
            Assert.DoesNotThrow(() => _frame.RunFrame(0.016f));
            Assert.DoesNotThrow(InvokeUpdate);

            // Assert
            Assert.That(_recorder.Log, Is.Empty);
            Assert.That(_frame.IsInitialized, Is.False);
        }
    }
}

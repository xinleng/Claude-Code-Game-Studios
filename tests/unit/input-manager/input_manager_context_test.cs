using System;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Dynasty.Core;
using Dynasty.Input;
using Dynasty.Tests.Helpers;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Dynasty.Tests.InputManagers
{
    /// <summary>
    /// Story IM-001 tests: PlayerControls maps/actions/schemes/bindings, single instance, InputSystem-free
    /// public surface, SetContext enables exactly one map, updateMode assertion, wrapper/asset sync.
    /// Uses only InputManagerTestSeam (no Unity.InputSystem reference in the test assembly).
    /// </summary>
    public class InputManagerContextTest
    {
        private const string GameplayMap = "Gameplay";
        private const string UiMap = "UI";

        private GameObject _go;
        private InputManager _input;
        private bool _restoreDynamic;

        [SetUp]
        public void SetUp()
        {
            _restoreDynamic = InputManagerTestSeam.IsDynamicUpdateMode();
            _go = new GameObject("InputManagerTest");
            _input = _go.AddComponent<InputManager>();
        }

        [TearDown]
        public void TearDown()
        {
            _input.Dispose();
            Object.DestroyImmediate(_go);
            InputManagerTestSeam.SetDynamicUpdateMode(_restoreDynamic);
        }

        private void Init()
        {
            _input.Initialize(new FakeGameClock(), TestInputTuning.Default());
        }

        private (bool Gameplay, bool Ui) Maps()
        {
            return (InputManagerTestSeam.IsMapEnabled(_input, GameplayMap), InputManagerTestSeam.IsMapEnabled(_input, UiMap));
        }

        // ---------------------------------------------------------------- AC-1

        [Test]
        public void test_input_manager_gameplay_map_has_exactly_move_parry_dodge()
        {
            // Arrange
            Init();

            // Act
            string[] actions = InputManagerTestSeam.DescribeActions(_input, GameplayMap);

            // Assert
            Assert.That(actions, Is.EqualTo(new[] { "Move|Value|Vector2", "Parry|Button|", "Dodge|Button|" }));
        }

        [Test]
        public void test_input_manager_ui_map_is_adr_0004_superset_without_separate_confirm()
        {
            // Arrange
            Init();

            // Act
            string[] names = InputManagerTestSeam.DescribeActions(_input, UiMap).Select(a => a.Split('|')[0]).ToArray();

            // Assert
            Assert.That(names, Is.SupersetOf(new[] { "Navigate", "Submit", "Cancel", "Point", "Click", "ScrollWheel" }));
            Assert.That(names, Has.No.Member("Confirm"), "Confirm is Submit's bindings (ADR-0004 rule 8)");
        }

        // ---------------------------------------------------------------- AC-2

        [Test]
        public void test_input_manager_control_schemes_are_keyboard_mouse_and_gamepad()
        {
            // Arrange
            Init();

            // Act
            string[] schemes = InputManagerTestSeam.ControlSchemes(_input);

            // Assert
            Assert.That(schemes, Is.EquivalentTo(new[] { "Keyboard&Mouse", "Gamepad" }));
        }

        [Test]
        public void test_input_manager_provisional_bindings_match_gdd_rule_3()
        {
            // Arrange
            Init();

            // Act
            string[] parry = InputManagerTestSeam.DescribeBindings(_input, GameplayMap, "Parry");
            string[] move = InputManagerTestSeam.DescribeBindings(_input, GameplayMap, "Move");
            string[] dodge = InputManagerTestSeam.DescribeBindings(_input, GameplayMap, "Dodge");
            string[] submit = InputManagerTestSeam.DescribeBindings(_input, UiMap, "Submit");
            string[] cancel = InputManagerTestSeam.DescribeBindings(_input, UiMap, "Cancel");

            // Assert
            Assert.That(parry, Has.Member("<Keyboard>/space|Keyboard&Mouse"));
            Assert.That(parry, Has.Member("<Gamepad>/rightTrigger|Gamepad"));
            Assert.That(move, Is.SupersetOf(new[]
            {
                "<Keyboard>/w|Keyboard&Mouse", "<Keyboard>/a|Keyboard&Mouse", "<Keyboard>/s|Keyboard&Mouse", "<Keyboard>/d|Keyboard&Mouse",
                "<Gamepad>/leftStick|Gamepad",
            }));
            Assert.That(dodge, Has.Member("<Keyboard>/leftShift|Keyboard&Mouse"));
            Assert.That(dodge.Any(b => b.EndsWith("|Gamepad")), Is.True, "provisional gamepad face button");
            Assert.That(submit, Is.SupersetOf(new[] { "<Keyboard>/enter|Keyboard&Mouse", "<Keyboard>/space|Keyboard&Mouse", "<Gamepad>/buttonSouth|Gamepad" }));
            Assert.That(cancel, Is.SupersetOf(new[] { "<Keyboard>/escape|Keyboard&Mouse", "<Gamepad>/buttonEast|Gamepad" }));
        }

        [Test]
        public void test_input_manager_generated_wrapper_matches_authored_asset()
        {
            // Act
            string diff = InputManagerTestSeam.WrapperVsAssetDiff();

            // Assert (run Tools ▸ Dynasty ▸ Regenerate PlayerControls Wrapper if this fails)
            Assert.That(diff, Is.Empty);
        }

        // ---------------------------------------------------------------- AC-3

        [Test]
        public void test_input_manager_initialize_creates_exactly_one_controls_instance()
        {
            // Arrange
            int before = InputManagerTestSeam.CountPlayerControlsAssets();

            // Act
            Init();
            object identity = InputManagerTestSeam.ControlsIdentity(_input);
            _input.SetContext(InputContext.Gameplay);
            _input.SetContext(InputContext.Menu);
            _input.SetContext(InputContext.Paused);
            int after = InputManagerTestSeam.CountPlayerControlsAssets();

            // Assert
            Assert.That(after - before, Is.EqualTo(1));
            Assert.That(InputManagerTestSeam.ControlsIdentity(_input), Is.SameAs(identity));
        }

        [Test]
        public void test_input_manager_second_initialize_is_rejected_without_new_instance()
        {
            // Arrange
            Init();
            object identity = InputManagerTestSeam.ControlsIdentity(_input);
            int before = InputManagerTestSeam.CountPlayerControlsAssets();

            // Act
            Assert.Throws<InvalidOperationException>(Init);

            // Assert
            Assert.That(InputManagerTestSeam.CountPlayerControlsAssets(), Is.EqualTo(before));
            Assert.That(InputManagerTestSeam.ControlsIdentity(_input), Is.SameAs(identity));
        }

        // ---------------------------------------------------------------- AC-4

        [Test]
        public void test_input_manager_public_authority_surface_has_no_input_system_types()
        {
            // Arrange
            Type[] surface = { typeof(IInputAuthority), typeof(SteerSample), typeof(InputContext), typeof(GameAction) };

            // Act
            string[] leaks = surface
                .SelectMany(t => t.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                .SelectMany(TypesOf)
                .Where(t => t.Namespace != null && t.Namespace.StartsWith("UnityEngine.InputSystem", StringComparison.Ordinal))
                .Select(t => t.FullName)
                .ToArray();

            // Assert
            Assert.That(leaks, Is.Empty);
            Assert.That(typeof(IInputAuthority).Assembly.GetName().Name, Is.EqualTo("Dynasty.Core"), "consumers need no Dynasty.Input/InputSystem reference");
        }

        private static Type[] TypesOf(MemberInfo member)
        {
            switch (member)
            {
                case MethodInfo m:
                    return new[] { m.ReturnType }.Concat(m.GetParameters().Select(p => p.ParameterType.IsByRef ? p.ParameterType.GetElementType() : p.ParameterType)).ToArray();
                case PropertyInfo p:
                    return new[] { p.PropertyType };
                case FieldInfo f:
                    return new[] { f.FieldType };
                default:
                    return Type.EmptyTypes;
            }
        }

        // ---------------------------------------------------------------- AC-5

        [Test]
        public void test_input_manager_set_context_enables_exactly_one_map_each_step()
        {
            // Arrange
            Init();

            // Act / Assert
            _input.SetContext(InputContext.Gameplay);
            Assert.That(Maps(), Is.EqualTo((true, false)));
            _input.SetContext(InputContext.Menu);
            Assert.That(Maps(), Is.EqualTo((false, true)));
            _input.SetContext(InputContext.Paused);
            Assert.That(Maps(), Is.EqualTo((false, true)));
            _input.SetContext(InputContext.Gameplay);
            Assert.That(Maps(), Is.EqualTo((true, false)));
        }

        [Test]
        public void test_input_manager_set_context_is_idempotent()
        {
            // Arrange
            Init();

            // Act
            _input.SetContext(InputContext.Gameplay);
            _input.SetContext(InputContext.Gameplay);

            // Assert
            Assert.That(Maps(), Is.EqualTo((true, false)));
            Assert.That(_input.Context, Is.EqualTo(InputContext.Gameplay));
        }

        [Test]
        public void test_input_manager_no_map_enabled_before_first_set_context()
        {
            // Act
            Init();

            // Assert (boot context is RSM/Bootstrap's call; capture stays inert)
            Assert.That(Maps(), Is.EqualTo((false, false)));
        }

        [Test]
        public void test_input_manager_set_context_before_initialize_throws()
        {
            // Act / Assert
            Assert.Throws<InvalidOperationException>(() => _input.SetContext(InputContext.Gameplay));
        }

        // ---------------------------------------------------------------- AC-6

        [Test]
        public void test_input_manager_non_dynamic_update_mode_logs_error_naming_update_mode()
        {
            // Arrange
            InputManagerTestSeam.SetDynamicUpdateMode(false);
            LogAssert.Expect(LogType.Error, new Regex("updateMode"));

            // Act
            Init();

            // Assert: the expectation above is the assertion
            Assert.That(_input.IsInitialized, Is.True);
        }

        [Test]
        public void test_input_manager_dynamic_update_mode_logs_nothing()
        {
            // Arrange
            InputManagerTestSeam.SetDynamicUpdateMode(true);

            // Act
            Init();

            // Assert
            LogAssert.NoUnexpectedReceived();
        }
    }
}

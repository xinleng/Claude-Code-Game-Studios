using System;
using Dynasty.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Dynasty.Input
{
    /// <summary>
    /// The single input authority (ADR-0004; TR-IM-001, TR-IM-002). Sole owner of the one
    /// <see cref="PlayerControls"/> instance, constructed in <see cref="Initialize"/> (injection time,
    /// never <c>Awake</c>). <see cref="SetContext"/> enables exactly one action map; RunSessionManager
    /// decides when. No Input System type leaves this assembly: consumers see <see cref="IInputAuthority"/>.
    /// <para>
    /// Initial map state: no map is enabled until the first <see cref="SetContext"/> call — the GDD and
    /// ADR leave the boot context to RSM/Bootstrap (input-manager Story 001 open item), and capture must
    /// stay inert before run start (AC-IM-10).
    /// </para>
    /// <para>
    /// Built incrementally: parry buffer + consume (Stories 002–003), capture + run-clock stamps (004),
    /// freeze (005), move/steer (006), <c>[-200]</c> anchor + <c>Tick()</c> (007), UI module binder (008),
    /// control card (011). Until then those members return neutral values. Main thread only.
    /// </para>
    /// </summary>
    /// <example>
    /// <code>
    /// var input = go.AddComponent&lt;InputManager&gt;();
    /// input.Initialize(clock);                    // Bootstrap
    /// input.SetContext(InputContext.Menu);        // RSM: selection screen
    /// </code>
    /// </example>
    [DisallowMultipleComponent]
    public sealed class InputManager : MonoBehaviour, IInputAuthority, IDisposable
    {
        private IGameClock _clock;
        private PlayerControls _controls;
        private bool _hasContext;
        private InputContext _context;

        /// <summary>True once <see cref="Initialize"/> has constructed the controls.</summary>
        public bool IsInitialized => _controls != null;

        /// <summary>The context set by the last <see cref="SetContext"/> call (Menu until the first call).</summary>
        public InputContext Context => _hasContext ? _context : InputContext.Menu;

        /// <inheritdoc />
        public Vector2 CurrentMoveDirection => Vector2.zero;

        /// <inheritdoc />
        public SteerSample LastSteerDirection => default;

        /// <summary>The one PlayerControls instance; reachable only inside <c>Dynasty.Input</c> (UiInputModuleBinder, Story 008).</summary>
        internal PlayerControls Controls => _controls;

        /// <summary>
        /// Injection-time initialisation: asserts the Input System update mode, then constructs the single
        /// <see cref="PlayerControls"/> instance with every map disabled. May be called exactly once.
        /// </summary>
        /// <param name="clock">Run clock (stamps parry presses from Story 004).</param>
        /// <exception cref="ArgumentNullException"><paramref name="clock"/> is null.</exception>
        /// <exception cref="InvalidOperationException">Already initialised (a second instance is never created).</exception>
        public void Initialize(IGameClock clock)
        {
            if (_controls != null)
            {
                throw new InvalidOperationException("InputManager.Initialize may only be called once; PlayerControls is a single instance (ADR-0004 rule 7).");
            }

            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            AssertUpdateMode();
            _controls = new PlayerControls();
        }

        /// <inheritdoc />
        /// <exception cref="InvalidOperationException">Called before <see cref="Initialize"/>.</exception>
        public void SetContext(InputContext context)
        {
            if (_controls == null)
            {
                throw new InvalidOperationException("InputManager.SetContext called before Initialize.");
            }

            // Disable before enable so the two maps are never enabled together (Space is bound in both).
            if (context == InputContext.Gameplay)
            {
                _controls.UI.Disable();
                _controls.Gameplay.Enable();
            }
            else
            {
                _controls.Gameplay.Disable();
                _controls.UI.Enable();
            }

            _context = context;
            _hasContext = true;
        }

        /// <inheritdoc />
        public bool TryConsumeParryPress(float notBefore, out float pressTime)
        {
            pressTime = 0f;
            return false;
        }

        /// <inheritdoc />
        public bool HasLiveParryPress()
        {
            return false;
        }

        /// <inheritdoc />
        public void SetRunStartTime(float runStartTime)
        {
        }

        /// <inheritdoc />
        public string GetActiveBindingDisplay(GameAction action)
        {
            return string.Empty;
        }

        /// <summary>Disables every map and disposes the controls (scene teardown).</summary>
        public void Dispose()
        {
            if (_controls == null)
            {
                return;
            }

            _controls.Disable();
            if (Application.isPlaying)
            {
                _controls.Dispose();
            }
            else
            {
                // The generated Dispose() calls Object.Destroy, which is illegal in edit mode (EditMode tests, editor tooling).
                DestroyImmediate(_controls.asset);
            }

            _controls = null;
            _hasContext = false;
        }

        private void OnDestroy()
        {
            Dispose();
        }

        private static void AssertUpdateMode()
        {
            InputSettings.UpdateMode mode = InputSystem.settings.updateMode;
            if (mode != InputSettings.UpdateMode.ProcessEventsInDynamicUpdate)
            {
                Debug.LogError("[InputManager] InputSystem.settings.updateMode is " + mode
                    + "; ADR-0004 requires ProcessEventsInDynamicUpdate (Dynamic Update).");
            }
        }
    }
}

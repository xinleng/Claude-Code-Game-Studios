using System;
using Dynasty.Core;
using Dynasty.GameData;
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
    /// Parry presses live in the engine-free <see cref="ParryPressBuffer"/> (Stories 002–003); peeks and
    /// consumes use the injected clock's <c>RunTime</c>. Capture (Story 004): <c>Parry.performed</c> only
    /// (one press per physical press, no autofire) → <c>ctx.time</c> converted to RunTime through the
    /// read-only <see cref="IRunTimeConverter"/> → recorded, but only after <see cref="SetRunStartTime"/>
    /// and while the context is Gameplay. Freeze (Story 005): outside Gameplay nothing is added and
    /// <see cref="Tick"/> does not prune. Still to come: move/steer (006), the <c>[-200]</c> anchor calling
    /// <see cref="Tick"/> (007), UI module binder (008), control card (011) — until then those
    /// members return neutral values. Main thread only.
    /// </para>
    /// </summary>
    /// <example>
    /// <code>
    /// var input = go.AddComponent&lt;InputManager&gt;();
    /// input.Initialize(clock, clock, tuning);     // Bootstrap: UnityGameClock is both IGameClock and IRunTimeConverter
    /// input.SetContext(InputContext.Menu);        // RSM: selection screen
    /// </code>
    /// </example>
    [DisallowMultipleComponent]
    public sealed class InputManager : MonoBehaviour, IInputAuthority, IDisposable
    {
        private IGameClock _clock;
        private IRunTimeConverter _runTimeConverter;
        private ParryPressBuffer _parryBuffer;
        private Action<InputAction.CallbackContext> _onParryPerformed;
        private bool _runStarted;
        private float _runStartTime;
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
        /// <param name="clock">Run clock: parry liveness and stamps use <see cref="IGameClock.RunTime"/>.</param>
        /// <param name="runTimeConverter">Converts <c>ctx.time</c> press stamps to RunTime (read-only seam; never the RSM control).</param>
        /// <param name="tuning">Input section of TuningConfig (parry window and buffer capacity).</param>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        /// <exception cref="InvalidOperationException">Already initialised (a second instance is never created).</exception>
        public void Initialize(IGameClock clock, IRunTimeConverter runTimeConverter, IInputTuning tuning)
        {
            if (_controls != null)
            {
                throw new InvalidOperationException("InputManager.Initialize may only be called once; PlayerControls is a single instance (ADR-0004 rule 7).");
            }

            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _runTimeConverter = runTimeConverter ?? throw new ArgumentNullException(nameof(runTimeConverter));
            _parryBuffer = new ParryPressBuffer(tuning ?? throw new ArgumentNullException(nameof(tuning)));
            AssertUpdateMode();
            _controls = new PlayerControls();
            _onParryPerformed = HandleParryPerformed;   // cached method-group delegate (ADR-0003: no per-press closures)
            _controls.Gameplay.Parry.performed += _onParryPerformed;
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
            if (_parryBuffer == null)
            {
                pressTime = 0f;
                return false;
            }

            return _parryBuffer.TryConsume(_clock.RunTime, notBefore, out pressTime);
        }

        /// <inheritdoc />
        public bool HasLiveParryPress()
        {
            return _parryBuffer != null && _parryBuffer.HasLive(_clock.RunTime);
        }

        /// <summary>The engine-free parry buffer (InputManager-internal; tests reach it via the seam).</summary>
        internal ParryPressBuffer ParryBuffer => _parryBuffer;

        /// <inheritdoc />
        /// <remarks>
        /// Arms parry capture (AC-IM-10). Stamps are converted through the injected
        /// <see cref="IRunTimeConverter"/>, whose origin RSM marked just before this call (ADR-0012 run-start
        /// order: origin → <c>SetRunStartTime</c> → <c>ResetForNewRun</c> → <c>SetContext(Gameplay)</c>); the value
        /// is kept for diagnostics. Whether a retry also clears the buffer (<c>IRunResettable</c>) is an open item.
        /// </remarks>
        public void SetRunStartTime(float runStartTime)
        {
            _runStartTime = runStartTime;
            _runStarted = true;
        }

        /// <summary>The value passed to the last <see cref="SetRunStartTime"/> call (diagnostics).</summary>
        public float RunStartTime => _runStartTime;

        /// <summary>True once <see cref="SetRunStartTime"/> has armed capture.</summary>
        public bool IsCaptureArmed => _runStarted;

        /// <summary>
        /// Device-free capture path (tests call it directly; the <c>performed</c> callback forwards
        /// <c>ctx.time</c> here). Records one press stamped on RunTime when capture is armed and the context
        /// is Gameplay; otherwise does nothing. Never reads scaled time; never consults i-frames.
        /// </summary>
        /// <param name="eventTime">Input event time on the <c>realtimeSinceStartup</c> basis.</param>
        internal void OnParryPerformed(double eventTime)
        {
            if (!_runStarted || !_hasContext || _context != InputContext.Gameplay)
            {
                return;
            }

            float pressTime = _runTimeConverter.ToRunTime(eventTime);
            if (float.IsNaN(pressTime) || float.IsInfinity(pressTime))
            {
                return;
            }

            _parryBuffer.Record(pressTime);
        }

        /// <inheritdoc />
        public string GetActiveBindingDisplay(GameAction action)
        {
            return string.Empty;
        }

        /// <summary>
        /// Per-frame input step (ADR-0004 rule 2). Prunes expired parry presses in the RunTime domain —
        /// only while the context is Gameplay: in Menu/Paused the buffer is frozen (no prune, no add; ADR-0004
        /// rule 4, GDD States table), so the first Gameplay tick after a menu prunes anything that aged out.
        /// Freezing never clears the buffer. Story 007 places this call in the <c>[-200]</c> slot; until then
        /// the caller pumps it.
        /// </summary>
        public void Tick()
        {
            if (_parryBuffer != null && _hasContext && _context == InputContext.Gameplay)
            {
                _parryBuffer.Prune(_clock.RunTime);
            }
        }

        /// <summary>Disables every map and disposes the controls (scene teardown).</summary>
        public void Dispose()
        {
            if (_controls == null)
            {
                return;
            }

            _controls.Gameplay.Parry.performed -= _onParryPerformed;
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

        private void HandleParryPerformed(InputAction.CallbackContext context)
        {
            OnParryPerformed(context.time);
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

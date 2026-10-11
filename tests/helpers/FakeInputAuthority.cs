using System.Collections.Generic;
using Dynasty.Core;
using Dynasty.GameData;
using Dynasty.Input;
using UnityEngine;

namespace Dynasty.Tests.Helpers
{
    /// <summary>
    /// <see cref="IInputAuthority"/> test double for consumer tests (ADR-0010 rule 3). Parry presses go
    /// through the same engine-free <see cref="ParryPressBuffer"/> as the real InputManager, so the
    /// consume-once / <c>notBefore</c> / most-recent-wins contract is identical (enforced by the shared
    /// contract test). Time is the settable <see cref="Now"/> (RunTime domain). Other members are
    /// settable stubs. References no Input System type.
    /// </summary>
    /// <example>
    /// <code>
    /// var input = new FakeInputAuthority { Now = 1.05f };
    /// input.InjectParryPress(1.00f);
    /// parry.Evaluate();   // calls input.TryConsumeParryPress(notBefore, out t)
    /// </code>
    /// </example>
    public sealed class FakeInputAuthority : IInputAuthority
    {
        private readonly ParryPressBuffer _buffer;
        private readonly Dictionary<GameAction, string> _bindings = new Dictionary<GameAction, string>();

        /// <summary>Creates a fake with the given input tuning (GDD defaults when null).</summary>
        public FakeInputAuthority(IInputTuning tuning = null)
        {
            _buffer = new ParryPressBuffer(tuning ?? TestInputTuning.Default());
        }

        /// <summary>Current RunTime used for liveness checks.</summary>
        public float Now { get; set; }

        /// <inheritdoc />
        public Vector2 CurrentMoveDirection { get; set; }

        /// <inheritdoc />
        public SteerSample LastSteerDirection { get; set; }

        /// <summary>Last context passed to <see cref="SetContext"/> (Menu until set).</summary>
        public InputContext Context { get; private set; } = InputContext.Menu;

        /// <summary>Last value passed to <see cref="SetRunStartTime"/>, if any.</summary>
        public float? RunStartTime { get; private set; }

        /// <summary>Records a parry press stamped <paramref name="pressTime"/> (RunTime domain).</summary>
        public void InjectParryPress(float pressTime)
        {
            _buffer.Record(pressTime);
        }

        /// <summary>Prunes expired presses at <see cref="Now"/> (InputManager does this at the start of Tick).</summary>
        public void Prune()
        {
            _buffer.Prune(Now);
        }

        /// <summary>Sets the control-card text returned for <paramref name="action"/>.</summary>
        public void SetBindingDisplay(GameAction action, string display)
        {
            _bindings[action] = display;
        }

        /// <inheritdoc />
        public bool TryConsumeParryPress(float notBefore, out float pressTime)
        {
            return _buffer.TryConsume(Now, notBefore, out pressTime);
        }

        /// <inheritdoc />
        public bool HasLiveParryPress()
        {
            return _buffer.HasLive(Now);
        }

        /// <inheritdoc />
        public void SetContext(InputContext context)
        {
            Context = context;
        }

        /// <inheritdoc />
        public void SetRunStartTime(float runStartTime)
        {
            RunStartTime = runStartTime;
        }

        /// <inheritdoc />
        public string GetActiveBindingDisplay(GameAction action)
        {
            return _bindings.TryGetValue(action, out string display) ? display : string.Empty;
        }
    }
}

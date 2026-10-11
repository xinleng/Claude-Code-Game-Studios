using UnityEngine;

namespace Dynasty.Core
{
    /// <summary>
    /// The single input authority (ADR-0004, architecture.md § InputManager). Implemented only by
    /// <c>InputManager</c> in the <c>Dynasty.Input</c> assembly; declared here so consumers compile
    /// without referencing <c>Unity.InputSystem</c> — no Input System type appears on this surface.
    /// Invariants: a consumed press is never returned again; capture is inert before
    /// <see cref="SetRunStartTime"/>; the parry buffer freezes outside <see cref="InputContext.Gameplay"/>.
    /// </summary>
    /// <example>
    /// <code>
    /// if (input.TryConsumeParryPress(window.OpenedAt, out float pressTime)) ResolveParry(pressTime);
    /// Vector2 move = input.CurrentMoveDirection;
    /// </code>
    /// </example>
    public interface IInputAuthority
    {
        /// <summary>Current movement input; may be zero.</summary>
        Vector2 CurrentMoveDirection { get; }

        /// <summary>Last non-zero steer direction with its run-local time.</summary>
        SteerSample LastSteerDirection { get; }

        /// <summary>
        /// Consumes the oldest live parry press stamped at or after <paramref name="notBefore"/>.
        /// A consumed press is never returned again.
        /// </summary>
        /// <param name="notBefore">Earliest acceptable press time (RunTime domain).</param>
        /// <param name="pressTime">Stamp of the consumed press (RunTime domain).</param>
        /// <returns>True if a press was consumed.</returns>
        bool TryConsumeParryPress(float notBefore, out float pressTime);

        /// <summary>Non-consuming peek: true if any live parry press is buffered.</summary>
        bool HasLiveParryPress();

        /// <summary>Enables exactly one action map for <paramref name="context"/>. RSM decides when.</summary>
        /// <param name="context">Gameplay, Menu or Paused.</param>
        void SetContext(InputContext context);

        /// <summary>RSM only, at run init: arms parry capture (inert before this call).</summary>
        /// <param name="runStartTime">Run start time.</param>
        void SetRunStartTime(float runStartTime);

        /// <summary>Human-readable binding for <paramref name="action"/> on the active control scheme (control card).</summary>
        /// <param name="action">Action to describe.</param>
        string GetActiveBindingDisplay(GameAction action);
    }
}

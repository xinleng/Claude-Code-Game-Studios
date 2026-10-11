using UnityEngine;

namespace Dynasty.Core
{
    /// <summary>
    /// Last non-zero steer input and when it happened, in the <see cref="IGameClock.RunTime"/> domain
    /// (architecture.md § InputManager; input-manager GDD Rule 6).
    /// </summary>
    /// <example><code>SteerSample s = input.LastSteerDirection; Vector2 facing = s.Direction;</code></example>
    public readonly struct SteerSample
    {
        /// <summary>Last non-zero steer direction.</summary>
        public readonly Vector2 Direction;

        /// <summary>Run-local time (<see cref="IGameClock.RunTime"/>) of that steer sample.</summary>
        public readonly float SteerTime;

        /// <summary>Creates a steer sample.</summary>
        /// <param name="direction">Steer direction.</param>
        /// <param name="steerTime">Run-local time of the sample.</param>
        public SteerSample(Vector2 direction, float steerTime)
        {
            Direction = direction;
            SteerTime = steerTime;
        }
    }
}

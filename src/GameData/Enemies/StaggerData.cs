using System;
using UnityEngine;

namespace Dynasty.GameData
{
    /// <summary>
    /// Elite/boss stagger behaviour. Read only through <see cref="EnemyDataSO.TryGetStagger"/>; absent on
    /// basic infantry, whose stagger has no authored duration.
    /// </summary>
    /// <example><code>if (data.TryGetStagger(out StaggerData stagger)) EnterStagger(stagger.DurationMs);</code></example>
    [Serializable]
    public struct StaggerData
    {
        [SerializeField] private int _durationMs;

        internal StaggerData(int durationMs)
        {
            _durationMs = durationMs;
        }

        /// <summary>Stagger duration in milliseconds (Iron Captain 800, Warlord 1000).</summary>
        public int DurationMs => _durationMs;
    }
}

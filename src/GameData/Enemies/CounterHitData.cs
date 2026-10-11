using System;
using UnityEngine;

namespace Dynasty.GameData
{
    /// <summary>
    /// Elite/boss counter-hit attack (Iron Captain, Warlord). Read only through
    /// <see cref="EnemyDataSO.TryGetCounterHit"/>; absent on basic infantry (no 0-sentinel).
    /// </summary>
    /// <example>
    /// <code>
    /// if (data.TryGetCounterHit(out CounterHitData counter))
    ///     ScheduleCounterHit(counter.TelegraphDurationMs, counter.Damage);
    /// </code>
    /// </example>
    [Serializable]
    public struct CounterHitData
    {
        [SerializeField] private int _damage;
        [SerializeField] private float _range;
        [SerializeField] private int _telegraphDurationMs;
        [SerializeField] private TelegraphShape _telegraphShape;

        internal CounterHitData(int damage, float range, int telegraphDurationMs, TelegraphShape telegraphShape)
        {
            _damage = damage;
            _range = range;
            _telegraphDurationMs = telegraphDurationMs;
            _telegraphShape = telegraphShape;
        }

        /// <summary>Counter-hit damage (int, per the GDD HP/damage type contract).</summary>
        public int Damage => _damage;

        /// <summary>Counter-hit reach in world units.</summary>
        public float Range => _range;

        /// <summary>Counter-hit telegraph duration in milliseconds (600/550/450 across Iron Captain tiers).</summary>
        public int TelegraphDurationMs => _telegraphDurationMs;

        /// <summary>Counter-hit telegraph shape (270° arc).</summary>
        public TelegraphShape TelegraphShape => _telegraphShape;
    }
}

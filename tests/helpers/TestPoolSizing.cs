using System.Collections.Generic;
using Dynasty.GameData;

namespace Dynasty.Tests.Helpers
{
    /// <summary>
    /// In-memory <see cref="IPoolSizing"/> fixture (stand-in for <c>TestConfigs</c> until game-data
    /// Stories 003/004 land — QA plan gap G-5). Small counts keep pool tests fast; factories are the
    /// single source of test pool sizes (no inline literals in tests).
    /// </summary>
    /// <example>
    /// <code>
    /// var sizing = TestPoolSizing.Small();
    /// var pool = new ObjectPool(prefabs, sizing, errors);
    /// </code>
    /// </example>
    public sealed class TestPoolSizing : IPoolSizing
    {
        private readonly Dictionary<EnemyType, int> _counts = new Dictionary<EnemyType, int>();

        /// <inheritdoc />
        public int MaxEnemyCount { get; set; }

        /// <inheritdoc />
        public int GetPoolCount(EnemyType type)
        {
            return _counts.TryGetValue(type, out int count) ? count : 0;
        }

        /// <summary>Sets the pool count for <paramref name="type"/>; returns this for chaining.</summary>
        public TestPoolSizing With(EnemyType type, int count)
        {
            _counts[type] = count;
            return this;
        }

        /// <summary>
        /// Valid small config: Levy 3, Pikeman 2, Archer 0, Iron Captain 1, Warlord 1; max_enemy_count 5
        /// (infantry total == cap, the no-error boundary).
        /// </summary>
        public static TestPoolSizing Small()
        {
            return new TestPoolSizing { MaxEnemyCount = 5 }
                .With(EnemyType.Levy, 3)
                .With(EnemyType.Pikeman, 2)
                .With(EnemyType.Archer, 0)
                .With(EnemyType.IronCaptain, 1)
                .With(EnemyType.Warlord, 1);
        }

        /// <summary><see cref="Small"/> with infantry total one below max_enemy_count (sizing error).</summary>
        public static TestPoolSizing InfantryShortByOne()
        {
            TestPoolSizing sizing = Small();
            sizing.MaxEnemyCount = sizing.GetPoolCount(EnemyType.Levy) + sizing.GetPoolCount(EnemyType.Pikeman) + 1;
            return sizing;
        }

        /// <summary><see cref="Small"/> with the Iron Captain pool at 0 (sizing error).</summary>
        public static TestPoolSizing NoEliteCaptain()
        {
            return Small().With(EnemyType.IronCaptain, 0);
        }
    }
}

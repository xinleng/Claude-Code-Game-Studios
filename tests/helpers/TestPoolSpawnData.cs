using System.Collections.Generic;
using Dynasty.GameData;
using UnityEngine;

namespace Dynasty.Tests.Helpers
{
    /// <summary>
    /// In-memory spawn-data map for pool tests: one <see cref="EnemyDataSO"/> per <see cref="EnemyType"/>
    /// built from <see cref="EnemyDataFixtures"/> (Iron Captain = 5-minute tier, Warlord = Phase 1).
    /// Created assets are added to the caller's cleanup list.
    /// </summary>
    /// <example>
    /// <code>
    /// var data = TestPoolSpawnData.Create(cleanup);
    /// var pool = new ObjectPool(prefabs, data, TestPoolSizing.Small(), errors, new System.Random(TestPoolSpawnData.Seed));
    /// </code>
    /// </example>
    public static class TestPoolSpawnData
    {
        /// <summary>Fixed RNG seed for pool tests (named constant, never re-rolled to make a test pass).</summary>
        public const int Seed = 20261010;

        /// <summary>Builds the per-type spawn-data map.</summary>
        /// <param name="cleanup">Receives every created asset for teardown.</param>
        public static Dictionary<EnemyType, EnemyDataSO> Create(List<Object> cleanup)
        {
            var map = new Dictionary<EnemyType, EnemyDataSO>
            {
                { EnemyType.Levy, EnemyDataSO.CreateForTests(EnemyDataFixtures.Levy()) },
                { EnemyType.Pikeman, EnemyDataSO.CreateForTests(EnemyDataFixtures.Pikeman()) },
                { EnemyType.Archer, EnemyDataSO.CreateForTests(EnemyDataFixtures.Archer()) },
                { EnemyType.IronCaptain, EnemyDataSO.CreateForTests(EnemyDataFixtures.IronCaptain5()) },
                { EnemyType.Warlord, EnemyDataSO.CreateForTests(EnemyDataFixtures.WarlordP1()) },
            };
            cleanup.AddRange(map.Values);
            return map;
        }
    }
}

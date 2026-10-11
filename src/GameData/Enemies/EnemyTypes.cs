namespace Dynasty.GameData
{
    /// <summary>Classification helpers over <see cref="EnemyType"/> (enemy-definition roster: Basic vs Elite/Boss).</summary>
    /// <example><code>if (!EnemyTypes.IsInfantry(type)) errors.Report("ObjectPool", "elite pool exhausted");</code></example>
    public static class EnemyTypes
    {
        /// <summary>Number of <see cref="EnemyType"/> values; enum values are contiguous from 0.</summary>
        public const int Count = 5;

        /// <summary>True for basic infantry (Levy, Pikeman, Archer); false for elite (Iron Captain) and boss (Warlord).</summary>
        /// <param name="type">Enemy type.</param>
        public static bool IsInfantry(EnemyType type)
        {
            return type == EnemyType.Levy || type == EnemyType.Pikeman || type == EnemyType.Archer;
        }
    }
}

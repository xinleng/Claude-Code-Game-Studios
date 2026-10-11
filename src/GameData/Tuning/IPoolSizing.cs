namespace Dynasty.GameData
{
    /// <summary>
    /// Read-only pool-sizing view of <c>TuningConfig</c> (ADR-0005 §1, ADR-0006 §4: "pool sizes ...
    /// exposed to systems as read-only typed views"). <c>TuningConfig</c> implements it in game-data
    /// Story 003; tests supply an in-memory fixture. Never a static <c>Config.Instance</c>.
    /// </summary>
    /// <example>
    /// <code>
    /// int levyCount = sizing.GetPoolCount(EnemyType.Levy);
    /// bool enough = infantryTotal &gt;= sizing.MaxEnemyCount;
    /// </code>
    /// </example>
    public interface IPoolSizing
    {
        /// <summary>Live-enemy cap (<c>max_enemy_count</c>, GDD default 200); infantry pools must sum to at least this.</summary>
        int MaxEnemyCount { get; }

        /// <summary>Number of instances to pre-warm for <paramref name="type"/> (0 is allowed; negative is treated as 0).</summary>
        /// <param name="type">Enemy pool key.</param>
        /// <returns>Configured pool capacity for the type.</returns>
        int GetPoolCount(EnemyType type);
    }
}

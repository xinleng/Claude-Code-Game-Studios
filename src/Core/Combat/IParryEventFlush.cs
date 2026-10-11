namespace Dynasty.Core
{
    /// <summary>
    /// Narrow flush seam the object pool depends on (ADR-0005 §3; enemy-definition Contract #2). The
    /// Parry epic's <c>IParrySystem</c> extends this interface, so ParrySystem satisfies it with no pool
    /// change; same signature as architecture.md's <c>IParrySystem.FlushEventsFor</c>. Main thread only.
    /// </summary>
    /// <example>
    /// <code>
    /// parryFlush.FlushEventsFor(enemy.AttackerId);   // step 1 of ObjectPool.ReturnToPool, enemy still active
    /// </code>
    /// </example>
    public interface IParryEventFlush
    {
        /// <summary>Drops every pending AttackEvent / open parry window for <paramref name="attackerId"/>. O(1).</summary>
        /// <param name="attackerId">The current-spawn AttackerId of the enemy being returned.</param>
        void FlushEventsFor(int attackerId);
    }
}

namespace Dynasty.Gameplay
{
    /// <summary>
    /// Narrow registration seam between pooled enemies and the EnemySystemManager (ADR-0001, ADR-0005;
    /// TR-ED-008). EnemySystemManager (Enemy AI epic) implements it; the pool binds it into each instance at
    /// pre-warm, so controllers never self-wire. <see cref="RegisterPending"/> only <b>stages</b>: the manager
    /// flushes staged entries at the start of its next tick, never mid-iteration. <see cref="Deregister"/> may
    /// be called while the manager is iterating its active list (e.g. a death in the state-transition phase);
    /// safe removal during iteration is the manager's responsibility. Main thread only.
    /// </summary>
    /// <example>
    /// <code>
    /// // EnemyController.OnEnable (only when initialised by a Spawn):
    /// _registration.RegisterPending(this);
    /// </code>
    /// </example>
    public interface IEnemyRegistration
    {
        /// <summary>Stages <paramref name="enemy"/> for registration at the start of the manager's next tick.</summary>
        void RegisterPending(EnemyController enemy);

        /// <summary>Removes <paramref name="enemy"/> from the manager (step 2 of <c>ObjectPool.ReturnToPool</c>).</summary>
        void Deregister(EnemyController enemy);
    }
}

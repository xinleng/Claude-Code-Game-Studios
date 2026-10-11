using Dynasty.GameData;
using UnityEngine;

namespace Dynasty.Gameplay
{
    /// <summary>
    /// Enemy lifecycle authority (ADR-0005, architecture.md API Boundaries). The only legal path to
    /// activate or deactivate a pooled enemy. Zero <c>Instantiate</c>/<c>Destroy</c> during a run.
    /// The pool key is <see cref="EnemyType"/> (architecture.md calls it <c>EnemyTypeId</c>).
    /// </summary>
    /// <example>
    /// <code>
    /// EnemyController enemy = pool.Spawn(EnemyType.Pikeman, position);
    /// if (enemy == null) { /* dropped this tick (infantry) or designer error (elite/boss) */ }
    /// pool.ReturnToPool(enemy);   // sole SetActive(false) site for enemies
    /// </code>
    /// </example>
    public interface IObjectPool
    {
        /// <summary>
        /// Pops an inactive pre-warmed instance of <paramref name="type"/>, places it at
        /// <paramref name="position"/>, activates it and returns it. Never instantiates. Returns
        /// <c>null</c> when the type's pool is exhausted: for infantry the request is silently dropped
        /// this tick; for elite/boss a <c>[DESIGNER-ERROR]</c> is also reported.
        /// </summary>
        /// <param name="type">Enemy pool key.</param>
        /// <param name="position">World position for the spawned instance.</param>
        /// <returns>The activated instance, or <c>null</c> if dropped.</returns>
        EnemyController Spawn(EnemyType type, Vector3 position);

        /// <summary>
        /// Deactivates <paramref name="enemy"/> and pushes it back to its type's pool for reuse.
        /// Lifecycle only: raises no death or XP signal.
        /// </summary>
        /// <param name="enemy">An instance previously returned by <see cref="Spawn"/>.</param>
        void ReturnToPool(EnemyController enemy);
    }
}

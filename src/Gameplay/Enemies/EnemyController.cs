using Dynasty.GameData;
using UnityEngine;

namespace Dynasty.Gameplay
{
    /// <summary>
    /// Per-enemy component (ADR-0001: data + reactions only; the EnemySystemManager ticks it — it never
    /// declares <c>Update()</c>). This is the lifecycle shell the object pool needs (object-pool
    /// Story 001); spawn parameters, AttackerId and state arrive in object-pool Story 002 and the Enemy
    /// AI epic. Never self-initialises in <c>Awake</c>: pooled instances are configured by the pool.
    /// </summary>
    /// <example>
    /// <code>
    /// EnemyController enemy = pool.Spawn(EnemyType.Levy, spawnPoint);
    /// if (enemy == null) return;   // request dropped this tick
    /// </code>
    /// </example>
    [DisallowMultipleComponent]
    public sealed class EnemyController : MonoBehaviour
    {
        private bool _hasPoolType;

        /// <summary>The pool this instance belongs to; assigned once by the pool at pre-warm.</summary>
        public EnemyType PoolType { get; private set; }

        /// <summary>True once the pool has claimed this instance at pre-warm.</summary>
        public bool IsPooled => _hasPoolType;

        /// <summary>Pool-only: records which per-type pool owns this instance.</summary>
        internal void AssignPoolType(EnemyType type)
        {
            PoolType = type;
            _hasPoolType = true;
        }
    }
}

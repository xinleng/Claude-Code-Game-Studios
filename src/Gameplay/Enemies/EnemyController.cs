using Dynasty.GameData;
using UnityEngine;

namespace Dynasty.Gameplay
{
    /// <summary>
    /// Per-enemy component (ADR-0001: data + reactions only; the EnemySystemManager ticks it — it never
    /// declares <c>Update()</c>). Lifecycle shell for the object pool (object-pool Stories 001–002);
    /// state machine and registration arrive in object-pool Story 003 and the Enemy AI epic. Never
    /// self-initialises in <c>Awake</c>: the pool calls <see cref="Initialize"/> before every activation.
    /// </summary>
    /// <example>
    /// <code>
    /// EnemyController enemy = pool.Spawn(EnemyType.Levy, spawnPoint);
    /// if (enemy == null) return;              // request dropped this tick
    /// int id = enemy.AttackerId;              // unique per spawn
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

        /// <summary>Per-spawn attacker identity (unique per spawn, not per slot) from the last <see cref="Initialize"/>.</summary>
        public int AttackerId { get; private set; }

        /// <summary>Spawn-time cooldown jitter in milliseconds from the last <see cref="Initialize"/>.</summary>
        public int InitialCooldownMs { get; private set; }

        /// <summary>Active enemy data from the last <see cref="Initialize"/> (Warlord: Phase 1 at spawn).</summary>
        public EnemyDataSO ActiveData { get; private set; }

        /// <summary>Test observation: number of <see cref="Initialize"/> calls on this instance.</summary>
        internal int InitializeCount { get; private set; }

        /// <summary>Test observation: <c>activeSelf</c> at the moment of the last <see cref="Initialize"/>.</summary>
        internal bool ActiveSelfAtLastInitialize { get; private set; }

        /// <summary>
        /// Pool-only: applies per-spawn parameters. Called on every spawn, including recycled instances,
        /// before <c>SetActive(true)</c> (ADR-0005 §2).
        /// </summary>
        /// <param name="spawnParams">AttackerId, cooldown jitter, active data and position for this spawn.</param>
        internal void Initialize(in SpawnParams spawnParams)
        {
            AttackerId = spawnParams.AttackerId;
            InitialCooldownMs = spawnParams.InitialCooldownMs;
            ActiveData = spawnParams.Data;
            InitializeCount++;
            ActiveSelfAtLastInitialize = gameObject.activeSelf;
        }

        /// <summary>Pool-only: records which per-type pool owns this instance.</summary>
        internal void AssignPoolType(EnemyType type)
        {
            PoolType = type;
            _hasPoolType = true;
        }
    }
}

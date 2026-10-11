using Dynasty.GameData;
using UnityEngine;

namespace Dynasty.Gameplay
{
    /// <summary>
    /// Per-enemy component (ADR-0001: data + reactions only; the EnemySystemManager ticks it — it never
    /// declares <c>Update()</c>/<c>FixedUpdate()</c>). Lifecycle shell for the object pool (object-pool
    /// Stories 001–004); state machine and HP ledger arrive in the Enemy AI epic. Never self-initialises in
    /// <c>Awake</c>: the pool binds its registration sink at pre-warm and calls <see cref="Initialize"/>
    /// before every activation.
    /// <para>
    /// Deferred registration (ADR-0005 §1–§2, amended; TR-ED-008): <c>OnEnable</c> stages exactly one
    /// <see cref="IEnemyRegistration.RegisterPending"/> entry, and only when <see cref="IsInitialized"/> —
    /// set by <see cref="Initialize"/>, cleared by the pool's return path — so pre-warm can never stage
    /// phantom enemies even if an activation callback runs during warm-up.
    /// </para>
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
        private IEnemyRegistration _registration;

        /// <summary>The pool this instance belongs to; assigned once by the pool at pre-warm.</summary>
        public EnemyType PoolType { get; private set; }

        /// <summary>True once the pool has claimed this instance at pre-warm.</summary>
        public bool IsPooled => _hasPoolType;

        /// <summary>True between <see cref="Initialize"/> (spawn) and the pool's return; gates registration staging.</summary>
        public bool IsInitialized { get; private set; }

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
        /// Pool-only: applies per-spawn parameters and marks the instance initialised (last step). Called on
        /// every spawn, including recycled instances, before <c>SetActive(true)</c> (ADR-0005 §2).
        /// </summary>
        /// <param name="spawnParams">AttackerId, cooldown jitter, active data and position for this spawn.</param>
        internal void Initialize(in SpawnParams spawnParams)
        {
            AttackerId = spawnParams.AttackerId;
            InitialCooldownMs = spawnParams.InitialCooldownMs;
            ActiveData = spawnParams.Data;
            InitializeCount++;
            ActiveSelfAtLastInitialize = gameObject.activeSelf;
            IsInitialized = true;
        }

        /// <summary>Pool-only: records which per-type pool owns this instance and binds the registration sink.</summary>
        internal void AssignPoolType(EnemyType type, IEnemyRegistration registration)
        {
            PoolType = type;
            _registration = registration;
            _hasPoolType = true;
        }

        /// <summary>Pool-only (return path): clears the initialised flag before deactivation.</summary>
        internal void ClearInitialized()
        {
            IsInitialized = false;
        }

        /// <summary>
        /// The <c>OnEnable</c> body, separated so EditMode tests can exercise the guard (Unity does not run
        /// <c>OnEnable</c> in edit mode). Stages only when initialised; nothing else.
        /// </summary>
        internal void HandleEnabled()
        {
            if (!IsInitialized || _registration == null)
            {
                return;
            }

            _registration.RegisterPending(this);
        }

        private void OnEnable()
        {
            HandleEnabled();
        }
    }
}

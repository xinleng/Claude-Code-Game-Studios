using Dynasty.GameData;
using UnityEngine;

namespace Dynasty.Gameplay
{
    /// <summary>
    /// Per-spawn parameters delivered by the pool to <see cref="EnemyController.Initialize"/> before
    /// activation (ADR-0005 §2, amended; enemy-definition Contracts #1–#2). Readonly struct passed by
    /// <c>in</c>: no per-spawn allocation. Fields are fixed by ADR-0005 — add none without an amendment.
    /// </summary>
    /// <example>
    /// <code>
    /// var p = new SpawnParams(attackerId, rng.Next(0, data.AttackCooldownMs), data, position);
    /// enemy.Initialize(in p);
    /// </code>
    /// </example>
    public readonly struct SpawnParams
    {
        /// <summary>Creates spawn parameters.</summary>
        /// <param name="attackerId">Unique per spawn (not per slot) within one pool instance.</param>
        /// <param name="initialCooldownMs">Spawn-time jitter in [0, attack_cooldown_ms); always assigned, never defaulted.</param>
        /// <param name="data">The spawned type's active data (Warlord: Phase 1).</param>
        /// <param name="position">World spawn position.</param>
        public SpawnParams(int attackerId, int initialCooldownMs, EnemyDataSO data, Vector3 position)
        {
            AttackerId = attackerId;
            InitialCooldownMs = initialCooldownMs;
            Data = data;
            Position = position;
        }

        /// <summary>Per-spawn attacker identity used by AttackEvents, parry flush and damage attribution.</summary>
        public int AttackerId { get; }

        /// <summary>First-cooldown jitter in milliseconds, drawn from the injected RNG in [0, attack_cooldown_ms).</summary>
        public int InitialCooldownMs { get; }

        /// <summary>Active enemy data for this spawn.</summary>
        public EnemyDataSO Data { get; }

        /// <summary>World spawn position.</summary>
        public Vector3 Position { get; }
    }
}

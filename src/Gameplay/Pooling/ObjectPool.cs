using System;
using System.Collections.Generic;
using System.Diagnostics;
using Dynasty.Core;
using Dynasty.GameData;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Dynasty.Gameplay
{
    /// <summary>
    /// Bespoke enemy pool (ADR-0005; TR-WS-001). Plain C# class constructed and injected by Bootstrap;
    /// <see cref="Prewarm"/> runs once after GameData validation (ADR-0012 boot order) and is the only
    /// place the pool calls <c>Instantiate</c>. Per-type free stacks are pre-allocated arrays, so
    /// <see cref="Spawn"/> and <see cref="ReturnToPool"/> are O(1) and allocation-free.
    /// <para>
    /// Activation strategy (ADR-0005 §1, amended): instances are instantiated as children of an
    /// <b>inactive</b> container so no <c>Awake</c>/<c>OnEnable</c> runs during warm-up; each instance is
    /// then self-deactivated and the container is activated. Instances stay <c>activeSelf == false</c>
    /// until <see cref="Spawn"/>, which is when <c>Awake</c> first runs. Main thread only.
    /// </para>
    /// <para>
    /// Spawn path (ADR-0005 §2, amended): pop → <see cref="EnemyController.Initialize"/> with
    /// <c>AttackerId = _nextInstanceId++</c> (instance field — per spawn, never per slot, never static)
    /// and <c>initialCooldownMs = rng.Next(0, attack_cooldown_ms)</c> from the injected
    /// <see cref="System.Random"/> → <c>SetActive(true)</c>.
    /// </para>
    /// <para>
    /// Not yet: the <c>_initialized</c> registration guard (Story 003), the binding flush → deregister →
    /// deactivate → push order (Story 004). Double <see cref="ReturnToPool"/> is unspecified by ADR-0005
    /// (open item).
    /// </para>
    /// </summary>
    /// <example>
    /// <code>
    /// var pool = new ObjectPool(prefabs, spawnData, tuningConfig, designerErrors, new System.Random(seed));
    /// pool.Prewarm();                                    // Bootstrap, after validation
    /// EnemyController levy = pool.Spawn(EnemyType.Levy, position);
    /// </code>
    /// </example>
    public sealed class ObjectPool : IObjectPool, IDisposable
    {
        /// <summary>AttackerId assigned to the first spawn of a fresh (or reset) pool. 0 is never a valid id.</summary>
        public const int FirstAttackerId = 1;

        private const string SystemTag = "ObjectPool";

        private readonly IReadOnlyDictionary<EnemyType, EnemyController> _prefabs;
        private readonly IReadOnlyDictionary<EnemyType, EnemyDataSO> _spawnData;
        private readonly IPoolSizing _sizing;
        private readonly IDesignerErrorLog _designerErrors;
        private readonly System.Random _rng;
        private readonly EnemyDataSO[] _dataByType = new EnemyDataSO[EnemyTypes.Count];
        private readonly EnemyController[][] _free = new EnemyController[EnemyTypes.Count][];
        private readonly int[] _freeCount = new int[EnemyTypes.Count];
        private GameObject _container;
        private int _nextInstanceId = FirstAttackerId;

        /// <summary>Creates an empty pool; call <see cref="Prewarm"/> before spawning.</summary>
        /// <param name="prefabs">Prefab per enemy type (types with a 0 pool count may be absent).</param>
        /// <param name="spawnData">Active spawn data per enemy type (Warlord: Phase 1 asset).</param>
        /// <param name="sizing">Read-only TuningConfig pool-size view.</param>
        /// <param name="designerErrors">Shared designer-error channel.</param>
        /// <param name="rng">Injected jitter source (ADR-0010 rule 4); never <c>UnityEngine.Random</c>.</param>
        public ObjectPool(
            IReadOnlyDictionary<EnemyType, EnemyController> prefabs,
            IReadOnlyDictionary<EnemyType, EnemyDataSO> spawnData,
            IPoolSizing sizing,
            IDesignerErrorLog designerErrors,
            System.Random rng)
        {
            _prefabs = prefabs ?? throw new ArgumentNullException(nameof(prefabs));
            _spawnData = spawnData ?? throw new ArgumentNullException(nameof(spawnData));
            _sizing = sizing ?? throw new ArgumentNullException(nameof(sizing));
            _designerErrors = designerErrors ?? throw new ArgumentNullException(nameof(designerErrors));
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
        }

        /// <summary>Parent of every pooled instance (null before <see cref="Prewarm"/>).</summary>
        public Transform Container => _container != null ? _container.transform : null;

        /// <summary>True once <see cref="Prewarm"/> has run.</summary>
        public bool IsPrewarmed => _container != null;

        /// <summary>
        /// Instantiates every pool to its configured count under an inactive container, then runs the
        /// load-time sizing check (infantry total ≥ <c>max_enemy_count</c>; every elite/boss type ≥ 1),
        /// reporting violations as designer errors. Idempotent: a second call does nothing.
        /// </summary>
        public void Prewarm()
        {
            if (IsPrewarmed)
            {
                return;
            }

            _container = new GameObject("EnemyPool");
            _container.SetActive(false);

            for (int i = 0; i < EnemyTypes.Count; i++)
            {
                PrewarmType((EnemyType)i);
            }

            _container.SetActive(true);
            CheckSizing();
        }

        /// <inheritdoc />
        public EnemyController Spawn(EnemyType type, Vector3 position)
        {
            int index = (int)type;
            if (_freeCount[index] == 0)
            {
                if (!EnemyTypes.IsInfantry(type))
                {
                    _designerErrors.Report(SystemTag, type + " pool exhausted: scripted events must never double-spawn.");
                }

                return null;
            }

            int top = --_freeCount[index];
            EnemyController enemy = _free[index][top];
            _free[index][top] = null;
            EnemyDataSO data = _dataByType[index];
            var spawnParams = new SpawnParams(_nextInstanceId++, _rng.Next(0, data.AttackCooldownMs), data, position);
            enemy.Initialize(in spawnParams);
            enemy.transform.position = position;
            enemy.gameObject.SetActive(true);
            return enemy;
        }

        /// <inheritdoc />
        public void ReturnToPool(EnemyController enemy)
        {
            if (enemy == null)
            {
                throw new ArgumentNullException(nameof(enemy));
            }

            int index = (int)enemy.PoolType;
            enemy.gameObject.SetActive(false);
            _free[index][_freeCount[index]++] = enemy;
        }

        /// <summary>
        /// Test seam (ADR-0010 rule 5; damage-health Contract #6): the next spawn receives
        /// <see cref="FirstAttackerId"/>. Editor-only — compiled out of player builds.
        /// </summary>
        [Conditional("UNITY_EDITOR")]
        public void ResetInstanceIdCounter()
        {
            _nextInstanceId = FirstAttackerId;
        }

        /// <summary>Number of inactive instances currently available for <paramref name="type"/>.</summary>
        /// <param name="type">Enemy pool key.</param>
        public int FreeCount(EnemyType type)
        {
            return _freeCount[(int)type];
        }

        /// <summary>Destroys the container and every pooled instance (end of session / test teardown only).</summary>
        public void Dispose()
        {
            if (_container != null)
            {
                if (Application.isPlaying)
                {
                    Object.Destroy(_container);
                }
                else
                {
                    Object.DestroyImmediate(_container);
                }

                _container = null;
            }

            Array.Clear(_freeCount, 0, _freeCount.Length);
        }

        private void PrewarmType(EnemyType type)
        {
            int index = (int)type;
            int count = Math.Max(0, _sizing.GetPoolCount(type));
            _free[index] = new EnemyController[count];
            if (count == 0)
            {
                return;
            }

            if (!_prefabs.TryGetValue(type, out EnemyController prefab) || prefab == null)
            {
                _free[index] = Array.Empty<EnemyController>();
                _designerErrors.Report(SystemTag, type + " has pool count " + count + " but no prefab assigned.");
                return;
            }

            if (!_spawnData.TryGetValue(type, out EnemyDataSO data) || data == null)
            {
                _free[index] = Array.Empty<EnemyController>();
                _designerErrors.Report(SystemTag, type + " has pool count " + count + " but no EnemyDataSO assigned.");
                return;
            }

            _dataByType[index] = data;

            Transform parent = _container.transform;
            for (int i = 0; i < count; i++)
            {
                EnemyController enemy = Object.Instantiate(prefab, parent);
                enemy.name = prefab.name + "_" + i;
                enemy.AssignPoolType(type);
                enemy.gameObject.SetActive(false);
                _free[index][i] = enemy;
            }

            _freeCount[index] = count;
        }

        private void CheckSizing()
        {
            int infantryTotal = 0;
            for (int i = 0; i < EnemyTypes.Count; i++)
            {
                var type = (EnemyType)i;
                int count = Math.Max(0, _sizing.GetPoolCount(type));
                if (EnemyTypes.IsInfantry(type))
                {
                    infantryTotal += count;
                }
                else if (count < 1)
                {
                    _designerErrors.Report(SystemTag, type + " pool count is " + count + "; every elite/boss type needs at least 1.");
                }
            }

            if (infantryTotal < _sizing.MaxEnemyCount)
            {
                _designerErrors.Report(SystemTag, "infantry pool total " + infantryTotal + " is below max_enemy_count " + _sizing.MaxEnemyCount + ".");
            }
        }
    }
}

using System;
using System.Collections.Generic;
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
    /// Not yet in this story: AttackerId + spawn jitter (Story 002), the <c>_initialized</c>
    /// registration guard (Story 003), the binding flush → deregister → deactivate → push order
    /// (Story 004). Double <see cref="ReturnToPool"/> is unspecified by ADR-0005 (open item).
    /// </para>
    /// </summary>
    /// <example>
    /// <code>
    /// var pool = new ObjectPool(prefabs, tuningConfig, designerErrors);
    /// pool.Prewarm();                                    // Bootstrap, after validation
    /// EnemyController levy = pool.Spawn(EnemyType.Levy, position);
    /// </code>
    /// </example>
    public sealed class ObjectPool : IObjectPool, IDisposable
    {
        private const string SystemTag = "ObjectPool";

        private readonly IReadOnlyDictionary<EnemyType, EnemyController> _prefabs;
        private readonly IPoolSizing _sizing;
        private readonly IDesignerErrorLog _designerErrors;
        private readonly EnemyController[][] _free = new EnemyController[EnemyTypes.Count][];
        private readonly int[] _freeCount = new int[EnemyTypes.Count];
        private GameObject _container;

        /// <summary>Creates an empty pool; call <see cref="Prewarm"/> before spawning.</summary>
        /// <param name="prefabs">Prefab per enemy type (types with a 0 pool count may be absent).</param>
        /// <param name="sizing">Read-only TuningConfig pool-size view.</param>
        /// <param name="designerErrors">Shared designer-error channel.</param>
        public ObjectPool(IReadOnlyDictionary<EnemyType, EnemyController> prefabs, IPoolSizing sizing, IDesignerErrorLog designerErrors)
        {
            _prefabs = prefabs ?? throw new ArgumentNullException(nameof(prefabs));
            _sizing = sizing ?? throw new ArgumentNullException(nameof(sizing));
            _designerErrors = designerErrors ?? throw new ArgumentNullException(nameof(designerErrors));
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

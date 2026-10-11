using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Dynasty.GameData;
using Dynasty.Gameplay;
using Dynasty.Tests.Helpers;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Dynasty.Tests.ObjectPools
{
    /// <summary>
    /// Story OP-001 tests: config-driven inactive pre-warm, sizing sanity check, Spawn pops without
    /// Instantiate, return + reuse, infantry drop vs elite/boss designer error, no death/XP signal.
    /// </summary>
    public class ObjectPoolCoreTest
    {
        private readonly List<Object> _cleanup = new List<Object>();
        private Dictionary<EnemyType, EnemyController> _prefabs;
        private Dictionary<EnemyType, EnemyDataSO> _spawnData;
        private RecordingDesignerErrorLog _errors;
        private ObjectPool _pool;

        [SetUp]
        public void SetUp()
        {
            _errors = new RecordingDesignerErrorLog();
            _prefabs = new Dictionary<EnemyType, EnemyController>();
            _spawnData = TestPoolSpawnData.Create(_cleanup);
            for (int i = 0; i < EnemyTypes.Count; i++)
            {
                var type = (EnemyType)i;
                var go = new GameObject("Prefab_" + type);
                go.SetActive(false);
                _prefabs[type] = go.AddComponent<EnemyController>();
                _cleanup.Add(go);
            }
        }

        [TearDown]
        public void TearDown()
        {
            _pool?.Dispose();
            foreach (Object o in _cleanup)
            {
                Object.DestroyImmediate(o);
            }

            _cleanup.Clear();
        }

        private ObjectPool Prewarmed(TestPoolSizing sizing)
        {
            _pool = new ObjectPool(_prefabs, _spawnData, sizing, _errors, new System.Random(TestPoolSpawnData.Seed), new RecordingEnemyRegistration(), new RecordingParryFlush());
            _pool.Prewarm();
            return _pool;
        }

        private EnemyController[] Children(EnemyType type)
        {
            return _pool.Container.GetComponentsInChildren<EnemyController>(includeInactive: true)
                .Where(e => e.PoolType == type)
                .ToArray();
        }

        // ---------------------------------------------------------------- AC-1

        [Test]
        public void test_object_pool_prewarm_creates_config_count_inactive_instances_under_container()
        {
            // Arrange
            TestPoolSizing sizing = TestPoolSizing.Small();

            // Act
            Prewarmed(sizing);

            // Assert
            for (int i = 0; i < EnemyTypes.Count; i++)
            {
                var type = (EnemyType)i;
                EnemyController[] instances = Children(type);
                Assert.That(instances.Length, Is.EqualTo(sizing.GetPoolCount(type)), type + " count");
                Assert.That(_pool.FreeCount(type), Is.EqualTo(sizing.GetPoolCount(type)), type + " free");
                Assert.That(instances.All(e => !e.gameObject.activeSelf), Is.True, type + " inactive");
                Assert.That(instances.All(e => e.transform.parent == _pool.Container), Is.True, type + " parented");
            }

            Assert.That(_errors.Count, Is.EqualTo(0));
        }

        [Test]
        public void test_object_pool_prewarm_count_follows_config_not_a_constant()
        {
            // Arrange
            int levy = TestPoolSizing.Small().GetPoolCount(EnemyType.Levy) + 2;
            TestPoolSizing sizing = TestPoolSizing.Small().With(EnemyType.Levy, levy);

            // Act
            Prewarmed(sizing);

            // Assert
            Assert.That(Children(EnemyType.Levy).Length, Is.EqualTo(levy));
            Assert.That(Children(EnemyType.Archer).Length, Is.EqualTo(0), "count 0 → no instances, no throw");
        }

        [Test]
        public void test_object_pool_enemy_types_count_matches_enum()
        {
            // Assert
            Assert.That(EnemyTypes.Count, Is.EqualTo(Enum.GetValues(typeof(EnemyType)).Length));
        }

        // ---------------------------------------------------------------- AC-2

        [Test]
        public void test_object_pool_sizing_infantry_below_max_enemy_count_logs_one_designer_error()
        {
            // Act
            Prewarmed(TestPoolSizing.InfantryShortByOne());

            // Assert
            Assert.That(_errors.Count, Is.EqualTo(1));
            Assert.That(_errors.ContainsFromSystem("ObjectPool"), Is.True);
        }

        [Test]
        public void test_object_pool_sizing_elite_count_zero_logs_one_designer_error()
        {
            // Act
            Prewarmed(TestPoolSizing.NoEliteCaptain());

            // Assert
            Assert.That(_errors.Count, Is.EqualTo(1));
            Assert.That(_errors.Contains(EnemyType.IronCaptain.ToString()), Is.True);
        }

        [Test]
        public void test_object_pool_sizing_at_boundaries_logs_no_error()
        {
            // Arrange: Small() has infantry total == max_enemy_count and every elite/boss at exactly 1
            TestPoolSizing sizing = TestPoolSizing.Small();

            // Act
            Prewarmed(sizing);

            // Assert
            Assert.That(sizing.GetPoolCount(EnemyType.Levy) + sizing.GetPoolCount(EnemyType.Pikeman) + sizing.GetPoolCount(EnemyType.Archer),
                Is.EqualTo(sizing.MaxEnemyCount));
            Assert.That(_errors.Count, Is.EqualTo(0));
        }

        // ---------------------------------------------------------------- AC-3

        [Test]
        public void test_object_pool_spawn_pops_prewarmed_instance_activates_and_places_it()
        {
            // Arrange
            Prewarmed(TestPoolSizing.Small());
            var prewarmed = new HashSet<EnemyController>(Children(EnemyType.Levy));
            int childCount = _pool.Container.childCount;
            int free = _pool.FreeCount(EnemyType.Levy);
            var position = new Vector3(3f, 0f, -2f);

            // Act
            EnemyController enemy = _pool.Spawn(EnemyType.Levy, position);

            // Assert
            Assert.That(prewarmed.Contains(enemy), Is.True);
            Assert.That(enemy.gameObject.activeSelf, Is.True);
            Assert.That(enemy.transform.position, Is.EqualTo(position));
            Assert.That(_pool.FreeCount(EnemyType.Levy), Is.EqualTo(free - 1));
            Assert.That(_pool.Container.childCount, Is.EqualTo(childCount), "no Instantiate");
        }

        [Test]
        public void test_object_pool_spawn_never_crosses_types()
        {
            // Arrange
            Prewarmed(TestPoolSizing.Small());
            var levies = new HashSet<EnemyController>(Children(EnemyType.Levy));

            // Act
            EnemyController pikeman = _pool.Spawn(EnemyType.Pikeman, Vector3.zero);

            // Assert
            Assert.That(levies.Contains(pikeman), Is.False);
            Assert.That(pikeman.PoolType, Is.EqualTo(EnemyType.Pikeman));
        }

        // ---------------------------------------------------------------- AC-4

        [Test]
        public void test_object_pool_return_deactivates_and_same_instance_is_reused()
        {
            // Arrange
            Prewarmed(TestPoolSizing.Small().With(EnemyType.Levy, 1).With(EnemyType.Pikeman, 4));
            EnemyController first = _pool.Spawn(EnemyType.Levy, Vector3.zero);

            // Act
            _pool.ReturnToPool(first);
            bool inactiveAfterReturn = !first.gameObject.activeSelf;
            int freeAfterReturn = _pool.FreeCount(EnemyType.Levy);
            EnemyController second = _pool.Spawn(EnemyType.Levy, Vector3.one);

            // Assert
            Assert.That(inactiveAfterReturn, Is.True);
            Assert.That(freeAfterReturn, Is.EqualTo(1));
            Assert.That(second, Is.SameAs(first));
        }

        [Test]
        public void test_object_pool_returning_out_of_order_restores_full_free_count()
        {
            // Arrange
            Prewarmed(TestPoolSizing.Small());
            int full = _pool.FreeCount(EnemyType.Levy);
            EnemyController a = _pool.Spawn(EnemyType.Levy, Vector3.zero);
            EnemyController b = _pool.Spawn(EnemyType.Levy, Vector3.zero);
            EnemyController c = _pool.Spawn(EnemyType.Levy, Vector3.zero);

            // Act
            _pool.ReturnToPool(b);
            _pool.ReturnToPool(a);
            _pool.ReturnToPool(c);

            // Assert
            Assert.That(_pool.FreeCount(EnemyType.Levy), Is.EqualTo(full));
        }

        // ---------------------------------------------------------------- AC-5

        [Test]
        public void test_object_pool_infantry_exhaustion_drops_request_silently()
        {
            // Arrange
            Prewarmed(TestPoolSizing.Small());
            int count = _pool.FreeCount(EnemyType.Pikeman);
            EnemyController last = null;
            for (int i = 0; i < count; i++)
            {
                last = _pool.Spawn(EnemyType.Pikeman, Vector3.zero);
            }

            int childCount = _pool.Container.childCount;

            // Act
            EnemyController dropped = null;
            Assert.DoesNotThrow(() => dropped = _pool.Spawn(EnemyType.Pikeman, Vector3.zero));

            // Assert
            Assert.That(dropped, Is.Null);
            Assert.That(_pool.Container.childCount, Is.EqualTo(childCount));
            Assert.That(_errors.Count, Is.EqualTo(0));

            // Edge: after one return, the next Spawn succeeds
            _pool.ReturnToPool(last);
            Assert.That(_pool.Spawn(EnemyType.Pikeman, Vector3.zero), Is.SameAs(last));
        }

        // ---------------------------------------------------------------- AC-6

        [TestCase(EnemyType.IronCaptain)]
        [TestCase(EnemyType.Warlord)]
        public void test_object_pool_elite_or_boss_exhaustion_is_a_designer_error(EnemyType type)
        {
            // Arrange
            Prewarmed(TestPoolSizing.Small());
            _pool.Spawn(type, Vector3.zero);
            int childCount = _pool.Container.childCount;

            // Act
            EnemyController second = _pool.Spawn(type, Vector3.zero);

            // Assert
            Assert.That(second, Is.Null);
            Assert.That(_errors.Count, Is.EqualTo(1));
            Assert.That(_errors.ContainsFromSystem("ObjectPool"), Is.True);
            Assert.That(_pool.Container.childCount, Is.EqualTo(childCount));
        }

        // ---------------------------------------------------------------- AC-7

        [Test]
        public void test_object_pool_exposes_no_events_so_return_raises_no_death_or_xp_signal()
        {
            // Arrange
            Prewarmed(TestPoolSizing.Small());
            EnemyController enemy = _pool.Spawn(EnemyType.Levy, Vector3.zero);
            const BindingFlags All = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

            // Act
            _pool.ReturnToPool(enemy);
            EventInfo[] events = typeof(ObjectPool).GetEvents(All).Concat(typeof(IObjectPool).GetEvents(All)).ToArray();
            FieldInfo[] delegates = typeof(ObjectPool).GetFields(All).Where(f => typeof(Delegate).IsAssignableFrom(f.FieldType)).ToArray();

            // Assert
            Assert.That(events, Is.Empty);
            Assert.That(delegates, Is.Empty);
            Assert.That(_errors.Count, Is.EqualTo(0));
        }
    }
}

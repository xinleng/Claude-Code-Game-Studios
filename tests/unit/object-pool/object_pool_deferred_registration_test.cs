using System.Collections.Generic;
using System.Linq;
using Dynasty.GameData;
using Dynasty.Gameplay;
using Dynasty.Tests.Helpers;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Dynasty.Tests.ObjectPools
{
    /// <summary>
    /// Story OP-003 EditMode tests: the <c>IsInitialized</c> guard and the OnEnable staging body
    /// (<c>HandleEnabled</c>, invoked directly because Unity does not run OnEnable in edit mode). The real
    /// activation-callback cases run in PlayMode: tests/PlayMode/object-pool/object_pool_deferred_registration_play_mode_test.cs.
    /// </summary>
    public class ObjectPoolDeferredRegistrationTest
    {
        private readonly List<Object> _cleanup = new List<Object>();
        private RecordingEnemyRegistration _registration;
        private ObjectPool _pool;

        [SetUp]
        public void SetUp()
        {
            _registration = new RecordingEnemyRegistration();
            var prefabs = new Dictionary<EnemyType, EnemyController>();
            for (int i = 0; i < EnemyTypes.Count; i++)
            {
                var go = new GameObject("Prefab_" + (EnemyType)i);
                go.SetActive(false);
                prefabs[(EnemyType)i] = go.AddComponent<EnemyController>();
                _cleanup.Add(go);
            }

            _pool = new ObjectPool(prefabs, TestPoolSpawnData.Create(_cleanup), TestPoolSizing.Small(), new RecordingDesignerErrorLog(),
                new System.Random(TestPoolSpawnData.Seed), _registration, new RecordingParryFlush());
            _pool.Prewarm();
        }

        [TearDown]
        public void TearDown()
        {
            _pool.Dispose();
            foreach (Object o in _cleanup)
            {
                Object.DestroyImmediate(o);
            }

            _cleanup.Clear();
        }

        private EnemyController[] AllInstances()
        {
            return _pool.Container.GetComponentsInChildren<EnemyController>(includeInactive: true);
        }

        // ---------------------------------------------------------------- AC-1

        [Test]
        public void test_object_pool_prewarm_stages_zero_registrations_even_if_enable_runs()
        {
            // Act: simulate an activation callback firing on every pre-warmed instance
            foreach (EnemyController enemy in AllInstances())
            {
                enemy.HandleEnabled();
            }

            // Assert
            Assert.That(AllInstances().Length, Is.GreaterThan(0));
            Assert.That(AllInstances().All(e => !e.IsInitialized), Is.True);
            Assert.That(_registration.Pending, Is.Empty);
        }

        // ---------------------------------------------------------------- AC-2 / AC-3

        [Test]
        public void test_object_pool_spawned_instance_stages_exactly_one_entry_on_enable()
        {
            // Arrange
            EnemyController enemy = _pool.Spawn(EnemyType.Levy, Vector3.zero);

            // Act (the OnEnable body)
            enemy.HandleEnabled();

            // Assert
            Assert.That(enemy.IsInitialized, Is.True);
            Assert.That(_registration.Pending, Is.EqualTo(new[] { enemy }));
            Assert.That(_registration.Deregistered, Is.Empty, "spawn never calls a direct registration/active-list API");
        }

        [Test]
        public void test_object_pool_return_clears_initialized_so_enable_stages_nothing()
        {
            // Arrange
            EnemyController enemy = _pool.Spawn(EnemyType.Levy, Vector3.zero);
            enemy.HandleEnabled();

            // Act
            _pool.ReturnToPool(enemy);
            enemy.HandleEnabled();

            // Assert
            Assert.That(enemy.IsInitialized, Is.False);
            Assert.That(_registration.Pending.Count, Is.EqualTo(1));
        }

        // ---------------------------------------------------------------- AC-4

        [Test]
        public void test_object_pool_recycled_instance_stages_one_new_entry_per_life()
        {
            // Arrange
            EnemyController first = _pool.Spawn(EnemyType.Levy, Vector3.zero);
            first.HandleEnabled();
            _pool.ReturnToPool(first);

            // Act: reuse the same slot
            EnemyController again = null;
            for (int i = 0; i < 3 && again != first; i++)
            {
                again = _pool.Spawn(EnemyType.Levy, Vector3.zero);
            }

            again.HandleEnabled();

            // Assert
            Assert.That(again, Is.SameAs(first));
            Assert.That(_registration.Pending.Count(e => e == first), Is.EqualTo(2));
        }
    }
}

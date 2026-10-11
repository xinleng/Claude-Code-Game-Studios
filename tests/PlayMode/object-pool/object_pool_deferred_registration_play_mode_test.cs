using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Dynasty.GameData;
using Dynasty.Gameplay;
using Dynasty.Tests.Helpers;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Dynasty.Tests.ObjectPools
{
    /// <summary>
    /// Story OP-003 PlayMode tests: real <c>Awake</c>/<c>OnEnable</c> callbacks. Pre-warming from an
    /// <b>active</b> prefab stages zero registrations (inactive container + initialised guard); a spawn's
    /// <c>SetActive(true)</c> stages exactly one; a recycled instance stages exactly one per life.
    /// </summary>
    public class ObjectPoolDeferredRegistrationPlayModeTest
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
                // Active template: Instantiate would run Awake/OnEnable without the inactive container.
                var go = new GameObject("Prefab_" + (EnemyType)i);
                prefabs[(EnemyType)i] = go.AddComponent<EnemyController>();
                _cleanup.Add(go);
            }

            _pool = new ObjectPool(prefabs, TestPoolSpawnData.Create(_cleanup), TestPoolSizing.Small(), new RecordingDesignerErrorLog(),
                new System.Random(TestPoolSpawnData.Seed), _registration, new RecordingParryFlush());
        }

        [TearDown]
        public void TearDown()
        {
            _pool.Dispose();
            foreach (Object o in _cleanup)
            {
                Object.Destroy(o);
            }

            _cleanup.Clear();
        }

        [UnityTest]
        public IEnumerator test_object_pool_play_mode_prewarm_stages_zero_registrations()
        {
            // Act
            _pool.Prewarm();
            yield return null;

            // Assert
            int instances = _pool.Container.GetComponentsInChildren<EnemyController>(includeInactive: true).Length;
            Assert.That(instances, Is.GreaterThan(0));
            Assert.That(_registration.Pending, Is.Empty);
        }

        [UnityTest]
        public IEnumerator test_object_pool_play_mode_spawn_stages_exactly_one_and_recycle_one_more()
        {
            // Arrange
            _pool.Prewarm();
            yield return null;

            // Act
            EnemyController enemy = _pool.Spawn(EnemyType.Levy, Vector3.zero);
            int afterFirstSpawn = _registration.Pending.Count;
            _pool.ReturnToPool(enemy);
            EnemyController again = null;
            for (int i = 0; i < 3 && again != enemy; i++)
            {
                again = _pool.Spawn(EnemyType.Levy, Vector3.zero);
            }

            yield return null;

            // Assert
            Assert.That(afterFirstSpawn, Is.EqualTo(1));
            Assert.That(again, Is.SameAs(enemy));
            Assert.That(_registration.Pending.Count(e => e == enemy), Is.EqualTo(2));
        }
    }
}

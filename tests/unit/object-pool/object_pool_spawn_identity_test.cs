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
    /// Story OP-002 tests: per-spawn AttackerId from the pool instance field, editor-only reset seam,
    /// spawn-time cooldown jitter from the injected RNG via Initialize(in SpawnParams) before activation,
    /// and the AC-18 same-frame jitter-spread precondition.
    /// </summary>
    public class ObjectPoolSpawnIdentityTest
    {
        /// <summary>Scripted RNG: records Next(min, max) arguments and returns queued values.</summary>
        private sealed class ScriptedRandom : System.Random
        {
            private readonly Queue<int> _values = new Queue<int>();

            public List<(int Min, int Max)> Calls { get; } = new List<(int, int)>();

            public ScriptedRandom Returning(params int[] values)
            {
                foreach (int v in values)
                {
                    _values.Enqueue(v);
                }

                return this;
            }

            public override int Next(int minValue, int maxValue)
            {
                Calls.Add((minValue, maxValue));
                return _values.Count > 0 ? _values.Dequeue() : minValue;
            }
        }

        private const float FrameMs = 1000f / 60f;
        private const int MaxPerFrameBucket = 3;

        private readonly List<Object> _cleanup = new List<Object>();
        private readonly List<ObjectPool> _pools = new List<ObjectPool>();
        private Dictionary<EnemyType, EnemyController> _prefabs;
        private Dictionary<EnemyType, EnemyDataSO> _spawnData;
        private RecordingDesignerErrorLog _errors;

        [SetUp]
        public void SetUp()
        {
            _errors = new RecordingDesignerErrorLog();
            _spawnData = TestPoolSpawnData.Create(_cleanup);
            _prefabs = new Dictionary<EnemyType, EnemyController>();
            for (int i = 0; i < EnemyTypes.Count; i++)
            {
                var go = new GameObject("Prefab_" + (EnemyType)i);
                go.SetActive(false);
                _prefabs[(EnemyType)i] = go.AddComponent<EnemyController>();
                _cleanup.Add(go);
            }
        }

        [TearDown]
        public void TearDown()
        {
            foreach (ObjectPool pool in _pools)
            {
                pool.Dispose();
            }

            foreach (Object o in _cleanup)
            {
                Object.DestroyImmediate(o);
            }

            _pools.Clear();
            _cleanup.Clear();
        }

        private ObjectPool NewPool(TestPoolSizing sizing, System.Random rng)
        {
            var pool = new ObjectPool(_prefabs, _spawnData, sizing, _errors, rng, new RecordingEnemyRegistration(), new RecordingParryFlush());
            pool.Prewarm();
            pool.ResetInstanceIdCounter();
            _pools.Add(pool);
            return pool;
        }

        private ObjectPool NewPool(TestPoolSizing sizing)
        {
            return NewPool(sizing, new System.Random(TestPoolSpawnData.Seed));
        }

        // ---------------------------------------------------------------- AC-1

        [Test]
        public void test_object_pool_attacker_ids_unique_and_strictly_increasing_across_types()
        {
            // Arrange
            TestPoolSizing sizing = TestPoolSizing.Small();
            ObjectPool pool = NewPool(sizing);
            EnemyType[] order = { EnemyType.Levy, EnemyType.Pikeman, EnemyType.Levy, EnemyType.IronCaptain, EnemyType.Pikeman, EnemyType.Levy, EnemyType.Warlord };

            // Act
            int[] ids = order.Select(t => pool.Spawn(t, Vector3.zero).AttackerId).ToArray();

            // Assert
            Assert.That(ids.Length, Is.EqualTo(order.Length));
            Assert.That(ids.Distinct().Count(), Is.EqualTo(ids.Length));
            for (int i = 1; i < ids.Length; i++)
            {
                Assert.That(ids[i], Is.GreaterThan(ids[i - 1]));
            }

            Assert.That(ids[0], Is.EqualTo(ObjectPool.FirstAttackerId));
        }

        // ---------------------------------------------------------------- AC-2

        [Test]
        public void test_object_pool_recycled_slot_gets_new_attacker_id_each_spawn()
        {
            // Arrange
            ObjectPool pool = NewPool(TestPoolSizing.Small().With(EnemyType.Levy, 1).With(EnemyType.Pikeman, 4));
            var ids = new List<int>();
            EnemyController first = null;

            // Act: 3 recycle cycles on the same object
            for (int cycle = 0; cycle < 3; cycle++)
            {
                EnemyController enemy = pool.Spawn(EnemyType.Levy, Vector3.zero);
                first = first ?? enemy;
                Assert.That(enemy, Is.SameAs(first));
                ids.Add(enemy.AttackerId);
                pool.ReturnToPool(enemy);
            }

            // Assert
            Assert.That(ids.Distinct().Count(), Is.EqualTo(3));
            Assert.That(ids[1], Is.GreaterThan(ids[0]));
            Assert.That(ids[2], Is.GreaterThan(ids[1]));
        }

        // ---------------------------------------------------------------- AC-3

        [Test]
        public void test_object_pool_reset_counter_restarts_ids_and_pools_are_independent()
        {
            // Arrange
            ObjectPool a = NewPool(TestPoolSizing.Small());
            for (int i = 0; i < 5; i++)
            {
                EnemyController e = a.Spawn(EnemyType.Levy, Vector3.zero) ?? a.Spawn(EnemyType.Pikeman, Vector3.zero);
                a.ReturnToPool(e);
            }

            // Act
            a.ResetInstanceIdCounter();
            int afterReset = a.Spawn(EnemyType.Levy, Vector3.zero).AttackerId;
            ObjectPool b = NewPool(TestPoolSizing.Small());
            int bFirst = b.Spawn(EnemyType.Levy, Vector3.zero).AttackerId;

            // Assert
            Assert.That(afterReset, Is.EqualTo(ObjectPool.FirstAttackerId));
            Assert.That(bFirst, Is.EqualTo(ObjectPool.FirstAttackerId));
        }

        [Test]
        public void test_object_pool_declares_no_static_counter_and_reset_is_editor_conditional()
        {
            // Arrange
            const BindingFlags Statics = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

            // Act
            FieldInfo[] staticFields = typeof(ObjectPool).GetFields(Statics).Where(f => !f.IsLiteral).ToArray();
            MethodInfo reset = typeof(ObjectPool).GetMethod(nameof(ObjectPool.ResetInstanceIdCounter));
            var conditional = reset.GetCustomAttribute<System.Diagnostics.ConditionalAttribute>();

            // Assert
            Assert.That(staticFields, Is.Empty);
            Assert.That(reset.IsStatic, Is.False);
            Assert.That(conditional, Is.Not.Null);
            Assert.That(conditional.ConditionString, Is.EqualTo("UNITY_EDITOR"));
        }

        // ---------------------------------------------------------------- AC-4

        [Test]
        public void test_object_pool_jitter_uses_injected_rng_with_type_cooldown()
        {
            // Arrange
            int levyCooldown = EnemyDataFixtures.Levy().AttackCooldownMs;
            const int Scripted = 777;
            var rng = new ScriptedRandom().Returning(Scripted);
            ObjectPool pool = NewPool(TestPoolSizing.Small(), rng);
            var position = new Vector3(1f, 0f, 2f);

            // Act
            EnemyController levy = pool.Spawn(EnemyType.Levy, position);

            // Assert
            Assert.That(rng.Calls, Is.EqualTo(new[] { (0, levyCooldown) }));
            Assert.That(levy.InitialCooldownMs, Is.EqualTo(Scripted));
            Assert.That(levy.AttackerId, Is.EqualTo(ObjectPool.FirstAttackerId));
            Assert.That(levy.ActiveData, Is.SameAs(_spawnData[EnemyType.Levy]));
            Assert.That(levy.transform.position, Is.EqualTo(position));
        }

        [Test]
        public void test_object_pool_elite_and_boss_jitter_use_their_own_cooldowns()
        {
            // Arrange
            var rng = new ScriptedRandom();
            ObjectPool pool = NewPool(TestPoolSizing.Small(), rng);

            // Act
            EnemyController captain = pool.Spawn(EnemyType.IronCaptain, Vector3.zero);
            EnemyController warlord = pool.Spawn(EnemyType.Warlord, Vector3.zero);

            // Assert
            Assert.That(rng.Calls[0], Is.EqualTo((0, EnemyDataFixtures.IronCaptain5().AttackCooldownMs)));
            Assert.That(rng.Calls[1], Is.EqualTo((0, EnemyDataFixtures.WarlordP1().AttackCooldownMs)));
            Assert.That(captain.ActiveData, Is.SameAs(_spawnData[EnemyType.IronCaptain]));
            Assert.That(warlord.ActiveData, Is.SameAs(_spawnData[EnemyType.Warlord]), "Warlord spawns with Phase 1 data");
        }

        // ---------------------------------------------------------------- AC-5

        [Test]
        public void test_object_pool_initialize_runs_once_per_spawn_before_activation()
        {
            // Arrange
            ObjectPool pool = NewPool(TestPoolSizing.Small().With(EnemyType.Levy, 1).With(EnemyType.Pikeman, 4));

            // Act
            EnemyController enemy = pool.Spawn(EnemyType.Levy, Vector3.zero);
            int countAfterFirst = enemy.InitializeCount;
            bool activeAtFirst = enemy.ActiveSelfAtLastInitialize;
            pool.ReturnToPool(enemy);
            pool.Spawn(EnemyType.Levy, Vector3.zero);

            // Assert
            Assert.That(countAfterFirst, Is.EqualTo(1));
            Assert.That(activeAtFirst, Is.False);
            Assert.That(enemy.InitializeCount, Is.EqualTo(2));
            Assert.That(enemy.ActiveSelfAtLastInitialize, Is.False);
            Assert.That(enemy.gameObject.activeSelf, Is.True);
        }

        [Test]
        public void test_object_pool_jitter_bounds_are_passed_through_unchanged()
        {
            // Arrange
            int upper = EnemyDataFixtures.Levy().AttackCooldownMs - 1;
            var rng = new ScriptedRandom().Returning(upper, 0);
            ObjectPool pool = NewPool(TestPoolSizing.Small(), rng);

            // Act
            EnemyController a = pool.Spawn(EnemyType.Levy, Vector3.zero);
            EnemyController b = pool.Spawn(EnemyType.Levy, Vector3.zero);

            // Assert
            Assert.That(a.InitialCooldownMs, Is.EqualTo(upper));
            Assert.That(b.InitialCooldownMs, Is.EqualTo(0));
            Assert.That(b.InitializeCount, Is.EqualTo(1), "a legitimately-zero jitter is still assigned");
        }

        // ---------------------------------------------------------------- AC-6

        [Test]
        public void test_object_pool_ten_levies_same_frame_jitter_spread_at_most_three_per_frame_bucket()
        {
            // Arrange
            const int LevyCount = 10;
            int cooldown = EnemyDataFixtures.Levy().AttackCooldownMs;
            Assert.That(cooldown, Is.EqualTo(2000), "GDD Formula 4 boundary value");
            ObjectPool pool = NewPool(TestPoolSizing.Small().With(EnemyType.Levy, LevyCount).With(EnemyType.Pikeman, 0));

            // Act
            int[] jitter = Enumerable.Range(0, LevyCount).Select(_ => pool.Spawn(EnemyType.Levy, Vector3.zero).InitialCooldownMs).ToArray();
            int maxBucket = jitter.GroupBy(v => (int)Mathf.Floor(v / FrameMs)).Max(g => g.Count());

            // Assert
            Assert.That(jitter.All(v => v >= 0 && v < cooldown), Is.True);
            Assert.That(maxBucket, Is.LessThanOrEqualTo(MaxPerFrameBucket));
        }
    }
}

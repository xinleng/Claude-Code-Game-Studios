using System.IO;
using System.Linq;
using System.Reflection;
using Dynasty.GameData;
using Dynasty.Tests.Helpers;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Dynasty.Tests.GameDataTests
{
    /// <summary>Holder with two serialized EnemyDataSO references (stands in for the Warlord prefab, AC-4).</summary>
    public sealed class WarlordPhaseHolder : ScriptableObject
    {
        public EnemyDataSO Phase1;
        public EnemyDataSO Phase2;
    }

    /// <summary>
    /// Story GD-001 tests: EnemyDataSO fields and types, the eight authored assets vs GDD-default
    /// fixtures, has-value guarded optional groups, Warlord P1/P2 coexistence, Formula 3, read-only API.
    /// </summary>
    public class GameDataEnemyDataTest
    {
        private const float Tolerance = 0.001f;

        private static EnemyDataSO LoadAsset(string name)
        {
            var asset = AssetDatabase.LoadAssetAtPath<EnemyDataSO>(EnemyDataFixtures.AssetFolder + "/" + name + ".asset");
            Assert.That(asset, Is.Not.Null, "missing asset " + name);
            return asset;
        }

        private static void AssertMatches(EnemyDataSO actual, EnemyDataSpec expected, string label)
        {
            Assert.That(actual.EnemyType, Is.EqualTo(expected.EnemyType), label + " EnemyType");
            Assert.That(actual.MaxHp, Is.EqualTo(expected.MaxHp), label + " MaxHp");
            Assert.That(actual.MoveSpeed, Is.EqualTo(expected.MoveSpeed).Within(Tolerance), label + " MoveSpeed");
            Assert.That(actual.AttackDamage, Is.EqualTo(expected.AttackDamage), label + " AttackDamage");
            Assert.That(actual.AttackRange, Is.EqualTo(expected.AttackRange).Within(Tolerance), label + " AttackRange");
            Assert.That(actual.Parriable, Is.EqualTo(expected.Parriable), label + " Parriable");
            Assert.That(actual.StaggerRule, Is.EqualTo(expected.StaggerRule), label + " StaggerRule");
            Assert.That(actual.TelegraphShape, Is.EqualTo(expected.TelegraphShape), label + " TelegraphShape");
            Assert.That(actual.TelegraphDurationMs, Is.EqualTo(expected.TelegraphDurationMs), label + " TelegraphDurationMs");
            Assert.That(actual.AttackCooldownMs, Is.EqualTo(expected.AttackCooldownMs), label + " AttackCooldownMs");
            Assert.That(actual.AggroRange, Is.EqualTo(expected.AggroRange).Within(Tolerance), label + " AggroRange");
            Assert.That(actual.XpWeight, Is.EqualTo(expected.XpWeight), label + " XpWeight");

            Assert.That(actual.TryGetSpawnWeight(out _), Is.EqualTo(expected.SpawnWeight.HasValue), label + " has SpawnWeight");

            Assert.That(actual.TryGetCounterHit(out CounterHitData counter), Is.EqualTo(expected.CounterHit.HasValue), label + " has CounterHit");
            if (expected.CounterHit.HasValue)
            {
                CounterHitData e = expected.CounterHit.Value;
                Assert.That(counter.Damage, Is.EqualTo(e.Damage), label + " CounterHit.Damage");
                Assert.That(counter.Range, Is.EqualTo(e.Range).Within(Tolerance), label + " CounterHit.Range");
                Assert.That(counter.TelegraphDurationMs, Is.EqualTo(e.TelegraphDurationMs), label + " CounterHit.TelegraphDurationMs");
                Assert.That(counter.TelegraphShape, Is.EqualTo(e.TelegraphShape), label + " CounterHit.TelegraphShape");
            }

            Assert.That(actual.TryGetStagger(out StaggerData stagger), Is.EqualTo(expected.Stagger.HasValue), label + " has Stagger");
            if (expected.Stagger.HasValue)
            {
                Assert.That(stagger.DurationMs, Is.EqualTo(expected.Stagger.Value.DurationMs), label + " Stagger.DurationMs");
            }

            Assert.That(actual.TryGetSlam(out SlamData slam), Is.EqualTo(expected.Slam.HasValue), label + " has Slam");
            if (expected.Slam.HasValue)
            {
                SlamData e = expected.Slam.Value;
                Assert.That(slam.Damage, Is.EqualTo(e.Damage), label + " Slam.Damage");
                Assert.That(slam.Radius, Is.EqualTo(e.Radius).Within(Tolerance), label + " Slam.Radius");
                Assert.That(slam.TelegraphDurationMs, Is.EqualTo(e.TelegraphDurationMs), label + " Slam.TelegraphDurationMs");
                Assert.That(slam.TelegraphShape, Is.EqualTo(e.TelegraphShape), label + " Slam.TelegraphShape");
                Assert.That(slam.Parriable, Is.EqualTo(e.Parriable), label + " Slam.Parriable");
            }
        }

        // ---------------------------------------------------------------- AC-1

        [Test]
        public void test_game_data_enemy_data_fields_round_trip_with_gdd_types()
        {
            // Arrange
            EnemyDataSpec levy = EnemyDataFixtures.Levy();

            // Act
            EnemyDataSO data = EnemyDataSO.CreateForTests(levy);

            // Assert
            Assert.That(typeof(EnemyDataSO).GetProperty(nameof(EnemyDataSO.MaxHp)).PropertyType, Is.EqualTo(typeof(int)));
            Assert.That(typeof(EnemyDataSO).GetProperty(nameof(EnemyDataSO.AttackDamage)).PropertyType, Is.EqualTo(typeof(int)));
            AssertMatches(data, levy, "Levy (in-memory)");
            Object.DestroyImmediate(data);
        }

        // ---------------------------------------------------------------- AC-2

        [Test]
        public void test_game_data_enemy_data_exactly_eight_assets_exist_in_game_data_folder()
        {
            // Act
            string[] names = AssetDatabase.FindAssets("t:" + nameof(EnemyDataSO), new[] { EnemyDataFixtures.AssetFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(Path.GetFileNameWithoutExtension)
                .OrderBy(n => n)
                .ToArray();

            // Assert
            Assert.That(names, Is.EqualTo(EnemyDataFixtures.All().Keys.OrderBy(n => n).ToArray()));
        }

        [Test]
        public void test_game_data_enemy_data_every_asset_matches_gdd_default_fixture()
        {
            foreach (var pair in EnemyDataFixtures.All())
            {
                // Arrange / Act
                EnemyDataSO asset = LoadAsset(pair.Key);

                // Assert
                AssertMatches(asset, pair.Value, pair.Key);
            }
        }

        [Test]
        public void test_game_data_enemy_data_iron_captain_tiers_differ_only_in_hp_and_counter_telegraph()
        {
            // Arrange
            EnemyDataSO t5 = LoadAsset("IronCaptain5");
            EnemyDataSO t10 = LoadAsset("IronCaptain10");
            EnemyDataSO t15 = LoadAsset("IronCaptain15");

            // Act
            t5.TryGetCounterHit(out CounterHitData c5);
            t10.TryGetCounterHit(out CounterHitData c10);
            t15.TryGetCounterHit(out CounterHitData c15);

            // Assert
            Assert.That(new[] { t5.MaxHp, t10.MaxHp, t15.MaxHp }, Is.EqualTo(new[]
            {
                EnemyDataFixtures.IronCaptain5().MaxHp, EnemyDataFixtures.IronCaptain10().MaxHp, EnemyDataFixtures.IronCaptain15().MaxHp,
            }));
            Assert.That(new[] { c5.TelegraphDurationMs, c10.TelegraphDurationMs, c15.TelegraphDurationMs }, Is.EqualTo(new[]
            {
                EnemyDataFixtures.IronCaptain5().CounterHit.Value.TelegraphDurationMs,
                EnemyDataFixtures.IronCaptain10().CounterHit.Value.TelegraphDurationMs,
                EnemyDataFixtures.IronCaptain15().CounterHit.Value.TelegraphDurationMs,
            }));
            Assert.That(t10.AttackDamage, Is.EqualTo(t5.AttackDamage));
            Assert.That(t15.AttackCooldownMs, Is.EqualTo(t5.AttackCooldownMs));
            Assert.That(c10.Damage, Is.EqualTo(c5.Damage));
        }

        // ---------------------------------------------------------------- AC-3

        [Test]
        public void test_game_data_enemy_data_basic_infantry_has_no_optional_groups()
        {
            // Arrange
            EnemyDataSO levy = LoadAsset("Levy");

            // Act / Assert
            Assert.That(levy.TryGetCounterHit(out _), Is.False);
            Assert.That(levy.TryGetStagger(out _), Is.False);
            Assert.That(levy.TryGetSlam(out _), Is.False);
        }

        [Test]
        public void test_game_data_enemy_data_iron_captain_has_counter_hit_but_no_slam()
        {
            // Arrange
            EnemyDataSO captain = LoadAsset("IronCaptain5");
            CounterHitData expected = EnemyDataFixtures.IronCaptain5().CounterHit.Value;

            // Act
            bool hasCounter = captain.TryGetCounterHit(out CounterHitData counter);
            bool hasSlam = captain.TryGetSlam(out _);

            // Assert
            Assert.That(hasCounter, Is.True);
            Assert.That(counter.Damage, Is.EqualTo(expected.Damage));
            Assert.That(counter.TelegraphDurationMs, Is.EqualTo(expected.TelegraphDurationMs));
            Assert.That(hasSlam, Is.False);
        }

        [Test]
        public void test_game_data_enemy_data_only_warlord_p2_has_a_non_parriable_slam()
        {
            // Act
            string[] withSlam = EnemyDataFixtures.All().Keys.Where(name => LoadAsset(name).TryGetSlam(out _)).ToArray();
            LoadAsset("WarlordP2").TryGetSlam(out SlamData slam);
            SlamData expected = EnemyDataFixtures.WarlordP2().Slam.Value;

            // Assert
            Assert.That(withSlam, Is.EqualTo(new[] { "WarlordP2" }));
            Assert.That(slam.Damage, Is.EqualTo(expected.Damage));
            Assert.That(slam.Radius, Is.EqualTo(expected.Radius).Within(Tolerance));
            Assert.That(slam.TelegraphDurationMs, Is.EqualTo(expected.TelegraphDurationMs));
            Assert.That(slam.Parriable, Is.False);
        }

        [Test]
        public void test_game_data_enemy_data_absent_groups_return_default_values()
        {
            // Arrange
            EnemyDataSO data = EnemyDataSO.CreateForTests(EnemyDataFixtures.Levy());

            // Act
            data.TryGetCounterHit(out CounterHitData counter);
            data.TryGetSpawnWeight(out float spawnWeight);

            // Assert
            Assert.That(counter.Damage, Is.EqualTo(0));
            Assert.That(spawnWeight, Is.EqualTo(0f));
            Assert.That(data.TryGetSpawnWeight(out _), Is.False, "no GDD spawn weight yet: unset, not a 0-sentinel");
            Object.DestroyImmediate(data);
        }

        // ---------------------------------------------------------------- AC-4

        [Test]
        public void test_game_data_enemy_data_warlord_phases_coexist_as_serialized_references()
        {
            // Arrange
            EnemyDataSO p1 = LoadAsset("WarlordP1");
            EnemyDataSO p2 = LoadAsset("WarlordP2");
            var holder = ScriptableObject.CreateInstance<WarlordPhaseHolder>();
            var serialized = new SerializedObject(holder);

            // Act
            serialized.FindProperty(nameof(WarlordPhaseHolder.Phase1)).objectReferenceValue = p1;
            serialized.FindProperty(nameof(WarlordPhaseHolder.Phase2)).objectReferenceValue = p2;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            // Assert
            Assert.That(holder.Phase1, Is.Not.Null);
            Assert.That(holder.Phase2, Is.Not.Null);
            Assert.That(holder.Phase1, Is.Not.SameAs(holder.Phase2));
            Assert.That(holder.Phase1.MoveSpeed, Is.EqualTo(EnemyDataFixtures.WarlordP1().MoveSpeed).Within(Tolerance));
            Assert.That(holder.Phase2.MoveSpeed, Is.EqualTo(EnemyDataFixtures.WarlordP2().MoveSpeed).Within(Tolerance));
            Assert.That(holder.Phase1.AttackCooldownMs, Is.EqualTo(EnemyDataFixtures.WarlordP1().AttackCooldownMs));
            Assert.That(holder.Phase2.AttackCooldownMs, Is.EqualTo(EnemyDataFixtures.WarlordP2().AttackCooldownMs));
            Object.DestroyImmediate(holder);
        }

        // ---------------------------------------------------------------- AC-5

        [Test]
        public void test_game_data_enemy_data_phase_two_trigger_hp_at_default_ratio()
        {
            // Arrange
            EnemyDataSO warlord = LoadAsset("WarlordP1");

            // Act
            float trigger = warlord.PhaseTwoTriggerHp(EnemyDataFixtures.DefaultPhase2ThresholdRatio);

            // Assert (800 × 0.5)
            Assert.That(trigger, Is.EqualTo(400f).Within(Tolerance));
        }

        [TestCase(0.3f, 240f)]
        [TestCase(0.7f, 560f)]
        public void test_game_data_enemy_formulas_phase_two_trigger_hp_boundaries(float ratio, float expected)
        {
            // Act (boundary-value test: literals are the point)
            float trigger = EnemyFormulas.PhaseTwoTriggerHp(800, ratio);

            // Assert
            Assert.That(trigger, Is.EqualTo(expected).Within(Tolerance));
        }

        // ---------------------------------------------------------------- AC-6

        [Test]
        public void test_game_data_enemy_data_public_api_is_read_only()
        {
            // Arrange
            const BindingFlags Public = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.DeclaredOnly;
            System.Type[] types = { typeof(EnemyDataSO), typeof(CounterHitData), typeof(StaggerData), typeof(SlamData) };

            // Act
            string[] setters = types.SelectMany(t => t.GetProperties(Public)).Where(p => p.GetSetMethod() != null).Select(p => p.Name).ToArray();
            string[] fields = types.SelectMany(t => t.GetFields(Public)).Select(f => f.Name).ToArray();
            MethodInfo testInit = typeof(EnemyDataSO).GetMethod("InitializeForTests", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

            // Assert
            Assert.That(setters, Is.Empty);
            Assert.That(fields, Is.Empty);
            Assert.That(testInit, Is.Not.Null);
            Assert.That(testInit.IsPublic, Is.False, "test-only initializer must not be public");
        }
    }
}

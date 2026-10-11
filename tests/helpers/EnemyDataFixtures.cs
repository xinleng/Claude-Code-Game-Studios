using System.Collections.Generic;
using Dynasty.GameData;

namespace Dynasty.Tests.Helpers
{
    /// <summary>
    /// GDD-default enemy values (enemy-definition.md stat/telegraph/cooldown/xp tables, mirrored in
    /// design/registry/entities.yaml) as test fixtures — the single place tests read expected enemy
    /// values from (no inline magic numbers). Keys are the authored asset names under
    /// <see cref="AssetFolder"/>. Warlord-P2 standard/counter values repeat Phase 1 until the designer
    /// tables them separately (GD-001 open item).
    /// </summary>
    /// <example>
    /// <code>
    /// EnemyDataSO levy = EnemyDataSO.CreateForTests(EnemyDataFixtures.Levy());
    /// Assert.That(levy.MaxHp, Is.EqualTo(EnemyDataFixtures.Levy().MaxHp));
    /// </code>
    /// </example>
    public static class EnemyDataFixtures
    {
        /// <summary>Project folder holding the eight authored EnemyDataSO assets.</summary>
        public const string AssetFolder = "Assets/GameData/Enemies";

        /// <summary>Default Warlord Phase 2 threshold ratio (enemy-definition Tuning Knobs).</summary>
        public const float DefaultPhase2ThresholdRatio = 0.5f;

        internal static EnemyDataSpec Levy() => Basic(EnemyType.Levy, 30, 1.5f, 5, 0.8f, TelegraphShape.HorizontalRing, 400, 2000, 5f, 1);

        internal static EnemyDataSpec Pikeman() => Basic(EnemyType.Pikeman, 40, 2.0f, 8, 1.8f, TelegraphShape.DirectionalLine, 350, 1800, 5f, 2);

        internal static EnemyDataSpec Archer() => Basic(EnemyType.Archer, 25, 0.8f, 10, 6.0f, TelegraphShape.ArrowheadAbove, 600, 2500, 8f, 2);

        internal static EnemyDataSpec IronCaptain5() => IronCaptain(300, 600);

        internal static EnemyDataSpec IronCaptain10() => IronCaptain(375, 550);

        internal static EnemyDataSpec IronCaptain15() => IronCaptain(450, 450);

        internal static EnemyDataSpec WarlordP1() => Warlord(1.2f, 2500, null);

        internal static EnemyDataSpec WarlordP2() => Warlord(2.0f, 1800, EnemyDataSpec.MakeSlam(40, 2.0f, 800, parriable: false));

        /// <summary>All eight fixtures keyed by authored asset name (file name without extension).</summary>
        internal static IReadOnlyDictionary<string, EnemyDataSpec> All()
        {
            return new Dictionary<string, EnemyDataSpec>
            {
                { "Levy", Levy() },
                { "Pikeman", Pikeman() },
                { "Archer", Archer() },
                { "IronCaptain5", IronCaptain5() },
                { "IronCaptain10", IronCaptain10() },
                { "IronCaptain15", IronCaptain15() },
                { "WarlordP1", WarlordP1() },
                { "WarlordP2", WarlordP2() },
            };
        }

        private static EnemyDataSpec Basic(EnemyType type, int hp, float speed, int damage, float range, TelegraphShape shape, int telegraphMs, int cooldownMs, float aggro, int xp)
        {
            return new EnemyDataSpec
            {
                EnemyType = type,
                MaxHp = hp,
                MoveSpeed = speed,
                AttackDamage = damage,
                AttackRange = range,
                Parriable = true,
                StaggerRule = StaggerRule.AnyFinisher,
                TelegraphShape = shape,
                TelegraphDurationMs = telegraphMs,
                AttackCooldownMs = cooldownMs,
                AggroRange = aggro,
                XpWeight = xp,
            };
        }

        private static EnemyDataSpec IronCaptain(int hp, int counterTelegraphMs)
        {
            return new EnemyDataSpec
            {
                EnemyType = EnemyType.IronCaptain,
                MaxHp = hp,
                MoveSpeed = 1.6f,
                AttackDamage = 12,
                AttackRange = 1.0f,
                Parriable = true,
                StaggerRule = StaggerRule.FullComboOnly,
                TelegraphShape = TelegraphShape.ThickRing,
                TelegraphDurationMs = 500,
                AttackCooldownMs = 3000,
                AggroRange = 6f,
                XpWeight = 15,
                CounterHit = EnemyDataSpec.MakeCounterHit(25, 1.5f, counterTelegraphMs),
                Stagger = EnemyDataSpec.MakeStagger(800),
            };
        }

        private static EnemyDataSpec Warlord(float speed, int cooldownMs, SlamData? slam)
        {
            return new EnemyDataSpec
            {
                EnemyType = EnemyType.Warlord,
                MaxHp = 800,
                MoveSpeed = speed,
                AttackDamage = 20,
                AttackRange = 1.0f,
                Parriable = true,
                StaggerRule = StaggerRule.FullComboOnly,
                TelegraphShape = TelegraphShape.ThickRing,
                TelegraphDurationMs = 500,
                AttackCooldownMs = cooldownMs,
                AggroRange = 10f,
                XpWeight = 50,
                CounterHit = EnemyDataSpec.MakeCounterHit(30, 1.5f, 450),
                Stagger = EnemyDataSpec.MakeStagger(1000),
                Slam = slam,
            };
        }
    }
}

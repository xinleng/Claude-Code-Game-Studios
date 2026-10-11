namespace Dynasty.GameData
{
    /// <summary>
    /// Plain value bag for building an <see cref="EnemyDataSO"/> in memory (test construction path,
    /// ADR-0010 Decision 3). Internal: visible to the test assemblies only. Optional groups are nullable
    /// here because this type is never serialized by Unity.
    /// </summary>
    internal sealed class EnemyDataSpec
    {
        public EnemyType EnemyType;
        public int MaxHp;
        public float MoveSpeed;
        public int AttackDamage;
        public float AttackRange;
        public bool Parriable;
        public StaggerRule StaggerRule;
        public TelegraphShape TelegraphShape;
        public int TelegraphDurationMs;
        public int AttackCooldownMs;
        public float AggroRange;
        public int XpWeight;
        public float? SpawnWeight;
        public CounterHitData? CounterHit;
        public StaggerData? Stagger;
        public SlamData? Slam;

        public static CounterHitData MakeCounterHit(int damage, float range, int telegraphDurationMs)
        {
            return new CounterHitData(damage, range, telegraphDurationMs, TelegraphShape.Arc270);
        }

        public static StaggerData MakeStagger(int durationMs)
        {
            return new StaggerData(durationMs);
        }

        public static SlamData MakeSlam(int damage, float radius, int telegraphDurationMs, bool parriable)
        {
            return new SlamData(damage, radius, telegraphDurationMs, TelegraphShape.FilledCircle, parriable);
        }
    }
}

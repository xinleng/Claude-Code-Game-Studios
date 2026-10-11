using UnityEngine;

namespace Dynasty.GameData
{
    /// <summary>
    /// Per-type/tier enemy data (ADR-0006 §1, enemy-definition.md). One asset each for Levy, Pikeman,
    /// Archer, IronCaptain-5/10/15, Warlord-P1 and Warlord-P2. Read-only to gameplay code: every value is
    /// a get-only property over a private serialized field. Elite/boss-only groups are guarded by
    /// has-value flags and read through <c>TryGet…</c> accessors — never 0-sentinels. Consumers receive
    /// assets by injection or serialized reference; no <c>Resources.Load</c>, no Addressables.
    /// </summary>
    /// <example>
    /// <code>
    /// [SerializeField] private EnemyDataSO _phase1Data;   // Warlord prefab holds both phases
    /// [SerializeField] private EnemyDataSO _phase2Data;
    /// float trigger = _phase1Data.PhaseTwoTriggerHp(tuning.Phase2ThresholdRatio);
    /// </code>
    /// </example>
    [CreateAssetMenu(menuName = "Dynasty/Game Data/Enemy Data", fileName = "EnemyData")]
    public sealed class EnemyDataSO : ScriptableObject
    {
        [SerializeField] private EnemyType _enemyType;
        [SerializeField] private int _maxHp;
        [SerializeField] private float _moveSpeed;
        [SerializeField] private int _attackDamage;
        [SerializeField] private float _attackRange;
        [SerializeField] private bool _parriable;
        [SerializeField] private StaggerRule _staggerRule;
        [SerializeField] private TelegraphShape _telegraphShape;
        [SerializeField] private int _telegraphDurationMs;
        [SerializeField] private int _attackCooldownMs;
        [SerializeField] private float _aggroRange;
        [SerializeField] private int _xpWeight;

        [Tooltip("Spawn weight has no GDD value yet (designer to supply); unset until then.")]
        [SerializeField] private bool _hasSpawnWeight;
        [SerializeField] private float _spawnWeight;

        [SerializeField] private bool _hasCounterHit;
        [SerializeField] private CounterHitData _counterHit;

        [SerializeField] private bool _hasStagger;
        [SerializeField] private StaggerData _stagger;

        [SerializeField] private bool _hasSlam;
        [SerializeField] private SlamData _slam;

        /// <summary>Enemy type this asset describes.</summary>
        public EnemyType EnemyType => _enemyType;

        /// <summary>Maximum HP (int — the GDD HP type contract; all HP arithmetic is integer).</summary>
        public int MaxHp => _maxHp;

        /// <summary>Move speed in world units per second.</summary>
        public float MoveSpeed => _moveSpeed;

        /// <summary>Standard attack damage (int).</summary>
        public int AttackDamage => _attackDamage;

        /// <summary>Standard attack reach in world units.</summary>
        public float AttackRange => _attackRange;

        /// <summary>Whether the standard attack (and counter-hit, if any) can be parried.</summary>
        public bool Parriable => _parriable;

        /// <summary>Which Finisher staggers this enemy.</summary>
        public StaggerRule StaggerRule => _staggerRule;

        /// <summary>Standard attack telegraph shape.</summary>
        public TelegraphShape TelegraphShape => _telegraphShape;

        /// <summary>Standard attack telegraph duration in milliseconds (also the parry window source).</summary>
        public int TelegraphDurationMs => _telegraphDurationMs;

        /// <summary>Attack cooldown in milliseconds (base value before per-spawn jitter).</summary>
        public int AttackCooldownMs => _attackCooldownMs;

        /// <summary>Aggro range in world units.</summary>
        public float AggroRange => _aggroRange;

        /// <summary>XP weight awarded on kill (enemy-definition Formula 1).</summary>
        public int XpWeight => _xpWeight;

        /// <summary>
        /// Reads the spawn weight used by the Wave Spawner. Returns false until the designer authors a
        /// value (no GDD default exists yet).
        /// </summary>
        /// <param name="spawnWeight">The spawn weight when present; 0 otherwise.</param>
        /// <returns>True if a spawn weight is authored.</returns>
        public bool TryGetSpawnWeight(out float spawnWeight)
        {
            spawnWeight = _hasSpawnWeight ? _spawnWeight : 0f;
            return _hasSpawnWeight;
        }

        /// <summary>Reads the counter-hit attack; false for enemies without one (basic infantry).</summary>
        /// <param name="counterHit">The counter-hit data when present; default otherwise.</param>
        /// <returns>True if this enemy has a counter-hit.</returns>
        public bool TryGetCounterHit(out CounterHitData counterHit)
        {
            counterHit = _hasCounterHit ? _counterHit : default;
            return _hasCounterHit;
        }

        /// <summary>Reads the stagger duration; false for enemies with no authored stagger duration.</summary>
        /// <param name="stagger">The stagger data when present; default otherwise.</param>
        /// <returns>True if this enemy has an authored stagger.</returns>
        public bool TryGetStagger(out StaggerData stagger)
        {
            stagger = _hasStagger ? _stagger : default;
            return _hasStagger;
        }

        /// <summary>Reads the ground slam; true only for the Warlord Phase 2 asset.</summary>
        /// <param name="slam">The slam data when present; default otherwise.</param>
        /// <returns>True if this enemy has a ground slam.</returns>
        public bool TryGetSlam(out SlamData slam)
        {
            slam = _hasSlam ? _slam : default;
            return _hasSlam;
        }

        /// <summary>
        /// Formula 3: <c>phase_2_trigger_hp = MaxHp × phase_2_threshold_ratio</c>. The ratio is the shared
        /// <c>TuningConfig</c> knob (game-data Story 003); nothing derived is cached on the asset.
        /// </summary>
        /// <param name="phase2ThresholdRatio">HP fraction at which Phase 2 fires; valid range (0, 1) exclusive.</param>
        /// <returns>The HP value at which the Phase 2 transition fires.</returns>
        public float PhaseTwoTriggerHp(float phase2ThresholdRatio)
        {
            return EnemyFormulas.PhaseTwoTriggerHp(_maxHp, phase2ThresholdRatio);
        }

        /// <summary>Test-only construction path (ADR-0010 Decision 3): copies a spec into the serialized fields.</summary>
        internal void InitializeForTests(EnemyDataSpec spec)
        {
            _enemyType = spec.EnemyType;
            _maxHp = spec.MaxHp;
            _moveSpeed = spec.MoveSpeed;
            _attackDamage = spec.AttackDamage;
            _attackRange = spec.AttackRange;
            _parriable = spec.Parriable;
            _staggerRule = spec.StaggerRule;
            _telegraphShape = spec.TelegraphShape;
            _telegraphDurationMs = spec.TelegraphDurationMs;
            _attackCooldownMs = spec.AttackCooldownMs;
            _aggroRange = spec.AggroRange;
            _xpWeight = spec.XpWeight;
            _hasSpawnWeight = spec.SpawnWeight.HasValue;
            _spawnWeight = spec.SpawnWeight ?? 0f;
            _hasCounterHit = spec.CounterHit.HasValue;
            _counterHit = spec.CounterHit ?? default;
            _hasStagger = spec.Stagger.HasValue;
            _stagger = spec.Stagger ?? default;
            _hasSlam = spec.Slam.HasValue;
            _slam = spec.Slam ?? default;
        }

        /// <summary>Test-only factory: an in-memory asset built from <paramref name="spec"/>.</summary>
        internal static EnemyDataSO CreateForTests(EnemyDataSpec spec)
        {
            var data = CreateInstance<EnemyDataSO>();
            data.InitializeForTests(spec);
            return data;
        }
    }
}

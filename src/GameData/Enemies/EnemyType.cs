namespace Dynasty.GameData
{
    /// <summary>The five MVP enemy types (enemy-definition.md roster). Tiers are separate assets of one type.</summary>
    /// <example><code>if (data.EnemyType == EnemyType.Warlord) { /* phase logic */ }</code></example>
    public enum EnemyType
    {
        /// <summary>Basic 1 — Levy Soldier.</summary>
        Levy = 0,

        /// <summary>Basic 2 — Halberd Pikeman.</summary>
        Pikeman = 1,

        /// <summary>Basic 3 — Fire Arrow Archer.</summary>
        Archer = 2,

        /// <summary>Elite — Iron Captain (5/10/15-minute tiers).</summary>
        IronCaptain = 3,

        /// <summary>Boss — Warlord (Phase 1 and Phase 2 assets).</summary>
        Warlord = 4,
    }
}

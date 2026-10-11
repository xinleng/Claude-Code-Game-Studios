namespace Dynasty.GameData
{
    /// <summary>
    /// Telegraph shapes from the enemy-definition telegraph table. Every shape is distinguishable by
    /// shape alone, never colour (Pillar 1 / colour-blind safety).
    /// </summary>
    /// <example><code>telegraphRenderer.Show(data.TelegraphShape, data.TelegraphDurationMs);</code></example>
    public enum TelegraphShape
    {
        /// <summary>Levy: horizontal ring glow around the torso.</summary>
        HorizontalRing = 0,

        /// <summary>Pikeman: directional lance glow from the weapon tip toward the player.</summary>
        DirectionalLine = 1,

        /// <summary>Archer: arrowhead indicator above the enemy (not a ground ring).</summary>
        ArrowheadAbove = 2,

        /// <summary>Iron Captain / Warlord standard attack: expanding thick ring.</summary>
        ThickRing = 3,

        /// <summary>Iron Captain / Warlord counter-hit: wide 270° ground arc.</summary>
        Arc270 = 4,

        /// <summary>Warlord Phase 2 ground slam: filled circle expanding outward (not a parry prompt).</summary>
        FilledCircle = 5,
    }
}

namespace Dynasty.GameData
{
    /// <summary>Pure enemy-definition formulas. No state; inputs come from config, never literals.</summary>
    /// <example><code>float trigger = EnemyFormulas.PhaseTwoTriggerHp(warlord.MaxHp, tuning.Phase2ThresholdRatio);</code></example>
    public static class EnemyFormulas
    {
        /// <summary>
        /// Formula 3: <c>phase_2_trigger_hp = warlord_max_hp × phase_2_threshold_ratio</c> (float, per the
        /// GDD variable table). Computed in double so e.g. 800 × 0.7 yields 560, not 559.99997. Range
        /// validation of the ratio belongs to <c>GameDataValidator</c> (game-data Story 005).
        /// </summary>
        /// <param name="warlordMaxHp">Warlord maximum HP.</param>
        /// <param name="phase2ThresholdRatio">HP fraction at which Phase 2 fires; valid range (0, 1) exclusive.</param>
        /// <returns>The HP value at which the Phase 2 transition fires.</returns>
        public static float PhaseTwoTriggerHp(int warlordMaxHp, float phase2ThresholdRatio)
        {
            return (float)(warlordMaxHp * (double)phase2ThresholdRatio);
        }
    }
}

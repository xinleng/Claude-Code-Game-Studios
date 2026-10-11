namespace Dynasty.GameData
{
    /// <summary>Which Finisher staggers this enemy (enemy-definition.md stagger rules).</summary>
    /// <example><code>bool staggers = data.StaggerRule == StaggerRule.AnyFinisher || comboCounter == 2;</code></example>
    public enum StaggerRule
    {
        /// <summary>Any Finisher staggers (basic infantry).</summary>
        AnyFinisher = 0,

        /// <summary>Only a full-combo Finisher (combo_counter == 2) staggers (Iron Captain, Warlord).</summary>
        FullComboOnly = 1,
    }
}

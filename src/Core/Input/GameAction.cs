namespace Dynasty.Core
{
    /// <summary>
    /// Player-facing actions for the control card (<see cref="IInputAuthority.GetActiveBindingDisplay"/>).
    /// <see cref="Submit"/> carries the GDD's "Confirm" bindings (ADR-0004 rule 8).
    /// </summary>
    /// <example><code>string label = input.GetActiveBindingDisplay(GameAction.Parry);   // e.g. "Space"</code></example>
    public enum GameAction
    {
        /// <summary>Gameplay: movement / steering.</summary>
        Move = 0,

        /// <summary>Gameplay: parry press (buffered).</summary>
        Parry = 1,

        /// <summary>Gameplay: dodge roll.</summary>
        Dodge = 2,

        /// <summary>UI: submit / confirm.</summary>
        Submit = 3,

        /// <summary>UI: cancel / back.</summary>
        Cancel = 4,
    }
}

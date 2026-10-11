namespace Dynasty.Core
{
    /// <summary>
    /// Input context set by RunSessionManager through <see cref="IInputAuthority.SetContext"/>
    /// (input-manager GDD States table). Exactly one action map is enabled per context.
    /// </summary>
    /// <example><code>input.SetContext(InputContext.Paused);   // level-up pick: UI map only</code></example>
    public enum InputContext
    {
        /// <summary>Run in progress: Gameplay map enabled, UI map disabled.</summary>
        Gameplay = 0,

        /// <summary>Menus (selection, win/death screens): UI map enabled, Gameplay map disabled.</summary>
        Menu = 1,

        /// <summary>Paused (level-up pick): UI map enabled, Gameplay map disabled; parry buffer frozen.</summary>
        Paused = 2,
    }
}

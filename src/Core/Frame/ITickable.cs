namespace Dynasty.Core
{
    /// <summary>
    /// Frame step for a combat subsystem advanced by scaled delta time (ADR-0002). Dodge, CombatCore
    /// and Combo implement it in their own epics; <see cref="CombatFrame"/> calls it once per frame.
    /// Implementations never declare their own <c>Update()</c>.
    /// </summary>
    /// <example>
    /// <code>
    /// public sealed class ComboSystem : IComboSystem, ITickable
    /// {
    ///     public void Tick(float dt) { /* counter timeout, Finisher on 3rd swing */ }
    /// }
    /// </code>
    /// </example>
    public interface ITickable
    {
        /// <summary>Advances the subsystem by one frame.</summary>
        /// <param name="dt">Scaled frame delta in seconds; 0 while paused.</param>
        void Tick(float dt);
    }
}

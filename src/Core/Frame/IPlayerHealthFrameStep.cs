namespace Dynasty.Core
{
    /// <summary>
    /// Player-health frame step (ADR-0002): applies unsuppressed damage, raises DamageTaken, advances
    /// i-frames. Runs after Combo so parry suppression and the Finisher are already resolved.
    /// </summary>
    /// <example>
    /// <code>
    /// public sealed class PlayerHealthSystem : IPlayerHealth, IPlayerHealthFrameStep
    /// {
    ///     public void Apply(float dt) { /* ordered DamageTaken dispatch */ }
    /// }
    /// </code>
    /// </example>
    public interface IPlayerHealthFrameStep
    {
        /// <summary>Applies this frame's pending damage and regen.</summary>
        /// <param name="dt">Scaled frame delta in seconds; 0 while paused.</param>
        void Apply(float dt);
    }
}

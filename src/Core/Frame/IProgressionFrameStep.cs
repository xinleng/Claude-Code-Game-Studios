namespace Dynasty.Core
{
    /// <summary>
    /// Progression frame step (ADR-0002): checks XP thresholds and raises level-up requests. Runs last
    /// in the combat sub-order. Takes no delta time.
    /// </summary>
    /// <example>
    /// <code>
    /// public sealed class ProgressionSystem : IProgressionFrameStep
    /// {
    ///     public void Tick() { /* XP thresholds → level-up request */ }
    /// }
    /// </code>
    /// </example>
    public interface IProgressionFrameStep
    {
        /// <summary>Evaluates progression thresholds for this frame.</summary>
        void Tick();
    }
}

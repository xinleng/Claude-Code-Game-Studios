namespace Dynasty.Core
{
    /// <summary>
    /// Parry frame step (ADR-0002): consumes buffered presses against open attack windows and
    /// suppresses parried hits. Runs first in the combat sub-order.
    /// </summary>
    /// <example>
    /// <code>
    /// public sealed class ParrySystem : IParrySystem, IParryFrameStep
    /// {
    ///     public void Evaluate() { /* TryConsumeParryPress vs open windows */ }
    /// }
    /// </code>
    /// </example>
    public interface IParryFrameStep
    {
        /// <summary>Resolves this frame's parry presses against open windows.</summary>
        void Evaluate();
    }
}

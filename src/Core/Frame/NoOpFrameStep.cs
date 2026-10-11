namespace Dynasty.Core
{
    /// <summary>
    /// Stand-in for a <see cref="CombatFrame"/> slot whose system is not implemented yet (BCF-005;
    /// ADR-0002: the slot order never changes, unbuilt slots get a no-op). Stateless; one shared instance
    /// may fill several slots.
    /// </summary>
    /// <example>
    /// <code>
    /// var noOp = new NoOpFrameStep();
    /// combatFrame.Initialize(clock, noOp, noOp, noOp, noOp, noOp, noOp);
    /// </code>
    /// </example>
    public sealed class NoOpFrameStep : IParryFrameStep, ITickable, IPlayerHealthFrameStep, IProgressionFrameStep
    {
        /// <inheritdoc />
        public void Evaluate()
        {
        }

        /// <inheritdoc />
        public void Tick(float dt)
        {
        }

        /// <inheritdoc />
        public void Apply(float dt)
        {
        }

        /// <inheritdoc />
        void IProgressionFrameStep.Tick()
        {
        }
    }
}

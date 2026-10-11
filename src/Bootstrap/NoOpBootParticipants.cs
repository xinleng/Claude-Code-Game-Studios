using Dynasty.Core;

namespace Dynasty.Bootstrap
{
    /// <summary>
    /// Placeholder boot participants for phases whose owning epic has not landed (BCF-005: the phase still
    /// runs in order, it just does nothing). Replace each with the real implementation as its epic lands:
    /// GameDataValidator (game-data), UiInputModuleBinder (input-manager 008), ObjectPool once TuningConfig and
    /// enemy prefabs exist (game-data 003, enemy AI), RunSessionManager (run-session-manager).
    /// </summary>
    /// <example><code>var p = new NoOpBootParticipants(); var boot = new BootSequence(p, root, p, p);</code></example>
    public sealed class NoOpBootParticipants : IGameDataValidation, IInputModuleBinding, IPoolPrewarm, IRunSessionEntry
    {
        /// <inheritdoc />
        public void ValidateAll()
        {
        }

        /// <inheritdoc />
        public void Bind()
        {
        }

        /// <inheritdoc />
        public void Prewarm()
        {
        }

        /// <inheritdoc />
        public void EnterSelection()
        {
        }
    }
}

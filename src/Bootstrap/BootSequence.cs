using System;
using Dynasty.Core;

namespace Dynasty.Bootstrap
{
    /// <summary>
    /// The one-time, synchronous boot order (ADR-0012 Decision 2): (a) GameData validation →
    /// (b) system construction + interface wiring, ending with the UI input-module bind → (c) object-pool
    /// pre-warm → (d) RSM enters <c>Selection</c>. Plain C# so the order is testable with recording fakes.
    /// Validation errors never stop boot (clamp-and-continue, ADR-0006 §5). Main thread only.
    /// </summary>
    /// <example>
    /// <code>
    /// var boot = new BootSequence(validation, compositionRoot, pool, runSession);
    /// boot.Run();   // second call is a no-op
    /// </code>
    /// </example>
    public sealed class BootSequence
    {
        private readonly IGameDataValidation _validation;
        private readonly IBootWiring _wiring;
        private readonly IPoolPrewarm _poolPrewarm;
        private readonly IRunSessionEntry _runSession;

        /// <summary>Creates the sequence from its four phase participants.</summary>
        /// <exception cref="ArgumentNullException">Any participant is null (wire a no-op participant instead).</exception>
        public BootSequence(IGameDataValidation validation, IBootWiring wiring, IPoolPrewarm poolPrewarm, IRunSessionEntry runSession)
        {
            _validation = validation ?? throw new ArgumentNullException(nameof(validation));
            _wiring = wiring ?? throw new ArgumentNullException(nameof(wiring));
            _poolPrewarm = poolPrewarm ?? throw new ArgumentNullException(nameof(poolPrewarm));
            _runSession = runSession ?? throw new ArgumentNullException(nameof(runSession));
        }

        /// <summary>True once <see cref="Run"/> has executed the phases.</summary>
        public bool HasRun { get; private set; }

        /// <summary>Runs the four phases in order, once. Later calls do nothing.</summary>
        public void Run()
        {
            if (HasRun)
            {
                return;
            }

            HasRun = true;
            _validation.ValidateAll();
            _wiring.Wire();
            _poolPrewarm.Prewarm();
            _runSession.EnterSelection();
        }
    }
}

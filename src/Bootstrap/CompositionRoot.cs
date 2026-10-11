using System;
using Dynasty.Core;

namespace Dynasty.Bootstrap
{
    /// <summary>
    /// Boot phase (b): the composition root (ADR-0002 wiring, ADR-0012). Receives the infrastructure
    /// built before validation — the single clock and the single designer-error channel — and injects the
    /// same instances everywhere. Today it initialises <see cref="CombatFrame"/> with a no-op step in every
    /// slot whose system is not built yet (the slot order never changes), then binds the UI input module.
    /// Later epics add their systems here (InputManager once TuningConfig exists — game-data Story 003;
    /// RSM receives <see cref="IRunClockControl"/>, nothing else does). Interfaces only; no lookups.
    /// </summary>
    /// <example>
    /// <code>
    /// var root = new CompositionRoot(clock, designerErrors, combatFrame, new NoOpInputModuleBinding());
    /// root.Wire();
    /// </code>
    /// </example>
    public sealed class CompositionRoot : IBootWiring
    {
        private readonly IGameClock _clock;
        private readonly IDesignerErrorLog _designerErrors;
        private readonly CombatFrame _combatFrame;
        private readonly IInputModuleBinding _inputModuleBinding;

        /// <summary>Creates the root over the boot infrastructure and serialized scene references.</summary>
        /// <param name="clock">The single run clock (also the run-clock control, reserved for RSM).</param>
        /// <param name="designerErrors">The single designer-error channel.</param>
        /// <param name="combatFrame">Scene CombatFrame (serialized reference on Bootstrap).</param>
        /// <param name="inputModuleBinding">UI input-module binder (no-op until input-manager Story 008).</param>
        public CompositionRoot(IGameClock clock, IDesignerErrorLog designerErrors, CombatFrame combatFrame, IInputModuleBinding inputModuleBinding)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _designerErrors = designerErrors ?? throw new ArgumentNullException(nameof(designerErrors));
            _combatFrame = combatFrame != null ? combatFrame : throw new ArgumentNullException(nameof(combatFrame));
            _inputModuleBinding = inputModuleBinding ?? throw new ArgumentNullException(nameof(inputModuleBinding));
        }

        /// <summary>The clock injected into every system (diagnostics and tests).</summary>
        public IGameClock Clock => _clock;

        /// <summary>The designer-error channel injected into every consumer (diagnostics and tests).</summary>
        public IDesignerErrorLog DesignerErrors => _designerErrors;

        /// <inheritdoc />
        public void Wire()
        {
            var noOp = new NoOpFrameStep();

            // Canonical slot order (ADR-0002): parry, dodge, combatCore, combo, playerHealth, progression.
            _combatFrame.Initialize(_clock, noOp, noOp, noOp, noOp, noOp, noOp);

            _inputModuleBinding.Bind();
        }
    }
}

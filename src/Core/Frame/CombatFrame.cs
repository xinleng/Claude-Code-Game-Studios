using System;
using UnityEngine;

namespace Dynasty.Core
{
    /// <summary>
    /// The combat tick orchestrator (ADR-0002, TR-DH-004, TR-IM-005, TR-TP-001). Runs the combat
    /// sub-order as explicit sequential calls: Parry → Dodge → CombatCore → Combo → PlayerHealth →
    /// Progression. The order is the code: there is no API to add, remove or reorder steps.
    /// <see cref="RunFrame"/> is the one true tick for both play mode and tests (ADR-0010 Decision 2);
    /// <c>Update()</c> only forwards the injected clock's scaled delta. Owns no gameplay state and
    /// performs no lookups; Bootstrap (Story BCF-005) calls <see cref="Initialize"/> once, wiring a
    /// no-op step in any slot whose system is not built yet. Main thread only.
    /// </summary>
    /// <example>
    /// <code>
    /// var frame = go.AddComponent&lt;CombatFrame&gt;();
    /// frame.Initialize(clock, parry, dodge, combatCore, combo, playerHealth, progression);
    /// frame.RunFrame(0.016f);   // tests pump frames directly; play mode reaches the same method via Update()
    /// </code>
    /// </example>
    [DefaultExecutionOrder(ExecutionOrder.CombatFrame)]
    public sealed class CombatFrame : MonoBehaviour
    {
        private IGameClock _clock;
        private IParryFrameStep _parry;
        private ITickable _dodge;
        private ITickable _combatCore;
        private ITickable _combo;
        private IPlayerHealthFrameStep _playerHealth;
        private IProgressionFrameStep _progression;
        private bool _initialized;

        /// <summary>True once <see cref="Initialize"/> has run; before that, frames are no-ops.</summary>
        public bool IsInitialized => _initialized;

        /// <summary>
        /// Injects the clock and the six step slots in canonical order. May be called exactly once; a
        /// second call is rejected so the wiring (and therefore the order) cannot change at runtime.
        /// </summary>
        /// <param name="clock">Clock whose scaled <see cref="IGameClock.DeltaTime"/> drives play-mode frames.</param>
        /// <param name="parry">Step 1: parry evaluation.</param>
        /// <param name="dodge">Step 2: dodge/dash state.</param>
        /// <param name="combatCore">Step 3: locomotion, auto-attack, SwingLanded (owns the Finisher handshake).</param>
        /// <param name="combo">Step 4: combo counter timeout and Finisher.</param>
        /// <param name="playerHealth">Step 5: unsuppressed damage, DamageTaken, i-frames.</param>
        /// <param name="progression">Step 6: XP thresholds and level-up requests.</param>
        /// <exception cref="ArgumentNullException">Any argument is null (wire a no-op step instead).</exception>
        /// <exception cref="InvalidOperationException">Already initialized.</exception>
        public void Initialize(
            IGameClock clock,
            IParryFrameStep parry,
            ITickable dodge,
            ITickable combatCore,
            ITickable combo,
            IPlayerHealthFrameStep playerHealth,
            IProgressionFrameStep progression)
        {
            if (_initialized)
            {
                throw new InvalidOperationException("CombatFrame.Initialize may only be called once; the step order is fixed at boot.");
            }

            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _parry = parry ?? throw new ArgumentNullException(nameof(parry));
            _dodge = dodge ?? throw new ArgumentNullException(nameof(dodge));
            _combatCore = combatCore ?? throw new ArgumentNullException(nameof(combatCore));
            _combo = combo ?? throw new ArgumentNullException(nameof(combo));
            _playerHealth = playerHealth ?? throw new ArgumentNullException(nameof(playerHealth));
            _progression = progression ?? throw new ArgumentNullException(nameof(progression));
            _initialized = true;
        }

        /// <summary>
        /// Runs one combat frame: each step exactly once, in canonical order, with <paramref name="dt"/>
        /// passed unchanged to every dt-taking step. A zero-dt (paused) frame still runs every step.
        /// Does nothing before <see cref="Initialize"/>. Allocation-free.
        /// </summary>
        /// <param name="dt">Scaled frame delta in seconds.</param>
        public void RunFrame(float dt)
        {
            if (!_initialized)
            {
                return;
            }

            _parry.Evaluate();
            _dodge.Tick(dt);
            _combatCore.Tick(dt);
            _combo.Tick(dt);
            _playerHealth.Apply(dt);
            _progression.Tick();
        }

        private void Update()
        {
            if (_initialized)
            {
                RunFrame(_clock.DeltaTime);
            }
        }
    }
}

using Dynasty.Core;
using UnityEngine;

namespace Dynasty.Bootstrap
{
    /// <summary>
    /// The single composition root in <c>MainArena.unity</c> (component on the "Bootstrap" GameObject; named
    /// <c>Bootstrapper</c> because a type called <c>Bootstrap</c> would collide with the <c>Dynasty.Bootstrap</c> namespace) (ADR-0012 Decisions 1–2). Holds only serialized
    /// scene references; in <c>Awake</c> it builds the boot infrastructure (exactly one <see cref="UnityGameClock"/>
    /// and one <see cref="UnityDesignerErrorLog"/>), then runs <see cref="BootSequence"/> once. Owns no
    /// gameplay state and ticks nothing (no <c>Update</c>/<c>FixedUpdate</c>/<c>LateUpdate</c>); no
    /// <c>Find*</c>, no singletons. Every other scene object stays passive until wired here.
    /// </summary>
    /// <example>
    /// <code>
    /// // Scene: one "Bootstrap" GameObject with this component; its CombatFrame field points at the
    /// // scene's CombatFrame. Nothing else in the scene wires itself.
    /// </code>
    /// </example>
    [DisallowMultipleComponent]
    public sealed class Bootstrapper : MonoBehaviour
    {
        [SerializeField] private CombatFrame _combatFrame;

        private BootSequence _boot;
        private CompositionRoot _root;
        private UnityGameClock _clock;

        /// <summary>True once the boot sequence has completed.</summary>
        public bool HasBooted => _boot != null && _boot.HasRun;

        /// <summary>The single clock, as the run-clock control — reserved for RunSessionManager when it lands.</summary>
        internal IRunClockControl RunClockControl => _clock;

        /// <summary>The composition root (diagnostics and tests).</summary>
        internal CompositionRoot Root => _root;

        private void Awake()
        {
            if (_boot != null)
            {
                return;
            }

            _clock = new UnityGameClock();
            var designerErrors = new UnityDesignerErrorLog();
            var placeholders = new NoOpBootParticipants();
            _root = new CompositionRoot(_clock, designerErrors, _combatFrame, placeholders);
            _boot = new BootSequence(placeholders, _root, placeholders, placeholders);
            _boot.Run();
        }
    }
}

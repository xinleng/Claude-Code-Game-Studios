namespace Dynasty.Core
{
    /// <summary>
    /// The only <c>[DefaultExecutionOrder]</c> anchors in the project (ADR-0002). Structural constants,
    /// not gameplay tuning. Frame order: Input → EnemyManager → CombatFrame → Presentation.
    /// </summary>
    /// <example>
    /// <code>
    /// [DefaultExecutionOrder(ExecutionOrder.Input)]
    /// public sealed class InputManager : MonoBehaviour { ... }
    /// </code>
    /// </example>
    public static class ExecutionOrder
    {
        /// <summary>InputManager: capture and prune the parry buffer before anything reads it.</summary>
        public const int Input = -200;

        /// <summary>EnemySystemManager: two-phase enemy tick and AttackEvent emission.</summary>
        public const int EnemyManager = -100;

        /// <summary>CombatFrame: the combat sub-order Parry → Dodge → CombatCore → Combo → PlayerHealth → Progression.</summary>
        public const int CombatFrame = 0;

        /// <summary>Presentation: UI Toolkit presenters, VFX and audio triggers, after all combat state settles.</summary>
        public const int Presentation = 100;
    }
}

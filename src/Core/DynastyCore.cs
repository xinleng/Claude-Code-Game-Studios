namespace Dynasty.Core
{
    /// <summary>
    /// Identity of the <c>Dynasty.Core</c> runtime assembly. Lets tests and tooling prove the
    /// assembly is compiled and referenced; real Core types (IGameClock, CombatFrame) live alongside.
    /// </summary>
    public static class DynastyCore
    {
        /// <summary>Assembly name as declared in <c>Dynasty.Core.asmdef</c>.</summary>
        public const string AssemblyName = "Dynasty.Core";
    }
}

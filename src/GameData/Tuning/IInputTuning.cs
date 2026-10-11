namespace Dynasty.GameData
{
    /// <summary>
    /// Read-only input section of <c>TuningConfig</c> (ADR-0004 rules 1–2, ADR-0006 §4). Implemented by
    /// <c>TuningConfig</c> in game-data Story 003; tests supply an in-memory fixture. Range clamping
    /// (60–200 ms, capacity 2–8) is <c>GameDataValidator</c>'s job, not the consumer's.
    /// </summary>
    /// <example>
    /// <code>
    /// var buffer = new ParryPressBuffer(tuning);   // window = tuning.ParryInputBufferMs, size = tuning.ParryBufferCapacity
    /// </code>
    /// </example>
    public interface IInputTuning
    {
        /// <summary><c>parry_input_buffer_ms</c>: how long a parry press stays live (GDD default 120, safe range 60–200).</summary>
        int ParryInputBufferMs { get; }

        /// <summary><c>parry_buffer_capacity</c>: ring size; the oldest press is overwritten when full (default 4, range 2–8).</summary>
        int ParryBufferCapacity { get; }
    }
}

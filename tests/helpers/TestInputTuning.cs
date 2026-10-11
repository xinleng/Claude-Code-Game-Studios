using Dynasty.GameData;

namespace Dynasty.Tests.Helpers
{
    /// <summary>
    /// In-memory <see cref="IInputTuning"/> fixture (stand-in for <c>TestConfigs</c> until game-data
    /// Stories 003/004 land). <see cref="Default"/> holds the GDD defaults; tests never inline them.
    /// </summary>
    /// <example><code>var buffer = new ParryPressBuffer(TestInputTuning.Default());</code></example>
    public sealed class TestInputTuning : IInputTuning
    {
        /// <inheritdoc />
        public int ParryInputBufferMs { get; set; }

        /// <inheritdoc />
        public int ParryBufferCapacity { get; set; }

        /// <summary>GDD defaults: <c>parry_input_buffer_ms</c> 120, <c>parry_buffer_capacity</c> 4.</summary>
        public static TestInputTuning Default()
        {
            return new TestInputTuning { ParryInputBufferMs = 120, ParryBufferCapacity = 4 };
        }

        /// <summary>Defaults with a different capacity (e.g. the lower safe bound 2).</summary>
        public static TestInputTuning WithCapacity(int capacity)
        {
            TestInputTuning t = Default();
            t.ParryBufferCapacity = capacity;
            return t;
        }
    }
}

using System.Collections.Generic;
using Dynasty.Gameplay;

namespace Dynasty.Tests.Helpers
{
    /// <summary>
    /// Recording fake for <see cref="IEnemyRegistration"/> (stands in for EnemySystemManager until the
    /// Enemy AI epic). Optionally logs "Deregister" into a shared <see cref="EventRecorder"/> for call-order tests.
    /// </summary>
    /// <example>
    /// <code>
    /// var registration = new RecordingEnemyRegistration(recorder);
    /// pool.ReturnToPool(enemy);
    /// Assert.That(registration.Deregistered, Is.EqualTo(new[] { enemy }));
    /// </code>
    /// </example>
    public sealed class RecordingEnemyRegistration : IEnemyRegistration
    {
        private readonly EventRecorder _recorder;

        /// <summary>Creates the fake; <paramref name="recorder"/> (optional) receives "RegisterPending"/"Deregister".</summary>
        public RecordingEnemyRegistration(EventRecorder recorder = null)
        {
            _recorder = recorder;
        }

        /// <summary>Every staged enemy, in call order.</summary>
        public List<EnemyController> Pending { get; } = new List<EnemyController>();

        /// <summary>Every deregistered enemy, in call order.</summary>
        public List<EnemyController> Deregistered { get; } = new List<EnemyController>();

        /// <summary>The enemy's <c>activeSelf</c> at each <see cref="Deregister"/> call.</summary>
        public List<bool> ActiveAtDeregister { get; } = new List<bool>();

        /// <inheritdoc />
        public void RegisterPending(EnemyController enemy)
        {
            Pending.Add(enemy);
            _recorder?.Record("RegisterPending");
        }

        /// <inheritdoc />
        public void Deregister(EnemyController enemy)
        {
            Deregistered.Add(enemy);
            ActiveAtDeregister.Add(enemy.gameObject.activeSelf);
            _recorder?.Record("Deregister");
        }
    }
}

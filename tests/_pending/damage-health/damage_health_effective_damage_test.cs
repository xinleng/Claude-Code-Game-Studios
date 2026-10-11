// Example EditMode test — demonstrates the ADR-0010 deterministic-harness patterns.
// Compiles once the Unity project + EditModeTests.asmdef exist (pre-implementation scaffold).
// GDD source: design/gdd/damage-health.md — AC-DH-02/03/04 (effective damage formula).
using NUnit.Framework;

namespace DynastySurvivors.Tests.Unit.DamageHealth
{
    public class damage_health_effective_damage_test
    {
        // TestConfigs mirrors GDD defaults (ADR-0010 rule: no inline magic numbers,
        // except boundary-value tests where the exact number IS the point).
        private PlayerHealthSystem _health;
        private FakeGameClock _clock;

        [SetUp]
        public void SetUp()
        {
            _clock = new FakeGameClock();
            _health = new PlayerHealthSystem(TestConfigs.Default(), _clock);
        }

        [Test]
        public void test_no_reduction_full_damage_applied()
        {
            // AC-DH-02: 20 raw at 0.0 reduction → 20 effective, HP 100 → 80
            _health.ApplyDamage(rawDamage: 20f, attackerId: 1, DamageType.Standard);
            Assert.AreEqual(80f, _health.CurrentHp);
        }

        [Test]
        public void test_half_reduction_floors_result()
        {
            // AC-DH-03: floor(25 × 0.5) = 12 (boundary values from the GDD example)
            _health.SetDamageReductionForTest(0.5f);
            _health.ApplyDamage(rawDamage: 25f, attackerId: 1, DamageType.Standard);
            Assert.AreEqual(88f, _health.CurrentHp);
        }

        [Test]
        public void test_minimum_one_floor_overrides_floored_zero()
        {
            // AC-DH-04: max(1, floor(1 × 0.25)) = 1 — the player can never block all damage
            _health.SetDamageReductionForTest(0.75f);
            _health.ApplyDamage(rawDamage: 1f, attackerId: 1, DamageType.Standard);
            Assert.AreEqual(99f, _health.CurrentHp);
        }

        [Test]
        public void test_iframe_window_discards_second_hit_until_expiry()
        {
            // AC-DH-07: hit at t=0 opens 500ms iframes; hit at 0.2s discarded; hit at 0.501s lands.
            // Time advances via the injected clock — never WaitForSeconds (ADR-0010).
            _health.ApplyDamage(10f, 1, DamageType.Standard);      // t = 0.0 → HP 90
            _clock.Advance(0.2f);
            _health.ApplyDamage(10f, 2, DamageType.Standard);      // inside iframes → discarded
            Assert.AreEqual(90f, _health.CurrentHp);
            _clock.Advance(0.301f);                                 // t = 0.501 → iframes expired
            _health.ApplyDamage(10f, 3, DamageType.Standard);
            Assert.AreEqual(80f, _health.CurrentHp);
        }
    }
}

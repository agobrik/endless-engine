// Tests for Story S1-12: Damage System — Wave-Scaled Enemy Damage Caching
// Type: Logic (Unit/EditMode)
// Story: production/epics/damage-system/story-002-wave-scaled-enemy-damage.md
//
// These tests verify:
//   (1) AC-DMG-04: BaseDamage=20, WaveNumber=8, Exponent=1.5 → ScaledDamage=452
//   (2) AC-DMG-05: ContactDamage=5, WaveNumber=8, Exponent=1.5 → ScaledContactDamage=113
//   (3) AC-DMG-09: WaveNumber=0 → ScaledDamage=1 (minimum floor, no exception)
//   (4) WaveNumber=1 → result equals base (1^exponent = 1 for any exponent)
//   (5) Minimum floor is 1 for any tiny base value
//
// To run: Unity Test Runner → EditMode → EndlessEngine.Tests.Unit.DamageSystem

using NUnit.Framework;
using EndlessEngine.Damage;

namespace EndlessEngine.Tests.Unit.DamageSystemTests
{
    /// <summary>
    /// Unit tests for WaveScalingCalculator.ComputeScaledValue (S1-12 / Story 002).
    /// </summary>
    [TestFixture]
    public class WaveScaledEnemyDamageTests
    {
        // ── AC-DMG-04: Scaled attack damage ──────────────────────────────────────

        [Test]
        [Description("AC-DMG-04: BaseDamage=20, WaveNumber=8, Exponent=1.5 → 452.")]
        public void ComputeScaledValue_Wave8_AttackDamage_Returns452()
        {
            // 20 × 8^1.5 = 20 × 22.6274... = 452.548... → Floor = 452
            long result = WaveScalingCalculator.ComputeScaledValue(20f, 8, 1.5f);
            Assert.AreEqual(452L, result,
                "Floor(20 × 8^1.5) must be 452");
        }

        // ── AC-DMG-05: Scaled contact damage ─────────────────────────────────────

        [Test]
        [Description("AC-DMG-05: ContactDamage=5, WaveNumber=8, Exponent=1.5 → 113.")]
        public void ComputeScaledValue_Wave8_ContactDamage_Returns113()
        {
            // 5 × 8^1.5 = 5 × 22.6274... = 113.137... → Floor = 113
            long result = WaveScalingCalculator.ComputeScaledValue(5f, 8, 1.5f);
            Assert.AreEqual(113L, result,
                "Floor(5 × 8^1.5) must be 113");
        }

        // ── AC-DMG-09: Wave 0 floor ───────────────────────────────────────────────

        [Test]
        [Description("AC-DMG-09: WaveNumber=0 returns minimum 1 regardless of base value.")]
        public void ComputeScaledValue_WaveNumberZero_ReturnsOne()
        {
            long result = WaveScalingCalculator.ComputeScaledValue(50f, 0, 1.5f);
            Assert.AreEqual(1L, result,
                "WaveNumber=0 must return 1 (minimum floor; 0^exponent = 0)");
        }

        [Test]
        [Description("AC-DMG-09: Negative WaveNumber also returns 1.")]
        public void ComputeScaledValue_NegativeWaveNumber_ReturnsOne()
        {
            long result = WaveScalingCalculator.ComputeScaledValue(50f, -5, 1.5f);
            Assert.AreEqual(1L, result, "Negative WaveNumber must return 1");
        }

        [Test]
        [Description("AC-DMG-09: WaveNumber=1 returns exactly the base value (1^any = 1).")]
        public void ComputeScaledValue_WaveNumberOne_ReturnsBaseValue()
        {
            long result = WaveScalingCalculator.ComputeScaledValue(50f, 1, 1.5f);
            Assert.AreEqual(50L, result,
                "WaveNumber=1 must return Floor(50 × 1^1.5) = 50");
        }

        // ── Edge cases ────────────────────────────────────────────────────────────

        [Test]
        [Description("Very small base value still floors to 1 minimum.")]
        public void ComputeScaledValue_TinyBaseValue_ReturnsAtLeastOne()
        {
            long result = WaveScalingCalculator.ComputeScaledValue(0.001f, 5, 1.5f);
            Assert.AreEqual(1L, result,
                "Tiny base value must still produce at least 1 via minimum floor");
        }

        [Test]
        [Description("Exponent=0 means wave number has no effect — result equals Floor(baseValue).")]
        public void ComputeScaledValue_ExponentZero_IgnoresWaveNumber()
        {
            // base × wave^0 = base × 1 = base
            long result = WaveScalingCalculator.ComputeScaledValue(50f, 100, 0f);
            Assert.AreEqual(50L, result,
                "Exponent=0 should produce Floor(base × 1) = Floor(base) regardless of wave");
        }

        [Test]
        [Description("Large wave number produces large but not overflowing result.")]
        public void ComputeScaledValue_LargeWaveNumber_DoesNotOverflow()
        {
            // Wave 100, base 100, exponent 1.5: 100 × 100^1.5 = 100 × 1000 = 100000
            long result = WaveScalingCalculator.ComputeScaledValue(100f, 100, 1.5f);
            Assert.AreEqual(100000L, result,
                "Floor(100 × 100^1.5) = Floor(100 × 1000) = 100000");
        }

        // ── EnemyRuntimeData struct ───────────────────────────────────────────────

        [Test]
        [Description("EnemyRuntimeData is a value type (struct) for stack allocation.")]
        public void EnemyRuntimeData_IsValueType()
        {
            Assert.IsTrue(typeof(EnemyRuntimeData).IsValueType,
                "EnemyRuntimeData must be a struct for zero-allocation per-enemy data");
        }

        [Test]
        [Description("EnemyRuntimeData.ScaledDamage and ScaledHP are long type.")]
        public void EnemyRuntimeData_ScaledFields_AreLong()
        {
            Assert.AreEqual(typeof(long), typeof(EnemyRuntimeData).GetField("ScaledDamage").FieldType);
            Assert.AreEqual(typeof(long), typeof(EnemyRuntimeData).GetField("ScaledHP").FieldType);
            Assert.AreEqual(typeof(long), typeof(EnemyRuntimeData).GetField("ScaledContactDamage").FieldType);
        }
    }
}

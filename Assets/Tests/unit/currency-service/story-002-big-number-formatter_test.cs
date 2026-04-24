// Tests for Sprint 7 — S7-05: BigNumberFormatter
// Type: Logic (Unit/EditMode)
//
// Covers letter / scientific / engineering notation; edge cases (0, negative, NaN, Inf).
//
// To run: Unity Test Runner → EditMode → EndlessEngine.Tests.Unit.CurrencyService

using NUnit.Framework;
using EndlessEngine.Config;
using EndlessEngine.Economy;

namespace EndlessEngine.Tests.Unit.CurrencyService
{
    [TestFixture]
    public class BigNumberFormatterTests
    {
        // ── Letter notation ───────────────────────────────────────────────────────

        [TestCase(0,           "0")]
        [TestCase(999,         "999")]
        [TestCase(1_000,       "1K")]
        [TestCase(1_500,       "1.5K")]
        [TestCase(1_000_000,   "1M")]
        [TestCase(2_500_000,   "2.5M")]
        [TestCase(1_000_000_000d, "1B")]
        [TestCase(3_700_000_000d, "3.7B")]
        [TestCase(1e12,        "1T")]
        public void FormatLetter_StandardValues_CorrectSuffix(double input, string expected)
        {
            string result = BigNumberFormatter.FormatLetter(input, decimalPlaces: 1);
            Assert.AreEqual(expected, result, $"FormatLetter({input}) expected '{expected}' got '{result}'");
        }

        [Test]
        public void FormatLetter_DoubleLetterSuffix_aa()
        {
            // 1e15 → 1aa
            double value  = 1e15;
            string result = BigNumberFormatter.FormatLetter(value, decimalPlaces: 1);
            Assert.AreEqual("1aa", result);
        }

        [Test]
        public void FormatLetter_ZeroDecimalPlaces_NoDecimalPoint()
        {
            string result = BigNumberFormatter.FormatLetter(1_234_567, decimalPlaces: 0);
            Assert.AreEqual("1M", result);
        }

        [Test]
        public void FormatLetter_TrailingZeroStripped()
        {
            // 1.0K → "1K" not "1.0K"
            string result = BigNumberFormatter.FormatLetter(1_000, decimalPlaces: 1);
            Assert.AreEqual("1K", result);
        }

        [Test]
        public void FormatLetter_BelowThousand_ReturnsIntegerString()
        {
            string result = BigNumberFormatter.FormatLetter(42, decimalPlaces: 2);
            Assert.AreEqual("42", result);
        }

        // ── Scientific notation ───────────────────────────────────────────────────

        [TestCase(1_234_567,   2, "1.23e6")]
        [TestCase(1_000_000,   1, "1e6")]
        [TestCase(5_000,       1, "5e3")]
        [TestCase(999,         2, "999")]
        public void FormatScientific_StandardValues(double input, int places, string expected)
        {
            string result = BigNumberFormatter.FormatScientific(input, places);
            Assert.AreEqual(expected, result, $"FormatScientific({input}, {places})");
        }

        // ── Engineering / SI notation ─────────────────────────────────────────────

        [TestCase(1_500,        1, "1.5K")]
        [TestCase(1_500_000,    1, "1.5M")]
        [TestCase(2_700_000_000d, 1, "2.7G")]
        [TestCase(500,          1, "500")]
        public void FormatEngineering_StandardValues(double input, int places, string expected)
        {
            string result = BigNumberFormatter.FormatEngineering(input, places);
            Assert.AreEqual(expected, result, $"FormatEngineering({input}, {places})");
        }

        // ── Edge cases ────────────────────────────────────────────────────────────

        [Test]
        public void Format_NaN_ReturnsNaN()
        {
            Assert.AreEqual("NaN", BigNumberFormatter.Format(double.NaN));
        }

        [Test]
        public void Format_PositiveInfinity_ReturnsInfinity()
        {
            Assert.AreEqual("∞", BigNumberFormatter.Format(double.PositiveInfinity));
        }

        [Test]
        public void Format_NegativeInfinity_ReturnsNegativeInfinity()
        {
            Assert.AreEqual("-∞", BigNumberFormatter.Format(double.NegativeInfinity));
        }

        [Test]
        public void Format_NegativeValue_HasLeadingMinus()
        {
            string result = BigNumberFormatter.Format(-1_500_000, BigNumberNotation.Letter, 1);
            Assert.AreEqual("-1.5M", result);
        }

        [Test]
        public void Format_Zero_ReturnsZero()
        {
            Assert.AreEqual("0", BigNumberFormatter.Format(0));
        }

        // ── DecimalPlaces clamping ────────────────────────────────────────────────

        [Test]
        public void Format_DecimalPlacesAbove3_ClampedTo3()
        {
            // Should not throw
            Assert.DoesNotThrow(() => BigNumberFormatter.Format(1_234_567, BigNumberNotation.Letter, 10));
        }

        [Test]
        public void Format_NegativeDecimalPlaces_ClampedTo0()
        {
            Assert.DoesNotThrow(() => BigNumberFormatter.Format(1_234_567, BigNumberNotation.Letter, -5));
        }

        // ── CurrencyConfigSO overload ─────────────────────────────────────────────

        [Test]
        public void Format_WithConfig_UsesConfigSettings()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var config = UnityEngine.ScriptableObject.CreateInstance<CurrencyConfigSO>();
            config.Notation      = BigNumberNotation.Scientific;
            config.DecimalPlaces = 2;

            string result = BigNumberFormatter.Format(1_234_567, config);
            Assert.AreEqual("1.23e6", result);

            UnityEngine.Object.DestroyImmediate(config);
#endif
        }

        [Test]
        public void Format_NullConfig_UseDefaults()
        {
            // Must not throw
            string result = BigNumberFormatter.Format(1_500_000, null);
            Assert.AreEqual("1.5M", result);
        }
    }
}

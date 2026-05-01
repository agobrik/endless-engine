using NUnit.Framework;
using EndlessEngine.Config;
using EndlessEngine.Economy.Math;

namespace EndlessEngine.Tests.Economy
{
    /// <summary>
    /// Unit tests for BigDouble — the v1.3 mantissa×10^exponent numeric backend.
    ///
    /// Gate rule: all BigDouble tests must pass before enabling NumberBackend.BigDouble
    /// in any shipping EconomyConfigSO.
    /// </summary>
    [TestFixture]
    public class BigDoubleTests
    {
        private const double Tolerance = 1e-10;

        // ── Construction & normalisation ──────────────────────────────────────────

        [Test]
        public void Constructor_NormalisesLargeMantissa()
        {
            var n = new BigDouble(150.0, 0);
            Assert.AreEqual(2, n.Exponent, "150 = 1.5e2 → exponent should be 2");
            Assert.AreEqual(1.5, n.Mantissa, Tolerance);
        }

        [Test]
        public void Constructor_NormalisesSmallMantissa()
        {
            var n = new BigDouble(0.05, 0);
            Assert.AreEqual(-2, n.Exponent);
            Assert.AreEqual(5.0, n.Mantissa, Tolerance);
        }

        [Test]
        public void Constructor_Zero_IsNormalisedToZero()
        {
            var n = new BigDouble(0.0, 999);
            Assert.IsTrue(n.IsZero);
            Assert.AreEqual(0, n.Exponent);
        }

        [Test]
        public void Constructor_NegativeValue_PreservesSign()
        {
            var n = new BigDouble(-3.5, 2);
            Assert.IsTrue(n.IsNegative);
            Assert.AreEqual(-3.5, n.Mantissa, Tolerance);
        }

        [Test]
        public void StaticZero_IsZero()
        {
            Assert.IsTrue(BigDouble.Zero.IsZero);
        }

        [Test]
        public void StaticOne_IsOne()
        {
            Assert.AreEqual(1.0, BigDouble.One.ToDouble(), Tolerance);
        }

        // ── Arithmetic ────────────────────────────────────────────────────────────

        [Test]
        public void Add_SameExponent()
        {
            var a = new BigDouble(1.5, 3);
            var b = new BigDouble(2.5, 3);
            var result = a.Add(b);
            Assert.AreEqual(4000.0, result.ToDouble(), 1.0);
        }

        [Test]
        public void Add_DifferentExponents_SmallIsNegligible()
        {
            var big   = new BigDouble(1.0, 20);
            var small = new BigDouble(1.0, 0);
            var result = big.Add(small);
            // small is 18 orders of magnitude smaller — should be negligible
            Assert.AreEqual(big.Mantissa, result.Mantissa, Tolerance);
            Assert.AreEqual(big.Exponent, result.Exponent);
        }

        [Test]
        public void Add_Zero_ReturnsSelf()
        {
            var a = new BigDouble(3.14, 5);
            var result = a.Add(BigDouble.Zero);
            Assert.AreEqual(a.ToDouble(), result.ToDouble(), 1.0);
        }

        [Test]
        public void Subtract_ProducesCorrectResult()
        {
            var a = new BigDouble(5.0, 0);
            var b = new BigDouble(3.0, 0);
            Assert.AreEqual(2.0, a.Subtract(b).ToDouble(), Tolerance);
        }

        [Test]
        public void Multiply_ByScalar()
        {
            var n = new BigDouble(2.0, 5);
            var result = n.Multiply(3.0);
            Assert.AreEqual(6e5, result.ToDouble(), 1.0);
        }

        [Test]
        public void Multiply_TwoBigDoubles_ExponentsAdd()
        {
            var a = new BigDouble(2.0, 100);
            var b = new BigDouble(3.0, 200);
            var result = a.Multiply(b);
            Assert.AreEqual(300, result.Exponent);
            Assert.AreEqual(6.0, result.Mantissa, Tolerance);
        }

        [Test]
        public void Divide_ByScalar()
        {
            var n = new BigDouble(9.0, 6);
            var result = n.Divide(3.0);
            Assert.AreEqual(3e6, result.ToDouble(), 1.0);
        }

        [Test]
        public void Divide_ByZero_ReturnsInfinity()
        {
            var n = new BigDouble(1.0, 0);
            var result = n.Divide(0.0);
            Assert.IsTrue(result.IsInfinite);
        }

        [Test]
        public void Multiply_ByZero_ReturnsZero()
        {
            var n = new BigDouble(1.234, 50);
            Assert.IsTrue(n.Multiply(0.0).IsZero);
        }

        // ── Power / Log ───────────────────────────────────────────────────────────

        [Test]
        public void Pow_SquaredValue()
        {
            var n = new BigDouble(2.0, 0); // 2
            var result = n.Pow(10);        // 2^10 = 1024
            Assert.AreEqual(1024.0, result.ToDouble(), 0.01);
        }

        [Test]
        public void Log10_OfTen_IsOne()
        {
            var n = new BigDouble(10.0, 0);
            Assert.AreEqual(1.0, n.Log10(), Tolerance);
        }

        [Test]
        public void Log10_LargeExponent()
        {
            var n = new BigDouble(1.0, 500);
            Assert.AreEqual(500.0, n.Log10(), Tolerance);
        }

        // ── Comparison ────────────────────────────────────────────────────────────

        [Test]
        public void Comparison_LargerExponent_WinsSign()
        {
            var large = new BigDouble(1.0, 10);
            var small = new BigDouble(9.9, 9);
            Assert.IsTrue(large > small);
            Assert.IsFalse(large < small);
        }

        [Test]
        public void Comparison_SameExponent_MantissaDecides()
        {
            var a = new BigDouble(5.0, 3);
            var b = new BigDouble(3.0, 3);
            Assert.IsTrue(a > b);
            Assert.IsTrue(b < a);
        }

        [Test]
        public void Comparison_Equal_Values()
        {
            var a = new BigDouble(1.5, 3);
            var b = new BigDouble(1.5, 3);
            Assert.IsTrue(a == b);
            Assert.IsFalse(a != b);
        }

        [Test]
        public void Comparison_NegativeVsPositive()
        {
            var pos = new BigDouble(1.0, 0);
            var neg = new BigDouble(-1.0, 0);
            Assert.IsTrue(pos > neg);
            Assert.IsTrue(neg < pos);
        }

        // ── Conversion ────────────────────────────────────────────────────────────

        [Test]
        public void ToDouble_WithinRange_IsAccurate()
        {
            var n = new BigDouble(1.5, 6);
            Assert.AreEqual(1.5e6, n.ToDouble(), 1.0);
        }

        [Test]
        public void ToDouble_ExponentBeyond308_ReturnsInfinity()
        {
            var n = new BigDouble(1.0, 400);
            Assert.IsTrue(double.IsPositiveInfinity(n.ToDouble()));
        }

        [Test]
        public void ToLong_ClampsAtMaxLong()
        {
            var huge = new BigDouble(1.0, 30);
            Assert.AreEqual(long.MaxValue, huge.ToLong());
        }

        [Test]
        public void ImplicitFromDouble()
        {
            BigDouble n = 42.0;
            Assert.AreEqual(42.0, n.ToDouble(), Tolerance);
        }

        [Test]
        public void ImplicitFromLong()
        {
            BigDouble n = 1_000_000L;
            Assert.AreEqual(1_000_000.0, n.ToDouble(), 1.0);
        }

        // ── Format ────────────────────────────────────────────────────────────────

        [Test]
        public void Format_WithinDoubleRange_DelegatesToBigNumberFormatter()
        {
            var n = new BigDouble(1.5, 6); // 1.5M
            string s = n.Format(BigNumberNotation.Letter, 1);
            Assert.AreEqual("1.5M", s);
        }

        [Test]
        public void Format_BeyondDoubleRange_UsesScientificExtended()
        {
            var n = new BigDouble(2.5, 400);
            string s = n.Format();
            Assert.IsTrue(s.Contains("e400"), $"Expected 'e400' in '{s}'");
        }

        // ── BigNumberFactory ──────────────────────────────────────────────────────

        [Test]
        public void Factory_CreateDouble_ReturnsBigDoubleWhenConfigured()
        {
            BigNumberFactory.Configure(NumberBackend.BigDouble);
            var n = BigNumberFactory.Create(12345.0);
            Assert.IsInstanceOf<BigDouble>(n);
            BigNumberFactory.Configure(NumberBackend.DoubleNumber); // restore
        }

        [Test]
        public void Factory_CreateDouble_ReturnsDoubleNumberByDefault()
        {
            BigNumberFactory.Configure(NumberBackend.DoubleNumber);
            var n = BigNumberFactory.Create(12345.0);
            Assert.IsInstanceOf<DoubleNumber>(n);
        }

        [Test]
        public void Factory_Convert_SwitchesBackend()
        {
            BigNumberFactory.Configure(NumberBackend.BigDouble);
            IBigNumber dn = new DoubleNumber(999.0);
            IBigNumber converted = BigNumberFactory.Convert(dn);
            Assert.IsInstanceOf<BigDouble>(converted);
            Assert.AreEqual(999.0, converted.ToDouble(), Tolerance);
            BigNumberFactory.Configure(NumberBackend.DoubleNumber); // restore
        }

        // ── IBigNumber interface compliance ───────────────────────────────────────

        [Test]
        public void IBigNumber_Add_ViaInterface()
        {
            IBigNumber a = new BigDouble(2.0, 3);
            IBigNumber b = new BigDouble(3.0, 3);
            IBigNumber result = a.Add(b);
            Assert.AreEqual(5000.0, result.ToDouble(), 1.0);
        }

        [Test]
        public void IBigNumber_IsGreaterThan_ViaInterface()
        {
            IBigNumber a = new BigDouble(1.0, 10);
            IBigNumber b = new BigDouble(1.0, 5);
            Assert.IsTrue(a.IsGreaterThan(b));
        }
    }
}

namespace EndlessEngine.Economy.Math
{
    /// <summary>
    /// Creates IBigNumber values using the backend selected in EconomyConfigSO.
    ///
    /// Game code that needs to construct numbers should use this factory rather
    /// than `new DoubleNumber(x)` or `new BigDouble(x)` directly. This ensures
    /// switching the backend in EconomyConfigSO.NumberBackend is the only change
    /// required at the config layer — all call sites remain unchanged.
    ///
    /// Bootstrap wiring:
    ///   BigNumberFactory.Configure(economyConfig.NumberBackend);
    ///   // Call once after ConfigRegistry is loaded.
    ///
    /// Typical usage:
    ///   IBigNumber cost = BigNumberFactory.Create(1000.0);
    ///   IBigNumber zero = BigNumberFactory.Zero;
    /// </summary>
    public static class BigNumberFactory
    {
        private static NumberBackend _backend = NumberBackend.DoubleNumber;

        /// <summary>Configures which backend Create() uses. Call once from Bootstrap.</summary>
        public static void Configure(NumberBackend backend) => _backend = backend;

        /// <summary>Active backend.</summary>
        public static NumberBackend Backend => _backend;

        // ── Factory methods ───────────────────────────────────────────────────────

        /// <summary>Creates a zero value using the configured backend.</summary>
        public static IBigNumber Zero => Create(0.0);

        /// <summary>Creates a one value using the configured backend.</summary>
        public static IBigNumber One => Create(1.0);

        /// <summary>Creates an IBigNumber from a double value.</summary>
        public static IBigNumber Create(double value) => _backend switch
        {
            NumberBackend.BigDouble => new BigDouble(value),
            _                      => new DoubleNumber(value),
        };

        /// <summary>Creates an IBigNumber from a long value.</summary>
        public static IBigNumber Create(long value) => _backend switch
        {
            NumberBackend.BigDouble => new BigDouble(value),
            _                      => new DoubleNumber(value),
        };

        /// <summary>
        /// Converts an existing IBigNumber to the configured backend.
        /// No-op if already the correct type.
        /// </summary>
        public static IBigNumber Convert(IBigNumber value)
        {
            if (value == null) return Zero;
            return _backend switch
            {
                NumberBackend.BigDouble => value is BigDouble ? value : new BigDouble(value.ToDouble()),
                _                      => value is DoubleNumber ? value : new DoubleNumber(value.ToDouble()),
            };
        }

        // ── Typed accessors (avoid boxing for struct-level hot paths) ─────────────

        /// <summary>Creates a DoubleNumber regardless of backend setting. Use on known double paths.</summary>
        public static DoubleNumber CreateDouble(double value) => new DoubleNumber(value);

        /// <summary>Creates a BigDouble regardless of backend setting. Use for deep-prestige paths.</summary>
        public static BigDouble CreateBig(double value) => new BigDouble(value);

        /// <summary>Creates a BigDouble from mantissa + exponent directly.</summary>
        public static BigDouble CreateBig(double mantissa, long exponent) => new BigDouble(mantissa, exponent);
    }
}

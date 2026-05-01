using EndlessEngine.Config;

namespace EndlessEngine.Stats
{
    /// <summary>
    /// Implemented by any service that contributes multipliers or additive bonuses
    /// to game stats (Prestige, Events, Pets, Skills, Research, Challenges, etc.).
    ///
    /// ModifierRegistry collects all registered sources and provides a single
    /// query point: GetTotal(stat) → combined modifier with full breakdown.
    ///
    /// Existing service APIs (GetPermanentMultiplier, GetCombinedIncomeMultiplier,
    /// GetActiveEffects) are kept intact — IModifierSource is an additional layer
    /// on top, not a replacement. Sources register themselves with ModifierRegistry
    /// in their Initialize() call.
    /// </summary>
    public interface IModifierSource
    {
        /// <summary>
        /// Unique stable identifier for this source (e.g. "prestige", "event", "pet", "skill").
        /// Used in breakdown strings and debug logs.
        /// </summary>
        string SourceId { get; }

        /// <summary>
        /// Returns the modifier this source contributes for the given stat.
        /// Return Modifier.None if this source does not affect the stat.
        /// </summary>
        Modifier GetModifier(StatType stat);
    }

    /// <summary>
    /// A single modifier contribution from one source for one stat.
    /// Additive bonuses are summed first, then all multiplicative factors are multiplied together.
    /// </summary>
    public readonly struct Modifier
    {
        /// <summary>Additive flat bonus (e.g. +500 gold/sec). Applied before multiplicative.</summary>
        public readonly double Additive;

        /// <summary>Multiplicative factor (e.g. 1.5 = +50%). All multiplied together.</summary>
        public readonly double Multiplicative;

        public Modifier(double additive, double multiplicative)
        {
            Additive       = additive;
            Multiplicative = multiplicative;
        }

        /// <summary>Returns a modifier that contributes nothing (additive=0, multiplicative=1).</summary>
        public static readonly Modifier None = new Modifier(0.0, 1.0);

        /// <summary>Convenience: pure multiplicative modifier.</summary>
        public static Modifier FromMultiplier(double factor) => new Modifier(0.0, factor);

        /// <summary>Convenience: pure additive modifier.</summary>
        public static Modifier FromAdditive(double bonus) => new Modifier(bonus, 1.0);

        public bool IsNone => Additive == 0.0 && Multiplicative == 1.0;
    }
}

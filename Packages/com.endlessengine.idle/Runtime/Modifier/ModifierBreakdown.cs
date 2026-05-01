using System.Collections.Generic;
using System.Text;
using EndlessEngine.Config;

namespace EndlessEngine.Stats
{
    /// <summary>
    /// Result of ModifierRegistry.GetTotal() — the combined modifier value
    /// and a per-source breakdown for tooltip display and debug logging.
    ///
    /// Usage:
    ///   var bd = registry.GetTotal(StatType.PassiveIncome);
    ///   float income = baseIncome * (float)bd.TotalMultiplicative + (float)bd.TotalAdditive;
    ///   tooltipText = bd.ToBreakdownString();
    /// </summary>
    public readonly struct ModifierBreakdown
    {
        public readonly StatType Stat;
        public readonly double   TotalAdditive;
        public readonly double   TotalMultiplicative;

        private readonly IReadOnlyList<(string sourceId, Modifier modifier)> _contributions;

        public ModifierBreakdown(
            StatType stat,
            double totalAdditive,
            double totalMultiplicative,
            IReadOnlyList<(string, Modifier)> contributions)
        {
            Stat                = stat;
            TotalAdditive       = totalAdditive;
            TotalMultiplicative = totalMultiplicative;
            _contributions      = contributions;
        }

        /// <summary>
        /// Applies this breakdown to a base value.
        /// Formula: (base + TotalAdditive) × TotalMultiplicative
        /// </summary>
        public double Apply(double baseValue) => (baseValue + TotalAdditive) * TotalMultiplicative;

        /// <summary>
        /// Human-readable per-source breakdown for UI tooltips.
        /// Example: "Prestige ×2.00\nEvent ×1.50\nPet +100.00\nTotal ×3.00 +100.00"
        /// </summary>
        public string ToBreakdownString()
        {
            if (_contributions == null || _contributions.Count == 0)
                return $"Total: ×{TotalMultiplicative:F2}";

            var sb = new StringBuilder();
            foreach (var (sourceId, mod) in _contributions)
            {
                if (mod.IsNone) continue;
                if (mod.Multiplicative != 1.0)
                    sb.AppendLine($"{sourceId}: ×{mod.Multiplicative:F2}");
                if (mod.Additive != 0.0)
                    sb.AppendLine($"{sourceId}: +{mod.Additive:F2}");
            }
            sb.Append($"Total: ×{TotalMultiplicative:F2}");
            if (TotalAdditive != 0.0)
                sb.Append($" +{TotalAdditive:F2}");
            return sb.ToString();
        }

        public static readonly ModifierBreakdown Identity = new ModifierBreakdown(
            default, 0.0, 1.0, null);
    }
}

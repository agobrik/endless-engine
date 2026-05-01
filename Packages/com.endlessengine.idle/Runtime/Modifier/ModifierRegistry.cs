using System.Collections.Generic;
using EndlessEngine.Config;
using EndlessEngine.Economy.Math;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace EndlessEngine.Stats
{
    /// <summary>
    /// Central query point for all stat modifiers across the game.
    ///
    /// Sources (Prestige, Events, Pets, Skills, Research, …) register themselves
    /// via Register(). Callers query via GetTotal(stat) and receive a
    /// ModifierBreakdown with the combined value and per-source attribution.
    ///
    /// Stack ordering is deterministic: sources are applied in registration order.
    /// All multiplicative factors are multiplied together (not added).
    /// Additive bonuses are summed first, then the multiplicative total is applied.
    ///
    /// Formula: (base + ΣAdditive) × Π(Multiplicative)
    ///
    /// Thread safety: Register/Unregister must be called from the main thread.
    /// GetTotal is read-only and safe to call from any context.
    /// </summary>
    public class ModifierRegistry
    {
        private readonly List<IModifierSource> _sources = new List<IModifierSource>(16);

        // ── Registration ──────────────────────────────────────────────────────────

        /// <summary>
        /// Registers a modifier source. Call from service Initialize().
        /// No-ops if the same source is already registered.
        /// </summary>
        public void Register(IModifierSource source)
        {
            if (source == null) return;
            if (_sources.Contains(source)) return;
            _sources.Add(source);
        }

        /// <summary>
        /// Unregisters a modifier source. Call from service OnDisable() or cleanup.
        /// </summary>
        public void Unregister(IModifierSource source)
        {
            _sources.Remove(source);
        }

        // ── Query ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the combined modifier for the given stat from all registered sources.
        /// Includes a per-source breakdown for tooltip display.
        /// </summary>
        public ModifierBreakdown GetTotal(StatType stat)
        {
            double totalAdditive       = 0.0;
            double totalMultiplicative = 1.0;

            var contributions = new List<(string, Modifier)>(_sources.Count);

            foreach (var source in _sources)
            {
                Modifier mod = Modifier.None;
                try
                {
                    mod = source.GetModifier(stat);
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[ModifierRegistry] Source '{source.SourceId}' threw on GetModifier({stat}): {ex.Message}");
                    continue;
                }

                if (mod.IsNone) continue;

                totalAdditive       += mod.Additive;
                totalMultiplicative *= mod.Multiplicative;
                contributions.Add((source.SourceId, mod));
            }

            return new ModifierBreakdown(stat, totalAdditive, totalMultiplicative, contributions);
        }

        /// <summary>
        /// Convenience: returns the combined multiplicative factor only.
        /// Equivalent to GetTotal(stat).TotalMultiplicative.
        /// Use when additive bonuses are not relevant to the caller.
        /// </summary>
        public double GetMultiplier(StatType stat) => GetTotal(stat).TotalMultiplicative;

        /// <summary>
        /// Applies the combined modifier to a base value.
        /// Equivalent to GetTotal(stat).Apply(baseValue).
        /// </summary>
        public double Apply(StatType stat, double baseValue) => GetTotal(stat).Apply(baseValue);

        /// <summary>
        /// IBigNumber overload — applies combined modifier to a backend-aware base value.
        /// (base + ΣAdditive) × ΠMultiplicative, all in IBigNumber space.
        /// </summary>
        public IBigNumber Apply(StatType stat, IBigNumber baseValue)
        {
            var breakdown = GetTotal(stat);
            IBigNumber result = baseValue.Add(BigNumberFactory.Create(breakdown.TotalAdditive));
            return result.Multiply(breakdown.TotalMultiplicative);
        }

        /// <summary>Returns the number of registered sources. For debug/testing.</summary>
        public int SourceCount => _sources.Count;

        /// <summary>Dumps all registered sources to the Unity console. Debug builds only.</summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public void LogSources()
        {
            Debug.Log($"[ModifierRegistry] {_sources.Count} sources registered:");
            foreach (var s in _sources)
                Debug.Log($"  • {s.SourceId}");
        }
    }
}

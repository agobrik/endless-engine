using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using EndlessEngine.Config;

namespace EndlessEngine.Core
{
    /// <summary>
    /// Static service that computes effective player stats by applying upgrade effects
    /// on top of base stats from ConfigRegistry.Player.
    ///
    /// Formula: (BaseStat + ΣAdditiveFlat) × (1 + ΣAdditivePercent) × PermanentMultiplier
    ///
    /// PermanentMultiplier applies only to Damage and MaxHP (not MoveSpeed etc.).
    /// Percent bonuses stack additively.
    ///
    /// Cache invalidated via dirty-flag on ApplyUpgradeEffect() / ClearRunEffects().
    /// GetEffectiveStat() is synchronous and zero-allocation on cache-hit.
    ///
    /// ADR: ADR-0009 — Upgrade Stat Model
    /// </summary>
    public static class UpgradeApplicationSystem
    {
        // ── Events ────────────────────────────────────────────────────────────────

        /// <summary>Fires after a stat is recomputed. Subscribers: HUD, combat readouts.</summary>
        public static event Action<StatType, float> OnEffectiveStatChanged;

        // ── Internal state ────────────────────────────────────────────────────────

        private static readonly Dictionary<StatType, List<UpgradeEffect>> _runEffects       = new();
        private static readonly Dictionary<StatType, List<UpgradeEffect>> _permanentEffects  = new();
        private static readonly Dictionary<StatType, float>               _cache             = new();
        private static readonly HashSet<StatType>                         _dirtyStats        = new();

        /// <summary>Stats that receive the PermanentMultiplier from prestige. MoveSpeed excluded.</summary>
        private static readonly HashSet<StatType> _amplifiedStats = new()
        {
            StatType.Damage,
            StatType.MaxHP,
        };

        // Stat clamp ranges: [min, max]. float.MaxValue = no upper clamp.
        private static readonly Dictionary<StatType, (float min, float max)> _statClamps = new()
        {
            { StatType.CritChance,      (0f,    1f)               },
            { StatType.AttackInterval,  (0.05f, float.MaxValue)   },
        };

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the effective value for <paramref name="stat"/>.
        /// Recomputes only if the stat is dirty; otherwise returns the cached value.
        /// Zero-allocation on cache-hit.
        /// </summary>
        public static float GetEffectiveStat(StatType stat)
        {
            if (_dirtyStats.Contains(stat))
            {
                float value = Recompute(stat);
                _cache[stat] = value;
                _dirtyStats.Remove(stat);
                OnEffectiveStatChanged?.Invoke(stat, value);
            }

            return _cache.TryGetValue(stat, out float v) ? v : GetBaseStat(stat);
        }

        /// <summary>
        /// Applies an upgrade effect to <paramref name="stat"/>.
        /// <paramref name="isPermanent"/> = true for prestige-permanent effects.
        /// Marks the stat dirty so the next GetEffectiveStat call recomputes.
        /// </summary>
        public static void ApplyUpgradeEffect(StatType stat, float magnitude, EffectType effectType, bool isPermanent = false)
        {
            var dict = isPermanent ? _permanentEffects : _runEffects;
            if (!dict.ContainsKey(stat))
                dict[stat] = new List<UpgradeEffect>();
            dict[stat].Add(new UpgradeEffect(magnitude, effectType));
            _dirtyStats.Add(stat);
        }

        /// <summary>
        /// Clears all run-scoped effects (called on prestige).
        /// Permanent effects survive. All stats marked dirty.
        /// </summary>
        public static void ClearRunEffects()
        {
            _runEffects.Clear();
            foreach (StatType stat in Enum.GetValues(typeof(StatType)))
                _dirtyStats.Add(stat);
        }

        /// <summary>
        /// Returns what the effective stat WOULD be if <paramref name="nodeId"/> were applied.
        /// Does NOT mutate state. Returns 0 if the node is not found.
        /// </summary>
        public static float SimulateEffect(string nodeId, int additionalRanks = 1)
        {
            var upgrades = ConfigRegistry.Upgrades;
            UpgradeNodeConfigSO node = null;
            foreach (var n in upgrades)
            {
                if (n.NodeId == nodeId) { node = n; break; }
            }
            if (node == null) return 0f;

            // Temporarily add the effect to a scratch copy — do not touch _runEffects
            float baseStat = GetBaseStat(node.AffectedStat);

            float flatSum = SumEffects(node.AffectedStat, EffectType.AdditiveFlat, _runEffects)
                          + SumEffects(node.AffectedStat, EffectType.AdditiveFlat, _permanentEffects);
            float pctSum  = SumEffects(node.AffectedStat, EffectType.AdditivePercent, _runEffects)
                          + SumEffects(node.AffectedStat, EffectType.AdditivePercent, _permanentEffects);
            float permanent = _amplifiedStats.Contains(node.AffectedStat) ? GetPermanentMultiplier() : 1f;

            float simMagnitude = node.EffectPerRank * additionalRanks;
            if (node.EffectType == UpgradeEffectType.FlatBonus)
                flatSum += simMagnitude;
            else
                pctSum += simMagnitude;

            float projected = (baseStat + flatSum) * (1f + pctSum) * permanent;
            projected = ClampStat(node.AffectedStat, projected);
            return projected;
        }

        // ── Internal computation ──────────────────────────────────────────────────

        private static float Recompute(StatType stat)
        {
            float baseStat  = GetBaseStat(stat);
            float flatSum   = SumEffects(stat, EffectType.AdditiveFlat, _runEffects)
                            + SumEffects(stat, EffectType.AdditiveFlat, _permanentEffects);
            float pctSum    = SumEffects(stat, EffectType.AdditivePercent, _runEffects)
                            + SumEffects(stat, EffectType.AdditivePercent, _permanentEffects);
            float permanent = _amplifiedStats.Contains(stat) ? GetPermanentMultiplier() : 1f;

            float result = (baseStat + flatSum) * (1f + pctSum) * permanent;
            return ClampStat(stat, result);
        }

        private static float SumEffects(StatType stat, EffectType type, Dictionary<StatType, List<UpgradeEffect>> source)
        {
            if (!source.TryGetValue(stat, out var effects)) return 0f;
            float sum = 0f;
            foreach (var e in effects)
                if (e.Type == type) sum += e.Magnitude;
            return sum;
        }

        private static float GetBaseStat(StatType stat)
        {
            var p = ConfigRegistry.Player;
            return stat switch
            {
                StatType.Damage         => p.BaseAttackDamage,
                StatType.MaxHP          => p.BaseMaxHP,
                StatType.MoveSpeed      => p.BaseMoveSpeed,
                StatType.CritChance     => p.BaseCritChance,
                StatType.CritMultiplier => p.BaseCritMultiplier,
                StatType.AttackInterval => p.BaseAttackInterval,
                StatType.IdleYieldRate  => ConfigRegistry.Economy.IdleYieldRateBase,
                _                       => 0f,
            };
        }

        private static float GetPermanentMultiplier()
        {
            // Read from PrestigeStateManager if available; fall back to 1.0
            // (PrestigeStateManager is not yet wired — placeholder for Core layer integration)
            return _permanentMultiplierOverride ?? 1f;
        }

        private static float ClampStat(StatType stat, float value)
        {
            if (_statClamps.TryGetValue(stat, out var range))
                return Mathf.Clamp(value, range.min, range.max == float.MaxValue ? value : range.max);
            return value;
        }

        // ── Prestige multiplier injection ─────────────────────────────────────────

        private static float? _permanentMultiplierOverride;

        /// <summary>
        /// Called by PrestigeStateManager after each prestige to update the permanent
        /// multiplier used in GetEffectiveStat for Damage and MaxHP.
        /// </summary>
        public static void SetPermanentMultiplier(float multiplier)
        {
            _permanentMultiplierOverride = multiplier;
            // Mark all amplified stats dirty
            foreach (var stat in _amplifiedStats)
                _dirtyStats.Add(stat);
        }

        // ── Test helpers ──────────────────────────────────────────────────────────

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>Clears all effects, cache, dirty flags, and multiplier. Call in TearDown.</summary>
        public static void ResetForTesting()
        {
            _runEffects.Clear();
            _permanentEffects.Clear();
            _cache.Clear();
            _dirtyStats.Clear();
            _permanentMultiplierOverride = null;
        }

        /// <summary>
        /// Applies a permanent effect directly (simulates post-prestige node restoration).
        /// </summary>
        public static void ApplyPermanentEffectForTesting(StatType stat, float magnitude, EffectType effectType)
        {
            ApplyUpgradeEffect(stat, magnitude, effectType, isPermanent: true);
        }
#endif
    }

    // ── Value types ───────────────────────────────────────────────────────────────

    /// <summary>Type of upgrade effect magnitude interpretation.</summary>
    public enum EffectType
    {
        AdditiveFlat,
        AdditivePercent,
    }

    /// <summary>A single upgrade bonus applied to a stat.</summary>
    public readonly struct UpgradeEffect
    {
        public readonly float      Magnitude;
        public readonly EffectType Type;

        public UpgradeEffect(float magnitude, EffectType type)
        {
            Magnitude = magnitude;
            Type      = type;
        }
    }
}

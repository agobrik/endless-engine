using EndlessEngine.Config;
using EndlessEngine.Core;

namespace EndlessEngine.Harvest
{
    /// <summary>
    /// Pure-logic class that computes the gold/currency awarded for a single harvest tick
    /// or a full node depletion.
    ///
    /// Formula:
    ///   TickYield  = (BaseYield / MaxHP × DamageApplied) × YieldMultiplier × ComboMultiplier
    ///   TotalYield = BaseYield × YieldMultiplier × ComboMultiplier  (on full depletion, not per-tick)
    ///
    /// Multi-node bonus: if more than one node is hit simultaneously, a flat percent bonus applies.
    /// </summary>
    public static class HarvestYieldResolver
    {
        /// <summary>
        /// Computes yield for a single damage application on a node.
        /// Used when HarvestNodeConfigSO.AwardYieldPerTick = true.
        /// </summary>
        public static float ResolveTickYield(HarvestNodeConfigSO config,
                                             float damageApplied,
                                             float comboMultiplier,
                                             int   simultaneousNodes = 1)
        {
            float yieldMult = GetYieldMultiplier();
            float perHp     = config.BaseYield / config.MaxHP;
            float raw       = perHp * damageApplied * yieldMult * comboMultiplier;
            return raw * GetMultiNodeBonus(simultaneousNodes);
        }

        /// <summary>
        /// Computes the full depletion yield (awarded once when HP hits 0 and AwardYieldPerTick = false).
        /// </summary>
        public static float ResolveDepletionYield(HarvestNodeConfigSO config,
                                                  float               comboMultiplier,
                                                  int                 simultaneousNodes = 1)
        {
            float yieldMult = GetYieldMultiplier();
            float raw       = config.BaseYield * yieldMult * comboMultiplier;
            return raw * GetMultiNodeBonus(simultaneousNodes);
        }

        // ── Private ───────────────────────────────────────────────────────────────

        private static float GetYieldMultiplier()
        {
            // 1.0 baseline + percent bonus from upgrades
            return 1f + UpgradeApplicationSystem.GetEffectiveStat(StatType.HarvestYieldMultiplier);
        }

        private static float GetMultiNodeBonus(int nodeCount)
        {
            if (nodeCount <= 1) return 1f;
            float bonus = UpgradeApplicationSystem.GetEffectiveStat(StatType.HarvestMultiNodeBonus);
            return 1f + bonus * (nodeCount - 1);
        }
    }
}

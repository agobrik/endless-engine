using EndlessEngine.Config;
using EndlessEngine.Core;

namespace EndlessEngine.ClickLoop
{
    /// <summary>
    /// Pure-logic yield calculator for click-loop targets.
    ///
    /// Per-click formula  (AwardYieldPerClick = true):
    ///   yield = (BaseYield / MaxHP × damage) × yieldMult × comboMult × critMult
    ///
    /// Destruction formula (AwardYieldPerClick = false):
    ///   yield = BaseYield × yieldMult × comboMult × critMult
    /// </summary>
    public static class ClickYieldResolver
    {
        public static float ResolveClickYield(
            ClickTargetConfigSO config,
            float               damageApplied,
            float               comboMultiplier,
            float               critMultiplier = 1f)
        {
            float yieldMult = GetYieldMultiplier();
            float perHp     = config.BaseYield / config.MaxHP;
            return perHp * damageApplied * yieldMult * comboMultiplier * critMultiplier;
        }

        public static float ResolveDestructionYield(
            ClickTargetConfigSO config,
            float               comboMultiplier,
            float               critMultiplier = 1f)
        {
            float yieldMult = GetYieldMultiplier();
            return config.BaseYield * yieldMult * comboMultiplier * critMultiplier;
        }

        private static float GetYieldMultiplier()
            => 1f + UpgradeApplicationSystem.GetEffectiveStat(StatType.ClickYieldMultiplier);
    }
}

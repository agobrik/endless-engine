using System;
using UnityEngine;

namespace EndlessEngine.Config
{
    /// <summary>
    /// Defines a single resource conversion recipe.
    /// Example: 100 gold → 1 gem, cooldown 60s.
    ///
    /// Input/output amounts are doubles to support BigNumber values.
    /// CurrencyId "gold" maps to EconomyService (primary); all others map to CurrencyService.
    ///
    /// ConversionService.TryConvert(recipeId) executes the recipe if conditions are met.
    /// </summary>
    [CreateAssetMenu(fileName = "ConversionRecipe", menuName = "Endless Engine/Config/Conversion Recipe")]
    public class ConversionRecipeSO : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Unique machine-readable id. Used as key in save data and API calls.")]
        public string RecipeId = "recipe_id";

        [Tooltip("Player-visible name shown in conversion UI.")]
        public string DisplayName = "Convert";

        [Header("Input")]
        [Tooltip("Currency id of the resource consumed. Use 'gold' for the primary currency.")]
        public string InputCurrencyId = "gold";

        [Tooltip("Amount consumed per conversion.")]
        public double InputAmount = 100;

        [Header("Output")]
        [Tooltip("Currency id of the resource produced.")]
        public string OutputCurrencyId = "gems";

        [Tooltip("Amount produced per conversion.")]
        public double OutputAmount = 1;

        [Header("Cooldown")]
        [Tooltip("Seconds between allowed conversions. 0 = no cooldown.")]
        public float CooldownSeconds = 0f;

        [Header("Unlock")]
        [Tooltip("Prestige count required before this recipe is available. 0 = always available.")]
        public int UnlockAtPrestigeCount = 0;

        [Header("Bulk")]
        [Tooltip("If true, player can convert multiple times at once (limited by balance).")]
        public bool AllowBulk = false;

        [Tooltip("Maximum times one bulk convert call can execute. 0 = unlimited by config (still capped by balance).")]
        public int MaxBulkCount = 0;

        // ── Runtime helpers ───────────────────────────────────────────────────────

        /// <summary>Returns the maximum times this recipe can run with the given balances.</summary>
        public int MaxExecutions(double inputBalance, double outputCap, double outputBalance)
        {
            if (InputAmount <= 0) return 0;
            int byInput = (int)Math.Floor(inputBalance / InputAmount);
            if (outputCap > 0 && OutputAmount > 0)
            {
                double headroom  = outputCap - outputBalance;
                int byOutputCap  = (int)Math.Floor(headroom / OutputAmount);
                byInput = Math.Min(byInput, byOutputCap);
            }
            if (MaxBulkCount > 0) byInput = Math.Min(byInput, MaxBulkCount);
            return Math.Max(0, byInput);
        }
    }
}

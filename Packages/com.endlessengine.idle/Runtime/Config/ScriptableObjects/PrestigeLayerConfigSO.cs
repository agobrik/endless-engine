using UnityEngine;

namespace EndlessEngine.Config
{
    /// <summary>
    /// What gets reset when this prestige layer is triggered.
    /// Higher-indexed layers typically reset more state.
    /// </summary>
    public enum AscensionResetScope
    {
        /// <summary>Resets gold, wave, upgrades (standard prestige).</summary>
        Standard,
        /// <summary>Resets everything Standard does PLUS generators and currencies.</summary>
        Deep,
        /// <summary>Resets everything including secondary layer counts.</summary>
        Full
    }

    /// <summary>
    /// Configures a single layer of the multi-layer prestige / ascension system.
    /// Layer 0 = standard prestige (maps to existing PrestigeConfigSO behaviour).
    /// Layer 1+ = ascension tiers with their own counts, currencies, and multipliers.
    ///
    /// Create via: Tools → Endless Engine → Create Prestige Layer Config
    ///
    /// PrestigeStateManager reads the array from AscensionDatabaseSO and manages
    /// per-layer counts in SaveData.AscensionCounts.
    /// </summary>
    [CreateAssetMenu(
        menuName = "Endless Engine/Prestige/Prestige Layer Config",
        fileName = "PrestigeLayerConfig")]
    public class PrestigeLayerConfigSO : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Index of this layer (0 = standard prestige, 1 = first ascension, etc.).")]
        public int LayerIndex = 0;

        [Tooltip("Player-facing name for this layer (e.g. 'Prestige', 'Ascend', 'Transcend').")]
        public string DisplayName = "Prestige";

        [Tooltip("Short verb shown on the button (e.g. 'PRESTIGE', 'ASCEND').")]
        public string ActionVerb = "PRESTIGE";

        [Header("Gate Conditions")]
        [Tooltip("Minimum wave required to trigger this layer. 0 = no gate.")]
        public int MinWaveRequired = 1;

        [Tooltip("Minimum number of completions of the PREVIOUS layer required to unlock this layer. 0 = no gate.")]
        public int RequiredPreviousLayerCount = 0;

        [Tooltip("Maximum times this layer can be triggered. 0 = unlimited.")]
        public int MaxCount = 0;

        [Header("Reset Scope")]
        [Tooltip("What is reset when this layer is triggered.")]
        public AscensionResetScope ResetScope = AscensionResetScope.Standard;

        [Tooltip("If true, secondary currencies with ResetsOnPrestige=true are reset.")]
        public bool ResetSecondaryCurrencies = false;

        [Tooltip("If true, generators are reset to 0 on this layer.")]
        public bool ResetGenerators = false;

        [Header("Currency Reward")]
        [Tooltip("ID of the currency awarded on each trigger of this layer (e.g. 'ascension_shards'). Empty = gold only.")]
        public string RewardCurrencyId = "";

        [Tooltip("Base amount of reward currency per trigger. Scales with CurrencyScalingPerCount.")]
        public double BaseCurrencyReward = 1;

        [Tooltip("Multiplier applied to BaseCurrencyReward for each time this layer has been triggered. Set to 1 for flat reward.")]
        public double CurrencyScalingPerCount = 1.0;

        [Header("Permanent Multiplier")]
        [Tooltip("Base multiplier per trigger of this layer. Applied multiplicatively to the standard prestige multiplier.")]
        public float BaseMultiplierPerTrigger = 1.2f;

        [Tooltip("Cap for this layer's permanent multiplier. 0 = unlimited.")]
        public float MaxPermanentMultiplier = 0f;

        // ── Helper methods ────────────────────────────────────────────────────────

        /// <summary>
        /// Computes the permanent multiplier contribution from this layer.
        /// Formula: Min(MaxPermanentMultiplier, BaseMultiplierPerTrigger ^ count)
        /// </summary>
        public float GetPermanentMultiplier(int count)
        {
            if (count <= 0) return 1f;
            float mult = Mathf.Pow(BaseMultiplierPerTrigger, count);
            return MaxPermanentMultiplier > 0f
                ? Mathf.Min(MaxPermanentMultiplier, mult)
                : mult;
        }

        /// <summary>
        /// Computes the currency reward for the next trigger of this layer.
        /// Formula: BaseCurrencyReward * CurrencyScalingPerCount ^ currentCount
        /// </summary>
        public double GetCurrencyReward(int currentCount)
        {
            if (BaseCurrencyReward <= 0) return 0;
            if (CurrencyScalingPerCount <= 0) return BaseCurrencyReward;
            return BaseCurrencyReward * System.Math.Pow(CurrencyScalingPerCount, currentCount);
        }
    }
}

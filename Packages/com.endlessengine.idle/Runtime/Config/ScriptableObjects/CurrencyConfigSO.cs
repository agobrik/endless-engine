using UnityEngine;

namespace EndlessEngine.Config
{
    /// <summary>
    /// Defines a single in-game currency type.
    /// Gold ("gold") is the primary currency managed by EconomyService (legacy path).
    /// Secondary currencies (gems, tokens, shards, etc.) are managed by CurrencyService.
    ///
    /// ADR: ADR-0009 — Upgrade Stat Model (same SO-driven config pattern)
    /// </summary>
    [CreateAssetMenu(fileName = "CurrencyConfig", menuName = "Endless Engine/Config/Currency")]
    public class CurrencyConfigSO : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Unique machine-readable key used in SaveData and code. No spaces. e.g. 'gold', 'gems', 'tokens'.")]
        public string CurrencyId = "currency";

        [Tooltip("Player-visible name shown in HUD and shop.")]
        public string DisplayName = "Currency";

        [Tooltip("Short symbol for compact display (e.g. 'G', '💎', 'T').")]
        public string Symbol = "C";

        [Tooltip("Optional icon shown next to the balance.")]
        public Sprite Icon;

        [Header("Balance Rules")]
        [Tooltip("Maximum balance the player can hold. 0 = no cap.")]
        public double HardCap = 0;

        [Tooltip("Starting balance for a new game.")]
        public double StartingAmount = 0;

        [Header("Display Format")]
        [Tooltip("Notation style when formatting large numbers.")]
        public BigNumberNotation Notation = BigNumberNotation.Letter;

        [Tooltip("Decimal places shown in formatted strings.")]
        [Range(0, 3)]
        public int DecimalPlaces = 1;

        [Header("Unlock")]
        [Tooltip("Prestige count required before this currency is visible to the player. 0 = always visible.")]
        public int UnlockAtPrestigeCount = 0;

        [Tooltip("If true, this currency resets to StartingAmount on prestige.")]
        public bool ResetsOnPrestige = false;
    }

    /// <summary>Notation style for BigNumber formatting.</summary>
    public enum BigNumberNotation
    {
        /// <summary>K / M / B / T / aa / bb ... letter suffix chain.</summary>
        Letter,
        /// <summary>Scientific: 1.23e6</summary>
        Scientific,
        /// <summary>Engineering: 1.23M (SI prefixes).</summary>
        Engineering,
    }
}

using UnityEngine;

namespace EndlessEngine.Config
{
    /// <summary>
    /// Module: Click-to-Produce
    /// Defines how manual clicks (or taps) generate gold directly.
    ///
    /// Manual click income is separate from passive generator income.
    /// Combo system: rapid clicks within ComboWindowSeconds multiply yield.
    ///
    /// Wire up: Bootstrap creates ClickYieldService and calls Initialize().
    /// </summary>
    [CreateAssetMenu(fileName = "ClickSourceConfig",
                     menuName = "Endless Engine/Modules/Click Source Config")]
    public class ClickSourceConfigSO : ScriptableObject
    {
        [Header("Base Click Yield")]
        [Tooltip("Gold earned per click at combo ×1 (no combo).")]
        [Min(1)]
        public float GoldPerClick = 10f;

        [Tooltip("If > 0, click yield scales with current passive income rate. " +
                 "E.g. 0.1 = each click earns 10% of current yield/s. Stacks additively with GoldPerClick.")]
        [Min(0f)]
        public float YieldRateClickFraction = 0f;

        [Header("Combo System")]
        [Tooltip("Enable combo multiplier. Rapid successive clicks build a combo chain.")]
        public bool EnableCombo = true;

        [Tooltip("Seconds between clicks to continue a combo chain. Exceeding this resets combo to 1.")]
        [Range(0.05f, 5f)]
        public float ComboWindowSeconds = 0.8f;

        [Tooltip("Maximum combo multiplier achievable.")]
        [Min(1f)]
        public float MaxComboMultiplier = 5f;

        [Tooltip("Multiplier added per click in the combo chain. " +
                 "E.g. 0.1 = ×1.0, ×1.1, ×1.2... up to MaxComboMultiplier.")]
        [Range(0.01f, 1f)]
        public float ComboMultiplierStep = 0.1f;

        [Header("Auto-Click Upgrades")]
        [Tooltip("Clicks per second added by 'auto-clicker' upgrades. 0 = no auto-click at baseline. " +
                 "Increased by upgrade tree via ClickYieldService.SetAutoClickRate().")]
        [Min(0f)]
        public float BaseAutoClicksPerSecond = 0f;

        [Header("Critical Clicks")]
        [Tooltip("Chance (0–1) that a click is a critical hit.")]
        [Range(0f, 1f)]
        public float CritChance = 0.05f;

        [Tooltip("Multiplier applied on a critical click.")]
        [Min(1f)]
        public float CritMultiplier = 3f;

        [Header("Caps & Modifiers")]
        [Tooltip("Maximum clicks per second counted for yield. Prevents macro/autoclicker abuse. 0 = uncapped.")]
        [Min(0f)]
        public float MaxClicksPerSecondCap = 20f;

        [Tooltip("Global multiplier applied to all click yield before adding to economy.")]
        [Min(0f)]
        public float GlobalMultiplier = 1f;
    }
}

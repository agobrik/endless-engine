using UnityEngine;

namespace EndlessEngine.Config
{
    /// <summary>
    /// Prestige system configuration. Controls prestige gate conditions,
    /// permanent multiplier formula, and stat amplification eligibility.
    /// </summary>
    [CreateAssetMenu(fileName = "PrestigeConfig", menuName = "Endless Engine/Config/Prestige")]
    public class PrestigeConfigSO : ScriptableObject
    {
        [Header("Prestige Gate")]
        [Tooltip("Minimum wave number the player must reach before prestige is available.")]
        public int MinWaveForPrestige = 20;

        [Tooltip("Minimum gold required to prestige (0 = no gold gate).")]
        public long MinGoldToPrestige = 0;

        [Tooltip("Maximum number of prestiges. 0 = unlimited.")]
        public int MaxPrestigeCount = 0;

        [Header("Permanent Multiplier")]
        [Tooltip("Base multiplier per prestige. Formula: Min(Cap, BaseMultiplier ^ PrestigeCount).")]
        public float BaseMultiplierPerPrestige = 1.5f;

        [Tooltip("Ceiling on the permanent multiplier regardless of prestige count.")]
        public float MaxPermanentMultiplier = 100f;

        [Header("Stat Amplification")]
        [Tooltip("Stats that receive the permanent multiplier. MoveSpeed is excluded.")]
        public StatType[] StatsAmplifiedByPrestige = { StatType.Damage, StatType.MaxHP };
    }
}

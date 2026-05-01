using UnityEngine;

namespace EndlessEngine.ClickLoop
{
    /// <summary>
    /// Global config for the click active loop. One asset per game.
    /// Baselines for combo, crit, and auto-click; all scaled by UpgradeApplicationSystem stats.
    /// </summary>
    [CreateAssetMenu(fileName = "ClickLoopConfig", menuName = "Endless Engine/Click Loop/Loop Config")]
    public class ClickLoopConfigSO : ScriptableObject
    {
        [Header("Combo")]
        [Tooltip("Seconds without a click before combo starts decaying.")]
        public float ComboDecayDelay = 1.5f;

        [Tooltip("Combo points lost per second after decay starts. Scaled by ClickComboDecayRate stat.")]
        public float ComboDecayRate = 8f;

        [Tooltip("Maximum combo multiplier.")]
        public float MaxComboMultiplier = 8f;

        [Tooltip("Combo points needed per ×1 multiplier step.")]
        public float ComboPointsPerStep = 5f;

        [Header("Crit")]
        [Tooltip("Base crit chance (0–1). Scaled by ClickCritChance stat.")]
        [Range(0f, 1f)]
        public float BaseCritChance = 0.05f;

        [Tooltip("Base crit damage multiplier. Scaled by ClickCritMultiplier stat.")]
        [Min(1f)]
        public float BaseCritMultiplier = 3f;

        [Header("Auto-Click")]
        [Tooltip("Auto-clicks per second at baseline (0 = none). Scaled by ClickAutoRate stat.")]
        [Min(0f)]
        public float BaseAutoClickRate = 0f;

        [Header("Offline")]
        [Tooltip("Maximum hours of offline click gains credited.")]
        public float OfflineCapHours = 8f;

        [Tooltip("Fraction of active click rate applied offline (0–1).")]
        [Range(0f, 1f)]
        public float OfflineEfficiency = 0.25f;
    }
}

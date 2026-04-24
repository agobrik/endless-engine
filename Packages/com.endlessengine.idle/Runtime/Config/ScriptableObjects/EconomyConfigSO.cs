using UnityEngine;

namespace EndlessEngine.Config
{
    /// <summary>
    /// Economy configuration: resource generation rates, costs, and caps.
    /// All values are read-only at runtime.
    /// </summary>
    [CreateAssetMenu(fileName = "EconomyConfig", menuName = "Endless Engine/Config/Economy")]
    public class EconomyConfigSO : ScriptableObject
    {
        [Header("Resource Generation")]
        [Tooltip("Base idle yield rate (resources per second) at prestige 0.")]
        public float IdleYieldRateBase = 10f;

        [Tooltip("Base multiplier per prestige count for idle yield.")]
        public float BaseMultiplierPerPrestige = 1.5f;

        [Tooltip("Maximum idle yield multiplier cap (prestige scaling ceiling).")]
        public float IdleYieldMultiplierCap = 100f;

        [Header("Starting Values")]
        [Tooltip("Gold the player starts with on a new game or after prestige reset.")]
        public long StartingGold = 0L;

        [Header("Caps")]
        [Tooltip("Maximum resources the player can hold.")]
        public long ResourceHardCap = 1_000_000_000L;

        [Header("Offline")]
        [Tooltip("Maximum hours of offline yield credited per session.")]
        public float OfflineCapHours = 8f;

        [Tooltip("Yield modifier applied when the player is in Active run state during offline (not IdleRecovery). Range [0, 1].")]
        public float ActiveRunStateOfflineModifier = 0.5f;

        [Header("Enemy Drop")]
        [Tooltip("Base gold drop per enemy kill before wave scaling.")]
        public float BaseGoldDropPerEnemy = 1f;

        [Tooltip("Wave scaling exponent for gold drops.")]
        public float GoldDropScalingExponent = 1.2f;
    }
}

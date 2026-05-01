using UnityEngine;

namespace EndlessEngine.ClickLoop
{
    /// <summary>
    /// Defines a tappable/clickable target object type.
    /// One asset per target type. World instances are ClickTarget components.
    ///
    /// Works identically to HarvestNodeConfigSO but driven by clicks, not cursor overlap.
    /// </summary>
    [CreateAssetMenu(fileName = "ClickTarget_", menuName = "Endless Engine/Click Loop/Target Config")]
    public class ClickTargetConfigSO : ScriptableObject
    {
        [Header("Identity")]
        public string TargetId;
        public string DisplayName;

        [Header("Health")]
        [Tooltip("HP the target has before it is destroyed. Scaled by ClickTargetMaxHP stat.")]
        public float MaxHP = 20f;

        [Tooltip("Base damage per click. Scaled by ClickDamage stat.")]
        public float DamagePerClick = 1f;

        [Header("Yield")]
        [Tooltip("Gold awarded when target is fully destroyed. Scaled by ClickYieldMultiplier stat.")]
        public float BaseYield = 10f;

        [Tooltip("Currency to award. Empty = primary gold via EconomyService.")]
        public string YieldCurrencyId = "";

        [Tooltip("Award a fraction of BaseYield on every click (BaseYield/MaxHP × damage). " +
                 "False = full yield only on destruction.")]
        public bool AwardYieldPerClick = false;

        [Header("Respawn")]
        [Tooltip("Seconds until the target respawns after destruction. Scaled by ClickTargetRespawnRate stat.")]
        public float RespawnSeconds = 3f;

        [Header("Visuals")]
        [Tooltip("Prefab for this target type. Must have ClickTarget component.")]
        public GameObject Prefab;

        [Tooltip("Particle/VFX prefab played on destruction.")]
        public GameObject DestructionVFXPrefab;

        [Header("Combo")]
        [Tooltip("Combo points this target adds per click.")]
        public float ComboContribution = 1f;
    }
}

using UnityEngine;
using EndlessEngine.Config;

namespace EndlessEngine.Harvest
{
    /// <summary>
    /// Defines a harvestable node type: tree, mineral, creature, asteroid etc.
    /// One asset per node type. Instances in the world are HarvestNode components.
    ///
    /// YieldCurrencyId links to CurrencyService — empty string means primary gold.
    /// </summary>
    [CreateAssetMenu(fileName = "HarvestNode_", menuName = "Endless Engine/Harvest/Node Config")]
    public class HarvestNodeConfigSO : ScriptableObject
    {
        [Header("Identity")]
        public string NodeId;
        public string DisplayName;

        [Header("Health & Harvest")]
        [Tooltip("How many HP the node has before it is fully harvested.")]
        public float MaxHP = 10f;

        [Tooltip("Harvest damage applied per tick while cursor overlaps the node.")]
        public float DamagePerTick = 1f;

        [Tooltip("Ticks per second the HarvestLoopService fires against this node type.")]
        public float TickRate = 4f;

        [Header("Yield")]
        [Tooltip("Base yield when the node is fully depleted. Scaled by HarvestYieldMultiplier stat.")]
        public float BaseYield = 5f;

        [Tooltip("Currency to award. Empty = primary gold via EconomyService.")]
        public string YieldCurrencyId = "";

        [Tooltip("Partial yield per tick (BaseYield / MaxHP * DamagePerTick). Calculated at runtime.")]
        public bool AwardYieldPerTick = true;

        [Header("Respawn")]
        [Tooltip("Seconds until the node respawns after depletion. Scaled by HarvestNodeRespawnRate stat.")]
        public float RespawnSeconds = 5f;

        [Header("Visuals")]
        [Tooltip("Prefab spawned for this node type. Must have HarvestNode component.")]
        public GameObject Prefab;

        [Tooltip("Particle prefab played on depletion.")]
        public GameObject DepletionVFXPrefab;

        [Header("Combo")]
        [Tooltip("Points this node adds to the combo meter when harvested.")]
        public float ComboContribution = 1f;
    }
}

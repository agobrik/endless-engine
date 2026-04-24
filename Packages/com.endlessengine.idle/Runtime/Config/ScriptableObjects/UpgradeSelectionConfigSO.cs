using UnityEngine;

namespace EndlessEngine.Config
{
    /// <summary>
    /// Configuration for the between-wave upgrade card selection system.
    /// One instance per realm pack — swap with RealmPackSO for per-realm tuning.
    ///
    /// GDD: design/gdd/upgrade-selection.md — Rule 12 / Tuning Knobs
    /// ADR: ADR-0012 — Upgrade Card Selection
    /// </summary>
    [CreateAssetMenu(fileName = "UpgradeSelectionConfig", menuName = "Endless Engine/Config/Upgrade Selection Config")]
    public class UpgradeSelectionConfigSO : ScriptableObject
    {
        [Header("Card Pool")]
        [Tooltip("Number of cards to present per selection event. GDD Rule 2: fallback to pool size if pool < NumCardsToShow.")]
        [Range(1, 5)]
        public int NumCardsToShow = 3;

        [Header("Consolation")]
        [Tooltip("Gold granted when the pool is exhausted (all nodes maxed or unaffordable). GDD Rule 2 / EC-UGS-01.")]
        public long GoldConsolationAmount = 500L;

        [Header("Cooldown")]
        [Tooltip("Waves before a shown-but-not-chosen node regains full selection weight. GDD Rule 5.")]
        [Range(0, 20)]
        public int SelectionCooldownWaves = 3;

        [Tooltip("Weight multiplier applied to nodes in cooldown. 0 = never show; 1 = no penalty. GDD Rule 5.")]
        [Range(0f, 1f)]
        public float CooldownWeightMultiplier = 0.25f;
    }
}

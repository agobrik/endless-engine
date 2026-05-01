using System;
using System.Collections.Generic;
using UnityEngine;
using EndlessEngine.Config;
using EndlessEngine.Economy;
using Debug = UnityEngine.Debug;

namespace EndlessEngine.Upgrade
{
    /// <summary>
    /// Between-wave upgrade card selection coordinator.
    ///
    /// Responsibilities:
    ///   - On OnUpgradeSelectionTriggered: build a weighted card pool (Rules 1-6 GDD)
    ///   - Fire OnCardsReady(UpgradeCardData[]) for Upgrade Card UI
    ///   - On OnCardChosen(cardIndex): route to EconomyService.TryPurchase or consolation
    ///   - Fire OnUpgradeSelected(nodeId) to resume AutoBattleController wave transition
    ///
    /// This class does NOT own UI, stat application, or Gold deduction — it delegates
    /// those to Upgrade Card UI, UpgradeApplicationSystem, and EconomyService respectively.
    ///
    /// ADR: ADR-0012 — Upgrade Card Selection
    /// GDD: design/gdd/upgrade-selection.md
    /// </summary>
    public class UpgradeSelectionService : MonoBehaviour
    {
        // ── Events ────────────────────────────────────────────────────────────────

        /// <summary>Fires when cards are ready for display. Upgrade Card UI subscribes.</summary>
        public static event Action<UpgradeCardData[]> OnCardsReady;

        /// <summary>
        /// Fires after a card is chosen and purchase resolves.
        /// AutoBattleController.NotifyUpgradeSelected() subscribes to resume combat.
        /// Parameter: nodeId of the chosen upgrade, or "GOLD_CONSOLATION".
        /// </summary>
        public static event Action<string> OnUpgradeSelected;

        // ── Constants ─────────────────────────────────────────────────────────────

        public const string GoldConsolationNodeId = "GOLD_CONSOLATION";

        // ── Dependencies (inject via Initialize) ──────────────────────────────────

        private IUpgradeTreeQuery   _upgradeTreeQuery;
        private UpgradeTreeService  _upgradeTreeService;   // for availability + SimulateEffect
        private EconomyService      _economyService;

        // ── State ─────────────────────────────────────────────────────────────────

        private bool               _selectionInProgress;  // EC-UGS-05 guard
        private UpgradeCardData[]  _currentCards;
        private int                _currentWave;          // updated by WaveSpawnManager via SetCurrentWave

        /// <summary>Cooldown state: nodeId → wave number when it was shown-but-not-chosen.</summary>
        private readonly Dictionary<string, int> _cooldownWave = new();

        // ── Initialization ────────────────────────────────────────────────────────

        /// <summary>
        /// Wire dependencies. Call before OnUpgradeSelectionTriggered can fire.
        /// </summary>
        public void Initialize(
            UpgradeTreeService  upgradeTreeService,
            IUpgradeTreeQuery   upgradeTreeQuery,
            EconomyService      economyService)
        {
            _upgradeTreeService = upgradeTreeService;
            _upgradeTreeQuery   = upgradeTreeQuery;
            _economyService     = economyService;
        }

        private void OnEnable()
        {
            Combat.AutoBattleController.OnUpgradeSelectionTriggered += HandleSelectionTriggered;
        }

        private void OnDisable()
        {
            Combat.AutoBattleController.OnUpgradeSelectionTriggered -= HandleSelectionTriggered;
        }

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>
        /// Called by Upgrade Card UI when the player selects a card by index.
        /// Routes the choice to EconomyService.TryPurchase or consolation handler.
        /// </summary>
        public void NotifyCardChosen(int cardIndex)
        {
            if (!_selectionInProgress) return;
            if (_currentCards == null || cardIndex < 0 || cardIndex >= _currentCards.Length) return;

            var card = _currentCards[cardIndex];

            if (card.NodeId == GoldConsolationNodeId)
            {
                HandleConsolationChosen();
                return;
            }

            // Re-check affordability (EC-UGS-02 / Rule 8)
            long cost = _upgradeTreeQuery?.GetNodeCost(card.NodeId) ?? 0L;
            if (_economyService != null && _economyService.CurrentResources < cost)
            {
                // Re-draw: the chosen card is now unaffordable (GDD Rule 8)
                BuildAndPresentCards();
                return;
            }

            // Remove from cooldown (chosen nodes have no penalty)
            _cooldownWave.Remove(card.NodeId);

            _economyService?.TryPurchase(card.NodeId);
            FinishSelection(card.NodeId);
        }

        /// <summary>Updates the current wave number. Call from WaveSpawnManager on wave start.</summary>
        public void SetCurrentWave(int wave) => _currentWave = wave;

        // ── Event Handlers ────────────────────────────────────────────────────────

        private void HandleSelectionTriggered()
        {
            // EC-UGS-05: ignore double-trigger
            if (_selectionInProgress) return;
            _selectionInProgress = true;
            BuildAndPresentCards();
        }

        // ── Pool building ─────────────────────────────────────────────────────────

        private void BuildAndPresentCards()
        {
            if (_upgradeTreeService == null || !_upgradeTreeService.IsReady)
            {
                Debug.LogWarning("[UpgradeSelectionService] UpgradeTreeService not ready — skipping selection.");
                FinishSelection(GoldConsolationNodeId);
                return;
            }

            var cfg = ConfigRegistry.UpgradeSelection;
            int numCards = cfg != null ? cfg.NumCardsToShow : 3;

            List<UpgradeNode> pool = BuildEligiblePool();

            if (pool.Count == 0)
            {
                // EC-UGS-01: no eligible nodes — consolation only
                PresentConsolationOnly(cfg);
                return;
            }

            int cardCount = Mathf.Min(numCards, pool.Count);
            List<UpgradeNode> drawn = WeightedDrawWithoutReplacement(pool, cardCount, cfg);

            // GDD Rule 6: re-draw once if all same stat type
            if (drawn.Count == 3 && AllSameStatType(drawn))
            {
                var redrawn = WeightedDrawWithoutReplacement(pool, cardCount, cfg);
                drawn = redrawn;
            }

            _currentCards = BuildCardData(drawn);
            OnCardsReady?.Invoke(_currentCards);
        }

        private List<UpgradeNode> BuildEligiblePool()
        {
            var available = _upgradeTreeService.GetAvailableNodes();
            if (_economyService == null) return available;

            double balance = _economyService.CurrentResources;
            var eligible = new List<UpgradeNode>(available.Count);
            foreach (var node in available)
            {
                long cost = _upgradeTreeQuery?.GetNodeCost(node.Config.NodeId) ?? 0L;
                if (balance >= cost)
                    eligible.Add(node);
            }
            return eligible;
        }

        private List<UpgradeNode> WeightedDrawWithoutReplacement(
            List<UpgradeNode>         pool,
            int                       count,
            UpgradeSelectionConfigSO  cfg)
        {
            // Build a working list with effective weights (cooldown applied)
            var workPool   = new List<(UpgradeNode node, float weight)>(pool.Count);
            float cooldownMult = cfg != null ? cfg.CooldownWeightMultiplier : 0.25f;
            int   cooldownWaves = cfg != null ? cfg.SelectionCooldownWaves : 3;

            float totalWeight = 0f;
            foreach (var node in pool)
            {
                float w = node.Config.SelectionWeight;
                if (w <= 0f) w = 1f; // EC-UGS-04: zero-weight fallback

                // Apply cooldown multiplier if within window
                if (_cooldownWave.TryGetValue(node.Config.NodeId, out int shownAtWave))
                {
                    if (_currentWave - shownAtWave <= cooldownWaves)
                        w *= cooldownMult;
                }

                workPool.Add((node, w));
                totalWeight += w;
            }

            // EC-UGS-04: all-zero guard
            if (totalWeight <= 0f)
            {
                Debug.LogError("[UpgradeSelectionService] All node weights are 0 — using uniform fallback.");
                totalWeight = workPool.Count;
                for (int i = 0; i < workPool.Count; i++)
                    workPool[i] = (workPool[i].node, 1f);
            }

            var drawn = new List<UpgradeNode>(count);
            for (int i = 0; i < count && workPool.Count > 0; i++)
            {
                float roll = UnityEngine.Random.value * totalWeight;
                float cumulative = 0f;
                int selectedIdx = workPool.Count - 1; // fallback: last item

                for (int j = 0; j < workPool.Count; j++)
                {
                    cumulative += workPool[j].weight;
                    if (cumulative >= roll)
                    {
                        selectedIdx = j;
                        break;
                    }
                }

                drawn.Add(workPool[selectedIdx].node);
                totalWeight -= workPool[selectedIdx].weight;
                workPool.RemoveAt(selectedIdx);
            }

            return drawn;
        }

        private static bool AllSameStatType(List<UpgradeNode> cards)
        {
            if (cards.Count < 2) return false;
            var first = cards[0].Config.AffectedStat;
            for (int i = 1; i < cards.Count; i++)
                if (cards[i].Config.AffectedStat != first) return false;
            return true;
        }

        private UpgradeCardData[] BuildCardData(List<UpgradeNode> drawn)
        {
            var cards = new UpgradeCardData[drawn.Count];
            for (int i = 0; i < drawn.Count; i++)
            {
                var node = drawn[i];
                long cost = _upgradeTreeQuery?.GetNodeCost(node.Config.NodeId) ?? 0L;
                bool affordable = _economyService != null && _economyService.CurrentResources >= cost;
                float preview = Core.UpgradeApplicationSystem.SimulateEffect(node.Config.NodeId);

                cards[i] = new UpgradeCardData
                {
                    NodeId             = node.Config.NodeId,
                    DisplayName        = node.Config.DisplayName,
                    Description        = node.Config.Description,
                    Cost               = cost,
                    IsAffordable       = affordable,
                    AffectedStat       = node.Config.AffectedStat,
                    EffectPreviewValue = preview,
                };
            }
            return cards;
        }

        private void PresentConsolationOnly(UpgradeSelectionConfigSO cfg)
        {
            long consolation = cfg != null ? cfg.GoldConsolationAmount : 500L;
            _currentCards = new[]
            {
                new UpgradeCardData
                {
                    NodeId       = GoldConsolationNodeId,
                    DisplayName  = "No Upgrades Available",
                    Description  = $"Take {consolation} Gold instead.",
                    Cost         = 0L,
                    IsAffordable = true,
                }
            };
            OnCardsReady?.Invoke(_currentCards);
        }

        // ── Resolution ────────────────────────────────────────────────────────────

        private void HandleConsolationChosen()
        {
            var cfg = ConfigRegistry.UpgradeSelection;
            long amount = cfg != null ? cfg.GoldConsolationAmount : 500L;
            _economyService?.AddResources(amount);
            FinishSelection(GoldConsolationNodeId);
        }

        private void FinishSelection(string chosenNodeId)
        {
            // Record shown-but-not-chosen nodes in cooldown
            if (_currentCards != null)
            {
                foreach (var card in _currentCards)
                {
                    if (card.NodeId != chosenNodeId && card.NodeId != GoldConsolationNodeId)
                        _cooldownWave[card.NodeId] = _currentWave;
                }
            }

            _selectionInProgress = false;
            _currentCards        = null;
            OnUpgradeSelected?.Invoke(chosenNodeId);
        }

        // ── Test helpers ──────────────────────────────────────────────────────────

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>Directly fires HandleSelectionTriggered for tests.</summary>
        public void SimulateSelectionTriggeredForTesting() => HandleSelectionTriggered();

        /// <summary>Injects current wave number for cooldown tests.</summary>
        public void SetCurrentWaveForTesting(int wave) => _currentWave = wave;

        /// <summary>Injects a cooldown entry for a node (simulates previous shown-not-chosen).</summary>
        public void InjectCooldownForTesting(string nodeId, int shownAtWave) => _cooldownWave[nodeId] = shownAtWave;

        /// <summary>Returns the current presented cards for assertion.</summary>
        public UpgradeCardData[] GetCurrentCardsForTesting() => _currentCards;

        /// <summary>Clears static events for test isolation.</summary>
        public static void ClearStaticEventsForTesting()
        {
            OnCardsReady      = null;
            OnUpgradeSelected = null;
        }

        /// <summary>Alias for ClearStaticEventsForTesting — consistent naming with other systems.</summary>
        public static void ClearStaticSubscribersForTesting() => ClearStaticEventsForTesting();

        /// <summary>Fires OnCardsReady directly for integration tests.</summary>
        public static void FireOnCardsReadyForTesting(UpgradeCardData[] cards)
            => OnCardsReady?.Invoke(cards);

        /// <summary>Fires OnUpgradeSelected directly for integration tests.</summary>
        public static void FireOnUpgradeSelectedForTesting(string nodeId)
            => OnUpgradeSelected?.Invoke(nodeId);

        /// <summary>
        /// Injects pre-built cards and sets _selectionInProgress = true.
        /// Allows NotifyCardChosen to be called without a full service initialization.
        /// </summary>
        public void InjectCardStateForTesting(UpgradeCardData[] cards)
        {
            _currentCards        = cards;
            _selectionInProgress = true;
        }
#endif
    }

    // ── Data transfer object ──────────────────────────────────────────────────────

    /// <summary>
    /// Display data for a single upgrade card. Pre-computed by UpgradeSelectionService
    /// so that Upgrade Card UI does not need to reference config SOs directly.
    ///
    /// GDD: design/gdd/upgrade-selection.md — UI Requirements
    /// </summary>
    public struct UpgradeCardData
    {
        public string   NodeId;
        public string   DisplayName;
        public string   Description;
        public long     Cost;
        public bool     IsAffordable;
        public StatType AffectedStat;       // primary stat for icon routing
        public float    EffectPreviewValue; // projected effective value if purchased
    }
}

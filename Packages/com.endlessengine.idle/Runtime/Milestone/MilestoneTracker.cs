using System;
using System.Collections.Generic;
using UnityEngine;
using EndlessEngine.Config;
using EndlessEngine.Economy;
using EndlessEngine.Generator;
using EndlessEngine.Prestige;
using EndlessEngine.SaveAndLoad;

namespace EndlessEngine.Milestone
{
    /// <summary>
    /// Tracks milestone completion. Listens to engine events and checks conditions
    /// whenever a relevant metric changes. Completed milestones are saved via ISaveStateProvider.
    ///
    /// Wire-up: VerticalSliceBootstrap.Initialize() → MilestoneTracker.Initialize().
    /// Subscribe to OnMilestoneCompleted for achievement popup / notification.
    ///
    /// ADR: ADR-0004 — ISaveStateProvider Pull-Based Save Collection
    /// </summary>
    public class MilestoneTracker : MonoBehaviour, ISaveStateProvider
    {
        // ── ISaveStateProvider ────────────────────────────────────────────────────

        public int ProviderOrder => SaveConstants.SaveProviderOrder.Milestone;

        // ── Events ────────────────────────────────────────────────────────────────

        /// <summary>Fires when a milestone is completed for the first time this session.</summary>
        public static event Action<MilestoneConfigSO> OnMilestoneCompleted;

        // ── Runtime state ─────────────────────────────────────────────────────────

        private MilestoneDatabaseSO  _database;
        private EconomyService       _economyService;
        private PrestigeStateManager _prestigeManager; // optional
        private CurrencyService      _currencyService; // optional
        private GeneratorSystem      _generatorSystem; // optional

        private readonly HashSet<string> _completed = new HashSet<string>();

        // ── Tracked counters (incremented by listening to events) ─────────────────

        private long   _totalGoldEarned;
        private int    _totalConversions;
        private long   _totalClicks;
        private int    _runsCompleted;
        private int    _upgradesPurchased;

        private bool _initialized;

        // ── Lifecycle ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Initialize the tracker. CurrencyService and GeneratorSystem are optional.
        /// </summary>
        public void Initialize(
            MilestoneDatabaseSO  database,
            EconomyService        economyService,
            PrestigeStateManager  prestigeManager  = null,
            CurrencyService       currencyService  = null,
            GeneratorSystem       generatorSystem   = null)
        {
            _database        = database;
            _economyService  = economyService;
            _prestigeManager = prestigeManager;
            _currencyService = currencyService;
            _generatorSystem = generatorSystem;
            _initialized     = true;
        }

        private void OnEnable()
        {
            EconomyService.OnResourcesChanged  += HandleResourcesChanged;
            EconomyService.OnUpgradePurchased  += HandleUpgradePurchased;
            PrestigeStateManager.OnPrestigeStarted += HandlePrestigeStarted;
            ConversionService.OnConverted      += HandleConverted;
        }

        private void OnDisable()
        {
            EconomyService.OnResourcesChanged  -= HandleResourcesChanged;
            EconomyService.OnUpgradePurchased  -= HandleUpgradePurchased;
            PrestigeStateManager.OnPrestigeStarted -= HandlePrestigeStarted;
            ConversionService.OnConverted      -= HandleConverted;
        }

        // ── ISaveStateProvider ────────────────────────────────────────────────────

        public void OnBeforeSave(SaveData saveData)
        {
            saveData.CompletedMilestones ??= new HashSet<string>();
            saveData.CompletedMilestones.Clear();
            foreach (var id in _completed)
                saveData.CompletedMilestones.Add(id);
        }

        public void OnAfterLoad(SaveData saveData)
        {
            _completed.Clear();
            if (saveData.CompletedMilestones != null)
                foreach (var id in saveData.CompletedMilestones)
                    _completed.Add(id);
        }

        // ── Event handlers ────────────────────────────────────────────────────────

        private void HandleResourcesChanged(long current, long delta)
        {
            if (delta > 0) _totalGoldEarned += delta;
            CheckAll();
        }

        private void HandleUpgradePurchased(string nodeId, long cost)
        {
            _upgradesPurchased++;
            CheckAll();
        }

        private void HandlePrestigeStarted()
        {
            if (_database == null) return;
            foreach (var m in _database.Milestones)
            {
                if (m != null && m.ResetsOnPrestige)
                    _completed.Remove(m.MilestoneId);
            }
        }

        private void HandleConverted(string recipeId, int runs, double totalInput, double totalOutput)
        {
            _totalConversions += runs;
            CheckAll();
        }

        /// <summary>Called externally when the player completes a run. Increments counter and checks.</summary>
        public void NotifyRunCompleted()
        {
            _runsCompleted++;
            CheckAll();
        }

        /// <summary>Called externally when the player clicks. Increments counter and checks.</summary>
        public void NotifyClick()
        {
            _totalClicks++;
            // Only check every 100 clicks to avoid per-frame evaluation
            if (_totalClicks % 100 == 0) CheckAll();
        }

        // ── Condition evaluation ─────────────────────────────────────────────────

        private void CheckAll()
        {
            if (!_initialized || _database == null || _database.Milestones == null) return;

            foreach (var milestone in _database.Milestones)
            {
                if (milestone == null) continue;
                if (_completed.Contains(milestone.MilestoneId)) continue;
                if (EvaluateNode(milestone.Condition))
                    CompleteMilestone(milestone);
            }
        }

        private bool EvaluateNode(MilestoneConditionNode node)
        {
            if (node == null) return false;

            switch (node.Type)
            {
                case MilestoneConditionType.And:
                    if (node.Children == null || node.Children.Count == 0) return true;
                    foreach (var child in node.Children)
                        if (!EvaluateNode(child)) return false;
                    return true;

                case MilestoneConditionType.Or:
                    if (node.Children == null || node.Children.Count == 0) return false;
                    foreach (var child in node.Children)
                        if (EvaluateNode(child)) return true;
                    return false;

                case MilestoneConditionType.Threshold:
                    return EvaluateThreshold(node);

                default:
                    return false;
            }
        }

        private bool EvaluateThreshold(MilestoneConditionNode node)
        {
            double value = GetMetricValue(node.Metric, node.MetricId);
            return value >= node.Threshold;
        }

        private double GetMetricValue(MilestoneMetric metric, string metricId)
        {
            switch (metric)
            {
                case MilestoneMetric.TotalGoldEarned:
                    return _totalGoldEarned;

                case MilestoneMetric.CurrentGold:
                    return _economyService != null ? _economyService.CurrentResources : 0;

                case MilestoneMetric.PrestigeCount:
                    return _prestigeManager != null ? _prestigeManager.PrestigeCount : 0;

                case MilestoneMetric.WaveNumber:
                    // WaveNumber read from EconomyService indirectly — use a static fallback
                    return _waveNumber;

                case MilestoneMetric.GeneratorCount:
                    return _generatorSystem != null ? _generatorSystem.TotalGeneratorsOwned() : 0;

                case MilestoneMetric.GeneratorTypeCount:
                    return _generatorSystem != null ? _generatorSystem.GetCount(metricId) : 0;

                case MilestoneMetric.UpgradesPurchased:
                    return _upgradesPurchased;

                case MilestoneMetric.SecondaryCurrencyBalance:
                    return _currencyService != null ? _currencyService.GetBalance(metricId) : 0;

                case MilestoneMetric.TotalConversions:
                    return _totalConversions;

                case MilestoneMetric.TotalClicks:
                    return _totalClicks;

                case MilestoneMetric.RunsCompleted:
                    return _runsCompleted;

                default:
                    return 0;
            }
        }

        // Wave number is fed in from outside since MilestoneTracker doesn't own it
        private int _waveNumber;
        public void NotifyWaveChanged(int waveNumber)
        {
            _waveNumber = waveNumber;
            CheckAll();
        }

        private void CompleteMilestone(MilestoneConfigSO milestone)
        {
            _completed.Add(milestone.MilestoneId);

            // Apply rewards
            if (milestone.GoldReward > 0)
                _economyService?.AddResources(milestone.GoldReward);

            if (!string.IsNullOrEmpty(milestone.RewardCurrencyId) && milestone.RewardCurrencyAmount > 0)
                _currencyService?.Add(milestone.RewardCurrencyId, milestone.RewardCurrencyAmount);

            // Notify UI
            OnMilestoneCompleted?.Invoke(milestone);
        }

        /// <summary>Returns true if the milestone with the given ID has been completed.</summary>
        public bool IsCompleted(string milestoneId) => _completed.Contains(milestoneId);

        // ── Test helpers ──────────────────────────────────────────────────────────

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public static void ClearSubscribersForTesting() => OnMilestoneCompleted = null;

        public void InjectCompletedForTesting(string milestoneId) => _completed.Add(milestoneId);

        public bool IsCompletedForTesting(string milestoneId) => IsCompleted(milestoneId);

        public void InjectGoldEarnedForTesting(long amount) => _totalGoldEarned = amount;

        public void InjectClicksForTesting(long count) => _totalClicks = count;

        public void InjectConversionsForTesting(int count) => _totalConversions = count;

        public void InjectUpgradesForTesting(int count) => _upgradesPurchased = count;

        public void InjectRunsCompletedForTesting(int count) => _runsCompleted = count;

        /// <summary>Force-evaluate all milestones. Used in tests after injecting state.</summary>
        public void ForceCheckForTesting() => CheckAll();

        /// <summary>
        /// Subscribes to runtime events for testing.
        /// Call after Initialize() in EditMode tests where OnEnable does not fire.
        /// </summary>
        public void SubscribeForTesting()
        {
            EconomyService.OnResourcesChanged      += HandleResourcesChanged;
            EconomyService.OnUpgradePurchased      += HandleUpgradePurchased;
            PrestigeStateManager.OnPrestigeStarted += HandlePrestigeStarted;
            ConversionService.OnConverted          += HandleConverted;
        }

        /// <summary>Unsubscribes from runtime events. Call in test TearDown.</summary>
        public void UnsubscribeForTesting()
        {
            EconomyService.OnResourcesChanged      -= HandleResourcesChanged;
            EconomyService.OnUpgradePurchased      -= HandleUpgradePurchased;
            PrestigeStateManager.OnPrestigeStarted -= HandlePrestigeStarted;
            ConversionService.OnConverted          -= HandleConverted;
        }
#endif
    }
}

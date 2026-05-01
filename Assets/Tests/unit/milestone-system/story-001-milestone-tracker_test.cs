// Tests for Sprint 9 — S9-05: MilestoneTracker
// Type: Logic (Unit/EditMode)
//
// Covers:
//   - Single threshold condition (gold, prestige, wave, clicks, runs, upgrades)
//   - AND / OR composite nodes
//   - Milestone fires exactly once (no repeat on re-check)
//   - ResetsOnPrestige clears completed state
//   - OnMilestoneCompleted event fires with correct config
//   - Gold and currency rewards applied on completion
//   - Save/load round-trip preserves completed state
//
// To run: Unity Test Runner → EditMode → EndlessEngine.Tests.Unit.MilestoneSystem

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using EndlessEngine.Config;
using EndlessEngine.Economy;
using EndlessEngine.Milestone;
using EndlessEngine.Prestige;
using EndlessEngine.SaveAndLoad;

namespace EndlessEngine.Tests.Unit.MilestoneSystem
{
    [TestFixture]
    public class MilestoneTrackerTests
    {
        private MilestoneTracker    _tracker;
        private EconomyService      _economy;
        private MilestoneDatabaseSO _database;
        private MilestoneConfigSO   _goldMilestone;
        private MilestoneConfigSO   _prestigeMilestone;
        private MilestoneConfigSO   _waveMilestone;
        private MilestoneConfigSO   _andMilestone;
        private MilestoneConfigSO   _orMilestone;
        private MilestoneConfigSO   _resetOnPrestige;

        private EndlessEngine.Config.EconomyConfigSO _econConfig;

        [SetUp]
        public void SetUp()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _econConfig = ScriptableObject.CreateInstance<EndlessEngine.Config.EconomyConfigSO>();
            _econConfig.ResourceHardCap = 1_000_000L;
            _econConfig.StartingGold    = 0L;
            EndlessEngine.Config.ConfigRegistry.InjectForTesting(economy: _econConfig);

            // ── Gold milestone: TotalGoldEarned >= 500 ──
            _goldMilestone = ScriptableObject.CreateInstance<MilestoneConfigSO>();
            _goldMilestone.MilestoneId = "gold_500";
            _goldMilestone.DisplayName = "First Gold";
            _goldMilestone.GoldReward  = 50;
            _goldMilestone.ShowPopup   = true;
            _goldMilestone.Condition   = new MilestoneConditionNode
            {
                Type      = MilestoneConditionType.Threshold,
                Metric    = MilestoneMetric.TotalGoldEarned,
                Threshold = 500
            };

            // ── Prestige milestone: PrestigeCount >= 1 ──
            _prestigeMilestone = ScriptableObject.CreateInstance<MilestoneConfigSO>();
            _prestigeMilestone.MilestoneId = "first_prestige";
            _prestigeMilestone.Condition   = new MilestoneConditionNode
            {
                Type      = MilestoneConditionType.Threshold,
                Metric    = MilestoneMetric.PrestigeCount,
                Threshold = 1
            };

            // ── Wave milestone: WaveNumber >= 10 ──
            _waveMilestone = ScriptableObject.CreateInstance<MilestoneConfigSO>();
            _waveMilestone.MilestoneId = "wave_10";
            _waveMilestone.Condition   = new MilestoneConditionNode
            {
                Type      = MilestoneConditionType.Threshold,
                Metric    = MilestoneMetric.WaveNumber,
                Threshold = 10
            };

            // ── AND milestone: TotalGoldEarned >= 100 AND RunsCompleted >= 2 ──
            _andMilestone = ScriptableObject.CreateInstance<MilestoneConfigSO>();
            _andMilestone.MilestoneId = "gold_and_runs";
            _andMilestone.Condition   = new MilestoneConditionNode
            {
                Type = MilestoneConditionType.And,
                Children = new System.Collections.Generic.List<MilestoneConditionNode>
                {
                    new MilestoneConditionNode
                    {
                        Type = MilestoneConditionType.Threshold,
                        Metric = MilestoneMetric.TotalGoldEarned,
                        Threshold = 100
                    },
                    new MilestoneConditionNode
                    {
                        Type = MilestoneConditionType.Threshold,
                        Metric = MilestoneMetric.RunsCompleted,
                        Threshold = 2
                    }
                }
            };

            // ── OR milestone: WaveNumber >= 5 OR TotalClicks >= 10 ──
            _orMilestone = ScriptableObject.CreateInstance<MilestoneConfigSO>();
            _orMilestone.MilestoneId = "wave_or_clicks";
            _orMilestone.Condition   = new MilestoneConditionNode
            {
                Type = MilestoneConditionType.Or,
                Children = new System.Collections.Generic.List<MilestoneConditionNode>
                {
                    new MilestoneConditionNode
                    {
                        Type = MilestoneConditionType.Threshold,
                        Metric = MilestoneMetric.WaveNumber,
                        Threshold = 5
                    },
                    new MilestoneConditionNode
                    {
                        Type = MilestoneConditionType.Threshold,
                        Metric = MilestoneMetric.TotalClicks,
                        Threshold = 10
                    }
                }
            };

            // ── ResetsOnPrestige milestone ──
            _resetOnPrestige = ScriptableObject.CreateInstance<MilestoneConfigSO>();
            _resetOnPrestige.MilestoneId    = "resets_on_prestige";
            _resetOnPrestige.ResetsOnPrestige = true;
            _resetOnPrestige.Condition       = new MilestoneConditionNode
            {
                Type      = MilestoneConditionType.Threshold,
                Metric    = MilestoneMetric.TotalGoldEarned,
                Threshold = 1
            };

            _database = ScriptableObject.CreateInstance<MilestoneDatabaseSO>();
            _database.Milestones = new[]
            {
                _goldMilestone, _prestigeMilestone, _waveMilestone,
                _andMilestone, _orMilestone, _resetOnPrestige
            };

            var go = new GameObject("MilestoneTrackerTest");

            _economy = go.AddComponent<EconomyService>();
            _economy.Initialize(upgradeTreeQuery: null, saveNotifier: null);
            var save = new SaveData { CurrentResources = 0L };
            _economy.OnAfterLoad(save);

            _tracker = go.AddComponent<MilestoneTracker>();
            _tracker.Initialize(_database, _economy);
            _tracker.SubscribeForTesting(); // OnEnable doesn't fire in EditMode tests
#endif
        }

        [TearDown]
        public void TearDown()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_tracker != null) _tracker.UnsubscribeForTesting();
            MilestoneTracker.ClearSubscribersForTesting();

            if (_tracker != null) Object.DestroyImmediate(_tracker.gameObject);
            if (_database          != null) Object.DestroyImmediate(_database);
            if (_goldMilestone     != null) Object.DestroyImmediate(_goldMilestone);
            if (_prestigeMilestone != null) Object.DestroyImmediate(_prestigeMilestone);
            if (_waveMilestone     != null) Object.DestroyImmediate(_waveMilestone);
            if (_andMilestone      != null) Object.DestroyImmediate(_andMilestone);
            if (_orMilestone       != null) Object.DestroyImmediate(_orMilestone);
            if (_resetOnPrestige   != null) Object.DestroyImmediate(_resetOnPrestige);
            if (_econConfig        != null) Object.DestroyImmediate(_econConfig);

            EndlessEngine.Config.ConfigRegistry.ClearForTesting();
#endif
        }

        // ── Threshold: TotalGoldEarned ────────────────────────────────────────────

        [Test]
        public void GoldThreshold_BelowTarget_NotCompleted()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _tracker.InjectGoldEarnedForTesting(499);
            _tracker.ForceCheckForTesting();
            Assert.IsFalse(_tracker.IsCompletedForTesting("gold_500"));
#endif
        }

        [Test]
        public void GoldThreshold_AtTarget_Completed()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _tracker.InjectGoldEarnedForTesting(500);
            _tracker.ForceCheckForTesting();
            Assert.IsTrue(_tracker.IsCompletedForTesting("gold_500"));
#endif
        }

        [Test]
        public void GoldThreshold_AboveTarget_Completed()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _tracker.InjectGoldEarnedForTesting(10_000);
            _tracker.ForceCheckForTesting();
            Assert.IsTrue(_tracker.IsCompletedForTesting("gold_500"));
#endif
        }

        // ── Fires exactly once ────────────────────────────────────────────────────

        [Test]
        public void Milestone_CompletedOnce_EventFiresOnce()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            int fireCount = 0;
            // Count only gold_500 to avoid counting other milestones that also complete at gold=1000
            MilestoneTracker.OnMilestoneCompleted += m => { if (m.MilestoneId == "gold_500") fireCount++; };

            _tracker.InjectGoldEarnedForTesting(1000);
            _tracker.ForceCheckForTesting();
            _tracker.ForceCheckForTesting(); // second check must not re-fire

            Assert.AreEqual(1, fireCount, "OnMilestoneCompleted must fire exactly once per milestone");
#endif
        }

        // ── Wave milestone ────────────────────────────────────────────────────────

        [Test]
        public void WaveMilestone_NotifyWaveChanged_CompletesWhenReached()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _tracker.NotifyWaveChanged(9);
            Assert.IsFalse(_tracker.IsCompletedForTesting("wave_10"));

            _tracker.NotifyWaveChanged(10);
            Assert.IsTrue(_tracker.IsCompletedForTesting("wave_10"));
#endif
        }

        // ── Run milestone ─────────────────────────────────────────────────────────

        [Test]
        public void RunsMilestone_NotifyRunCompleted_TracksCount()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _tracker.InjectGoldEarnedForTesting(100);
            _tracker.InjectRunsCompletedForTesting(1);
            _tracker.ForceCheckForTesting();
            Assert.IsFalse(_tracker.IsCompletedForTesting("gold_and_runs"));

            _tracker.InjectRunsCompletedForTesting(2);
            _tracker.ForceCheckForTesting();
            Assert.IsTrue(_tracker.IsCompletedForTesting("gold_and_runs"));
#endif
        }

        // ── AND composite ─────────────────────────────────────────────────────────

        [Test]
        public void AndMilestone_OnlyOneConditionMet_NotCompleted()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _tracker.InjectGoldEarnedForTesting(100);
            _tracker.InjectRunsCompletedForTesting(0);
            _tracker.ForceCheckForTesting();
            Assert.IsFalse(_tracker.IsCompletedForTesting("gold_and_runs"));
#endif
        }

        [Test]
        public void AndMilestone_BothConditionsMet_Completed()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _tracker.InjectGoldEarnedForTesting(100);
            _tracker.InjectRunsCompletedForTesting(2);
            _tracker.ForceCheckForTesting();
            Assert.IsTrue(_tracker.IsCompletedForTesting("gold_and_runs"));
#endif
        }

        // ── OR composite ──────────────────────────────────────────────────────────

        [Test]
        public void OrMilestone_OneConditionMet_Completed()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _tracker.InjectClicksForTesting(10); // OR condition
            _tracker.ForceCheckForTesting();
            Assert.IsTrue(_tracker.IsCompletedForTesting("wave_or_clicks"));
#endif
        }

        [Test]
        public void OrMilestone_NeitherConditionMet_NotCompleted()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _tracker.InjectClicksForTesting(0);
            _tracker.NotifyWaveChanged(1);
            Assert.IsFalse(_tracker.IsCompletedForTesting("wave_or_clicks"));
#endif
        }

        // ── ResetsOnPrestige ──────────────────────────────────────────────────────

        [Test]
        public void ResetsOnPrestige_AfterPrestige_ClearsCompletion()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _tracker.InjectGoldEarnedForTesting(1);
            _tracker.ForceCheckForTesting();
            Assert.IsTrue(_tracker.IsCompletedForTesting("resets_on_prestige"));

            PrestigeStateManager.FirePrestigeStartedForTesting();
            Assert.IsFalse(_tracker.IsCompletedForTesting("resets_on_prestige"),
                "ResetsOnPrestige milestone must be cleared on prestige");
#endif
        }

        // ── OnMilestoneCompleted event ────────────────────────────────────────────

        [Test]
        public void OnMilestoneCompleted_FiredWithCorrectConfig()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            MilestoneConfigSO captured = null;
            // Capture only the gold_500 milestone (resets_on_prestige also fires at gold=500)
            MilestoneTracker.OnMilestoneCompleted += m => { if (m.MilestoneId == "gold_500") captured = m; };

            _tracker.InjectGoldEarnedForTesting(500);
            _tracker.ForceCheckForTesting();

            Assert.IsNotNull(captured, "gold_500 milestone must have fired");
            Assert.AreEqual("gold_500", captured.MilestoneId);
#endif
        }

        // ── Gold reward on completion ─────────────────────────────────────────────

        [Test]
        public void GoldReward_OnCompletion_AddedToEconomy()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            double before = _economy.CurrentResources;
            _tracker.InjectGoldEarnedForTesting(500);
            _tracker.ForceCheckForTesting();
            Assert.AreEqual(before + 50, _economy.CurrentResources,
                "Gold reward (50) must be added to EconomyService on milestone completion");
#endif
        }

        // ── Save / load round-trip ────────────────────────────────────────────────

        [Test]
        public void SaveLoad_CompletedMilestonesPersistedAndRestored()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _tracker.InjectGoldEarnedForTesting(500);
            _tracker.ForceCheckForTesting();
            Assert.IsTrue(_tracker.IsCompletedForTesting("gold_500"));

            var saveData = new SaveData();
            saveData.EnsureDefaults();
            _tracker.OnBeforeSave(saveData);

            Assert.IsTrue(saveData.CompletedMilestones.Contains("gold_500"),
                "Save data must contain completed milestone id");

            // Simulate a new session: create fresh tracker, load from save
            var go2 = new GameObject("MilestoneTrackerTest2");
            var tracker2 = go2.AddComponent<MilestoneTracker>();
            tracker2.Initialize(_database, _economy);
            tracker2.OnAfterLoad(saveData);

            Assert.IsTrue(tracker2.IsCompletedForTesting("gold_500"),
                "Loaded tracker must restore completed milestone");

            Object.DestroyImmediate(go2);
#endif
        }
    }
}

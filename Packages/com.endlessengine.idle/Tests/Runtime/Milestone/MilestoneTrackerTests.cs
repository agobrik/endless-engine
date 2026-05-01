using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using EndlessEngine.Config;
using EndlessEngine.Economy;
using EndlessEngine.Economy.Math;
using EndlessEngine.Milestone;
using EndlessEngine.Prestige;
using EndlessEngine.SaveAndLoad;

namespace EndlessEngine.Tests.Milestone
{
    [TestFixture]
    public class MilestoneTrackerTests
    {
        private MilestoneTracker _tracker;
        private EconomyService   _economy;
        private List<MilestoneConfigSO> _completedLog;

        [SetUp]
        public void SetUp()
        {
            BigNumberFactory.Configure(NumberBackend.DoubleNumber);
            MilestoneTracker.ClearSubscribersForTesting();
            EconomyService.ClearSubscribersForTesting();

            var go = new GameObject("MilestoneTracker");
            _tracker = go.AddComponent<MilestoneTracker>();

            var ecoGo = new GameObject("EconomyService");
            _economy = ecoGo.AddComponent<EconomyService>();
            _economy.InjectStateForTesting(0.0, 1_000_000.0, 0.0);

            _completedLog = new List<MilestoneConfigSO>();
            MilestoneTracker.OnMilestoneCompleted += m => _completedLog.Add(m);
        }

        [TearDown]
        public void TearDown()
        {
            MilestoneTracker.ClearSubscribersForTesting();
            EconomyService.ClearSubscribersForTesting();
            PrestigeStateManager.ClearStaticEventsForTesting();
            _tracker.UnsubscribeForTesting();
            Object.DestroyImmediate(_tracker.gameObject);
            Object.DestroyImmediate(_economy.gameObject);
        }

        private MilestoneDatabaseSO BuildDatabase(params MilestoneConfigSO[] milestones)
        {
            var db = ScriptableObject.CreateInstance<MilestoneDatabaseSO>();
            db.Milestones = milestones;
            return db;
        }

        private MilestoneConfigSO MakeGoldMilestone(string id, double threshold)
        {
            var m = ScriptableObject.CreateInstance<MilestoneConfigSO>();
            m.MilestoneId = id;
            m.DisplayName = id;
            m.Condition   = new MilestoneConditionNode
            {
                Type      = MilestoneConditionType.Threshold,
                Metric    = MilestoneMetric.CurrentGold,
                Threshold = threshold
            };
            return m;
        }

        private MilestoneConfigSO MakeClickMilestone(string id, double threshold)
        {
            var m = ScriptableObject.CreateInstance<MilestoneConfigSO>();
            m.MilestoneId = id;
            m.Condition   = new MilestoneConditionNode
            {
                Type      = MilestoneConditionType.Threshold,
                Metric    = MilestoneMetric.TotalClicks,
                Threshold = threshold
            };
            return m;
        }

        // ── Basic completion ──────────────────────────────────────────────────────

        [Test]
        public void ForceCheck_WhenConditionMet_CompletesMilestone()
        {
            var m  = MakeGoldMilestone("gold_100", 100);
            var db = BuildDatabase(m);

            _tracker.Initialize(db, _economy);
            _tracker.SubscribeForTesting();
            _tracker.InjectGoldEarnedForTesting(200);
            _economy.InjectStateForTesting(200.0, 1_000_000.0, 0.0);

            _tracker.ForceCheckForTesting();

            Assert.AreEqual(1, _completedLog.Count);
            Assert.IsTrue(_tracker.IsCompleted("gold_100"));
        }

        [Test]
        public void ForceCheck_WhenConditionNotMet_DoesNotComplete()
        {
            var m  = MakeGoldMilestone("gold_500", 500);
            var db = BuildDatabase(m);

            _tracker.Initialize(db, _economy);
            _tracker.SubscribeForTesting();
            _economy.InjectStateForTesting(100.0, 1_000_000.0, 0.0);

            _tracker.ForceCheckForTesting();

            Assert.AreEqual(0, _completedLog.Count);
        }

        [Test]
        public void Milestone_DoesNotFireTwice_WhenCheckedRepeatedly()
        {
            var m  = MakeGoldMilestone("gold_10", 10);
            var db = BuildDatabase(m);

            _tracker.Initialize(db, _economy);
            _tracker.SubscribeForTesting();
            _economy.InjectStateForTesting(100.0, 1_000_000.0, 0.0);

            _tracker.ForceCheckForTesting();
            _tracker.ForceCheckForTesting();

            Assert.AreEqual(1, _completedLog.Count, "Completed milestone must not fire again.");
        }

        // ── AND / OR condition nodes ──────────────────────────────────────────────

        [Test]
        public void And_Condition_RequiresAllChildren()
        {
            var goldCond = new MilestoneConditionNode
            {
                Type      = MilestoneConditionType.Threshold,
                Metric    = MilestoneMetric.CurrentGold,
                Threshold = 50
            };
            var clickCond = new MilestoneConditionNode
            {
                Type      = MilestoneConditionType.Threshold,
                Metric    = MilestoneMetric.TotalClicks,
                Threshold = 100
            };

            var m = ScriptableObject.CreateInstance<MilestoneConfigSO>();
            m.MilestoneId = "and_test";
            m.Condition   = new MilestoneConditionNode
            {
                Type     = MilestoneConditionType.And,
                Children = new List<MilestoneConditionNode> { goldCond, clickCond }
            };

            var db = BuildDatabase(m);
            _tracker.Initialize(db, _economy);
            _tracker.SubscribeForTesting();

            // Only gold met
            _economy.InjectStateForTesting(100.0, 1_000_000.0, 0.0);
            _tracker.InjectClicksForTesting(5);
            _tracker.ForceCheckForTesting();
            Assert.AreEqual(0, _completedLog.Count, "AND requires both conditions.");

            // Both met
            _tracker.InjectClicksForTesting(200);
            _tracker.ForceCheckForTesting();
            Assert.AreEqual(1, _completedLog.Count);
        }

        [Test]
        public void Or_Condition_CompletesWhenAnyChildMet()
        {
            var goldCond = new MilestoneConditionNode
            {
                Type      = MilestoneConditionType.Threshold,
                Metric    = MilestoneMetric.CurrentGold,
                Threshold = 999_999
            };
            var clickCond = new MilestoneConditionNode
            {
                Type      = MilestoneConditionType.Threshold,
                Metric    = MilestoneMetric.TotalClicks,
                Threshold = 1
            };

            var m = ScriptableObject.CreateInstance<MilestoneConfigSO>();
            m.MilestoneId = "or_test";
            m.Condition   = new MilestoneConditionNode
            {
                Type     = MilestoneConditionType.Or,
                Children = new List<MilestoneConditionNode> { goldCond, clickCond }
            };

            var db = BuildDatabase(m);
            _tracker.Initialize(db, _economy);
            _tracker.SubscribeForTesting();

            // Gold unmet, clicks met
            _economy.InjectStateForTesting(0.0, 1_000_000.0, 0.0);
            _tracker.InjectClicksForTesting(5);
            _tracker.ForceCheckForTesting();
            Assert.AreEqual(1, _completedLog.Count, "OR should complete when any child condition is met.");
        }

        // ── Save / Load prefix isolation (regression for v1.6 bug) ───────────────

        [Test]
        public void OnBeforeSave_PreservesQuestPrefixedEntries()
        {
            var db = BuildDatabase();
            _tracker.Initialize(db, _economy);
            _tracker.SubscribeForTesting();

            var save = new SaveData();
            save.EnsureDefaults();
            save.CompletedMilestones.Add("quest:daily_1");
            save.CompletedMilestones.Add("tutorial:step_0");
            save.CompletedMilestones.Add("trait:speedster");
            save.CompletedMilestones.Add("unlock:realm_2");
            save.CompletedMilestones.Add("bare_milestone");

            _tracker.OnBeforeSave(save);

            Assert.IsTrue(save.CompletedMilestones.Contains("quest:daily_1"),   "quest: prefix must survive OnBeforeSave.");
            Assert.IsTrue(save.CompletedMilestones.Contains("tutorial:step_0"), "tutorial: prefix must survive OnBeforeSave.");
            Assert.IsTrue(save.CompletedMilestones.Contains("trait:speedster"),  "trait: prefix must survive OnBeforeSave.");
            Assert.IsTrue(save.CompletedMilestones.Contains("unlock:realm_2"),  "unlock: prefix must survive OnBeforeSave.");
        }

        [Test]
        public void OnBeforeSave_RemovesOldBareMilestones_BeforeRewriting()
        {
            var m  = MakeGoldMilestone("gold_10", 10);
            var db = BuildDatabase(m);
            _tracker.Initialize(db, _economy);
            _tracker.SubscribeForTesting();
            _tracker.InjectCompletedForTesting("gold_10");

            var save = new SaveData();
            save.EnsureDefaults();
            // Stale bare entry from previous session
            save.CompletedMilestones.Add("old_stale_milestone");

            _tracker.OnBeforeSave(save);

            Assert.IsFalse(save.CompletedMilestones.Contains("old_stale_milestone"), "Stale bare milestones must be removed.");
            Assert.IsTrue(save.CompletedMilestones.Contains("gold_10"), "Current completed milestone must be written.");
        }

        [Test]
        public void OnAfterLoad_OnlyLoadsBareIds_IgnoresPrefixedEntries()
        {
            var db = BuildDatabase();
            _tracker.Initialize(db, _economy);

            var save = new SaveData();
            save.EnsureDefaults();
            save.CompletedMilestones.Add("bare_milestone");
            save.CompletedMilestones.Add("quest:daily_1");
            save.CompletedMilestones.Add("tutorial:step_0");

            _tracker.OnAfterLoad(save);

            Assert.IsTrue(_tracker.IsCompleted("bare_milestone"),   "Bare milestone must be loaded.");
            Assert.IsFalse(_tracker.IsCompleted("quest:daily_1"),   "quest: prefixed entry must NOT be loaded as milestone.");
            Assert.IsFalse(_tracker.IsCompleted("tutorial:step_0"), "tutorial: prefixed entry must NOT be loaded as milestone.");
        }

        // ── Click threshold (every-100 optimization) ──────────────────────────────

        [Test]
        public void NotifyClick_ChecksEvery100Clicks()
        {
            var m  = MakeClickMilestone("clicks_100", 100);
            var db = BuildDatabase(m);
            _tracker.Initialize(db, _economy);
            _tracker.SubscribeForTesting();

            for (int i = 0; i < 99; i++)
                _tracker.NotifyClick();

            Assert.AreEqual(0, _completedLog.Count, "Milestone must not fire before 100 clicks.");

            _tracker.NotifyClick(); // 100th click triggers CheckAll

            Assert.AreEqual(1, _completedLog.Count, "Milestone must fire at exactly 100 clicks.");
        }

        // ── Wave number ───────────────────────────────────────────────────────────

        [Test]
        public void NotifyWaveChanged_TriggersWaveThresholdCheck()
        {
            var m = ScriptableObject.CreateInstance<MilestoneConfigSO>();
            m.MilestoneId = "wave_10";
            m.Condition   = new MilestoneConditionNode
            {
                Type      = MilestoneConditionType.Threshold,
                Metric    = MilestoneMetric.WaveNumber,
                Threshold = 10
            };
            var db = BuildDatabase(m);
            _tracker.Initialize(db, _economy);
            _tracker.SubscribeForTesting();

            _tracker.NotifyWaveChanged(10);

            Assert.AreEqual(1, _completedLog.Count);
        }

        // ── Prestige resets ───────────────────────────────────────────────────────

        [Test]
        public void PrestigeStarted_RemovesMilestones_MarkedResetsOnPrestige()
        {
            var m = ScriptableObject.CreateInstance<MilestoneConfigSO>();
            m.MilestoneId   = "wave_100";
            m.ResetsOnPrestige = true;
            m.Condition     = new MilestoneConditionNode
            {
                Type      = MilestoneConditionType.Threshold,
                Metric    = MilestoneMetric.WaveNumber,
                Threshold = 100
            };
            var db = BuildDatabase(m);
            _tracker.Initialize(db, _economy);
            _tracker.SubscribeForTesting();
            _tracker.InjectCompletedForTesting("wave_100");

            PrestigeStateManager.FirePrestigeStartedForTesting();

            Assert.IsFalse(_tracker.IsCompleted("wave_100"), "Milestone with ResetsOnPrestige must be removed on prestige.");
        }
    }
}

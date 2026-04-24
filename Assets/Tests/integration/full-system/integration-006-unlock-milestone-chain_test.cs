// Integration Tests — Sprint 22 — S22-04
// Test chain: MilestoneTracker condition met → MilestoneCompleted fired,
// UnlockLogService.Unlock triggered by subscriber → IsUnlocked returns true,
// save/load round-trip preserves both completed milestone and unlock state.
//
// To run: Unity Test Runner → EditMode → EndlessEngine.Tests.Integration.FullSystem

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using EndlessEngine.Config;
using EndlessEngine.Economy;
using EndlessEngine.Milestone;
using EndlessEngine.SaveAndLoad;
using EndlessEngine.UnlockLog;

namespace EndlessEngine.Tests.Integration.FullSystem
{
    [TestFixture]
    public class UnlockMilestoneChainTests
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD

        private MilestoneTracker    _milestoneTracker;
        private UnlockLogService    _unlockLog;
        private EconomyService      _economy;
        private SaveService         _saveService;
        private MilestoneDatabaseSO _database;
        private MilestoneConfigSO   _goldMilestone;
        private UnlockEntryConfigSO _unlockEntry;

        private EndlessEngine.Config.EconomyConfigSO _econConfig;

        [SetUp]
        public void SetUp()
        {
            _econConfig = ScriptableObject.CreateInstance<EndlessEngine.Config.EconomyConfigSO>();
            _econConfig.ResourceHardCap = 1_000_000L;
            _econConfig.StartingGold    = 0L;
            EndlessEngine.Config.ConfigRegistry.InjectForTesting(economy: _econConfig);

            // Economy + Save
            var ecoGo    = new GameObject("Economy");
            _economy     = ecoGo.AddComponent<EconomyService>();
            var savGo    = new GameObject("Save");
            _saveService = savGo.AddComponent<SaveService>();
            _economy.Initialize(null, _saveService);

            // Unlock entry that gets unlocked when the milestone completes
            _unlockEntry = ScriptableObject.CreateInstance<UnlockEntryConfigSO>();
            _unlockEntry.EntryId              = "milestone_gold_100";
            _unlockEntry.DisplayName          = "First 100 Gold";
            _unlockEntry.Category             = UnlockCategory.Milestone;
            _unlockEntry.IsHiddenUntilUnlocked = true;

            // UnlockLogService
            var ulGo    = new GameObject("UnlockLog");
            _unlockLog  = ulGo.AddComponent<UnlockLogService>();
            _unlockLog.Initialize(new[] { _unlockEntry });

            // Milestone: TotalGoldEarned >= 100
            _goldMilestone = ScriptableObject.CreateInstance<MilestoneConfigSO>();
            _goldMilestone.MilestoneId  = "gold_100";
            _goldMilestone.DisplayName  = "Earn 100 Gold";
            _goldMilestone.GoldReward   = 0;
            _goldMilestone.Condition    = new MilestoneConditionNode
            {
                Type      = MilestoneConditionType.Threshold,
                Metric    = MilestoneMetric.TotalGoldEarned,
                Threshold = 100
            };

            _database = ScriptableObject.CreateInstance<MilestoneDatabaseSO>();
            _database.Milestones = new[] { _goldMilestone };

            // MilestoneTracker
            var mtGo          = new GameObject("MilestoneTracker");
            _milestoneTracker = mtGo.AddComponent<MilestoneTracker>();
            _milestoneTracker.Initialize(_database, _economy);

            // Wire: when milestone completes, unlock the corresponding log entry
            MilestoneTracker.OnMilestoneCompleted += m => _unlockLog.Unlock("milestone_" + m.MilestoneId);

            // Load empty save
            var sd = new SaveData();
            sd.EnsureDefaults();
            _economy.OnAfterLoad(sd);
            _unlockLog.OnAfterLoad(sd);
            _milestoneTracker.OnAfterLoad(sd);
        }

        [TearDown]
        public void TearDown()
        {
            if (_milestoneTracker != null) Object.DestroyImmediate(_milestoneTracker.gameObject);
            if (_unlockLog        != null) Object.DestroyImmediate(_unlockLog.gameObject);
            if (_economy          != null) Object.DestroyImmediate(_economy.gameObject);
            if (_saveService      != null) Object.DestroyImmediate(_saveService.gameObject);
            if (_database         != null) Object.DestroyImmediate(_database);
            if (_goldMilestone    != null) Object.DestroyImmediate(_goldMilestone);
            if (_unlockEntry      != null) Object.DestroyImmediate(_unlockEntry);

            MilestoneTracker.ClearSubscribersForTesting();
            UnlockLogService.ClearSubscribersForTesting();
            if (_econConfig != null) Object.DestroyImmediate(_econConfig);
            EndlessEngine.Config.ConfigRegistry.ClearForTesting();
        }

        [Test]
        public void ConditionMet_MilestoneCompletes_AndEntryUnlocked()
        {
            Assert.IsFalse(_milestoneTracker.IsCompleted("gold_100"),
                "Milestone should not be complete before threshold is met");
            Assert.IsFalse(_unlockLog.IsUnlocked("milestone_gold_100"),
                "Unlock entry should not be unlocked before milestone");

            // Inject earned gold so the tracker sees >= 100
            _milestoneTracker.InjectGoldEarnedForTesting(100);
            _milestoneTracker.ForceCheckForTesting();

            Assert.IsTrue(_milestoneTracker.IsCompleted("gold_100"),
                "Milestone should complete when threshold is met");
            Assert.IsTrue(_unlockLog.IsUnlocked("milestone_gold_100"),
                "Unlock entry should be unlocked after milestone completes");
        }

        [Test]
        public void ConditionNotMet_MilestoneNotCompleted()
        {
            _milestoneTracker.InjectGoldEarnedForTesting(50); // below threshold
            _milestoneTracker.ForceCheckForTesting();

            Assert.IsFalse(_milestoneTracker.IsCompleted("gold_100"),
                "Milestone should not complete when threshold is not reached");
            Assert.IsFalse(_unlockLog.IsUnlocked("milestone_gold_100"),
                "Unlock entry should remain locked");
        }

        [Test]
        public void SaveLoad_RoundTrip_PreservesCompletedMilestoneAndUnlock()
        {
            // Complete the milestone
            _milestoneTracker.InjectGoldEarnedForTesting(100);
            _milestoneTracker.ForceCheckForTesting();
            Assert.IsTrue(_milestoneTracker.IsCompleted("gold_100"));
            Assert.IsTrue(_unlockLog.IsUnlocked("milestone_gold_100"));

            // Save
            var sd = new SaveData();
            sd.EnsureDefaults();
            _milestoneTracker.OnBeforeSave(sd);
            _unlockLog.OnBeforeSave(sd);

            // Fresh instances
            var mt2Go = new GameObject("MilestoneTracker2");
            var mt2   = mt2Go.AddComponent<MilestoneTracker>();
            mt2.Initialize(_database, _economy);

            var ul2Go = new GameObject("UnlockLog2");
            var ul2   = ul2Go.AddComponent<UnlockLogService>();
            ul2.Initialize(new[] { _unlockEntry });

            mt2.OnAfterLoad(sd);
            ul2.OnAfterLoad(sd);

            Assert.IsTrue(mt2.IsCompleted("gold_100"),
                "Completed milestone persisted after save/load");
            Assert.IsTrue(ul2.IsUnlocked("milestone_gold_100"),
                "Unlocked entry persisted after save/load");

            Object.DestroyImmediate(mt2Go);
            Object.DestroyImmediate(ul2Go);
        }

#endif
    }
}

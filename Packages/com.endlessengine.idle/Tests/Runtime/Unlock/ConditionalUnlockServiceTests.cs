using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using EndlessEngine.SaveAndLoad;
using EndlessEngine.Unlock;

namespace EndlessEngine.Tests.Unlock
{
    [TestFixture]
    public class ConditionalUnlockServiceTests
    {
        private ConditionalUnlockService _service;
        private List<string> _unlockedLog;

        [SetUp]
        public void SetUp()
        {
            ConditionalUnlockService.ClearSubscribersForTesting();

            var go = new GameObject("ConditionalUnlockService");
            _service = go.AddComponent<ConditionalUnlockService>();

            _unlockedLog = new List<string>();
            ConditionalUnlockService.OnEntryUnlocked += id => _unlockedLog.Add(id);
        }

        [TearDown]
        public void TearDown()
        {
            ConditionalUnlockService.ClearSubscribersForTesting();
            Object.DestroyImmediate(_service.gameObject);
        }

        // ── Check / unlock ────────────────────────────────────────────────────────

        [Test]
        public void Check_WhenConditionMet_UnlocksEntry()
        {
            _service.Register(new AlwaysTrueCondition("realm_2"));

            _service.Check();

            Assert.IsTrue(_service.IsUnlocked("realm_2"));
            Assert.AreEqual(1, _unlockedLog.Count);
            Assert.AreEqual("realm_2", _unlockedLog[0]);
        }

        [Test]
        public void Check_WhenConditionNotMet_DoesNotUnlock()
        {
            _service.Register(new AlwaysFalseCondition("realm_3"));

            _service.Check();

            Assert.IsFalse(_service.IsUnlocked("realm_3"));
            Assert.AreEqual(0, _unlockedLog.Count);
        }

        [Test]
        public void Check_AlreadyUnlocked_DoesNotFireAgain()
        {
            _service.Register(new AlwaysTrueCondition("feature_x"));

            _service.Check();
            _service.Check();

            Assert.AreEqual(1, _unlockedLog.Count, "Already-unlocked entry must not fire OnEntryUnlocked again.");
        }

        [Test]
        public void Check_MultipleConditions_UnlocksAllMet()
        {
            _service.Register(new AlwaysTrueCondition("a"));
            _service.Register(new AlwaysTrueCondition("b"));
            _service.Register(new AlwaysFalseCondition("c"));

            _service.Check();

            Assert.IsTrue(_service.IsUnlocked("a"));
            Assert.IsTrue(_service.IsUnlocked("b"));
            Assert.IsFalse(_service.IsUnlocked("c"));
            Assert.AreEqual(2, _unlockedLog.Count);
        }

        // ── Register overwrite ────────────────────────────────────────────────────

        [Test]
        public void Register_OverwritesSameEntryId_UsesLatestCondition()
        {
            _service.Register(new AlwaysFalseCondition("feature_x"));
            _service.Register(new AlwaysTrueCondition("feature_x")); // overwrite

            _service.Check();

            Assert.IsTrue(_service.IsUnlocked("feature_x"), "Latest registration should be used.");
        }

        [Test]
        public void Register_NullCondition_IsIgnored()
        {
            Assert.DoesNotThrow(() => _service.Register(null),
                "Null condition must be silently ignored.");
        }

        [Test]
        public void Register_EmptyEntryId_IsIgnored()
        {
            var cond = new AlwaysTrueCondition("");
            Assert.DoesNotThrow(() => _service.Register(cond));
            _service.Check();
            Assert.AreEqual(0, _unlockedLog.Count);
        }

        // ── Manual condition toggling ─────────────────────────────────────────────

        [Test]
        public void Check_ManualCondition_UnlocksOnlyWhenMet()
        {
            var cond = new ManualCondition("wave_gate");
            _service.Register(cond);

            cond.IsMet = false;
            _service.Check();
            Assert.IsFalse(_service.IsUnlocked("wave_gate"));

            cond.IsMet = true;
            _service.Check();
            Assert.IsTrue(_service.IsUnlocked("wave_gate"));
        }

        // ── Save / Load ───────────────────────────────────────────────────────────

        [Test]
        public void OnBeforeSave_WritesUnlockedEntriesWithPrefix()
        {
            _service.ForceUnlockForTesting("realm_2");
            _service.ForceUnlockForTesting("shop_slot_2");

            var save = new SaveData();
            save.EnsureDefaults();
            _service.OnBeforeSave(save);

            Assert.IsTrue(save.CompletedMilestones.Contains("unlock:realm_2"),   "realm_2 must be persisted with unlock: prefix.");
            Assert.IsTrue(save.CompletedMilestones.Contains("unlock:shop_slot_2"), "shop_slot_2 must be persisted with unlock: prefix.");
        }

        [Test]
        public void OnAfterLoad_RestoresUnlockedEntries()
        {
            var save = new SaveData();
            save.EnsureDefaults();
            save.CompletedMilestones.Add("unlock:realm_2");
            save.CompletedMilestones.Add("unlock:prestige_shop");

            _service.OnAfterLoad(save);

            Assert.IsTrue(_service.IsUnlocked("realm_2"),      "realm_2 must be restored from save.");
            Assert.IsTrue(_service.IsUnlocked("prestige_shop"), "prestige_shop must be restored from save.");
        }

        [Test]
        public void OnAfterLoad_IgnoresNonUnlockEntries()
        {
            var save = new SaveData();
            save.EnsureDefaults();
            save.CompletedMilestones.Add("quest:daily_1");
            save.CompletedMilestones.Add("bare_milestone");

            _service.OnAfterLoad(save);

            Assert.IsFalse(_service.IsUnlocked("daily_1"),      "Non-unlock entries must be ignored.");
            Assert.IsFalse(_service.IsUnlocked("bare_milestone"), "Bare milestones must not be loaded as unlocks.");
        }

        [Test]
        public void OnAfterLoad_ClearsPreviousState()
        {
            _service.ForceUnlockForTesting("old_entry");

            var save = new SaveData();
            save.EnsureDefaults();
            save.CompletedMilestones.Add("unlock:new_entry");

            _service.OnAfterLoad(save);

            Assert.IsFalse(_service.IsUnlocked("old_entry"), "Old unlocked state must be cleared on load.");
            Assert.IsTrue(_service.IsUnlocked("new_entry"),  "New entry from save must be loaded.");
        }

        [Test]
        public void SaveLoadRoundtrip_PreservesUnlocks()
        {
            _service.ForceUnlockForTesting("a");
            _service.ForceUnlockForTesting("b");

            var save = new SaveData();
            save.EnsureDefaults();
            _service.OnBeforeSave(save);

            var go2      = new GameObject("Service2");
            var service2 = go2.AddComponent<ConditionalUnlockService>();
            service2.OnAfterLoad(save);

            Assert.IsTrue(service2.IsUnlocked("a"), "Entry 'a' must survive roundtrip.");
            Assert.IsTrue(service2.IsUnlocked("b"), "Entry 'b' must survive roundtrip.");

            Object.DestroyImmediate(go2);
        }

        // ── Check_WhenNoConditionsRegistered ─────────────────────────────────────

        [Test]
        public void Check_NoConditions_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _service.Check(),
                "Check with no conditions registered must not throw.");
        }

        // ── Stub conditions ───────────────────────────────────────────────────────

        private class AlwaysTrueCondition : IUnlockCondition
        {
            public string EntryId { get; }
            public bool   IsMet   => true;
            public AlwaysTrueCondition(string id) => EntryId = id;
        }

        private class AlwaysFalseCondition : IUnlockCondition
        {
            public string EntryId { get; }
            public bool   IsMet   => false;
            public AlwaysFalseCondition(string id) => EntryId = id;
        }

        private class ManualCondition : IUnlockCondition
        {
            public string EntryId { get; }
            public bool   IsMet   { get; set; }
            public ManualCondition(string id) => EntryId = id;
        }
    }
}

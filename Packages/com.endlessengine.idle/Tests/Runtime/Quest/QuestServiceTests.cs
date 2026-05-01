using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using EndlessEngine.Quest;
using EndlessEngine.SaveAndLoad;

namespace EndlessEngine.Tests.Quest
{
    [TestFixture]
    public class QuestServiceTests
    {
        private QuestService _service;
        private List<QuestConfigSO> _completedLog;

        [SetUp]
        public void SetUp()
        {
            QuestService.ClearSubscribersForTesting();

            var go = new GameObject("QuestService");
            _service = go.AddComponent<QuestService>();

            _completedLog = new List<QuestConfigSO>();
            QuestService.OnQuestCompleted += q => _completedLog.Add(q);
        }

        [TearDown]
        public void TearDown()
        {
            QuestService.ClearSubscribersForTesting();
            UnityEngine.Object.DestroyImmediate(_service.gameObject);
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static QuestConfigSO MakeQuest(string id, params string[] conditionIds)
        {
            var so = ScriptableObject.CreateInstance<QuestConfigSO>();
            so.QuestId      = id;
            so.DisplayName  = id;
            so.ConditionIds = new List<string>(conditionIds);
            so.Repeatable   = false;
            return so;
        }

        private static QuestConfigSO MakeRepeatableQuest(string id, float cooldown, params string[] conditionIds)
        {
            var so = MakeQuest(id, conditionIds);
            so.Repeatable             = true;
            so.RepeatCooldownSeconds  = cooldown;
            return so;
        }

        private class AlwaysTrueCondition : IQuestCondition
        {
            public string ConditionId { get; }
            public bool   IsMet       => true;
            public float  Progress    => 1f;
            public AlwaysTrueCondition(string id) => ConditionId = id;
        }

        private class AlwaysFalseCondition : IQuestCondition
        {
            public string ConditionId { get; }
            public bool   IsMet       => false;
            public float  Progress    => 0f;
            public AlwaysFalseCondition(string id) => ConditionId = id;
        }

        private class ManualCondition : IQuestCondition
        {
            public string ConditionId { get; }
            public bool   IsMet       { get; set; }
            public float  Progress    => IsMet ? 1f : 0f;
            public ManualCondition(string id) => ConditionId = id;
        }

        // ── Basic completion ──────────────────────────────────────────────────────

        [Test]
        public void Check_CompletesQuest_WhenAllConditionsMet()
        {
            var quest = MakeQuest("q1", "cond_a");
            _service.Initialize(new[] { quest }, null);
            _service.RegisterCondition(new AlwaysTrueCondition("cond_a"));

            _service.Check();

            Assert.AreEqual(1, _completedLog.Count);
            Assert.IsTrue(_service.IsCompleted("q1"));
        }

        [Test]
        public void Check_DoesNotComplete_WhenConditionNotMet()
        {
            var quest = MakeQuest("q1", "cond_a");
            _service.Initialize(new[] { quest }, null);
            _service.RegisterCondition(new AlwaysFalseCondition("cond_a"));

            _service.Check();

            Assert.AreEqual(0, _completedLog.Count);
            Assert.IsFalse(_service.IsCompleted("q1"));
        }

        [Test]
        public void Check_RequiresAllConditions_WhenMultiple()
        {
            var quest = MakeQuest("q1", "cond_a", "cond_b");
            _service.Initialize(new[] { quest }, null);
            _service.RegisterCondition(new AlwaysTrueCondition("cond_a"));
            _service.RegisterCondition(new AlwaysFalseCondition("cond_b"));

            _service.Check();

            Assert.AreEqual(0, _completedLog.Count, "Both conditions must be met.");
        }

        [Test]
        public void Check_NoConditionRegistered_DoesNotComplete()
        {
            var quest = MakeQuest("q1", "cond_a");
            _service.Initialize(new[] { quest }, null);
            // no RegisterCondition call

            _service.Check();

            Assert.AreEqual(0, _completedLog.Count);
        }

        [Test]
        public void Check_QuestWithNoConditionIds_DoesNotComplete()
        {
            var quest = MakeQuest("q1"); // empty conditionIds
            _service.Initialize(new[] { quest }, null);

            _service.Check();

            Assert.AreEqual(0, _completedLog.Count);
        }

        // ── One-time completion guard ─────────────────────────────────────────────

        [Test]
        public void Check_NonRepeatable_DoesNotFireTwice()
        {
            var quest = MakeQuest("q1", "cond_a");
            _service.Initialize(new[] { quest }, null);
            _service.RegisterCondition(new AlwaysTrueCondition("cond_a"));

            _service.Check();
            _service.Check();

            Assert.AreEqual(1, _completedLog.Count, "Non-repeatable must only complete once.");
        }

        // ── Repeatable quests ─────────────────────────────────────────────────────

        [Test]
        public void Check_Repeatable_NoCooldown_CanFireMultipleTimes()
        {
            var quest = MakeRepeatableQuest("q1", 0f, "cond_a");
            _service.Initialize(new[] { quest }, null);
            _service.RegisterCondition(new AlwaysTrueCondition("cond_a"));

            _service.Check();
            _service.Check();

            Assert.AreEqual(2, _completedLog.Count);
        }

        // ── Progress ──────────────────────────────────────────────────────────────

        [Test]
        public void GetProgress_ReturnsZero_WhenNothingMet()
        {
            var quest = MakeQuest("q1", "cond_a", "cond_b");
            _service.Initialize(new[] { quest }, null);
            _service.RegisterCondition(new AlwaysFalseCondition("cond_a"));
            _service.RegisterCondition(new AlwaysFalseCondition("cond_b"));

            Assert.AreEqual(0f, _service.GetProgressForTesting("q1"), 0.01f);
        }

        [Test]
        public void GetProgress_ReturnsHalf_WhenOneOfTwoMet()
        {
            var quest = MakeQuest("q1", "cond_a", "cond_b");
            _service.Initialize(new[] { quest }, null);
            _service.RegisterCondition(new AlwaysTrueCondition("cond_a"));
            _service.RegisterCondition(new AlwaysFalseCondition("cond_b"));

            Assert.AreEqual(0.5f, _service.GetProgressForTesting("q1"), 0.01f);
        }

        [Test]
        public void GetProgress_ReturnsOne_WhenCompleted()
        {
            var quest = MakeQuest("q1", "cond_a");
            _service.Initialize(new[] { quest }, null);
            _service.RegisterCondition(new AlwaysTrueCondition("cond_a"));
            _service.Check();

            Assert.AreEqual(1f, _service.GetProgressForTesting("q1"), 0.01f);
        }

        // ── Save / Load ───────────────────────────────────────────────────────────

        [Test]
        public void OnBeforeSave_WritesCompletedQuestsWithPrefix()
        {
            var quest = MakeQuest("q1", "cond_a");
            _service.Initialize(new[] { quest }, null);
            _service.RegisterCondition(new AlwaysTrueCondition("cond_a"));
            _service.Check();

            var save = new SaveData();
            save.EnsureDefaults();
            _service.OnBeforeSave(save);

            Assert.IsTrue(save.CompletedMilestones.Contains("quest:q1"));
        }

        [Test]
        public void OnAfterLoad_RestoresCompletedQuests_FromSave()
        {
            var quest = MakeQuest("q1", "cond_a");
            _service.Initialize(new[] { quest }, null);

            var save = new SaveData();
            save.EnsureDefaults();
            save.CompletedMilestones.Add("quest:q1");
            _service.OnAfterLoad(save);

            Assert.IsTrue(_service.IsCompletedForTesting("q1"));
        }

        [Test]
        public void OnAfterLoad_DoesNotLoadTutorialOrMilestoneEntries()
        {
            _service.Initialize(System.Array.Empty<QuestConfigSO>(), null);

            var save = new SaveData();
            save.EnsureDefaults();
            save.CompletedMilestones.Add("tutorial:step_0");
            save.CompletedMilestones.Add("milestone_reached");
            _service.OnAfterLoad(save);

            Assert.IsFalse(_service.IsCompletedForTesting("step_0"));
            Assert.IsFalse(_service.IsCompletedForTesting("milestone_reached"));
        }

        // ── Condition registration overwrite ──────────────────────────────────────

        [Test]
        public void RegisterCondition_Overwrite_UsesLatestRegistration()
        {
            var quest = MakeQuest("q1", "cond_a");
            _service.Initialize(new[] { quest }, null);
            _service.RegisterCondition(new AlwaysFalseCondition("cond_a"));
            _service.RegisterCondition(new AlwaysTrueCondition("cond_a")); // overwrite

            _service.Check();

            Assert.AreEqual(1, _completedLog.Count, "Latest registration should be used.");
        }

        // ── Daily quest UTC window ─────────────────────────────────────────────────

        [Test]
        public void DailyQuest_AvailableAtStart_WhenNeverCompleted()
        {
            var quest = MakeScheduledQuest("daily_q", QuestScheduleType.Daily, "cond_a");
            _service.Initialize(new[] { quest }, null);
            _service.RegisterCondition(new AlwaysTrueCondition("cond_a"));

            _service.Check();

            Assert.AreEqual(1, _completedLog.Count, "Daily quest must be completable when never completed before.");
        }

        [Test]
        public void DailyQuest_NotAvailable_WhenCompletedThisUTCDay()
        {
            var quest = MakeScheduledQuest("daily_q", QuestScheduleType.Daily, "cond_a");
            _service.Initialize(new[] { quest }, null);
            _service.RegisterCondition(new AlwaysTrueCondition("cond_a"));

            // Inject a last-completion time within today's UTC day window
            DateTime today = DateTime.UtcNow.Date;
            _service.InjectLastCompletedForTesting("daily_q", today.AddHours(1));

            _service.Check();

            Assert.AreEqual(0, _completedLog.Count, "Daily quest must not be available again within the same UTC day.");
        }

        [Test]
        public void DailyQuest_Available_WhenCompletedYesterday()
        {
            var quest = MakeScheduledQuest("daily_q", QuestScheduleType.Daily, "cond_a");
            _service.Initialize(new[] { quest }, null);
            _service.RegisterCondition(new AlwaysTrueCondition("cond_a"));

            // Inject last-completion as yesterday (before today's window start)
            DateTime yesterday = DateTime.UtcNow.Date.AddDays(-1);
            _service.InjectLastCompletedForTesting("daily_q", yesterday);

            _service.Check();

            Assert.AreEqual(1, _completedLog.Count, "Daily quest must be available again if completed before today's UTC midnight.");
        }

        // ── Weekly quest UTC window ────────────────────────────────────────────────

        [Test]
        public void WeeklyQuest_AvailableAtStart_WhenNeverCompleted()
        {
            var quest = MakeScheduledQuest("weekly_q", QuestScheduleType.Weekly, "cond_a");
            _service.Initialize(new[] { quest }, null);
            _service.RegisterCondition(new AlwaysTrueCondition("cond_a"));

            _service.Check();

            Assert.AreEqual(1, _completedLog.Count, "Weekly quest must be completable when never completed before.");
        }

        [Test]
        public void WeeklyQuest_NotAvailable_WhenCompletedThisWeek()
        {
            var quest = MakeScheduledQuest("weekly_q", QuestScheduleType.Weekly, "cond_a");
            _service.Initialize(new[] { quest }, null);
            _service.RegisterCondition(new AlwaysTrueCondition("cond_a"));

            // This Monday's 00:00 UTC
            DateTime now             = DateTime.UtcNow;
            int daysSinceMonday      = ((int)now.DayOfWeek + 6) % 7;
            DateTime thisMonday      = now.Date.AddDays(-daysSinceMonday);
            _service.InjectLastCompletedForTesting("weekly_q", thisMonday.AddHours(3));

            _service.Check();

            Assert.AreEqual(0, _completedLog.Count, "Weekly quest must not be available again within the same week.");
        }

        [Test]
        public void WeeklyQuest_Available_WhenCompletedLastWeek()
        {
            var quest = MakeScheduledQuest("weekly_q", QuestScheduleType.Weekly, "cond_a");
            _service.Initialize(new[] { quest }, null);
            _service.RegisterCondition(new AlwaysTrueCondition("cond_a"));

            // Last week: 8 days ago
            DateTime lastWeek = DateTime.UtcNow.AddDays(-8);
            _service.InjectLastCompletedForTesting("weekly_q", lastWeek);

            _service.Check();

            Assert.AreEqual(1, _completedLog.Count, "Weekly quest must be available again if completed before this week's Monday.");
        }

        // ── Scheduled quest save / load ───────────────────────────────────────────

        [Test]
        public void DailyQuest_LastCompletionTime_PersistedInSave()
        {
            var quest = MakeScheduledQuest("daily_q", QuestScheduleType.Daily, "cond_a");
            _service.Initialize(new[] { quest }, null);
            _service.RegisterCondition(new AlwaysTrueCondition("cond_a"));

            _service.Check();

            var save = new SaveData();
            save.EnsureDefaults();
            _service.OnBeforeSave(save);

            bool found = false;
            foreach (var entry in save.CompletedMilestones)
                if (entry.StartsWith("quest_ts:daily_q=")) { found = true; break; }

            Assert.IsTrue(found, "Scheduled quest last-completion time must be persisted with 'quest_ts:' prefix.");
        }

        [Test]
        public void DailyQuest_LastCompletionTime_RestoredOnLoad()
        {
            var quest = MakeScheduledQuest("daily_q", QuestScheduleType.Daily, "cond_a");
            _service.Initialize(new[] { quest }, null);

            DateTime completedAt = DateTime.UtcNow.AddMinutes(-30);
            var save = new SaveData();
            save.EnsureDefaults();
            save.CompletedMilestones.Add($"quest_ts:daily_q={completedAt.Ticks}");

            _service.OnAfterLoad(save);

            Assert.IsTrue(_service.TryGetLastCompletedForTesting("daily_q", out var loaded));
            Assert.AreEqual(completedAt.Ticks, loaded.Ticks, "Last-completion ticks must survive save/load roundtrip.");
        }

        // ── Helper for scheduled quests ───────────────────────────────────────────

        private static QuestConfigSO MakeScheduledQuest(string id, QuestScheduleType schedule, params string[] conditionIds)
        {
            var so = ScriptableObject.CreateInstance<QuestConfigSO>();
            so.QuestId      = id;
            so.DisplayName  = id;
            so.ConditionIds = new System.Collections.Generic.List<string>(conditionIds);
            so.Repeatable   = true;
            so.ScheduleType = schedule;
            return so;
        }
    }
}

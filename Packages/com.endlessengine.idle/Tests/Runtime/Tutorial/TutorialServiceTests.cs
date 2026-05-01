using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using EndlessEngine.Tutorial;

namespace EndlessEngine.Tests.Tutorial
{
    [TestFixture]
    public class TutorialServiceTests
    {
        private TutorialService           _service;
        private List<TutorialStepConfigSO> _stepStartedLog;
        private List<TutorialStepConfigSO> _stepCompletedLog;
        private int                        _finishedCount;

        [SetUp]
        public void SetUp()
        {
            TutorialService.ClearSubscribersForTesting();

            var go = new GameObject("TutorialService");
            _service = go.AddComponent<TutorialService>();

            _stepStartedLog   = new List<TutorialStepConfigSO>();
            _stepCompletedLog = new List<TutorialStepConfigSO>();
            _finishedCount    = 0;

            TutorialService.OnStepStarted    += s => _stepStartedLog.Add(s);
            TutorialService.OnStepCompleted  += s => _stepCompletedLog.Add(s);
            TutorialService.OnTutorialFinished += () => _finishedCount++;
        }

        [TearDown]
        public void TearDown()
        {
            TutorialService.ClearSubscribersForTesting();
            Object.DestroyImmediate(_service.gameObject);
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static TutorialStepConfigSO MakeStep(string id, int index,
            TutorialCompletionType type = TutorialCompletionType.Manual,
            string eventName = null)
        {
            var so = ScriptableObject.CreateInstance<TutorialStepConfigSO>();
            so.StepId          = id;
            so.SequenceIndex   = index;
            so.CompletionType  = type;
            so.CompletionEventName = eventName ?? string.Empty;
            return so;
        }

        private void InitWith(params TutorialStepConfigSO[] steps)
            => _service.Initialize(steps, null);

        // ── Begin / no steps ─────────────────────────────────────────────────────

        [Test]
        public void Begin_WithNoSteps_FiresFinishedImmediately()
        {
            InitWith();
            _service.Begin();
            Assert.AreEqual(1, _finishedCount);
            Assert.IsNull(_service.CurrentStep);
        }

        [Test]
        public void Begin_SetsCurrentStep_ToFirstIncomplete()
        {
            var s0 = MakeStep("step_0", 0);
            var s1 = MakeStep("step_1", 1);
            InitWith(s0, s1);
            _service.Begin();
            Assert.AreEqual(s0, _service.CurrentStep);
            Assert.AreEqual(1, _stepStartedLog.Count);
        }

        [Test]
        public void Begin_SkipsAlreadyCompletedSteps_OnLoad()
        {
            var s0 = MakeStep("step_0", 0);
            var s1 = MakeStep("step_1", 1);
            InitWith(s0, s1);

            // Simulate s0 already completed via save load
            var save = new EndlessEngine.SaveAndLoad.SaveData();
            save.EnsureDefaults();
            save.CompletedMilestones.Add("tutorial:step_0");
            _service.OnAfterLoad(save);

            _service.Begin();

            Assert.AreEqual(s1, _service.CurrentStep, "Should skip completed step_0.");
        }

        // ── Manual completion ─────────────────────────────────────────────────────

        [Test]
        public void CompleteCurrentStep_Manual_AdvancesToNext()
        {
            var s0 = MakeStep("step_0", 0);
            var s1 = MakeStep("step_1", 1);
            InitWith(s0, s1);
            _service.Begin();

            _service.CompleteCurrentStep();

            Assert.AreEqual(1, _stepCompletedLog.Count);
            Assert.AreEqual(s0, _stepCompletedLog[0]);
            Assert.AreEqual(s1, _service.CurrentStep, "Should advance to step_1.");
        }

        [Test]
        public void CompleteAllSteps_FiresTutorialFinished()
        {
            var s0 = MakeStep("step_0", 0);
            var s1 = MakeStep("step_1", 1);
            InitWith(s0, s1);
            _service.Begin();

            _service.CompleteCurrentStep(); // s0
            _service.CompleteCurrentStep(); // s1

            Assert.AreEqual(1, _finishedCount);
            Assert.IsNull(_service.CurrentStep);
            Assert.IsTrue(_service.IsFinished);
        }

        // ── EventFired completion ─────────────────────────────────────────────────

        [Test]
        public void NotifyEvent_MatchingName_CompletesStep()
        {
            var s0 = MakeStep("step_0", 0, TutorialCompletionType.EventFired, "generator_purchased");
            InitWith(s0);
            _service.Begin();

            _service.NotifyEvent("generator_purchased");

            Assert.AreEqual(1, _stepCompletedLog.Count);
            Assert.IsTrue(_service.IsFinished);
        }

        [Test]
        public void NotifyEvent_NonMatchingName_DoesNotComplete()
        {
            var s0 = MakeStep("step_0", 0, TutorialCompletionType.EventFired, "generator_purchased");
            InitWith(s0);
            _service.Begin();

            _service.NotifyEvent("upgrade_purchased");

            Assert.AreEqual(0, _stepCompletedLog.Count);
            Assert.AreEqual(s0, _service.CurrentStep);
        }

        // ── TapAnywhere / TapHighlight ────────────────────────────────────────────

        [Test]
        public void NotifyTap_TapAnywhereStep_CompletesStep()
        {
            var s0 = MakeStep("step_0", 0, TutorialCompletionType.TapAnywhere);
            InitWith(s0);
            _service.Begin();

            _service.NotifyTap();

            Assert.AreEqual(1, _stepCompletedLog.Count);
        }

        [Test]
        public void NotifyHighlightTapped_TapHighlightStep_CompletesStep()
        {
            var s0 = MakeStep("step_0", 0, TutorialCompletionType.TapHighlight);
            InitWith(s0);
            _service.Begin();

            _service.NotifyHighlightTapped();

            Assert.AreEqual(1, _stepCompletedLog.Count);
        }

        // ── Skip ─────────────────────────────────────────────────────────────────

        [Test]
        public void Skip_FiresFinished_AndClearsCurrentStep()
        {
            var s0 = MakeStep("step_0", 0);
            InitWith(s0);
            _service.Begin();

            _service.Skip();

            Assert.AreEqual(1, _finishedCount);
            Assert.IsNull(_service.CurrentStep);
            Assert.IsFalse(_service.IsActive);
        }

        // ── Save / Load ───────────────────────────────────────────────────────────

        [Test]
        public void OnBeforeSave_WritesCompletedStepsWithPrefix()
        {
            var s0 = MakeStep("step_0", 0);
            InitWith(s0);
            _service.Begin();
            _service.CompleteCurrentStep();

            var save = new EndlessEngine.SaveAndLoad.SaveData();
            save.EnsureDefaults();
            _service.OnBeforeSave(save);

            Assert.IsTrue(save.CompletedMilestones.Contains("tutorial:step_0"));
        }

        [Test]
        public void OnAfterLoad_RestoresCompletedSteps_FromSave()
        {
            var s0 = MakeStep("step_0", 0);
            var s1 = MakeStep("step_1", 1);
            InitWith(s0, s1);

            var save = new EndlessEngine.SaveAndLoad.SaveData();
            save.EnsureDefaults();
            save.CompletedMilestones.Add("tutorial:step_0");
            _service.OnAfterLoad(save);

            Assert.IsTrue(_service.IsStepCompletedForTesting("step_0"));
            Assert.IsFalse(_service.IsStepCompletedForTesting("step_1"));
        }

        [Test]
        public void OnAfterLoad_DoesNotLoadQuestOrMilestoneEntries()
        {
            var s0 = MakeStep("step_0", 0);
            InitWith(s0);

            var save = new EndlessEngine.SaveAndLoad.SaveData();
            save.EnsureDefaults();
            save.CompletedMilestones.Add("quest:some_quest");
            save.CompletedMilestones.Add("milestone_reached");
            _service.OnAfterLoad(save);

            Assert.IsFalse(_service.IsStepCompletedForTesting("some_quest"));
            Assert.IsFalse(_service.IsStepCompletedForTesting("milestone_reached"));
        }

        // ── Sequence ordering ─────────────────────────────────────────────────────

        [Test]
        public void Steps_AreProcessedInSequenceIndexOrder_RegardlessOfInitOrder()
        {
            // Registered out of order
            var s1 = MakeStep("step_1", 1);
            var s0 = MakeStep("step_0", 0);
            InitWith(s1, s0);
            _service.Begin();

            Assert.AreEqual(s0, _service.CurrentStep, "Step with lower SequenceIndex must come first.");
        }
    }
}

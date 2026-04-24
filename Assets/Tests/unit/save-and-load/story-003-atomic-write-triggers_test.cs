// Tests for Story S1-06: Save & Load — Atomic Write and Auto-Save Triggers
// Type: Logic (Unit/EditMode)
// Story: production/epics/save-and-load/story-003-atomic-write-triggers.md
//
// These tests verify:
//   (1) AC-SAV-09: Auto-save timer fires OnSaveStarted on expiry
//   (2) AC-SAV-10: Debounce consolidates rapid upgrade purchase events into one save
//   (3) AC-SAV-12: Three consecutive failures raise OnPersistentWriteFailure exactly once
//   (4) Failure counter resets on a successful save
//
// Note: AC-SAV-03 (filesystem crash recovery) and AC-SAV-11 (quit-path synchronous save)
// require real I/O or process lifecycle hooks and are DEFERRED to integration/smoke testing.
//
// To run: Unity Test Runner → EditMode → EndlessEngine.Tests.Unit.SaveAndLoad

using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Text.RegularExpressions;
using EndlessEngine.Config;
using EndlessEngine.SaveAndLoad;

namespace EndlessEngine.Tests.Unit.SaveAndLoad
{
    /// <summary>
    /// Unit tests for atomic write and auto-save triggers (S1-06 / Story 003).
    /// Validates AC-SAV-09, AC-SAV-10, and AC-SAV-12.
    /// </summary>
    [TestFixture]
    public class AtomicWriteTriggersTests
    {
        private SaveService _saveService;

        // ── SetUp / TearDown ──────────────────────────────────────────────────────

        [SetUp]
        public void SetUp()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var schema   = ScriptableObject.CreateInstance<SchemaVersionSO>();
            var prestige = ScriptableObject.CreateInstance<PrestigeConfigSO>();
            var realm    = ScriptableObject.CreateInstance<RealmIdentityConfigSO>();
            schema.CurrentSchemaVersion = 0;
            prestige.BaseMultiplierPerPrestige = 1.5f;
            realm.RealmSlug = "base";
            ConfigRegistry.InjectForTesting(prestige: prestige, realm: realm, schema: schema);

            var go = new GameObject("SaveServiceTriggersTest");
            _saveService = go.AddComponent<SaveService>();

            // Prime SaveService into Ready state so triggers can fire
            _saveService.InjectForTesting(SaveDataFactory.CreateNewGame(), isNewGame: true);
#endif
        }

        [TearDown]
        public void TearDown()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _saveService.ResetForTesting();
            if (_saveService != null)
                UnityEngine.Object.DestroyImmediate(_saveService.gameObject);
            ConfigRegistry.ClearForTesting();
#endif
        }

        // ── AC-SAV-09: Auto-save timer ────────────────────────────────────────────

        [Test]
        [Description("AC-SAV-09: OnSaveStarted fires when auto-save timer expires.")]
        public void AutoSave_TimerExpired_OnSaveStartedFires()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            bool saveStarted = false;
            _saveService.OnSaveStarted += () => saveStarted = true;

            _saveService.ExpireAutoSaveTimerForTesting();

            // Simulate one Update frame via SendMessage (calls the private Update method)
            _saveService.TickUpdateForTesting();

            Assert.IsTrue(saveStarted, "OnSaveStarted must fire when auto-save timer expires");
#endif
        }

        [Test]
        [Description("AC-SAV-09: OnSaveCompleted fires after auto-save triggered by timer.")]
        public void AutoSave_TimerExpired_OnSaveCompletedFires()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            bool? completedResult = null;
            _saveService.OnSaveCompleted += result => completedResult = result;

            _saveService.ExpireAutoSaveTimerForTesting();
            _saveService.TickUpdateForTesting();

            // SaveAsync is async — wait for task completion using the synchronous path
            // (Unity test runner EditMode flushes Tasks in same frame via coroutine)
            // We verify completion fired; actual success depends on write-path availability.
            Assert.IsNotNull(completedResult,
                "OnSaveCompleted must fire after auto-save triggered by timer expiry");
#endif
        }

        // ── AC-SAV-10: Debounce ───────────────────────────────────────────────────

        [Test]
        [Description("AC-SAV-10: Multiple NotifyUpgradePurchased calls produce no immediate save.")]
        public void Debounce_MultipleUpgradePurchases_NoImmediateSave()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            int saveCount = 0;
            _saveService.OnSaveStarted += () => saveCount++;

            // 3 rapid purchases — debounce should hold them all
            _saveService.NotifyUpgradePurchased();
            _saveService.NotifyUpgradePurchased();
            _saveService.NotifyUpgradePurchased();

            // No Update tick — no time has passed
            Assert.AreEqual(0, saveCount,
                "No save must fire while still within the debounce window");
#endif
        }

        [Test]
        [Description("AC-SAV-10: Save fires exactly once after debounce window expires.")]
        public void Debounce_WindowExpires_ExactlyOneSaveFires()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            int saveCount = 0;
            _saveService.OnSaveStarted += () => saveCount++;

            // 3 rapid purchases
            _saveService.NotifyUpgradePurchased();
            _saveService.NotifyUpgradePurchased();
            _saveService.NotifyUpgradePurchased();

            // Expire debounce timer and simulate Update
            _saveService.ExpireDebounceTimerForTesting();
            _saveService.TickUpdateForTesting();

            Assert.AreEqual(1, saveCount,
                "Exactly one save must fire after the debounce window expires, regardless of purchase count");
#endif
        }

        [Test]
        [Description("AC-SAV-10: NotifyUpgradePurchased in non-Ready state is silently ignored.")]
        public void Debounce_NonReadyState_IsIgnored()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _saveService.ResetForTesting(); // puts service back in Uninitialized

            int saveCount = 0;
            _saveService.OnSaveStarted += () => saveCount++;

            _saveService.NotifyUpgradePurchased();
            _saveService.ExpireDebounceTimerForTesting();
            _saveService.TickUpdateForTesting();

            Assert.AreEqual(0, saveCount,
                "NotifyUpgradePurchased must be ignored when SaveService is not Ready");
#endif
        }

        // ── AC-SAV-12: Consecutive failure tracking ───────────────────────────────

        [Test]
        [Description("AC-SAV-12: OnPersistentWriteFailure fires after 3 consecutive failures.")]
        public void ConsecutiveFailures_ThreeFailures_RaisesEvent()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            int failureEventCount = 0;
            _saveService.OnPersistentWriteFailure += () => failureEventCount++;

            _saveService.SimulateSaveResultForTesting(success: false);
            Assert.AreEqual(0, failureEventCount, "Event must not fire after 1 failure");

            _saveService.SimulateSaveResultForTesting(success: false);
            Assert.AreEqual(0, failureEventCount, "Event must not fire after 2 failures");

            _saveService.SimulateSaveResultForTesting(success: false);
            Assert.AreEqual(1, failureEventCount, "Event must fire exactly once after 3 failures");
#endif
        }

        [Test]
        [Description("AC-SAV-12: OnPersistentWriteFailure fires at most once per session.")]
        public void ConsecutiveFailures_FourthAndFifthFailures_DoNotRaiseEventAgain()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            int failureEventCount = 0;
            _saveService.OnPersistentWriteFailure += () => failureEventCount++;

            // Reach threshold
            _saveService.SimulateSaveResultForTesting(success: false);
            _saveService.SimulateSaveResultForTesting(success: false);
            _saveService.SimulateSaveResultForTesting(success: false);
            Assert.AreEqual(1, failureEventCount);

            // Additional failures must not re-raise
            _saveService.SimulateSaveResultForTesting(success: false);
            _saveService.SimulateSaveResultForTesting(success: false);
            Assert.AreEqual(1, failureEventCount,
                "OnPersistentWriteFailure must not fire more than once per session");
#endif
        }

        [Test]
        [Description("AC-SAV-12: Failure counter resets after a successful save.")]
        public void ConsecutiveFailures_SuccessResetsCounter()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            int failureEventCount = 0;
            _saveService.OnPersistentWriteFailure += () => failureEventCount++;

            // 2 failures (below threshold)
            _saveService.SimulateSaveResultForTesting(success: false);
            _saveService.SimulateSaveResultForTesting(success: false);

            // Success resets the counter
            _saveService.SimulateSaveResultForTesting(success: true);

            // 2 more failures — should not fire (counter was reset)
            _saveService.SimulateSaveResultForTesting(success: false);
            _saveService.SimulateSaveResultForTesting(success: false);
            Assert.AreEqual(0, failureEventCount,
                "Event must not fire when failure count was reset mid-sequence");

            // Now reach 3 from fresh baseline — event fires
            _saveService.SimulateSaveResultForTesting(success: false);
            Assert.AreEqual(1, failureEventCount,
                "Event must fire after 3 new consecutive failures from reset baseline");
#endif
        }

        [Test]
        [Description("AC-SAV-12: OnSaveCompleted(false) fires on each write failure.")]
        public void ConsecutiveFailures_OnSaveCompleted_FiresWithFalseOnEachFailure()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var completedResults = new System.Collections.Generic.List<bool>();
            _saveService.OnSaveCompleted += result => completedResults.Add(result);

            _saveService.SimulateSaveResultForTesting(success: false);
            _saveService.SimulateSaveResultForTesting(success: false);

            Assert.AreEqual(2, completedResults.Count, "OnSaveCompleted must fire for each failure");
            Assert.IsFalse(completedResults[0], "First failure: OnSaveCompleted must fire with false");
            Assert.IsFalse(completedResults[1], "Second failure: OnSaveCompleted must fire with false");
#endif
        }
    }
}

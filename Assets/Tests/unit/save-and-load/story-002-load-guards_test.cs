// Tests for Story S1-05: Save & Load — Load Guards (Clock-Skew + Prestige Rollback)
// Type: Logic (Unit/EditMode)
// Story: production/epics/save-and-load/story-002-load-guards.md
//
// These tests verify that the two validation guards in SaveService.LoadAsync()
// run correctly:
//   (1) Clock-skew: future LastSessionTimestamp is clamped to UtcNow
//   (2) Prestige rollback: PrestigeInProgress=true restores pre-prestige snapshot
//
// Both guards are exercised via SaveService.InjectForTesting(), which applies
// the same guard logic as LoadAsync() without file I/O.
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
    /// Unit tests for load guards (S1-05 / Story 002).
    /// Validates AC-SAV-07 (clock-skew clamp) and AC-SAV-08 (prestige rollback).
    /// </summary>
    [TestFixture]
    public class LoadGuardsTests
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

            var go = new GameObject("SaveServiceGuardTest");
            _saveService = go.AddComponent<SaveService>();
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

        // ── AC-SAV-07: Clock-skew guard ───────────────────────────────────────────

        [Test]
        [Description("AC-SAV-07: LastSessionTimestamp 30 minutes in future is clamped to UtcNow.")]
        public void ClockSkew_TimestampFarInFuture_IsClamped()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Arrange: timestamp 30 minutes in the future
            var futureTime = DateTime.UtcNow.AddMinutes(30);
            var saveData   = new SaveData
            {
                LastSessionTimestamp = futureTime,
                CurrentResources = 100L,
            };

            SaveData received = null;
            _saveService.OnSaveLoaded += (data, _) => received = data;

            LogAssert.Expect(LogType.Warning, new Regex("Clock skew"));

            // Act
            _saveService.InjectForTesting(saveData, isNewGame: false);

            // Assert: timestamp was clamped (within 2 seconds of UtcNow)
            var delta = (DateTime.UtcNow - received.LastSessionTimestamp).TotalSeconds;
            Assert.LessOrEqual(Math.Abs(delta), 2.0,
                $"LastSessionTimestamp should be clamped to UtcNow but was {received.LastSessionTimestamp:O}");
            Assert.Less(received.LastSessionTimestamp, futureTime,
                "Clamped timestamp must be earlier than the original future timestamp");
#endif
        }

        [Test]
        [Description("AC-SAV-07: Timestamp exactly 5 minutes in future is NOT clamped (boundary).")]
        public void ClockSkew_TimestampExactlyFiveMinutes_NotClamped()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Exactly at boundary should not trigger clamp
            var borderTime = DateTime.UtcNow.AddMinutes(5).AddSeconds(-1); // just under threshold
            var saveData   = new SaveData { LastSessionTimestamp = borderTime };

            SaveData received = null;
            _saveService.OnSaveLoaded += (data, _) => received = data;

            // No warning expected
            _saveService.InjectForTesting(saveData, isNewGame: false);

            // Timestamp should remain near borderTime (not clamped)
            var delta = (borderTime - received.LastSessionTimestamp).TotalSeconds;
            Assert.LessOrEqual(Math.Abs(delta), 2.0,
                "Timestamp just under 5-minute threshold should not be clamped");
#endif
        }

        [Test]
        [Description("AC-SAV-07: Past timestamp (normal case) passes through unchanged.")]
        public void ClockSkew_PastTimestamp_PassesThrough()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var pastTime = DateTime.UtcNow.AddHours(-2);
            var saveData = new SaveData { LastSessionTimestamp = pastTime };

            SaveData received = null;
            _saveService.OnSaveLoaded += (data, _) => received = data;

            _saveService.InjectForTesting(saveData, isNewGame: false);

            // Past timestamp should not be modified (within floating-point noise)
            var delta = (pastTime - received.LastSessionTimestamp).TotalSeconds;
            Assert.LessOrEqual(Math.Abs(delta), 0.1,
                "Past timestamp should pass through without modification");
#endif
        }

        // ── AC-SAV-08: Prestige rollback guard ───────────────────────────────────

        [Test]
        [Description("AC-SAV-08: PrestigeInProgress=true restores pre-prestige resources and wave number.")]
        public void PrestigeRollback_InProgress_RestoresSnapshot()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var saveData = new SaveData
            {
                PrestigeInProgress              = true,
                PrestigeCount                   = 3,
                CurrentResources                = 0L,       // post-reset value (should be discarded)
                WaveNumber                      = 0,        // post-reset value (should be discarded)
                PrePrestigeResources            = 8000L,    // snapshot
                PrePrestigeWaveNumber           = 22,       // snapshot
                PrePrestigeUpgradeNodeStates    = new Dictionary<string, int> { ["node_damage_01"] = 2 },
            };

            SaveData received = null;
            _saveService.OnSaveLoaded += (data, _) => received = data;

            LogAssert.Expect(LogType.Warning, new Regex("rollback"));

            // Act
            _saveService.InjectForTesting(saveData, isNewGame: false);

            // Assert: snapshot values restored
            Assert.AreEqual(8000L, received.CurrentResources, "CurrentResources must be restored from snapshot");
            Assert.AreEqual(22, received.WaveNumber, "WaveNumber must be restored from snapshot");
            Assert.AreEqual(3, received.PrestigeCount, "PrestigeCount must NOT be incremented by rollback");
            Assert.IsFalse(received.PrestigeInProgress, "PrestigeInProgress must be cleared after rollback");

            // Snapshot fields must be cleared after rollback
            Assert.AreEqual(0L, received.PrePrestigeResources, "PrePrestigeResources must be cleared after rollback");
            Assert.AreEqual(0, received.PrePrestigeWaveNumber, "PrePrestigeWaveNumber must be cleared after rollback");
#endif
        }

        [Test]
        [Description("AC-SAV-08: PrestigeInProgress=false (normal case) — rollback guard skipped.")]
        public void PrestigeRollback_NotInProgress_NoRollback()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var saveData = new SaveData
            {
                PrestigeInProgress   = false,
                CurrentResources     = 5000L,
                WaveNumber           = 10,
                PrestigeCount        = 2,
            };

            SaveData received = null;
            _saveService.OnSaveLoaded += (data, _) => received = data;

            _saveService.InjectForTesting(saveData, isNewGame: false);

            // Values must be preserved (no rollback applied)
            Assert.AreEqual(5000L, received.CurrentResources, "CurrentResources must not change when not in prestige");
            Assert.AreEqual(10, received.WaveNumber, "WaveNumber must not change when not in prestige");
            Assert.AreEqual(2, received.PrestigeCount, "PrestigeCount must not change when not in prestige");
#endif
        }

        [Test]
        [Description("AC-SAV-08: Rollback with null PrePrestigeUpgradeNodeStates uses empty dictionary (no crash).")]
        public void PrestigeRollback_NullSnapshotUpgrades_UsesEmptyDictionary()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var saveData = new SaveData
            {
                PrestigeInProgress              = true,
                PrePrestigeResources            = 100L,
                PrePrestigeUpgradeNodeStates    = null, // null snapshot — should not crash
            };

            SaveData received = null;
            _saveService.OnSaveLoaded += (data, _) => received = data;

            LogAssert.Expect(LogType.Warning, new Regex("rollback"));

            Assert.DoesNotThrow(() => _saveService.InjectForTesting(saveData, isNewGame: false),
                "Rollback with null snapshot upgrades must not throw");

            Assert.IsNotNull(received.UpgradeNodeStates,
                "UpgradeNodeStates must be non-null after rollback with null snapshot");
            Assert.AreEqual(0, received.UpgradeNodeStates.Count,
                "UpgradeNodeStates must be empty when snapshot was null");
#endif
        }

        [Test]
        [Description("AC-SAV-08: OnSaveLoaded never fires with PrestigeInProgress=true after rollback.")]
        public void PrestigeRollback_OnSaveLoaded_AlwaysReceivesClearedFlag()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var saveData = new SaveData
            {
                PrestigeInProgress   = true,
                PrePrestigeResources = 500L,
            };

            bool prestigeInProgressOnLoad = true; // will be overwritten
            _saveService.OnSaveLoaded += (data, _) => prestigeInProgressOnLoad = data.PrestigeInProgress;

            LogAssert.Expect(LogType.Warning, new Regex("rollback"));
            _saveService.InjectForTesting(saveData, isNewGame: false);

            Assert.IsFalse(prestigeInProgressOnLoad,
                "OnSaveLoaded must never fire with PrestigeInProgress=true");
#endif
        }
    }
}

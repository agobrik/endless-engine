// Tests for Story S1-04: Save & Load — Load Sequence
// Type: Logic (Unit/EditMode)
// Story: production/epics/save-and-load/story-001-load-sequence.md
//
// Tests use SaveService.InjectForTesting() to bypass file I/O.
// ConfigRegistry.InjectForTesting() provides required SO mocks.
//
// To run: Unity Test Runner → EditMode → EndlessEngine.Tests.Unit.SaveAndLoad

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using EndlessEngine.Config;
using EndlessEngine.SaveAndLoad;

namespace EndlessEngine.Tests.Unit.SaveAndLoad
{
    /// <summary>
    /// Unit tests for the Save & Load load sequence (S1-04 / Story 001).
    /// Validates AC-SAV-01 (new game defaults) and AC-SAV-02 (existing save deserialization).
    /// </summary>
    [TestFixture]
    public class LoadSequenceTests
    {
        private SaveService _saveService;

        // ── SetUp / TearDown ──────────────────────────────────────────────────────

        [SetUp]
        public void SetUp()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Inject minimal valid config so SaveDataFactory.CreateNewGame() can run
            var schema   = ScriptableObject.CreateInstance<SchemaVersionSO>();
            var prestige = ScriptableObject.CreateInstance<PrestigeConfigSO>();
            var realm    = ScriptableObject.CreateInstance<RealmIdentityConfigSO>();

            schema.CurrentSchemaVersion     = 0;
            schema.MinimumCompatibleVersion = 0;
            prestige.BaseMultiplierPerPrestige = 1.5f;
            realm.RealmSlug = "base";

            ConfigRegistry.InjectForTesting(prestige: prestige, realm: realm, schema: schema);

            // Create SaveService as a plain instance (no scene needed for test injection path)
            var go = new GameObject("SaveServiceTest");
            _saveService = go.AddComponent<SaveService>();
#endif
        }

        [TearDown]
        public void TearDown()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _saveService.ResetForTesting();
            if (_saveService != null)
                Object.DestroyImmediate(_saveService.gameObject);
            ConfigRegistry.ClearForTesting();
#endif
        }

        // ── AC-SAV-01: New game defaults ──────────────────────────────────────────

        [Test]
        [Description("AC-SAV-01: OnSaveLoaded fires with isNewGame=true and default SaveData fields.")]
        public void NewGame_OnSaveLoaded_FiresWithCorrectDefaults()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Arrange
            SaveData receivedData = null;
            bool receivedIsNewGame = false;
            _saveService.OnSaveLoaded += (data, isNewGame) =>
            {
                receivedData = data;
                receivedIsNewGame = isNewGame;
            };

            var newGameData = SaveDataFactory.CreateNewGame();

            // Act
            _saveService.InjectForTesting(newGameData, isNewGame: true);

            // Assert
            Assert.IsNotNull(receivedData, "OnSaveLoaded must fire with non-null SaveData");
            Assert.IsTrue(receivedIsNewGame, "isNewGame must be true for new game");
            Assert.AreEqual(0L, receivedData.CurrentResources, "CurrentResources must be 0 for new game");
            Assert.AreEqual(0, receivedData.PrestigeCount, "PrestigeCount must be 0 for new game");
            Assert.AreEqual(1, receivedData.WaveNumber, "WaveNumber starts at 1 for new game (first wave)");
            Assert.IsNotNull(receivedData.UpgradeNodeStates, "UpgradeNodeStates must not be null");
            Assert.AreEqual(0, receivedData.UpgradeNodeStates.Count, "UpgradeNodeStates must be empty");
            Assert.AreEqual(RunState.Active, receivedData.CurrentRunState, "CurrentRunState must be Active");
#endif
        }

        [Test]
        [Description("AC-SAV-01: New game SaveData schema version matches ConfigRegistry.Schema.CurrentSchemaVersion.")]
        public void NewGame_SchemaVersion_MatchesConfig()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var newGameData = SaveDataFactory.CreateNewGame();
            Assert.AreEqual(ConfigRegistry.Schema.CurrentSchemaVersion, newGameData.SchemaVersion,
                "New game SchemaVersion must match ConfigRegistry.Schema.CurrentSchemaVersion");
#endif
        }

        [Test]
        [Description("AC-SAV-01: New game BaseMultiplierPerPrestige matches ConfigRegistry.Prestige value.")]
        public void NewGame_BaseMultiplierPerPrestige_MatchesConfig()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var newGameData = SaveDataFactory.CreateNewGame();
            Assert.AreEqual(ConfigRegistry.Prestige.BaseMultiplierPerPrestige,
                newGameData.BaseMultiplierPerPrestige, 0.001f,
                "New game BaseMultiplierPerPrestige must copy from ConfigRegistry.Prestige");
#endif
        }

        [Test]
        [Description("AC-SAV-01: New game UnlockedRealmSlugs contains the current realm slug.")]
        public void NewGame_UnlockedRealmSlugs_ContainsCurrentRealm()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var newGameData = SaveDataFactory.CreateNewGame();
            Assert.IsNotNull(newGameData.UnlockedRealmSlugs, "UnlockedRealmSlugs must not be null");
            Assert.Contains(ConfigRegistry.Realm.RealmSlug, newGameData.UnlockedRealmSlugs,
                "UnlockedRealmSlugs must contain the starting realm slug");
#endif
        }

        // ── AC-SAV-02: Existing save deserialization ──────────────────────────────

        [Test]
        [Description("AC-SAV-02: OnSaveLoaded fires with isNewGame=false and correct deserialized values.")]
        public void ExistingSave_OnSaveLoaded_FiresWithIsNewGameFalse()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Arrange
            var existingSave = new SaveData
            {
                SchemaVersion          = 0,
                CurrentResources       = 5000L,
                PrestigeCount          = 2,
                WaveNumber             = 14,
                UpgradeNodeStates      = new Dictionary<string, int> { ["node_damage_01"] = 3 },
                CurrentRunState        = RunState.Active,
                BaseMultiplierPerPrestige = 1.5f,
            };

            SaveData receivedData    = null;
            bool receivedIsNewGame   = true; // will be overwritten
            _saveService.OnSaveLoaded += (data, isNewGame) =>
            {
                receivedData      = data;
                receivedIsNewGame = isNewGame;
            };

            // Act
            _saveService.InjectForTesting(existingSave, isNewGame: false);

            // Assert
            Assert.IsFalse(receivedIsNewGame, "isNewGame must be false for existing save");
            Assert.AreEqual(5000L, receivedData.CurrentResources, "CurrentResources must match saved value");
            Assert.AreEqual(2, receivedData.PrestigeCount, "PrestigeCount must match saved value");
            Assert.AreEqual(14, receivedData.WaveNumber, "WaveNumber must match saved value");
            Assert.IsTrue(receivedData.UpgradeNodeStates.ContainsKey("node_damage_01"),
                "UpgradeNodeStates must contain persisted node");
            Assert.AreEqual(3, receivedData.UpgradeNodeStates["node_damage_01"],
                "UpgradeNodeStates rank must match saved value");
#endif
        }

        [Test]
        [Description("AC-SAV-02: Loaded save WaveNumber at TotalWavesPerRun boundary is preserved.")]
        public void ExistingSave_WaveNumberAtMax_IsPreserved()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var existingSave = new SaveData { WaveNumber = 30, CurrentResources = 0L };
            SaveData received = null;
            _saveService.OnSaveLoaded += (data, _) => received = data;

            _saveService.InjectForTesting(existingSave, isNewGame: false);

            Assert.AreEqual(30, received.WaveNumber, "WaveNumber at max boundary must be preserved");
#endif
        }

        // ── Uninitialized state rejects queries ───────────────────────────────────

        [Test]
        [Description("SaveService in Uninitialized state: OnSaveLoaded has not fired.")]
        public void Uninitialized_OnSaveLoaded_HasNotFired()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            bool fired = false;
            _saveService.OnSaveLoaded += (_, __) => fired = true;

            // No load or inject called
            Assert.IsFalse(fired, "OnSaveLoaded must not fire before LoadAsync or InjectForTesting is called");
#endif
        }

        // ── Provider notification ─────────────────────────────────────────────────

        [Test]
        [Description("After InjectForTesting, registered providers receive OnAfterLoad.")]
        public void InjectForTesting_RegisteredProvider_ReceivesOnAfterLoad()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var provider = new TestSaveStateProvider();
            _saveService.RegisterStateProvider(provider);

            var data = SaveDataFactory.CreateNewGame();
            _saveService.InjectForTesting(data);

            Assert.IsTrue(provider.AfterLoadCalled, "Registered provider must receive OnAfterLoad");
            Assert.AreSame(data, provider.LastLoadedData, "Provider must receive the correct SaveData");
#endif
        }
    }

    // ── Test helpers ──────────────────────────────────────────────────────────────

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    /// <summary>Minimal ISaveStateProvider implementation for test observation.</summary>
    internal class TestSaveStateProvider : ISaveStateProvider
    {
        public bool AfterLoadCalled { get; private set; }
        public SaveData LastLoadedData { get; private set; }
        public int ProviderOrder => 0;

        public void OnBeforeSave(SaveData saveData) { }

        public void OnAfterLoad(SaveData saveData)
        {
            AfterLoadCalled = true;
            LastLoadedData  = saveData;
        }
    }
#endif
}

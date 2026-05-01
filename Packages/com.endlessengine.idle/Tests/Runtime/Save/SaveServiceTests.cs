using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using EndlessEngine.SaveAndLoad;

namespace EndlessEngine.Tests.Save
{
    [TestFixture]
    public class SaveServiceTests
    {
        private SaveService _saveService;

        [SetUp]
        public void SetUp()
        {
            var go = new GameObject("SaveService");
            _saveService = go.AddComponent<SaveService>();
        }

        [TearDown]
        public void TearDown()
        {
            _saveService.ResetForTesting();
            Object.DestroyImmediate(_saveService.gameObject);
        }

        // ── Provider registration ─────────────────────────────────────────────────

        [Test]
        public void RegisterStateProvider_SortsProvidersByOrder()
        {
            var high  = new StubProvider(order: 100);
            var low   = new StubProvider(order: 10);
            var mid   = new StubProvider(order: 50);

            _saveService.RegisterStateProvider(high);
            _saveService.RegisterStateProvider(low);
            _saveService.RegisterStateProvider(mid);

            var data = new SaveData();
            data.EnsureDefaults();
            _saveService.InjectForTesting(data);

            // Providers called in order low → mid → high
            Assert.Less(low.LoadCallTime, mid.LoadCallTime,  "low (10) should load before mid (50).");
            Assert.Less(mid.LoadCallTime, high.LoadCallTime, "mid (50) should load before high (100).");
        }

        [Test]
        public void InjectForTesting_SetsStateToReady_AndFiresOnSaveLoaded()
        {
            bool fired = false;
            bool wasNewGame = false;
            _saveService.OnSaveLoaded += (_, isNew) => { fired = true; wasNewGame = isNew; };

            var data = new SaveData();
            data.EnsureDefaults();
            _saveService.InjectForTesting(data, isNewGame: true);

            Assert.IsTrue(fired,      "OnSaveLoaded must fire after InjectForTesting.");
            Assert.IsTrue(wasNewGame, "isNewGame flag must be forwarded correctly.");
        }

        [Test]
        public void InjectForTesting_CallsOnAfterLoad_OnAllProviders()
        {
            var p1 = new StubProvider(1);
            var p2 = new StubProvider(2);
            _saveService.RegisterStateProvider(p1);
            _saveService.RegisterStateProvider(p2);

            var data = new SaveData();
            data.EnsureDefaults();
            _saveService.InjectForTesting(data);

            Assert.IsTrue(p1.WasLoaded, "Provider 1 OnAfterLoad must be called.");
            Assert.IsTrue(p2.WasLoaded, "Provider 2 OnAfterLoad must be called.");
        }

        // ── Debounce ──────────────────────────────────────────────────────────────

        [Test]
        public void NotifyUpgradePurchased_ThenExpireDebounce_TriggersOnSaveStarted()
        {
            var data = new SaveData();
            data.EnsureDefaults();
            _saveService.InjectForTesting(data);

            bool saveStarted = false;
            _saveService.OnSaveStarted += () => saveStarted = true;

            _saveService.NotifyUpgradePurchased();
            _saveService.ExpireDebounceTimerForTesting();
            _saveService.TickUpdateForTesting();

            Assert.IsTrue(saveStarted, "OnSaveStarted must fire after debounce expires.");
        }

        [Test]
        public void NotifyUpgradePurchased_MultipleRapidCalls_CollapseToOneSave()
        {
            var data = new SaveData();
            data.EnsureDefaults();
            _saveService.InjectForTesting(data);

            int saveCount = 0;
            _saveService.OnSaveStarted += () => saveCount++;

            _saveService.NotifyUpgradePurchased();
            _saveService.NotifyUpgradePurchased();
            _saveService.NotifyUpgradePurchased();

            _saveService.ExpireDebounceTimerForTesting();
            _saveService.TickUpdateForTesting();

            Assert.AreEqual(1, saveCount, "Multiple rapid NotifyUpgradePurchased calls must collapse into a single save.");
        }

        // ── Auto-save timer ───────────────────────────────────────────────────────

        [Test]
        public void AutoSaveTimer_WhenExpired_TriggersOnSaveStarted()
        {
            var data = new SaveData();
            data.EnsureDefaults();
            _saveService.InjectForTesting(data);

            bool saveStarted = false;
            _saveService.OnSaveStarted += () => saveStarted = true;

            _saveService.ExpireAutoSaveTimerForTesting();
            _saveService.TickUpdateForTesting();

            Assert.IsTrue(saveStarted, "OnSaveStarted must fire when auto-save timer expires.");
        }

        // ── Failure tracking ──────────────────────────────────────────────────────

        [Test]
        public void SimulateSaveResult_ThreeConsecutiveFailures_FiresOnPersistentWriteFailure()
        {
            var data = new SaveData();
            data.EnsureDefaults();
            _saveService.InjectForTesting(data);

            bool failureEventFired = false;
            _saveService.OnPersistentWriteFailure += () => failureEventFired = true;

            _saveService.SimulateSaveResultForTesting(false);
            _saveService.SimulateSaveResultForTesting(false);
            _saveService.SimulateSaveResultForTesting(false);

            Assert.IsTrue(failureEventFired, "OnPersistentWriteFailure must fire after 3 consecutive failures.");
        }

        [Test]
        public void SimulateSaveResult_SuccessResetsFailureCounter()
        {
            var data = new SaveData();
            data.EnsureDefaults();
            _saveService.InjectForTesting(data);

            bool failureEventFired = false;
            _saveService.OnPersistentWriteFailure += () => failureEventFired = true;

            _saveService.SimulateSaveResultForTesting(false);
            _saveService.SimulateSaveResultForTesting(false);
            _saveService.SimulateSaveResultForTesting(true);  // reset
            _saveService.SimulateSaveResultForTesting(false);
            _saveService.SimulateSaveResultForTesting(false);
            // Only 2 failures after reset — event must NOT fire

            Assert.IsFalse(failureEventFired, "Success must reset failure counter, preventing premature OnPersistentWriteFailure.");
        }

        [Test]
        public void SimulateSaveResult_PersistentFailureEvent_FiresOnlyOnce()
        {
            var data = new SaveData();
            data.EnsureDefaults();
            _saveService.InjectForTesting(data);

            int fireCount = 0;
            _saveService.OnPersistentWriteFailure += () => fireCount++;

            for (int i = 0; i < 6; i++)
                _saveService.SimulateSaveResultForTesting(false);

            Assert.AreEqual(1, fireCount, "OnPersistentWriteFailure must fire at most once per failure streak.");
        }

        // ── GetCurrentSaveData / ApplyImportedSaveData ────────────────────────────

        [Test]
        public void GetCurrentSaveData_ReturnsNull_BeforeLoad()
        {
            Assert.IsNull(_saveService.GetCurrentSaveData(), "Should return null before InjectForTesting/LoadAsync.");
        }

        [Test]
        public void GetCurrentSaveData_ReturnsData_AfterLoad()
        {
            var data = new SaveData();
            data.EnsureDefaults();
            data.CurrentResources = 500.0;
            _saveService.InjectForTesting(data);

            var result = _saveService.GetCurrentSaveData();
            Assert.IsNotNull(result);
            Assert.AreEqual(500.0, result.CurrentResources, 1e-9);
        }

        [Test]
        public void ApplyImportedSaveData_NullInput_DoesNotThrow()
        {
            var data = new SaveData();
            data.EnsureDefaults();
            _saveService.InjectForTesting(data);

            Assert.DoesNotThrow(() => _saveService.ApplyImportedSaveData(null));
        }

        [Test]
        public void ApplyImportedSaveData_NotifiesProviders_ViaOnAfterLoad()
        {
            var p = new StubProvider(1);
            _saveService.RegisterStateProvider(p);

            var initial = new SaveData();
            initial.EnsureDefaults();
            _saveService.InjectForTesting(initial);

            p.WasLoaded = false; // reset after initial inject

            var imported = new SaveData();
            imported.CurrentResources = 9999.0;
            _saveService.ApplyImportedSaveData(imported);

            Assert.IsTrue(p.WasLoaded, "ApplyImportedSaveData must call OnAfterLoad on all providers.");
        }

        // ── Load guards ───────────────────────────────────────────────────────────

        [Test]
        public void LoadGuard_PrestigeInProgress_RollsBackResources()
        {
            var data = new SaveData();
            data.EnsureDefaults();
            data.PrestigeInProgress          = true;
            data.CurrentResources            = 0;
            data.PrePrestigeResources        = 1234L;
            data.WaveNumber                  = 5;
            data.PrePrestigeWaveNumber       = 3;

            _saveService.InjectForTesting(data);

            var loaded = _saveService.GetCurrentSaveData();
            Assert.IsFalse(loaded.PrestigeInProgress, "PrestigeInProgress must be cleared after rollback.");
            Assert.AreEqual(1234L, (long)loaded.CurrentResources, "Resources must be rolled back to pre-prestige snapshot.");
            Assert.AreEqual(3, loaded.WaveNumber, "WaveNumber must be rolled back to pre-prestige snapshot.");
        }

        // ── Stub provider ─────────────────────────────────────────────────────────

        private class StubProvider : ISaveStateProvider
        {
            public int ProviderOrder { get; }
            public bool WasLoaded   { get; set; }
            public bool WasSaved    { get; set; }
            public long LoadCallTime { get; private set; }

            private static long _callCounter;

            public StubProvider(int order) => ProviderOrder = order;

            public void OnBeforeSave(SaveData d) => WasSaved = true;
            public void OnAfterLoad(SaveData d)
            {
                WasLoaded    = true;
                LoadCallTime = System.Threading.Interlocked.Increment(ref _callCounter);
            }
        }
    }
}

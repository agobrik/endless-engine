using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using EndlessEngine.Economy;
using EndlessEngine.Economy.Math;
using EndlessEngine.SaveAndLoad;

namespace EndlessEngine.Tests.Economy
{
    [TestFixture]
    public class EconomyServiceTests
    {
        private EconomyService _economy;
        private List<(double current, double delta)> _changeLog;

        [SetUp]
        public void SetUp()
        {
            BigNumberFactory.Configure(NumberBackend.DoubleNumber);
            EconomyService.ClearSubscribersForTesting();

            var go = new GameObject("EconomyService");
            _economy = go.AddComponent<EconomyService>();

            _changeLog = new List<(double, double)>();
            EconomyService.OnResourcesChanged += (c, d) => _changeLog.Add((c, d));
        }

        [TearDown]
        public void TearDown()
        {
            EconomyService.ClearSubscribersForTesting();
            Object.DestroyImmediate(_economy.gameObject);
        }

        private void Inject(double current = 0, double hardCap = 1_000_000, double starting = 0)
            => _economy.InjectStateForTesting(current, hardCap, starting);

        // ── AddResources ──────────────────────────────────────────────────────────

        [Test]
        public void AddResources_IncreasesBalance_AndFiresEvent()
        {
            Inject(current: 100);
            _changeLog.Clear();

            _economy.AddResources(50.0);

            Assert.AreEqual(150.0, _economy.CurrentResources, 1e-9);
            Assert.AreEqual(1, _changeLog.Count);
            Assert.AreEqual(150.0, _changeLog[0].current, 1e-9);
            Assert.AreEqual(50.0,  _changeLog[0].delta,   1e-9);
        }

        [Test]
        public void AddResources_ZeroAmount_DoesNotChangeBalance()
        {
            Inject(current: 200);
            _changeLog.Clear();

            _economy.AddResources(0.0);

            Assert.AreEqual(200.0, _economy.CurrentResources, 1e-9);
            Assert.AreEqual(1, _changeLog.Count, "Event still fires with 0 delta.");
        }

        [Test]
        public void AddResources_NegativeAmount_ClampsToZero()
        {
            Inject(current: 100);
            double before = _economy.CurrentResources;

            _economy.AddResources(-50.0);

            Assert.AreEqual(before, _economy.CurrentResources, 1e-9, "Negative add must not reduce balance.");
        }

        [Test]
        public void AddResources_CapacityFull_TruncatesGain()
        {
            Inject(current: 999_990, hardCap: 1_000_000);

            _economy.AddResources(100.0);

            Assert.AreEqual(1_000_000.0, _economy.CurrentResources, 1e-9, "Balance must not exceed hard cap.");
        }

        [Test]
        public void AddResources_LongOverload_Works()
        {
            Inject(current: 0);

            _economy.AddResources(500L);

            Assert.AreEqual(500.0, _economy.CurrentResources, 1e-9);
        }

        // ── DeductResources ───────────────────────────────────────────────────────

        [Test]
        public void DeductResources_DecreasesBalance_AndFiresNegativeDelta()
        {
            Inject(current: 500);
            _changeLog.Clear();

            _economy.DeductResources(200.0);

            Assert.AreEqual(300.0, _economy.CurrentResources, 1e-9);
            Assert.AreEqual(-200.0, _changeLog[0].delta, 1e-9);
        }

        [Test]
        public void DeductResources_InsufficientBalance_DoesNothing()
        {
            Inject(current: 100);
            double before = _economy.CurrentResources;

            _economy.DeductResources(500.0);

            Assert.AreEqual(before, _economy.CurrentResources, 1e-9, "Deduction beyond balance must be rejected.");
        }

        [Test]
        public void DeductResources_NegativeAmount_DoesNothing()
        {
            Inject(current: 200);
            double before = _economy.CurrentResources;

            _economy.DeductResources(-50.0);

            Assert.AreEqual(before, _economy.CurrentResources, 1e-9, "Negative deduction must be rejected.");
        }

        [Test]
        public void DeductResources_ExactBalance_LeavesZero()
        {
            Inject(current: 300);

            _economy.DeductResources(300.0);

            Assert.AreEqual(0.0, _economy.CurrentResources, 1e-9);
        }

        // ── Prestige reset ────────────────────────────────────────────────────────

        [Test]
        public void PrestigeReset_SetsBalanceToStartingGold_AndFiresEvent()
        {
            Inject(current: 50_000, starting: 100);
            _changeLog.Clear();

            _economy.InjectPrestigeResetForTesting();

            Assert.AreEqual(100.0, _economy.CurrentResources, 1e-9, "Balance must revert to StartingGold on prestige.");
            Assert.AreEqual(1, _changeLog.Count, "Event must fire on prestige reset.");
        }

        // ── Save / Load roundtrip ─────────────────────────────────────────────────

        [Test]
        public void OnBeforeSave_WritesCurrentResources_ToSaveData()
        {
            Inject(current: 12_345.0);

            var save = new SaveData();
            save.EnsureDefaults();
            _economy.OnBeforeSave(save);

            Assert.AreEqual(12_345.0, save.CurrentResources, 1e-9);
        }

        [Test]
        public void OnBeforeSave_StoresNumberBackendName()
        {
            BigNumberFactory.Configure(NumberBackend.DoubleNumber);
            Inject(current: 0);

            var save = new SaveData();
            save.EnsureDefaults();
            _economy.OnBeforeSave(save);

            Assert.AreEqual("DoubleNumber", save.NumberBackendName);
        }

        // ── Static accessor sync ──────────────────────────────────────────────────

        [Test]
        public void CurrentResourcesStatic_UpdatesOnChange()
        {
            Inject(current: 0);
            _economy.AddResources(777.0);

            Assert.AreEqual(777.0, EconomyService.CurrentResourcesStatic, 1e-9);
        }

        // ── Long accessor ─────────────────────────────────────────────────────────

        [Test]
        public void CurrentResourcesLong_ClampsAtMaxLong()
        {
            BigNumberFactory.Configure(NumberBackend.BigDouble);
            _economy.InjectStateForTesting(1e30, 1e35, 0.0);

            long result = _economy.CurrentResourcesLong;
            Assert.AreEqual(long.MaxValue, result, "Long accessor must clamp at long.MaxValue.");

            BigNumberFactory.Configure(NumberBackend.DoubleNumber);
        }

        // ── TryPurchase ───────────────────────────────────────────────────────────

        [Test]
        public void TryPurchase_InsufficientBalance_FiresOnPurchaseFailed()
        {
            Inject(current: 50);

            var stubQuery = new StubUpgradeQuery(costFor: 100.0);
            _economy.Initialize(stubQuery, null);

            string failedNode = null;
            EconomyService.OnPurchaseFailed += (id, cost, bal) => failedNode = id;

            _economy.TryPurchase("node_x");

            Assert.AreEqual("node_x", failedNode);
        }

        [Test]
        public void TryPurchase_SufficientBalance_FiresOnUpgradePurchasedAndDeducts()
        {
            Inject(current: 500);

            var stubQuery = new StubUpgradeQuery(costFor: 200.0);
            _economy.Initialize(stubQuery, null);

            string purchasedNode = null;
            EconomyService.OnUpgradePurchased += (id, _) => purchasedNode = id;

            _economy.TryPurchase("node_y");

            Assert.AreEqual("node_y", purchasedNode);
            Assert.AreEqual(300.0, _economy.CurrentResources, 1e-9);
        }

        // ── Stub helpers ──────────────────────────────────────────────────────────

        private class StubUpgradeQuery : IUpgradeTreeQuery
        {
            private readonly double _cost;
            public StubUpgradeQuery(double costFor) => _cost = costFor;
            public double GetNodeCostDouble(string nodeId) => _cost;
            public long   GetNodeCost(string nodeId)       => (long)_cost;
        }
    }
}

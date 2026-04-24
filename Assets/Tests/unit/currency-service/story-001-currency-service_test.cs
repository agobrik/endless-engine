// Tests for Sprint 7 — S7-05: CurrencyService multi-currency
// Type: Logic (Unit/EditMode)
//
// Covers:
//   - Add / TrySpend / CanAfford / GetBalance basics
//   - HardCap enforcement
//   - Save/load round-trip (ISaveStateProvider)
//   - Unknown currency no-op behaviour
//   - Prestige reset (ResetsOnPrestige)
//   - OnCurrencyChanged / OnSpendFailed events
//
// To run: Unity Test Runner → EditMode → EndlessEngine.Tests.Unit.CurrencyService

using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using EndlessEngine.Config;
using EndlessEngine.Economy;
using EndlessEngine.SaveAndLoad;

namespace EndlessEngine.Tests.Unit.CurrencyService
{
    [TestFixture]
    public class CurrencyServiceTests
    {
        private Economy.CurrencyService _service;
        private CurrencyDatabaseSO      _database;
        private CurrencyConfigSO        _gemConfig;
        private CurrencyConfigSO        _tokenConfig;

        [SetUp]
        public void SetUp()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _gemConfig             = ScriptableObject.CreateInstance<CurrencyConfigSO>();
            _gemConfig.CurrencyId  = "gems";
            _gemConfig.HardCap     = 10_000;
            _gemConfig.StartingAmount = 0;
            _gemConfig.ResetsOnPrestige = false;
            _gemConfig.Notation    = BigNumberNotation.Letter;
            _gemConfig.DecimalPlaces = 1;

            _tokenConfig               = ScriptableObject.CreateInstance<CurrencyConfigSO>();
            _tokenConfig.CurrencyId    = "tokens";
            _tokenConfig.HardCap       = 0;   // no cap
            _tokenConfig.StartingAmount = 5;
            _tokenConfig.ResetsOnPrestige = true;
            _tokenConfig.Notation      = BigNumberNotation.Letter;
            _tokenConfig.DecimalPlaces = 0;

            _database            = ScriptableObject.CreateInstance<CurrencyDatabaseSO>();
            _database.Currencies = new[] { _gemConfig, _tokenConfig };

            var go = new GameObject("CurrencyServiceTest");
            _service = go.AddComponent<Economy.CurrencyService>();
            _service.Initialize(_database);
            _service.SubscribeForTesting(); // OnEnable doesn't fire in EditMode tests

            // OnAfterLoad with empty save (new game)
            var saveData = new SaveData();
            saveData.EnsureDefaults();
            _service.OnAfterLoad(saveData);
#endif
        }

        [TearDown]
        public void TearDown()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_service != null) _service.UnsubscribeForTesting();
            Economy.CurrencyService.ClearSubscribersForTesting();
            if (_service != null) UnityEngine.Object.DestroyImmediate(_service.gameObject);
            if (_database != null) UnityEngine.Object.DestroyImmediate(_database);
            if (_gemConfig != null) UnityEngine.Object.DestroyImmediate(_gemConfig);
            if (_tokenConfig != null) UnityEngine.Object.DestroyImmediate(_tokenConfig);
#endif
        }

        // ── GetBalance ────────────────────────────────────────────────────────────

        [Test]
        public void GetBalance_NewGame_ReturnsStartingAmount()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Assert.AreEqual(0, _service.GetBalance("gems"));
            Assert.AreEqual(5, _service.GetBalance("tokens"));
#endif
        }

        [Test]
        public void GetBalance_UnknownCurrency_ReturnsZero()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Assert.AreEqual(0, _service.GetBalance("nonexistent"));
#endif
        }

        // ── Add ───────────────────────────────────────────────────────────────────

        [Test]
        public void Add_PositiveAmount_IncreasesBalance()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _service.Add("gems", 100);
            Assert.AreEqual(100, _service.GetBalance("gems"), 0.001);
#endif
        }

        [Test]
        public void Add_ExceedsHardCap_ClampsToHardCap()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _service.Add("gems", 20_000);
            Assert.AreEqual(10_000, _service.GetBalance("gems"), 0.001,
                "Balance must be clamped to HardCap");
#endif
        }

        [Test]
        public void Add_NoHardCap_AccumulatesUnlimited()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _service.Add("tokens", 1_000_000);
            Assert.AreEqual(1_000_005, _service.GetBalance("tokens"), 0.001);
#endif
        }

        [Test]
        public void Add_UnknownCurrency_NoOp()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Assert.DoesNotThrow(() => _service.Add("nonexistent", 100));
#endif
        }

        [Test]
        public void Add_NegativeAmount_NoOp()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _service.Add("gems", -50);
            Assert.AreEqual(0, _service.GetBalance("gems"));
#endif
        }

        // ── TrySpend ──────────────────────────────────────────────────────────────

        [Test]
        public void TrySpend_SufficientBalance_ReturnsTrue()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _service.Add("gems", 500);
            bool result = _service.TrySpend("gems", 200);
            Assert.IsTrue(result);
            Assert.AreEqual(300, _service.GetBalance("gems"), 0.001);
#endif
        }

        [Test]
        public void TrySpend_InsufficientBalance_ReturnsFalse()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _service.Add("gems", 100);
            bool result = _service.TrySpend("gems", 500);
            Assert.IsFalse(result);
            Assert.AreEqual(100, _service.GetBalance("gems"), 0.001, "Balance must be unchanged on failed spend");
#endif
        }

        [Test]
        public void TrySpend_ExactBalance_ReturnsTrue()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _service.Add("gems", 100);
            bool result = _service.TrySpend("gems", 100);
            Assert.IsTrue(result);
            Assert.AreEqual(0, _service.GetBalance("gems"), 0.001);
#endif
        }

        [Test]
        public void TrySpend_UnknownCurrency_ReturnsFalse()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            bool result = _service.TrySpend("nonexistent", 10);
            Assert.IsFalse(result);
#endif
        }

        // ── CanAfford ─────────────────────────────────────────────────────────────

        [Test]
        public void CanAfford_SufficientBalance_ReturnsTrue()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _service.Add("gems", 1000);
            Assert.IsTrue(_service.CanAfford("gems", 500));
#endif
        }

        [Test]
        public void CanAfford_InsufficientBalance_ReturnsFalse()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Assert.IsFalse(_service.CanAfford("gems", 100));
#endif
        }

        // ── Events ────────────────────────────────────────────────────────────────

        [Test]
        public void Add_FiresOnCurrencyChangedWithCorrectDelta()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            string capturedId   = null;
            double capturedNew  = 0;
            double capturedDelta = 0;
            Economy.CurrencyService.OnCurrencyChanged += (id, newBal, delta) =>
            {
                capturedId    = id;
                capturedNew   = newBal;
                capturedDelta = delta;
            };

            _service.Add("gems", 250);

            Assert.AreEqual("gems", capturedId);
            Assert.AreEqual(250, capturedNew, 0.001);
            Assert.AreEqual(250, capturedDelta, 0.001);
#endif
        }

        [Test]
        public void TrySpend_Failure_FiresOnSpendFailed()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            bool failFired = false;
            Economy.CurrencyService.OnSpendFailed += (_, _, _) => failFired = true;

            _service.TrySpend("gems", 9999);
            Assert.IsTrue(failFired);
#endif
        }

        // ── ISaveStateProvider round-trip ─────────────────────────────────────────

        [Test]
        public void SaveLoad_RoundTrip_BalancesPreserved()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _service.Add("gems", 3333);
            _service.Add("tokens", 77);

            // Save
            var saveData = new SaveData();
            saveData.EnsureDefaults();
            _service.OnBeforeSave(saveData);

            // Restore fresh instance
            _service.ResetForTesting();
            _service.Initialize(_database);
            _service.OnAfterLoad(saveData);

            Assert.AreEqual(3333, _service.GetBalance("gems"), 0.001);
            Assert.AreEqual(82,   _service.GetBalance("tokens"), 0.001); // 5 starting + 77
#endif
        }

        [Test]
        public void OnAfterLoad_UnknownCurrencyInSave_IsIgnored()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var saveData = new SaveData { CurrencyBalances = new Dictionary<string, double> { ["ancient_coins"] = 999 } };
            Assert.DoesNotThrow(() => _service.OnAfterLoad(saveData));
            Assert.AreEqual(0, _service.GetBalance("ancient_coins"));
#endif
        }

        [Test]
        public void OnAfterLoad_BalanceExceedsHardCap_IsClamped()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var saveData = new SaveData { CurrencyBalances = new Dictionary<string, double> { ["gems"] = 999_999 } };
            _service.OnAfterLoad(saveData);
            Assert.AreEqual(10_000, _service.GetBalance("gems"), 0.001, "Overcapped save must be clamped on load");
#endif
        }

        // ── Prestige reset ────────────────────────────────────────────────────────

        [Test]
        public void PrestigeStarted_ResetsOnPrestige_BalanceReturnsToStarting()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _service.Add("tokens", 5000);
            Assert.AreEqual(5005, _service.GetBalance("tokens"), 0.001);

            Prestige.PrestigeStateManager.FirePrestigeStartedForTesting();

            Assert.AreEqual(5, _service.GetBalance("tokens"), 0.001,
                "Tokens (ResetsOnPrestige=true) must reset to StartingAmount");
#endif
        }

        [Test]
        public void PrestigeStarted_NotResetsOnPrestige_BalancePreserved()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _service.Add("gems", 999);

            Prestige.PrestigeStateManager.FirePrestigeStartedForTesting();

            Assert.AreEqual(999, _service.GetBalance("gems"), 0.001,
                "Gems (ResetsOnPrestige=false) must not reset on prestige");
#endif
        }
    }
}

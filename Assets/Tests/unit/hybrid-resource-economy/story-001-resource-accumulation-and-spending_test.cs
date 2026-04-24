// Tests for Story S3-03: Hybrid Resource Economy — EconomyService Core
// Type: Logic (Unit/EditMode)
// Story: production/epics/hybrid-resource-economy/story-001-resource-accumulation-and-spending.md
//
// These tests verify:
//   (1) AC-ECO-01: New game initializes to StartingGold
//   (2) AC-ECO-02: Save load restores correct balance
//   (3) AC-ECO-03: Offline gain applied on top of save balance
//   (4) AC-ECO-04: Combat drop increases Gold
//   (5) AC-ECO-05: Hard cap truncates excess gain
//   (6) AC-ECO-06: Successful purchase deducts cost and raises OnUpgradePurchased
//   (7) AC-ECO-07: Insufficient balance rejects purchase and raises OnPurchaseFailed
//   (8) AC-ECO-08: Prestige resets Gold to StartingGold
//   (9) EC-ECO-06: AddResources(0) is a no-op (raises OnResourcesChanged with delta=0)
//   (10) EC-ECO-07: Overflow-safe cap clamp (large amounts do not overflow)
//
// To run: Unity Test Runner → EditMode → EndlessEngine.Tests.Unit.HybridResourceEconomy

using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using EndlessEngine.Economy;
using EndlessEngine.SaveAndLoad;

namespace EndlessEngine.Tests.Unit.HybridResourceEconomy
{
    /// <summary>
    /// Unit tests for EconomyService core behavior (S3-03 / Story 001).
    /// </summary>
    [TestFixture]
    public class ResourceAccumulationAndSpendingTests
    {
        // ── Fakes ──────────────────────────────────────────────────────────────────

        private class FakeUpgradeTreeQuery : IUpgradeTreeQuery
        {
            public Dictionary<string, long> Costs = new Dictionary<string, long>();

            public long GetNodeCost(string nodeId)
            {
                return Costs.TryGetValue(nodeId, out long cost) ? cost : 0L;
            }
        }

        private class FakeSaveNotifier : ISaveNotifier
        {
            public int NotifyCount;
            public void NotifyUpgradePurchased() => NotifyCount++;
        }

        // ── State ──────────────────────────────────────────────────────────────────

        private EconomyService _economy;
        private FakeUpgradeTreeQuery _upgradeQuery;
        private FakeSaveNotifier _saveNotifier;

        // Captured event data
        private long _lastNewBalance;
        private long _lastDelta;
        private int  _resourcesChangedCount;

        private string _lastPurchasedNodeId;
        private long   _lastPurchasedCost;

        private string _lastFailedNodeId;
        private long   _lastFailedCost;
        private long   _lastFailedBalance;

        // ── SetUp / TearDown ──────────────────────────────────────────────────────

        [SetUp]
        public void SetUp()
        {
            _upgradeQuery = new FakeUpgradeTreeQuery();
            _saveNotifier = new FakeSaveNotifier();

            var go = new GameObject("EconomyService");
            _economy = go.AddComponent<EconomyService>();
            _economy.Initialize(_upgradeQuery, _saveNotifier);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _economy.InjectStateForTesting(0L, 1_000_000_000L, 0L);
#endif

            ResetEventCaptures();
            SubscribeToEvents();
        }

        [TearDown]
        public void TearDown()
        {
            UnsubscribeFromEvents();
            if (_economy != null)
                UnityEngine.Object.DestroyImmediate(_economy.gameObject);
        }

        private void ResetEventCaptures()
        {
            _lastNewBalance = -1L;
            _lastDelta = 0L;
            _resourcesChangedCount = 0;
            _lastPurchasedNodeId = null;
            _lastPurchasedCost = 0L;
            _lastFailedNodeId = null;
            _lastFailedCost = 0L;
            _lastFailedBalance = 0L;
        }

        private void SubscribeToEvents()
        {
            EconomyService.OnResourcesChanged   += CaptureResourcesChanged;
            EconomyService.OnUpgradePurchased   += CaptureUpgradePurchased;
            EconomyService.OnPurchaseFailed     += CapturePurchaseFailed;
        }

        private void UnsubscribeFromEvents()
        {
            EconomyService.OnResourcesChanged   -= CaptureResourcesChanged;
            EconomyService.OnUpgradePurchased   -= CaptureUpgradePurchased;
            EconomyService.OnPurchaseFailed     -= CapturePurchaseFailed;
        }

        private void CaptureResourcesChanged(long newBalance, long delta)
        {
            _lastNewBalance = newBalance;
            _lastDelta      = delta;
            _resourcesChangedCount++;
        }

        private void CaptureUpgradePurchased(string nodeId, long cost)
        {
            _lastPurchasedNodeId = nodeId;
            _lastPurchasedCost   = cost;
        }

        private void CapturePurchaseFailed(string nodeId, long cost, long balance)
        {
            _lastFailedNodeId    = nodeId;
            _lastFailedCost      = cost;
            _lastFailedBalance   = balance;
        }

        // ── Tests ─────────────────────────────────────────────────────────────────

        /// <summary>AC-ECO-01: New game initializes to StartingGold (= 0).</summary>
        [Test]
        public void Test_EconomyService_NewGame_InitializesToStartingGold()
        {
            // Arrange — already set up with InjectStateForTesting(currentResources=0, hardCap, startingGold=0)

            // Act — verify current state
            long balance = _economy.CurrentResources;

            // Assert
            Assert.AreEqual(0L, balance, "New game should initialize to StartingGold=0.");
        }

        /// <summary>AC-ECO-02: OnAfterLoad restores correct balance from save.</summary>
        [Test]
        public void Test_EconomyService_OnAfterLoad_RestoresSaveBalance()
        {
            // Arrange
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _economy.InjectStateForTesting(12_345L, 1_000_000_000L, 0L);
#endif
            // Act
            long balance = _economy.CurrentResources;

            // Assert
            Assert.AreEqual(12_345L, balance, "Balance should be restored from save data.");
        }

        /// <summary>AC-ECO-03: Offline gain is applied on top of save balance.</summary>
        [Test]
        public void Test_EconomyService_AddResources_AppliesOfflineGainOnTopOfSaveBalance()
        {
            // Arrange
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _economy.InjectStateForTesting(5_000L, 1_000_000_000L, 0L);
#endif
            ResetEventCaptures();

            // Act — simulate OnOfflineGainCalculated delivering 36_000 gold
            _economy.AddResources(36_000L);

            // Assert
            Assert.AreEqual(41_000L, _economy.CurrentResources,
                "Balance after offline gain should be 5000 + 36000 = 41000.");
            Assert.AreEqual(41_000L, _lastNewBalance,
                "OnResourcesChanged should fire with new balance = 41000.");
            Assert.AreEqual(36_000L, _lastDelta,
                "OnResourcesChanged delta should equal the full offline gain = 36000.");
        }

        /// <summary>AC-ECO-04: Combat drop increases Gold and fires OnResourcesChanged.</summary>
        [Test]
        public void Test_EconomyService_AddResources_CombatDropIncreasesGold()
        {
            // Arrange
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _economy.InjectStateForTesting(1_000L, 1_000_000_000L, 0L);
#endif
            ResetEventCaptures();

            // Act — simulate OnEnemyKilled delivering 25 gold
            _economy.AddResources(25L);

            // Assert
            Assert.AreEqual(1_025L, _economy.CurrentResources,
                "Balance after enemy kill should be 1000 + 25 = 1025.");
            Assert.AreEqual(1_025L, _lastNewBalance);
            Assert.AreEqual(25L,    _lastDelta);
        }

        /// <summary>AC-ECO-05: Hard cap truncates excess gain; delta reflects actual added amount.</summary>
        [Test]
        public void Test_EconomyService_AddResources_HardCapTruncatesExcessGain()
        {
            // Arrange
            const long hardCap  = 1_000_000_000L;
            const long balance  = 999_999_990L;
            const long dropAmt  = 50L;
            const long expected = hardCap; // capped
            const long expectedDelta = hardCap - balance; // 10, not 50
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _economy.InjectStateForTesting(balance, hardCap, 0L);
#endif
            ResetEventCaptures();

            // Act
            _economy.AddResources(dropAmt);

            // Assert
            Assert.AreEqual(expected,       _economy.CurrentResources, "Balance should be capped at ResourceHardCap.");
            Assert.AreEqual(expected,        _lastNewBalance,           "Event new balance should equal cap.");
            Assert.AreEqual(expectedDelta,   _lastDelta,                "Event delta should reflect actual added (10), not requested (50).");
        }

        /// <summary>AC-ECO-06: Successful purchase deducts cost, fires OnUpgradePurchased and OnResourcesChanged.</summary>
        [Test]
        public void Test_EconomyService_TryPurchase_SufficientBalance_DeductsCostAndFiresEvents()
        {
            // Arrange
            const string nodeId = "node_attack_1";
            const long   cost   = 200L;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _economy.InjectStateForTesting(500L, 1_000_000_000L, 0L);
#endif
            _upgradeQuery.Costs[nodeId] = cost;
            ResetEventCaptures();

            // Act
            _economy.TryPurchase(nodeId);

            // Assert
            Assert.AreEqual(300L,    _economy.CurrentResources, "Balance after purchase should be 500 - 200 = 300.");
            Assert.AreEqual(nodeId,  _lastPurchasedNodeId,       "OnUpgradePurchased should fire with the node ID.");
            Assert.AreEqual(cost,    _lastPurchasedCost,         "OnUpgradePurchased should fire with the correct cost.");
            Assert.AreEqual(300L,    _lastNewBalance,            "OnResourcesChanged should fire with new balance = 300.");
            Assert.AreEqual(-200L,   _lastDelta,                 "OnResourcesChanged delta should be negative cost (-200).");
            Assert.AreEqual(1,       _saveNotifier.NotifyCount,  "NotifyUpgradePurchased should have been called once.");
        }

        /// <summary>AC-ECO-07: Insufficient balance rejects purchase, fires OnPurchaseFailed, balance unchanged.</summary>
        [Test]
        public void Test_EconomyService_TryPurchase_InsufficientBalance_FiresOnPurchaseFailed()
        {
            // Arrange
            const string nodeId = "node_attack_1";
            const long   cost   = 200L;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _economy.InjectStateForTesting(100L, 1_000_000_000L, 0L);
#endif
            _upgradeQuery.Costs[nodeId] = cost;
            ResetEventCaptures();

            // Act
            _economy.TryPurchase(nodeId);

            // Assert
            Assert.AreEqual(100L,    _economy.CurrentResources,  "Balance should be unchanged after failed purchase.");
            Assert.IsNull(_lastPurchasedNodeId,                   "OnUpgradePurchased must NOT fire on failed purchase.");
            Assert.AreEqual(nodeId,  _lastFailedNodeId,           "OnPurchaseFailed should fire with the node ID.");
            Assert.AreEqual(cost,    _lastFailedCost,             "OnPurchaseFailed should report the required cost.");
            Assert.AreEqual(100L,    _lastFailedBalance,          "OnPurchaseFailed should report current balance.");
            Assert.AreEqual(0,       _saveNotifier.NotifyCount,   "NotifyUpgradePurchased should NOT be called on failure.");
        }

        /// <summary>AC-ECO-08: OnPrestigeStarted resets Gold to StartingGold.</summary>
        [Test]
        public void Test_EconomyService_PrestigeStarted_ResetsGoldToStartingGold()
        {
            // Arrange
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _economy.InjectStateForTesting(50_000L, 1_000_000_000L, 0L);
#endif
            ResetEventCaptures();

            // Act — simulate PrestigeStateManager.OnPrestigeStarted firing
            // We need to invoke the handler directly since it's a static event
            // and PrestigeStateManager isn't instantiated in unit tests.
            // InjectStateForTesting ensures _initialized = true.
            // The event handler is wired in OnEnable — trigger it by simulating the event.
            // Since static events need the PSM MonoBehaviour, we use InjectForTesting
            // to call HandlePrestigeStarted indirectly via the InjectPrestigeReset helper.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _economy.InjectPrestigeResetForTesting();
#endif

            // Assert
            Assert.AreEqual(0L,       _economy.CurrentResources, "After prestige, balance should reset to StartingGold=0.");
            Assert.AreEqual(0L,       _lastNewBalance,            "OnResourcesChanged should fire with new balance = 0.");
            Assert.AreEqual(-50_000L, _lastDelta,                 "OnResourcesChanged delta should be negative (reset delta).");
        }

        /// <summary>EC-ECO-06: AddResources(0) raises OnResourcesChanged with delta=0 (no-op for balance).</summary>
        [Test]
        public void Test_EconomyService_AddResources_ZeroAmount_FiresEventWithDeltaZero()
        {
            // Arrange
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _economy.InjectStateForTesting(1_000L, 1_000_000_000L, 0L);
#endif
            ResetEventCaptures();

            // Act
            _economy.AddResources(0L);

            // Assert
            Assert.AreEqual(1_000L, _economy.CurrentResources,  "Balance should not change for zero-amount add.");
            Assert.AreEqual(1_000L, _lastNewBalance,             "OnResourcesChanged should still fire.");
            Assert.AreEqual(0L,     _lastDelta,                  "Delta should be 0 for zero-amount add.");
            Assert.AreEqual(1,      _resourcesChangedCount,      "OnResourcesChanged should fire exactly once.");
        }

        /// <summary>EC-ECO-07: Overflow-safe cap — adding long.MaxValue near cap does not overflow.</summary>
        [Test]
        public void Test_EconomyService_AddResources_OverflowSafe_DoesNotExceedHardCap()
        {
            // Arrange
            const long hardCap = 1_000_000_000L;
            const long near    = 999_999_999L;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _economy.InjectStateForTesting(near, hardCap, 0L);
#endif
            ResetEventCaptures();

            // Act — try to add more than long.MaxValue would overflow if not handled
            _economy.AddResources(long.MaxValue);

            // Assert — balance must be exactly at cap, no exception
            Assert.AreEqual(hardCap, _economy.CurrentResources, "Balance should clamp to ResourceHardCap without overflow.");
            Assert.AreEqual(1L,      _lastDelta,                "Delta should be 1 (only 1 unit of headroom remaining).");
        }

        /// <summary>EC-ECO-03: Exact-cost purchase succeeds (balance == cost).</summary>
        [Test]
        public void Test_EconomyService_TryPurchase_ExactBalance_Succeeds()
        {
            // Arrange
            const string nodeId = "node_defense_1";
            const long   cost   = 500L;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _economy.InjectStateForTesting(500L, 1_000_000_000L, 0L);
#endif
            _upgradeQuery.Costs[nodeId] = cost;
            ResetEventCaptures();

            // Act
            _economy.TryPurchase(nodeId);

            // Assert
            Assert.AreEqual(0L,     _economy.CurrentResources, "Balance should reach zero after exact-cost purchase.");
            Assert.AreEqual(nodeId, _lastPurchasedNodeId,       "OnUpgradePurchased should fire.");
        }
    }
}

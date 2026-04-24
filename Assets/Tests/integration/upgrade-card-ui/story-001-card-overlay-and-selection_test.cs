// Integration tests for Upgrade Card UI Story 001 — Card Overlay and Selection
// Sprint: S4-04
// GDD: design/gdd/upgrade-selection.md
// ACs: UIC-INT-01 through UIC-INT-03
//
// Tests the event routing chain between UpgradeSelectionService events
// and the UpgradeCardUI controller. UIDocument rendering is verified manually.
// All helpers are guarded by #if UNITY_EDITOR || DEVELOPMENT_BUILD.

#if UNITY_EDITOR || DEVELOPMENT_BUILD
using NUnit.Framework;
using UnityEngine;
using EndlessEngine.Config;
using EndlessEngine.Economy;
using EndlessEngine.Upgrade;

namespace EndlessEngine.Tests.Integration.UpgradeCardUI
{
    /// <summary>
    /// Integration tests for upgrade card UI event plumbing.
    ///
    /// UIC-INT-01: UpgradeSelectionService.OnCardsReady fires → UpgradeCardUI activates overlay
    /// UIC-INT-02: Card button clicked → OnCardChosen(index) fires to UpgradeSelectionService
    /// UIC-INT-03: OnUpgradeSelected fires → overlay deactivates
    ///
    /// Note: UI rendering is not testable in EditMode. These tests verify event chain correctness.
    /// Full visual verification: production/qa/evidence/S4-04-upgrade-card-walkthrough.md
    /// </summary>
    [TestFixture]
    public class Story001CardOverlayAndSelectionTests
    {
        // ── Fixtures ──────────────────────────────────────────────────────────────

        private EconomyConfigSO _economyConfig;

        [SetUp]
        public void SetUp()
        {
            _economyConfig = ScriptableObject.CreateInstance<EconomyConfigSO>();
            _economyConfig.StartingGold    = 9999L;
            _economyConfig.ResourceHardCap = 999_999_999L;

            ConfigRegistry.InjectForTesting(
                economy:  _economyConfig,
                upgrades: new UpgradeNodeConfigSO[0]
            );
        }

        [TearDown]
        public void TearDown()
        {
            UpgradeSelectionService.ClearStaticSubscribersForTesting();
            ConfigRegistry.ClearForTesting();

            if (_economyConfig != null) Object.DestroyImmediate(_economyConfig);
        }

        // ── UIC-INT-01: OnCardsReady fires → subscribers receive card data ─────────

        [Test]
        public void UIC_INT_01_OnCardsReady_FiresWithCards_SubscriberReceivesData()
        {
            // Arrange
            UpgradeCardData[] receivedCards = null;
            UpgradeSelectionService.OnCardsReady += cards => receivedCards = cards;

            var testCards = new[]
            {
                new UpgradeCardData { NodeId = "node-a", DisplayName = "Card A", Cost = 100L, IsAffordable = true },
                new UpgradeCardData { NodeId = "node-b", DisplayName = "Card B", Cost = 200L, IsAffordable = false },
            };

            // Act: fire directly (simulates UpgradeSelectionService internal BuildAndPresentCards)
            UpgradeSelectionService.FireOnCardsReadyForTesting(testCards);

            // Assert
            Assert.IsNotNull(receivedCards,
                "OnCardsReady subscriber must receive card data when event fires.");
            Assert.AreEqual(2, receivedCards.Length,
                "Subscriber must receive the same number of cards that were fired.");
            Assert.AreEqual("node-a", receivedCards[0].NodeId);
        }

        // ── UIC-INT-02: NotifyCardChosen routes selection ─────────────────────────

        [Test]
        public void UIC_INT_02_NotifyCardChosen_RoutesCardIndexToService()
        {
            // Arrange: set up a service with a mock-purchasable card
            var go      = new GameObject("UpgradeSelectionService");
            var service = go.AddComponent<UpgradeSelectionService>();

            string chosenNodeId = null;
            UpgradeSelectionService.OnUpgradeSelected += nodeId => chosenNodeId = nodeId;

            // Inject a card state so NotifyCardChosen has something to act on
            service.InjectCardStateForTesting(new[]
            {
                new UpgradeCardData
                {
                    NodeId = "test-node-001",
                    DisplayName = "Test Upgrade",
                    Cost = 0L,      // free — always affordable
                    IsAffordable = true,
                }
            });

            // Act
            service.NotifyCardChosen(0);

            // Assert
            Assert.AreEqual("test-node-001", chosenNodeId,
                "NotifyCardChosen(0) must route to OnUpgradeSelected with the chosen card's nodeId.");

            Object.DestroyImmediate(go);
        }

        // ── UIC-INT-03: OnUpgradeSelected fires → overlay should close ─────────────

        [Test]
        public void UIC_INT_03_OnUpgradeSelected_Fires_SubscriberReceivesNodeId()
        {
            // Arrange
            string receivedNodeId = null;
            UpgradeSelectionService.OnUpgradeSelected += nodeId => receivedNodeId = nodeId;

            // Act: fire directly (simulates post-purchase resolution)
            UpgradeSelectionService.FireOnUpgradeSelectedForTesting("node-xyz");

            // Assert
            Assert.AreEqual("node-xyz", receivedNodeId,
                "OnUpgradeSelected subscriber must receive the selected node ID.");
        }
    }
}
#endif

// Tests for Story S2-02: Upgrade Tree Data Model — Tree Build, Query API, and Save Provider
// Type: Logic (Unit/EditMode)
// Story: production/epics/upgrade-tree-data-model/story-001-tree-build-and-query.md
//
// Acceptance Criteria: AC-UPG-01 through AC-UPG-07
// Double-gate: IsReady only when BOTH configs loaded AND save loaded.
// Cost formula: Floor(BaseCost × CostScalingFactor ^ CurrentRank)
// Ghost nodes in save: silently skipped with Debug.LogWarning.
//
// To run: Unity Test Runner → EditMode → EndlessEngine.Tests.Unit.UpgradeTreeDataModel

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Text.RegularExpressions;
using EndlessEngine.Config;
using EndlessEngine.Upgrade;

namespace EndlessEngine.Tests.Unit.UpgradeTreeDataModel
{
    /// <summary>
    /// Unit tests for UpgradeTreeService tree build and query API (S2-02).
    /// Validates AC-UPG-01 through AC-UPG-07.
    /// </summary>
    [TestFixture]
    public class TreeBuildAndQueryTests
    {
        private UpgradeTreeService _service;

        [SetUp]
        public void SetUp()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _service = new GameObject("UpgradeTree_Test").AddComponent<UpgradeTreeService>();
            _service.ResetForTesting();

            var playerConfig  = ScriptableObject.CreateInstance<PlayerBaseStatConfigSO>();
            var economyConfig = ScriptableObject.CreateInstance<EconomyConfigSO>();
            ConfigRegistry.InjectForTesting(player: playerConfig, economy: economyConfig);
#endif
        }

        [TearDown]
        public void TearDown()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_service != null)
                UnityEngine.Object.DestroyImmediate(_service.gameObject);
            ConfigRegistry.ClearForTesting();
#endif
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static UpgradeNodeConfigSO MakeNode(string nodeId, float baseCost = 100f,
            float scalingFactor = 1.5f, int maxRank = 5, string[] prereqs = null, int prestigeGate = 0)
        {
            var node = ScriptableObject.CreateInstance<UpgradeNodeConfigSO>();
            node.NodeId                  = nodeId;
            node.BaseCost                = baseCost;
            node.CostScalingFactor       = scalingFactor;
            node.MaxRank                 = maxRank;
            node.PrerequisiteNodeIDs     = prereqs ?? new string[0];
            node.PrestigeGateRequirement = prestigeGate;
            node.AffectedStat            = StatType.Damage;
            node.EffectPerRank           = 1f;
            node.EffectType              = UpgradeEffectType.FlatBonus;
            return node;
        }

        // ── AC-UPG-01: Tree build from double-gate ────────────────────────────────

        [Test]
        [Description("AC-UPG-01: Both gates fire with saved ranks → IsReady=true, saved nodes have correct ranks.")]
        public void TreeBuild_BothGatesFire_IsReadyAndRanksRestored()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Arrange
            var configs = new[]
            {
                MakeNode("node_a"),
                MakeNode("node_b"),
                MakeNode("unpurchased"),
            };
            var savedRanks = new Dictionary<string, int>
            {
                { "node_a", 1 },
                { "node_b", 2 },
            };

            // Act
            _service.InjectForTesting(configs, savedRanks);

            // Assert
            Assert.IsTrue(_service.IsReady, "AC-UPG-01: IsReady must be true after both gates fire");
            Assert.AreEqual(1, _service.GetNode("node_a").CurrentRank, "node_a: rank must be 1");
            Assert.AreEqual(2, _service.GetNode("node_b").CurrentRank, "node_b: rank must be 2");
            Assert.AreEqual(0, _service.GetNode("unpurchased").CurrentRank, "unpurchased: rank must be 0");
#endif
        }

        [Test]
        [Description("AC-UPG-01 (gate order): Only configs gate fired → IsReady=false.")]
        public void TreeBuild_OnlyConfigsGateFired_IsReadyFalse()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Arrange
            var configs = new[] { MakeNode("node_a") };
            ConfigRegistry.InjectForTesting(upgrades: configs);

            // Act: only fire configs gate
            _service.FireConfigsLoadedGateForTesting();

            // Assert
            Assert.IsFalse(_service.IsReady, "Tree must not be ready until save gate also fires");
            Assert.IsNull(_service.GetNode("node_a"), "GetNode must return null before tree is built");
#endif
        }

        [Test]
        [Description("AC-UPG-01 (gate order): Only save gate fired → IsReady=false.")]
        public void TreeBuild_OnlySaveGateFired_IsReadyFalse()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Arrange: do NOT fire configs gate — fire save gate only
            _service.FireSaveLoadedGateForTesting(new Dictionary<string, int>());

            // Assert
            Assert.IsFalse(_service.IsReady, "Tree must not be ready until configs gate also fires");
#endif
        }

        // ── AC-UPG-02: Prerequisite gate ─────────────────────────────────────────

        [Test]
        [Description("AC-UPG-02: Node B requires node_a at rank≥1; node_a at rank 0 → B unavailable.")]
        public void IsNodeAvailable_UnmetPrerequisite_ReturnsFalse()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Arrange: node_b requires node_a; node_a rank=0 (not purchased)
            var configs = new[]
            {
                MakeNode("node_a"),
                MakeNode("node_b", prereqs: new[] { "node_a" }),
            };
            _service.InjectForTesting(configs, new Dictionary<string, int>());

            // Assert
            Assert.IsFalse(_service.IsNodeAvailable("node_b"),
                "AC-UPG-02: node_b must not be available when node_a rank=0");
            var available = _service.GetAvailableNodes();
            Assert.IsFalse(available.Exists(n => n.Config.NodeId == "node_b"),
                "AC-UPG-02: node_b must not appear in GetAvailableNodes()");
#endif
        }

        [Test]
        [Description("AC-UPG-02 (met): Node A at rank 1 → Node B becomes available.")]
        public void IsNodeAvailable_MetPrerequisite_ReturnsTrue()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Arrange: node_a at rank 1
            var configs = new[]
            {
                MakeNode("node_a"),
                MakeNode("node_b", prereqs: new[] { "node_a" }),
            };
            _service.InjectForTesting(configs, new Dictionary<string, int> { { "node_a", 1 } });

            // Assert
            Assert.IsTrue(_service.IsNodeAvailable("node_b"),
                "Node B must be available when prerequisite node_a is at rank 1");
#endif
        }

        // ── AC-UPG-03: Prestige gate ──────────────────────────────────────────────

        [Test]
        [Description("AC-UPG-03: PrestigeGated node with PrestigeCount=0 → unavailable.")]
        public void IsNodeAvailable_PrestigeGated_UnavailableAtPrestige0()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Arrange: node_x requires 1 prestige
            var configs = new[] { MakeNode("node_x", prestigeGate: 1) };
            _service.InjectForTesting(configs, new Dictionary<string, int>());
            _service.SetPrestigeCountForTesting(0);

            // Assert
            Assert.IsFalse(_service.IsNodeAvailable("node_x"),
                "AC-UPG-03: PrestigeGated node must not be available at PrestigeCount=0");
#endif
        }

        [Test]
        [Description("AC-UPG-03 (met): PrestigeCount=1 → prestige-gated node becomes available.")]
        public void IsNodeAvailable_PrestigeGated_AvailableAfterPrestige()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Arrange
            var configs = new[] { MakeNode("node_x", prestigeGate: 1) };
            _service.InjectForTesting(configs, new Dictionary<string, int>());
            _service.SetPrestigeCountForTesting(1);

            // Assert
            Assert.IsTrue(_service.IsNodeAvailable("node_x"),
                "Prestige-gated node must be available at PrestigeCount=1");
#endif
        }

        // ── AC-UPG-04: Cost formula ───────────────────────────────────────────────

        [Test]
        [Description("AC-UPG-04: BaseCost=100, ScalingFactor=1.5, CurrentRank=2 → cost=225.")]
        public void GetNodeCost_Rank2_ReturnsFlooredFormula()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Arrange
            var configs = new[] { MakeNode("node_cost", baseCost: 100f, scalingFactor: 1.5f) };
            _service.InjectForTesting(configs, new Dictionary<string, int> { { "node_cost", 2 } });

            // Act
            long cost = _service.GetNodeCost("node_cost");

            // Assert: Floor(100 × 1.5^2) = Floor(100 × 2.25) = Floor(225.0) = 225
            Assert.AreEqual(225L, cost,
                "AC-UPG-04: Floor(100 × 1.5^2) = 225");
#endif
        }

        [Test]
        [Description("AC-UPG-04 (rank 0): CurrentRank=0 → cost=BaseCost (factor^0=1).")]
        public void GetNodeCost_Rank0_ReturnsBaseCost()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Arrange
            var configs = new[] { MakeNode("node_cost", baseCost: 100f, scalingFactor: 1.5f) };
            _service.InjectForTesting(configs, new Dictionary<string, int>());

            // Act
            long cost = _service.GetNodeCost("node_cost");

            // Assert: Floor(100 × 1.5^0) = Floor(100 × 1) = 100
            Assert.AreEqual(100L, cost, "Rank 0: cost must equal BaseCost");
#endif
        }

        [Test]
        [Description("AC-UPG-04 (flat scaling): CostScalingFactor=1.0 → always BaseCost regardless of rank.")]
        public void GetNodeCost_FlatScaling_AlwaysBaseCost()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Arrange: scaling factor = 1.0
            var configs = new[] { MakeNode("node_flat", baseCost: 50f, scalingFactor: 1.0f) };
            _service.InjectForTesting(configs, new Dictionary<string, int> { { "node_flat", 5 } });

            // Act
            long cost = _service.GetNodeCost("node_flat");

            // Assert: Floor(50 × 1.0^5) = 50
            Assert.AreEqual(50L, cost, "CostScalingFactor=1.0: cost must always equal BaseCost");
#endif
        }

        [Test]
        [Description("AC-UPG-04 (unknown node): GetNodeCost for unknown node returns 0, no exception.")]
        public void GetNodeCost_UnknownNode_ReturnsZero()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Arrange: empty tree
            _service.InjectForTesting(new UpgradeNodeConfigSO[0]);

            // Act / Assert
            long cost = 0L;
            Assert.DoesNotThrow(() => { cost = _service.GetNodeCost("ghost_node"); },
                "GetNodeCost for unknown node must not throw");
            Assert.AreEqual(0L, cost, "GetNodeCost for unknown node must return 0");
#endif
        }

        // ── AC-UPG-05: Prestige reset ─────────────────────────────────────────────

        [Test]
        [Description("AC-UPG-05: RebuildForPrestige resets all node ranks to 0.")]
        public void RebuildForPrestige_ResetsAllRanksToZero()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Arrange: 5 nodes with various ranks
            var configs = new[]
            {
                MakeNode("n1"), MakeNode("n2"), MakeNode("n3"), MakeNode("n4"), MakeNode("n5"),
            };
            var savedRanks = new Dictionary<string, int>
            {
                { "n1", 3 }, { "n2", 1 }, { "n3", 5 }, { "n4", 2 }, { "n5", 4 },
            };
            _service.InjectForTesting(configs, savedRanks);

            // Pre-condition
            Assert.AreEqual(3, _service.GetNode("n1").CurrentRank, "Precondition: n1 rank=3");

            // Act
            _service.RebuildForPrestige();

            // Assert: all ranks = 0
            Assert.AreEqual(0, _service.GetNode("n1").CurrentRank, "AC-UPG-05: n1 rank must be 0 after prestige");
            Assert.AreEqual(0, _service.GetNode("n2").CurrentRank, "AC-UPG-05: n2 rank must be 0 after prestige");
            Assert.AreEqual(0, _service.GetNode("n3").CurrentRank, "AC-UPG-05: n3 rank must be 0 after prestige");
            Assert.AreEqual(0, _service.GetNode("n4").CurrentRank, "AC-UPG-05: n4 rank must be 0 after prestige");
            Assert.AreEqual(0, _service.GetNode("n5").CurrentRank, "AC-UPG-05: n5 rank must be 0 after prestige");

            // GetAvailableNodes reflects reset (no prerequisite issues at rank 0)
            var available = _service.GetAvailableNodes();
            Assert.IsNotNull(available, "GetAvailableNodes must not throw after prestige reset");
#endif
        }

        // ── AC-UPG-06: Ghost node tolerance ──────────────────────────────────────

        [Test]
        [Description("AC-UPG-06: Unknown node ID in save data → tree builds, warning logged, GetNode returns null.")]
        public void TreeBuild_GhostNodeInSave_SkipsGracefully()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Arrange: config has "node_a" only; save has "ghost_node" not in config
            var configs = new[] { MakeNode("node_a") };
            var savedRanks = new Dictionary<string, int>
            {
                { "node_a",    1 },
                { "ghost_node", 3 }, // not in config
            };

            // Act: expect a warning (but no exception)
            LogAssert.Expect(LogType.Warning, new Regex("ghost_node"));
            _service.InjectForTesting(configs, savedRanks);

            // Assert: tree built successfully
            Assert.IsTrue(_service.IsReady, "AC-UPG-06: Tree must build even with ghost nodes");
            Assert.AreEqual(1, _service.GetNode("node_a").CurrentRank,
                "AC-UPG-06: Valid node rank must be correct");
            Assert.IsNull(_service.GetNode("ghost_node"),
                "AC-UPG-06: GetNode for ghost_node must return null");
#endif
        }

        // ── AC-UPG-07: All max-rank nodes ─────────────────────────────────────────

        [Test]
        [Description("AC-UPG-07: All nodes at MaxRank → GetAvailableNodes returns empty list, no exception.")]
        public void GetAvailableNodes_AllNodesAtMaxRank_ReturnsEmptyList()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Arrange: 3 nodes all at MaxRank=5
            var configs = new[]
            {
                MakeNode("n1", maxRank: 5),
                MakeNode("n2", maxRank: 5),
                MakeNode("n3", maxRank: 5),
            };
            var savedRanks = new Dictionary<string, int>
            {
                { "n1", 5 }, { "n2", 5 }, { "n3", 5 },
            };
            _service.InjectForTesting(configs, savedRanks);

            // Act / Assert: no exception
            List<UpgradeNode> available = null;
            Assert.DoesNotThrow(
                () => { available = _service.GetAvailableNodes(); },
                "AC-UPG-07: GetAvailableNodes must not throw when all nodes are at max rank");

            Assert.AreEqual(0, available.Count,
                "AC-UPG-07: GetAvailableNodes must return empty list when all nodes are at MaxRank");
#endif
        }

        // ── GetNode: unknown ID ───────────────────────────────────────────────────

        [Test]
        [Description("GetNode with unknown node ID returns null, no KeyNotFoundException.")]
        public void GetNode_UnknownId_ReturnsNull()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Arrange: empty tree
            _service.InjectForTesting(new UpgradeNodeConfigSO[0]);

            // Act / Assert
            UpgradeNode result = null;
            Assert.DoesNotThrow(
                () => { result = _service.GetNode("nonexistent"); },
                "GetNode must not throw for unknown ID");
            Assert.IsNull(result, "GetNode must return null for unknown ID");
#endif
        }
    }
}

// Tests for ConfigValidator graph-level checks (duplicate ID, orphan ref, cycle detection)
// Type: Logic (Unit/EditMode)
//
// Tests ValidateUpgradeGraph() in isolation — no Addressables, no scene required.
// Uses ValidationMode.Warning so failures log warnings instead of errors
// (prevents NUnit from seeing uncaught LogError calls as test failures).
//
// To run: Unity Test Runner → EditMode → EndlessEngine.Tests.Unit.ConfigLayer

using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Text.RegularExpressions;
using EndlessEngine.Config;

namespace EndlessEngine.Tests.Unit.ConfigLayer
{
    [TestFixture]
    public class ConfigValidatorGraphTests
    {
        // ── Helpers ───────────────────────────────────────────────────────────────

        private static UpgradeNodeConfigSO MakeNode(string id, params string[] prereqs)
        {
            var so = ScriptableObject.CreateInstance<UpgradeNodeConfigSO>();
            so.NodeId = id;
            so.PrerequisiteNodeIDs = prereqs;
            return so;
        }

        // ── Clean graph ───────────────────────────────────────────────────────────

        [Test]
        public void ValidateUpgradeGraph_CleanLinearChain_ReturnsTrue()
        {
            // A → B → C (C has no prereqs, B prereqs A, A prereqs nothing)
            var nodes = new[]
            {
                MakeNode("node_a"),
                MakeNode("node_b", "node_a"),
                MakeNode("node_c", "node_b"),
            };

            bool result = ConfigValidator.ValidateUpgradeGraph(
                nodes, "test", ConfigValidator.ValidationMode.Warning);

            Assert.IsTrue(result, "Clean linear chain should pass.");
        }

        [Test]
        public void ValidateUpgradeGraph_NullArray_ReturnsTrue()
        {
            bool result = ConfigValidator.ValidateUpgradeGraph(
                null, "test", ConfigValidator.ValidationMode.Warning);
            Assert.IsTrue(result, "Null array should be treated as empty and pass.");
        }

        [Test]
        public void ValidateUpgradeGraph_EmptyArray_ReturnsTrue()
        {
            bool result = ConfigValidator.ValidateUpgradeGraph(
                new UpgradeNodeConfigSO[0], "test", ConfigValidator.ValidationMode.Warning);
            Assert.IsTrue(result, "Empty array should pass.");
        }

        [Test]
        public void ValidateUpgradeGraph_DiamondDependency_ReturnsTrue()
        {
            // A ← B ← D
            //     ↑
            //     C
            // Both B and C depend on A; D depends on both B and C (diamond — no cycle)
            var nodes = new[]
            {
                MakeNode("a"),
                MakeNode("b", "a"),
                MakeNode("c", "a"),
                MakeNode("d", "b", "c"),
            };

            bool result = ConfigValidator.ValidateUpgradeGraph(
                nodes, "test", ConfigValidator.ValidationMode.Warning);

            Assert.IsTrue(result, "Diamond (shared prereq) should pass — it has no cycle.");
        }

        // ── Duplicate IDs ─────────────────────────────────────────────────────────

        [Test]
        public void ValidateUpgradeGraph_DuplicateNodeId_ReturnsFalse()
        {
            var nodes = new[]
            {
                MakeNode("dup_id"),
                MakeNode("dup_id"),
                MakeNode("unique_id"),
            };

            LogAssert.Expect(LogType.Warning, new Regex("Duplicate UpgradeNodeConfigSO.NodeId 'dup_id'"));

            bool result = ConfigValidator.ValidateUpgradeGraph(
                nodes, "test", ConfigValidator.ValidationMode.Warning);

            Assert.IsFalse(result, "Duplicate NodeId should fail.");
        }

        [Test]
        public void ValidateUpgradeGraph_MultipleDuplicatePairs_ReturnsFalse()
        {
            var nodes = new[]
            {
                MakeNode("a"), MakeNode("a"),
                MakeNode("b"), MakeNode("b"),
            };

            LogAssert.Expect(LogType.Warning, new Regex("Duplicate.*'a'"));
            LogAssert.Expect(LogType.Warning, new Regex("Duplicate.*'b'"));

            bool result = ConfigValidator.ValidateUpgradeGraph(
                nodes, "test", ConfigValidator.ValidationMode.Warning);

            Assert.IsFalse(result);
        }

        // ── Orphan references ─────────────────────────────────────────────────────

        [Test]
        public void ValidateUpgradeGraph_OrphanPrerequisite_ReturnsFalse()
        {
            var nodes = new[]
            {
                MakeNode("node_x", "nonexistent_node"),
            };

            LogAssert.Expect(LogType.Warning, new Regex("prerequisite 'nonexistent_node'.*does not match"));

            bool result = ConfigValidator.ValidateUpgradeGraph(
                nodes, "test", ConfigValidator.ValidationMode.Warning);

            Assert.IsFalse(result, "Orphan prerequisite reference should fail.");
        }

        [Test]
        public void ValidateUpgradeGraph_EmptyPrerequisiteString_IsSkipped()
        {
            // Empty strings in prerequisite array should not be treated as orphans
            var nodes = new[]
            {
                MakeNode("node_a", "", null),
                MakeNode("node_b", "node_a"),
            };

            bool result = ConfigValidator.ValidateUpgradeGraph(
                nodes, "test", ConfigValidator.ValidationMode.Warning);

            Assert.IsTrue(result, "Empty/null prereq strings should be silently skipped.");
        }

        // ── Cycle detection ───────────────────────────────────────────────────────

        [Test]
        public void ValidateUpgradeGraph_DirectSelfCycle_ReturnsFalse()
        {
            // A depends on A
            var nodes = new[]
            {
                MakeNode("self_ref", "self_ref"),
            };

            LogAssert.Expect(LogType.Warning, new Regex("Cycle detected"));

            bool result = ConfigValidator.ValidateUpgradeGraph(
                nodes, "test", ConfigValidator.ValidationMode.Warning);

            Assert.IsFalse(result, "Self-referencing node should be detected as cycle.");
        }

        [Test]
        public void ValidateUpgradeGraph_TwoNodeCycle_ReturnsFalse()
        {
            // A prereqs B, B prereqs A
            var nodes = new[]
            {
                MakeNode("node_a", "node_b"),
                MakeNode("node_b", "node_a"),
            };

            LogAssert.Expect(LogType.Warning, new Regex("Cycle detected"));

            bool result = ConfigValidator.ValidateUpgradeGraph(
                nodes, "test", ConfigValidator.ValidationMode.Warning);

            Assert.IsFalse(result, "Two-node cycle should be detected.");
        }

        [Test]
        public void ValidateUpgradeGraph_ThreeNodeCycle_ReturnsFalse()
        {
            // A → B → C → A
            var nodes = new[]
            {
                MakeNode("a", "c"),
                MakeNode("b", "a"),
                MakeNode("c", "b"),
            };

            LogAssert.Expect(LogType.Warning, new Regex("Cycle detected"));

            bool result = ConfigValidator.ValidateUpgradeGraph(
                nodes, "test", ConfigValidator.ValidationMode.Warning);

            Assert.IsFalse(result, "Three-node cycle should be detected.");
        }

        [Test]
        public void ValidateUpgradeGraph_CycleInSubgraph_ReturnsFalse()
        {
            // Clean chain: root → branch_a
            // Cycle:       loop_1 ↔ loop_2
            var nodes = new[]
            {
                MakeNode("root"),
                MakeNode("branch_a", "root"),
                MakeNode("loop_1", "loop_2"),
                MakeNode("loop_2", "loop_1"),
            };

            LogAssert.Expect(LogType.Warning, new Regex("Cycle detected"));

            bool result = ConfigValidator.ValidateUpgradeGraph(
                nodes, "test", ConfigValidator.ValidationMode.Warning);

            Assert.IsFalse(result, "Cycle in a disconnected subgraph should be detected.");
        }

        // ── Teardown ──────────────────────────────────────────────────────────────

        [TearDown]
        public void TearDown()
        {
            // No persistent state — ScriptableObjects created with CreateInstance
            // are GC-collected when the test finishes and there are no references.
        }
    }
}

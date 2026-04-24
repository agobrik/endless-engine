// Tests for Sprint 12 — S12-05: SkillTreeService
// Type: Logic (Unit/EditMode)
//
// Covers:
//   - Unlock success (enough points, no prerequisites)
//   - Unlock fails when already unlocked
//   - Unlock fails with insufficient points
//   - Unlock fails when prerequisite not met
//   - Unlock fails when treeId or nodeId not found
//   - Refund success (single node)
//   - Refund fails if not unlocked
//   - Refund fails if node is not refundable
//   - Refund fails when dependents exist
//   - AddPoints fires OnSkillPointsChanged
//   - GetAllActiveEffects returns effects of all unlocked nodes
//   - Save / load round-trip (OnBeforeSave / OnAfterLoad)
//
// To run: Unity Test Runner → EditMode → EndlessEngine.Tests.Unit.SkillTree

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using EndlessEngine.Config;
using EndlessEngine.Upgrade;
using EndlessEngine.SaveAndLoad;

namespace EndlessEngine.Tests.Unit.SkillTree
{
    [TestFixture]
    public class SkillTreeServiceTests
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private SkillTreeService    _service;
        private SkillTreeConfigSO   _tree;

        private SkillNodeConfigSO _rootNode;
        private SkillNodeConfigSO _childNode;
        private SkillNodeConfigSO _lockedNode;    // non-refundable
        private SkillNodeConfigSO _deepNode;      // requires childNode

        private readonly List<(string, string)> _unlockEvents = new List<(string, string)>();
        private readonly List<(string, string)> _refundEvents = new List<(string, string)>();
        private readonly List<int>              _pointEvents  = new List<int>();

        private const string TreeId = "combat";

        [SetUp]
        public void SetUp()
        {
            // Nodes
            _rootNode = ScriptableObject.CreateInstance<SkillNodeConfigSO>();
            _rootNode.NodeId     = "root";
            _rootNode.PointCost  = 1;
            _rootNode.Refundable = true;
            _rootNode.PrerequisiteIds = new List<string>();
            _rootNode.Effects = new List<SkillEffect>
            {
                new SkillEffect { Type = SkillEffectType.StatMultiplier, TargetId = "damage", Value = 1.1f }
            };

            _childNode = ScriptableObject.CreateInstance<SkillNodeConfigSO>();
            _childNode.NodeId     = "child";
            _childNode.PointCost  = 2;
            _childNode.Refundable = true;
            _childNode.PrerequisiteIds = new List<string> { "root" };
            _childNode.Effects = new List<SkillEffect>
            {
                new SkillEffect { Type = SkillEffectType.StatAdditive, TargetId = "armor", Value = 5f }
            };

            _lockedNode = ScriptableObject.CreateInstance<SkillNodeConfigSO>();
            _lockedNode.NodeId     = "locked";
            _lockedNode.PointCost  = 1;
            _lockedNode.Refundable = false;
            _lockedNode.PrerequisiteIds = new List<string>();
            _lockedNode.Effects = new List<SkillEffect>();

            _deepNode = ScriptableObject.CreateInstance<SkillNodeConfigSO>();
            _deepNode.NodeId     = "deep";
            _deepNode.PointCost  = 1;
            _deepNode.Refundable = true;
            _deepNode.PrerequisiteIds = new List<string> { "child" };
            _deepNode.Effects = new List<SkillEffect>();

            // Tree
            _tree = ScriptableObject.CreateInstance<SkillTreeConfigSO>();
            _tree.TreeId = TreeId;
            _tree.Nodes  = new[] { _rootNode, _childNode, _lockedNode, _deepNode };

            // Service (new GO in editor — not registered in scene)
            var go = new GameObject("SkillTreeService");
            _service = go.AddComponent<SkillTreeService>();
            _service.Initialize(new[] { _tree }, startingPoints: 0);

            SkillTreeService.ClearSubscribersForTesting();
            SkillTreeService.OnNodeUnlocked      += (t, n) => _unlockEvents.Add((t, n));
            SkillTreeService.OnNodeRefunded      += (t, n) => _refundEvents.Add((t, n));
            SkillTreeService.OnSkillPointsChanged += p => _pointEvents.Add(p);

            _unlockEvents.Clear();
            _refundEvents.Clear();
            _pointEvents.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            SkillTreeService.ClearSubscribersForTesting();
            if (_service != null) Object.DestroyImmediate(_service.gameObject);
            if (_tree       != null) Object.DestroyImmediate(_tree);
            if (_rootNode   != null) Object.DestroyImmediate(_rootNode);
            if (_childNode  != null) Object.DestroyImmediate(_childNode);
            if (_lockedNode != null) Object.DestroyImmediate(_lockedNode);
            if (_deepNode   != null) Object.DestroyImmediate(_deepNode);
        }

        // ── Unlock ────────────────────────────────────────────────────────────────

        [Test]
        public void TryUnlock_WithPoints_NoPrereq_Succeeds()
        {
            _service.AddPoints(5);
            bool result = _service.TryUnlock(TreeId, "root");

            Assert.IsTrue(result);
            Assert.IsTrue(_service.IsUnlocked(TreeId, "root"));
            Assert.AreEqual(4, _service.SkillPoints);
        }

        [Test]
        public void TryUnlock_FiresOnNodeUnlocked()
        {
            _service.AddPoints(5);
            _service.TryUnlock(TreeId, "root");

            Assert.AreEqual(1, _unlockEvents.Count);
            Assert.AreEqual((TreeId, "root"), _unlockEvents[0]);
        }

        [Test]
        public void TryUnlock_AlreadyUnlocked_ReturnsFalse()
        {
            _service.AddPoints(5);
            _service.TryUnlock(TreeId, "root");
            bool result = _service.TryUnlock(TreeId, "root");

            Assert.IsFalse(result);
            Assert.AreEqual(1, _unlockEvents.Count, "OnNodeUnlocked must not fire twice");
        }

        [Test]
        public void TryUnlock_InsufficientPoints_ReturnsFalse()
        {
            // 0 points
            bool result = _service.TryUnlock(TreeId, "root");
            Assert.IsFalse(result);
            Assert.IsFalse(_service.IsUnlocked(TreeId, "root"));
        }

        [Test]
        public void TryUnlock_PrerequisiteNotMet_ReturnsFalse()
        {
            _service.AddPoints(10);
            bool result = _service.TryUnlock(TreeId, "child"); // root not unlocked

            Assert.IsFalse(result);
            Assert.IsFalse(_service.IsUnlocked(TreeId, "child"));
        }

        [Test]
        public void TryUnlock_AfterPrerequisiteMet_Succeeds()
        {
            _service.AddPoints(10);
            _service.TryUnlock(TreeId, "root");
            bool result = _service.TryUnlock(TreeId, "child");

            Assert.IsTrue(result);
            Assert.IsTrue(_service.IsUnlocked(TreeId, "child"));
        }

        [Test]
        public void TryUnlock_UnknownTree_ReturnsFalse()
        {
            _service.AddPoints(5);
            bool result = _service.TryUnlock("nonexistent_tree", "root");
            Assert.IsFalse(result);
        }

        [Test]
        public void TryUnlock_UnknownNode_ReturnsFalse()
        {
            _service.AddPoints(5);
            bool result = _service.TryUnlock(TreeId, "nonexistent_node");
            Assert.IsFalse(result);
        }

        // ── Refund ────────────────────────────────────────────────────────────────

        [Test]
        public void TryRefund_UnlockedRefundableNode_Succeeds()
        {
            _service.AddPoints(5);
            _service.TryUnlock(TreeId, "root");
            int pointsBefore = _service.SkillPoints;

            bool result = _service.TryRefund(TreeId, "root");

            Assert.IsTrue(result);
            Assert.IsFalse(_service.IsUnlocked(TreeId, "root"));
            Assert.AreEqual(pointsBefore + _rootNode.PointCost, _service.SkillPoints,
                "Refund must return the node's PointCost");
        }

        [Test]
        public void TryRefund_FiresOnNodeRefunded()
        {
            _service.AddPoints(5);
            _service.TryUnlock(TreeId, "root");
            _refundEvents.Clear();

            _service.TryRefund(TreeId, "root");
            Assert.AreEqual(1, _refundEvents.Count);
            Assert.AreEqual((TreeId, "root"), _refundEvents[0]);
        }

        [Test]
        public void TryRefund_NotUnlocked_ReturnsFalse()
        {
            bool result = _service.TryRefund(TreeId, "root");
            Assert.IsFalse(result);
        }

        [Test]
        public void TryRefund_NonRefundableNode_ReturnsFalse()
        {
            _service.AddPoints(5);
            _service.TryUnlock(TreeId, "locked");

            bool result = _service.TryRefund(TreeId, "locked");
            Assert.IsFalse(result);
            Assert.IsTrue(_service.IsUnlocked(TreeId, "locked"), "Node must remain unlocked");
        }

        [Test]
        public void TryRefund_WithDependentUnlocked_ReturnsFalse()
        {
            _service.AddPoints(10);
            _service.TryUnlock(TreeId, "root");
            _service.TryUnlock(TreeId, "child"); // child depends on root

            bool result = _service.TryRefund(TreeId, "root");
            Assert.IsFalse(result, "Refund must fail when a dependent node is still unlocked");
        }

        // ── Points ────────────────────────────────────────────────────────────────

        [Test]
        public void AddPoints_FiresOnSkillPointsChanged()
        {
            _service.AddPoints(3);
            Assert.AreEqual(1, _pointEvents.Count);
            Assert.AreEqual(3, _pointEvents[0]);
        }

        [Test]
        public void AddPoints_ZeroOrNegative_NoChange()
        {
            _service.AddPoints(0);
            _service.AddPoints(-5);
            Assert.AreEqual(0, _pointEvents.Count, "No event for 0 or negative");
            Assert.AreEqual(0, _service.SkillPoints);
        }

        // ── GetAllActiveEffects ───────────────────────────────────────────────────

        [Test]
        public void GetAllActiveEffects_ReturnsEffectsForUnlockedNodes()
        {
            _service.AddPoints(10);
            _service.TryUnlock(TreeId, "root");
            _service.TryUnlock(TreeId, "child");

            var effects = _service.GetAllActiveEffects();
            Assert.AreEqual(2, effects.Count);

            bool hasDamage = false, hasArmor = false;
            foreach (var e in effects)
            {
                if (e.TargetId == "damage") hasDamage = true;
                if (e.TargetId == "armor")  hasArmor  = true;
            }
            Assert.IsTrue(hasDamage, "Root node damage effect must be present");
            Assert.IsTrue(hasArmor,  "Child node armor effect must be present");
        }

        [Test]
        public void GetAllActiveEffects_EmptyWhenNothingUnlocked()
        {
            var effects = _service.GetAllActiveEffects();
            Assert.IsEmpty(effects);
        }

        // ── Save / Load ───────────────────────────────────────────────────────────

        [Test]
        public void SaveLoad_RoundTrip_RestoresUnlockedNodesAndPoints()
        {
            _service.AddPoints(10);
            _service.TryUnlock(TreeId, "root");
            _service.TryUnlock(TreeId, "child");

            var saveData = new SaveData();
            saveData.EnsureDefaults();
            _service.OnBeforeSave(saveData);

            // New service instance (simulates fresh load)
            var go2     = new GameObject("SkillTreeService2");
            var service2 = go2.AddComponent<SkillTreeService>();
            service2.Initialize(new[] { _tree });
            service2.OnAfterLoad(saveData);

            Assert.IsTrue(service2.IsUnlocked(TreeId, "root"));
            Assert.IsTrue(service2.IsUnlocked(TreeId, "child"));
            Assert.AreEqual(_service.SkillPoints, service2.SkillPoints);

            Object.DestroyImmediate(go2);
        }
#endif
    }
}

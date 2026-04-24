// Tests for Sprint 14 — S14-05: ResearchService
// Type: Logic (Unit/EditMode)
//
// Covers:
//   - TryEnqueue success (no prereqs, enough gold)
//   - TryEnqueue fails: tree not found, node not found
//   - TryEnqueue fails: prerequisite not met
//   - TryEnqueue fails: already completed
//   - TryEnqueue fails: already queued
//   - TryEnqueue fails: insufficient gold
//   - TryEnqueue deducts gold
//   - TryDequeue removes from queue (non-head), refunds gold
//   - TryDequeue returns false for head-of-queue (active)
//   - OnTick advances progress, fires OnResearchProgress
//   - OnTick completes node when ticks >= ResearchTicks
//   - OnNodeCompleted fires after completion
//   - Queue ordering is FIFO
//   - Save / load round-trip
//
// To run: Unity Test Runner → EditMode → EndlessEngine.Tests.Unit.ChallengeResearch

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using EndlessEngine.Config;
using EndlessEngine.Economy;
using EndlessEngine.Research;
using EndlessEngine.SaveAndLoad;

namespace EndlessEngine.Tests.Unit.ChallengeResearch
{
    [TestFixture]
    public class ResearchServiceTests
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private ResearchService      _service;
        private EconomyService       _economy;
        private ResearchTreeConfigSO _tree;

        private ResearchNodeConfigSO _nodeA;   // tier 0, no prereqs
        private ResearchNodeConfigSO _nodeB;   // tier 1, requires nodeA
        private ResearchNodeConfigSO _nodeC;   // tier 0, no prereqs, fast (1 tick)

        private readonly List<(string, string)>           _queuedEvents    = new List<(string, string)>();
        private readonly List<(string, string, int, int)> _progressEvents  = new List<(string, string, int, int)>();
        private readonly List<(string, string)>           _completedEvents = new List<(string, string)>();
        private readonly List<(string, string, string)>   _failedEvents    = new List<(string, string, string)>();

        private const string TreeId = "tech";
        private EndlessEngine.Config.EconomyConfigSO _econConfig;

        [SetUp]
        public void SetUp()
        {
            _econConfig = ScriptableObject.CreateInstance<EndlessEngine.Config.EconomyConfigSO>();
            _econConfig.ResourceHardCap = 1_000_000L;
            _econConfig.StartingGold    = 0L;
            EndlessEngine.Config.ConfigRegistry.InjectForTesting(economy: _econConfig);

            ResearchService.ClearSubscribersForTesting();
            ResearchService.OnNodeQueued       += (t, n) => _queuedEvents.Add((t, n));
            ResearchService.OnResearchProgress += (t, n, d, total) => _progressEvents.Add((t, n, d, total));
            ResearchService.OnNodeCompleted    += (t, n) => _completedEvents.Add((t, n));
            ResearchService.OnEnqueueFailed    += (t, n, r) => _failedEvents.Add((t, n, r));
            _queuedEvents.Clear(); _progressEvents.Clear(); _completedEvents.Clear(); _failedEvents.Clear();

            // Economy
            var ecoGo = new GameObject("Economy");
            _economy  = ecoGo.AddComponent<EconomyService>();
            _economy.Initialize(null, new GameObject("Save").AddComponent<SaveService>());
            var sd = new SaveData(); sd.EnsureDefaults(); sd.CurrentResources = 10000;
            _economy.OnAfterLoad(sd);

            // Nodes
            _nodeA = ScriptableObject.CreateInstance<ResearchNodeConfigSO>();
            _nodeA.NodeId        = "node_a";
            _nodeA.Tier          = 0;
            _nodeA.GoldCost      = 100;
            _nodeA.ResearchTicks = 5;
            _nodeA.PrerequisiteIds = new List<string>();

            _nodeB = ScriptableObject.CreateInstance<ResearchNodeConfigSO>();
            _nodeB.NodeId        = "node_b";
            _nodeB.Tier          = 1;
            _nodeB.GoldCost      = 200;
            _nodeB.ResearchTicks = 3;
            _nodeB.PrerequisiteIds = new List<string> { "node_a" };

            _nodeC = ScriptableObject.CreateInstance<ResearchNodeConfigSO>();
            _nodeC.NodeId        = "node_c";
            _nodeC.Tier          = 0;
            _nodeC.GoldCost      = 50;
            _nodeC.ResearchTicks = 1;
            _nodeC.PrerequisiteIds = new List<string>();

            _tree = ScriptableObject.CreateInstance<ResearchTreeConfigSO>();
            _tree.TreeId = TreeId;
            _tree.Nodes  = new[] { _nodeA, _nodeB, _nodeC };

            var go    = new GameObject("ResearchService");
            _service  = go.AddComponent<ResearchService>();
            _service.Initialize(new[] { _tree }, _economy);
        }

        [TearDown]
        public void TearDown()
        {
            ResearchService.ClearSubscribersForTesting();
            if (_service    != null) Object.DestroyImmediate(_service.gameObject);
            if (_economy    != null) Object.DestroyImmediate(_economy.gameObject);
            if (_tree       != null) Object.DestroyImmediate(_tree);
            if (_nodeA      != null) Object.DestroyImmediate(_nodeA);
            if (_nodeB      != null) Object.DestroyImmediate(_nodeB);
            if (_nodeC      != null) Object.DestroyImmediate(_nodeC);
            if (_econConfig != null) Object.DestroyImmediate(_econConfig);
            EndlessEngine.Config.ConfigRegistry.ClearForTesting();
        }

        // ── TryEnqueue ────────────────────────────────────────────────────────────

        [Test]
        public void TryEnqueue_NoPrereqs_Succeeds()
        {
            bool result = _service.TryEnqueue(TreeId, "node_a");
            Assert.IsTrue(result);
            Assert.AreEqual(1, _service.QueueCount);
        }

        [Test]
        public void TryEnqueue_DeductsGold()
        {
            long goldBefore = _economy.CurrentResources;
            _service.TryEnqueue(TreeId, "node_a");
            Assert.AreEqual(goldBefore - _nodeA.GoldCost, _economy.CurrentResources);
        }

        [Test]
        public void TryEnqueue_FiresOnNodeQueued()
        {
            _service.TryEnqueue(TreeId, "node_a");
            Assert.AreEqual(1, _queuedEvents.Count);
            Assert.AreEqual((TreeId, "node_a"), _queuedEvents[0]);
        }

        [Test]
        public void TryEnqueue_UnknownTree_Fails()
        {
            bool result = _service.TryEnqueue("bad_tree", "node_a");
            Assert.IsFalse(result);
            Assert.AreEqual(1, _failedEvents.Count);
            Assert.AreEqual("TreeNotFound", _failedEvents[0].Item3);
        }

        [Test]
        public void TryEnqueue_UnknownNode_Fails()
        {
            bool result = _service.TryEnqueue(TreeId, "nonexistent");
            Assert.IsFalse(result);
            Assert.AreEqual("NodeNotFound", _failedEvents[0].Item3);
        }

        [Test]
        public void TryEnqueue_PrerequisiteNotMet_Fails()
        {
            bool result = _service.TryEnqueue(TreeId, "node_b"); // requires node_a
            Assert.IsFalse(result);
            Assert.AreEqual("PrerequisiteNotMet", _failedEvents[0].Item3);
        }

        [Test]
        public void TryEnqueue_AlreadyQueued_Fails()
        {
            _service.TryEnqueue(TreeId, "node_a");
            bool result = _service.TryEnqueue(TreeId, "node_a");
            Assert.IsFalse(result);
            Assert.AreEqual("AlreadyQueued", _failedEvents[0].Item3);
        }

        [Test]
        public void TryEnqueue_AlreadyCompleted_Fails()
        {
            _service.InjectCompletedForTesting(TreeId, "node_a");
            bool result = _service.TryEnqueue(TreeId, "node_a");
            Assert.IsFalse(result);
            Assert.AreEqual("AlreadyCompleted", _failedEvents[0].Item3);
        }

        [Test]
        public void TryEnqueue_InsufficientGold_Fails()
        {
            _economy.DeductResources(9990); // only 10 gold left, node costs 100
            bool result = _service.TryEnqueue(TreeId, "node_a");
            Assert.IsFalse(result);
            Assert.AreEqual("InsufficientGold", _failedEvents[0].Item3);
        }

        // ── TryDequeue ────────────────────────────────────────────────────────────

        [Test]
        public void TryDequeue_NonHead_RemovesAndRefunds()
        {
            _service.TryEnqueue(TreeId, "node_a"); // head
            _service.TryEnqueue(TreeId, "node_c"); // index 1

            long goldBefore = _economy.CurrentResources;
            bool result = _service.TryDequeue(TreeId, "node_c");

            Assert.IsTrue(result);
            Assert.AreEqual(1, _service.QueueCount);
            Assert.AreEqual(goldBefore + _nodeC.GoldCost, _economy.CurrentResources);
        }

        [Test]
        public void TryDequeue_HeadNode_ReturnsFalse()
        {
            _service.TryEnqueue(TreeId, "node_a");
            bool result = _service.TryDequeue(TreeId, "node_a");
            Assert.IsFalse(result, "Cannot cancel head (active) research");
        }

        // ── OnTick ────────────────────────────────────────────────────────────────

        [Test]
        public void OnTick_AdvancesProgress_FiresProgressEvent()
        {
            _service.TryEnqueue(TreeId, "node_a"); // 5 ticks
            _service.OnTick(1f);

            Assert.AreEqual(1, _progressEvents.Count);
            Assert.AreEqual(1, _progressEvents[0].Item3, "1 tick done");
            Assert.AreEqual(5, _progressEvents[0].Item4, "5 ticks total");
        }

        [Test]
        public void OnTick_CompletesNode_WhenTicksReached()
        {
            _service.TryEnqueue(TreeId, "node_c"); // 1 tick
            _service.OnTick(1f);

            Assert.AreEqual(1, _completedEvents.Count);
            Assert.AreEqual((TreeId, "node_c"), _completedEvents[0]);
            Assert.IsTrue(_service.IsCompleted(TreeId, "node_c"));
            Assert.AreEqual(0, _service.QueueCount);
        }

        [Test]
        public void OnTick_NodeCompletion_AllowsNextNodeToStart()
        {
            _service.TryEnqueue(TreeId, "node_c"); // 1 tick — head
            _service.TryEnqueue(TreeId, "node_a"); // 5 ticks — second

            _service.OnTick(1f); // completes node_c
            Assert.AreEqual(1, _service.QueueCount);
            Assert.IsTrue(_service.IsQueued(TreeId, "node_a"));
        }

        // ── Queue ordering ────────────────────────────────────────────────────────

        [Test]
        public void Queue_IsFIFO()
        {
            _service.TryEnqueue(TreeId, "node_a");
            _service.TryEnqueue(TreeId, "node_c");

            Assert.AreEqual($"{TreeId}:node_a", _service.Queue[0]);
            Assert.AreEqual($"{TreeId}:node_c", _service.Queue[1]);
        }

        // ── GetActiveProgress ─────────────────────────────────────────────────────

        [Test]
        public void GetActiveProgress_ReturnsZeroWhenIdle()
        {
            var (done, total) = _service.GetActiveProgress();
            Assert.AreEqual(0, done);
            Assert.AreEqual(0, total);
        }

        [Test]
        public void GetActiveProgress_ReflectsTicksDone()
        {
            _service.TryEnqueue(TreeId, "node_a"); // 5 ticks
            _service.InjectActiveTicksForTesting(3);

            var (done, total) = _service.GetActiveProgress();
            Assert.AreEqual(3, done);
            Assert.AreEqual(5, total);
        }

        // ── Save / Load ───────────────────────────────────────────────────────────

        [Test]
        public void SaveLoad_RoundTrip_RestoresQueueAndCompleted()
        {
            _service.InjectCompletedForTesting(TreeId, "node_c");
            _service.TryEnqueue(TreeId, "node_a");
            _service.InjectActiveTicksForTesting(2);

            var saveData = new SaveData();
            saveData.EnsureDefaults();
            _service.OnBeforeSave(saveData);

            var go2      = new GameObject("ResearchService2");
            var service2 = go2.AddComponent<ResearchService>();
            service2.Initialize(new[] { _tree }, _economy);
            service2.OnAfterLoad(saveData);

            Assert.IsTrue(service2.IsCompleted(TreeId, "node_c"));
            Assert.AreEqual(1, service2.QueueCount);
            Assert.AreEqual(2, service2.GetActiveProgress().done);

            Object.DestroyImmediate(go2);
        }
#endif
    }
}

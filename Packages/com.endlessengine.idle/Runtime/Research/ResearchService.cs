using System;
using System.Collections.Generic;
using UnityEngine;
using EndlessEngine.Config;
using EndlessEngine.Economy;
using EndlessEngine.Flow;
using EndlessEngine.Stats;
using EndlessEngine.SaveAndLoad;

namespace EndlessEngine.Research
{
    /// <summary>
    /// Manages the research queue and tick-driven progress.
    ///
    /// Architecture:
    ///   - Multiple ResearchTreeConfigSO instances supported (one per tree).
    ///   - Node keys are "treeId:nodeId" composite strings (same pattern as SkillTreeService).
    ///   - Queue is FIFO; the head node receives ticks each TickEngine.OnTick.
    ///   - When a node completes it is removed from the queue and added to _completed.
    ///
    /// Persists via ISaveStateProvider (order = 90).
    ///
    /// Bootstrap wiring:
    ///   researchService.Initialize(treeSOs, economyService, currencyService);
    ///   TickEngine.OnTick += researchService.OnTick;   (done in bootstrap)
    ///   saveService.RegisterStateProvider(researchService);
    /// </summary>
    public class ResearchService : MonoBehaviour, ISaveStateProvider, IModifierSource
    {
        // ── ISaveStateProvider ────────────────────────────────────────────────────

        public int ProviderOrder => SaveConstants.SaveProviderOrder.Research;

        // ── Events ────────────────────────────────────────────────────────────────

        /// <summary>Fires when a node is enqueued. Parameters: treeId, nodeId.</summary>
        public static event Action<string, string> OnNodeQueued;

        /// <summary>Fires each tick while research is in progress. Parameters: treeId, nodeId, ticksDone, ticksTotal.</summary>
        public static event Action<string, string, int, int> OnResearchProgress;

        /// <summary>Fires when a node completes. Parameters: treeId, nodeId.</summary>
        public static event Action<string, string> OnNodeCompleted;

        /// <summary>Fires when enqueue fails. Parameters: treeId, nodeId, reason.</summary>
        public static event Action<string, string, string> OnEnqueueFailed;

        // ── Runtime state ─────────────────────────────────────────────────────────

        private readonly Dictionary<string, ResearchTreeConfigSO>  _trees     = new Dictionary<string, ResearchTreeConfigSO>();
        private readonly HashSet<string>                           _completed = new HashSet<string>();
        private readonly List<string>                              _queue     = new List<string>(); // ordered "treeId:nodeId" keys

        private int _activeTicks; // ticks spent on queue[0]

        private EconomyService  _economyService;
        private object          _currencyService; // optional — typed as object to avoid hard dep

        // ── Initialization ────────────────────────────────────────────────────────

        public void Initialize(ResearchTreeConfigSO[] trees, EconomyService economyService, MonoBehaviour currencyService = null)
        {
            _trees.Clear();
            if (trees != null)
                foreach (var t in trees)
                    if (t != null && !string.IsNullOrEmpty(t.TreeId))
                        _trees[t.TreeId] = t;

            _economyService  = economyService;
            _currencyService = currencyService;
        }

        // ── TickEngine hook ───────────────────────────────────────────────────────

        /// <summary>Called by TickEngine.OnTick. Advances the head-of-queue by dt-ticks (1 per second).</summary>
        public void OnTick(float dt)
        {
            if (_queue.Count == 0) return;

            string key = _queue[0];
            if (!TryParseKey(key, out var treeId, out var nodeId)) { _queue.RemoveAt(0); return; }
            if (!_trees.TryGetValue(treeId, out var tree)) { _queue.RemoveAt(0); return; }
            var node = tree.GetNode(nodeId);
            if (node == null) { _queue.RemoveAt(0); return; }

            _activeTicks++;
            OnResearchProgress?.Invoke(treeId, nodeId, _activeTicks, node.ResearchTicks);

            if (_activeTicks >= node.ResearchTicks)
                CompleteHead(treeId, nodeId, node);
        }

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>Returns true if the given node is completed.</summary>
        public bool IsCompleted(string treeId, string nodeId) =>
            _completed.Contains(MakeKey(treeId, nodeId));

        /// <summary>Returns true if the given node is currently in the queue.</summary>
        public bool IsQueued(string treeId, string nodeId) =>
            _queue.Contains(MakeKey(treeId, nodeId));

        /// <summary>Current queue length (includes the active node at index 0).</summary>
        public int QueueCount => _queue.Count;

        /// <summary>
        /// Attempts to enqueue a research node.
        /// Checks: tree/node exists, prerequisites met, not already completed, not already queued, gold cost.
        /// Returns true on success.
        /// </summary>
        public bool TryEnqueue(string treeId, string nodeId)
        {
            string key = MakeKey(treeId, nodeId);

            if (_completed.Contains(key))
            { OnEnqueueFailed?.Invoke(treeId, nodeId, "AlreadyCompleted"); return false; }

            if (_queue.Contains(key))
            { OnEnqueueFailed?.Invoke(treeId, nodeId, "AlreadyQueued"); return false; }

            if (!_trees.TryGetValue(treeId, out var tree))
            { OnEnqueueFailed?.Invoke(treeId, nodeId, "TreeNotFound"); return false; }

            var node = tree.GetNode(nodeId);
            if (node == null)
            { OnEnqueueFailed?.Invoke(treeId, nodeId, "NodeNotFound"); return false; }

            // Prerequisites
            foreach (var prereqId in node.PrerequisiteIds)
            {
                if (!_completed.Contains(MakeKey(treeId, prereqId)))
                { OnEnqueueFailed?.Invoke(treeId, nodeId, "PrerequisiteNotMet"); return false; }
            }

            // Gold cost
            if (node.GoldCost > 0)
            {
                if (_economyService == null || _economyService.CurrentResources < node.GoldCost)
                { OnEnqueueFailed?.Invoke(treeId, nodeId, "InsufficientGold"); return false; }
                _economyService.DeductResources(node.GoldCost);
            }

            _queue.Add(key);
            OnNodeQueued?.Invoke(treeId, nodeId);
            return true;
        }

        /// <summary>
        /// Removes the node from the queue and refunds its gold cost.
        /// Returns false if the node is the active (head) node — cannot cancel in-progress research.
        /// </summary>
        public bool TryDequeue(string treeId, string nodeId)
        {
            string key = MakeKey(treeId, nodeId);
            int idx = _queue.IndexOf(key);
            if (idx < 0) return false;
            if (idx == 0) return false; // active research cannot be cancelled

            if (!_trees.TryGetValue(treeId, out var tree)) return false;
            var node = tree.GetNode(nodeId);
            if (node == null) return false;

            _queue.RemoveAt(idx);
            if (node.GoldCost > 0) _economyService?.AddResources(node.GoldCost);
            return true;
        }

        /// <summary>Returns ticks done / ticks total for the active node, or (0, 0) if idle.</summary>
        public (int done, int total) GetActiveProgress()
        {
            if (_queue.Count == 0) return (0, 0);
            if (!TryParseKey(_queue[0], out var treeId, out var nodeId)) return (0, 0);
            if (!_trees.TryGetValue(treeId, out var tree)) return (0, 0);
            var node = tree.GetNode(nodeId);
            return node != null ? (_activeTicks, node.ResearchTicks) : (0, 0);
        }

        /// <summary>Returns the currently active research node key, or null if idle.</summary>
        public string ActiveNodeKey => _queue.Count > 0 ? _queue[0] : null;

        /// <summary>Returns a read-only ordered view of the queue.</summary>
        public IReadOnlyList<string> Queue => _queue;

        // ── ISaveStateProvider ────────────────────────────────────────────────────

        public void OnBeforeSave(SaveData saveData)
        {
            saveData.CompletedResearchNodes ??= new HashSet<string>();
            saveData.CompletedResearchNodes.Clear();
            foreach (var k in _completed) saveData.CompletedResearchNodes.Add(k);

            saveData.ResearchQueue ??= new List<string>();
            saveData.ResearchQueue.Clear();
            foreach (var k in _queue) saveData.ResearchQueue.Add(k);

            saveData.ResearchActiveTicks = _activeTicks;
        }

        public void OnAfterLoad(SaveData saveData)
        {
            _completed.Clear();
            _queue.Clear();
            _activeTicks = 0;

            if (saveData.CompletedResearchNodes != null)
                foreach (var k in saveData.CompletedResearchNodes) _completed.Add(k);

            if (saveData.ResearchQueue != null)
                foreach (var k in saveData.ResearchQueue) _queue.Add(k);

            _activeTicks = saveData.ResearchActiveTicks;
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static string MakeKey(string treeId, string nodeId) => $"{treeId}:{nodeId}";

        private static bool TryParseKey(string key, out string treeId, out string nodeId)
        {
            treeId = nodeId = null;
            if (string.IsNullOrEmpty(key)) return false;
            int sep = key.IndexOf(':');
            if (sep < 0) return false;
            treeId = key.Substring(0, sep);
            nodeId = key.Substring(sep + 1);
            return true;
        }

        private void CompleteHead(string treeId, string nodeId, ResearchNodeConfigSO node)
        {
            string key = MakeKey(treeId, nodeId);
            _queue.RemoveAt(0);
            _activeTicks = 0;
            _completed.Add(key);
            OnNodeCompleted?.Invoke(treeId, nodeId);
        }

        // ── IModifierSource ───────────────────────────────────────────────────────

        public string SourceId => "research";

        public Modifier GetModifier(StatType stat)
        {
            double additive = 0.0;
            double mult     = 1.0;
            foreach (var key in _completed)
            {
                if (!TryParseKey(key, out var treeId, out var nodeId)) continue;
                if (!_trees.TryGetValue(treeId, out var tree)) continue;
                var node = tree.GetNode(nodeId);
                if (node?.Effects == null) continue;
                foreach (var effect in node.Effects)
                {
                    if (!System.Enum.TryParse<StatType>(effect.TargetId, ignoreCase: true, out var targetStat)) continue;
                    if (targetStat != stat) continue;
                    if (effect.Type == SkillEffectType.StatMultiplier) mult     *= effect.Value;
                    else if (effect.Type == SkillEffectType.StatAdditive
                          || effect.Type == SkillEffectType.IncomeBonus) additive += effect.Value;
                }
            }
            return (additive == 0.0 && mult == 1.0) ? Modifier.None : new Modifier(additive, mult);
        }

        // ── Test helpers ──────────────────────────────────────────────────────────

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public static void ClearSubscribersForTesting()
        {
            OnNodeQueued      = null;
            OnResearchProgress = null;
            OnNodeCompleted   = null;
            OnEnqueueFailed   = null;
        }

        public void InjectCompletedForTesting(string treeId, string nodeId) =>
            _completed.Add(MakeKey(treeId, nodeId));

        public void InjectActiveTicksForTesting(int ticks) => _activeTicks = ticks;
#endif
    }
}

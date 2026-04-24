using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using EndlessEngine.Config;
using EndlessEngine.Research;

namespace EndlessEngine.UI
{
    /// <summary>
    /// Displays the research tree overlay.
    ///
    /// Shows nodes grouped by tier tabs. Completed nodes are visually distinct.
    /// Queued nodes show "QUEUED". Active research shows a progress bar.
    /// Clicking a node's Research button enqueues it via ResearchService.
    ///
    /// Wiring:
    ///   Assign _researchService and _researchTree in Inspector.
    ///   Call Show() to open.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class ResearchScreenController : MonoBehaviour
    {
        [SerializeField] private ResearchService     _researchService;
        [SerializeField] private ResearchTreeConfigSO _researchTree;

        public static event Action OnClosed;

        private UIDocument    _doc;
        private VisualElement _root;

        private Label         _activeLabel;
        private VisualElement _progressFill;
        private Label         _progressText;
        private Label         _queueDisplay;
        private VisualElement _tierBar;
        private VisualElement _nodesGrid;

        private int _selectedTier = 0;

        // ── Lifecycle ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            _doc  = GetComponent<UIDocument>();
            _root = _doc.rootVisualElement.Q("research-root");

            _activeLabel  = _root.Q<Label>("active-label");
            _progressFill = _root.Q("progress-fill");
            _progressText = _root.Q<Label>("progress-text");
            _queueDisplay = _root.Q<Label>("queue-display");
            _tierBar      = _root.Q("tier-bar");
            _nodesGrid    = _root.Q("nodes-grid");

            _root.Q<Button>("close-button").clicked += Hide;
            Hide();
        }

        private void OnEnable()
        {
            ResearchService.OnResearchProgress += HandleProgress;
            ResearchService.OnNodeCompleted    += HandleCompleted;
            ResearchService.OnNodeQueued       += HandleQueued;
        }

        private void OnDisable()
        {
            ResearchService.OnResearchProgress -= HandleProgress;
            ResearchService.OnNodeCompleted    -= HandleCompleted;
            ResearchService.OnNodeQueued       -= HandleQueued;
        }

        // ── Public API ────────────────────────────────────────────────────────────

        public void Show()
        {
            _root.style.display = DisplayStyle.Flex;
            BuildTierTabs();
            RefreshTierView(_selectedTier);
            RefreshProgress();
        }

        public void Hide()
        {
            _root.style.display = DisplayStyle.None;
            OnClosed?.Invoke();
        }

        // ── Tier tabs ─────────────────────────────────────────────────────────────

        private void BuildTierTabs()
        {
            _tierBar.Clear();
            if (_researchTree?.Nodes == null) return;

            int maxTier = 0;
            foreach (var n in _researchTree.Nodes)
                if (n != null && n.Tier > maxTier) maxTier = n.Tier;

            for (int t = 0; t <= maxTier; t++)
            {
                int tier = t;
                var btn = new Button(() => SelectTier(tier)) { text = $"Tier {tier}" };
                btn.AddToClassList("tier-tab");
                if (t == _selectedTier) btn.AddToClassList("tier-tab-active");
                _tierBar.Add(btn);
            }
        }

        private void SelectTier(int tier)
        {
            _selectedTier = tier;
            // Update tab active state
            int i = 0;
            foreach (var child in _tierBar.Children())
            {
                child.RemoveFromClassList("tier-tab-active");
                if (i == tier) child.AddToClassList("tier-tab-active");
                i++;
            }
            RefreshTierView(tier);
        }

        // ── Node grid ─────────────────────────────────────────────────────────────

        private void RefreshTierView(int tier)
        {
            _nodesGrid.Clear();
            if (_researchTree?.Nodes == null || _researchService == null) return;

            foreach (var node in _researchTree.Nodes)
            {
                if (node == null || node.Tier != tier) continue;
                _nodesGrid.Add(BuildNodeCard(node));
            }
        }

        private VisualElement BuildNodeCard(ResearchNodeConfigSO node)
        {
            bool completed = _researchService.IsCompleted(_researchTree.TreeId, node.NodeId);
            bool queued    = _researchService.IsQueued(_researchTree.TreeId, node.NodeId);
            bool locked    = !completed && !queued && !ArePrereqsMet(node);

            var card = new VisualElement();
            card.AddToClassList("research-node-card");
            if (completed) card.AddToClassList("research-node-card-completed");
            else if (queued) card.AddToClassList("research-node-card-queued");
            else if (locked) card.AddToClassList("research-node-card-locked");

            var name = new Label(node.DisplayName); name.AddToClassList("node-card-name");
            var desc = new Label(node.Description); desc.AddToClassList("node-card-desc");
            var cost = new Label($"Cost: {node.GoldCost:N0} gold"); cost.AddToClassList("node-card-cost");
            var time = new Label($"Time: {FormatTicks(node.ResearchTicks)}"); time.AddToClassList("node-card-time");

            card.Add(name); card.Add(desc); card.Add(cost); card.Add(time);

            string btnText = completed ? "DONE" : queued ? "QUEUED" : locked ? "LOCKED" : "RESEARCH";
            var btn = new Button(() => OnEnqueue(node)) { text = btnText };
            btn.AddToClassList("node-card-btn");
            btn.SetEnabled(!completed && !queued && !locked);
            card.Add(btn);

            return card;
        }

        private bool ArePrereqsMet(ResearchNodeConfigSO node)
        {
            if (node.PrerequisiteIds == null || node.PrerequisiteIds.Count == 0) return true;
            foreach (var pid in node.PrerequisiteIds)
                if (!_researchService.IsCompleted(_researchTree.TreeId, pid)) return false;
            return true;
        }

        private void OnEnqueue(ResearchNodeConfigSO node)
        {
            if (_researchService == null || _researchTree == null) return;
            _researchService.TryEnqueue(_researchTree.TreeId, node.NodeId);
            RefreshTierView(_selectedTier);
            RefreshQueue();
        }

        // ── Progress bar ──────────────────────────────────────────────────────────

        private void RefreshProgress()
        {
            var (done, total) = _researchService?.GetActiveProgress() ?? (0, 0);
            if (total == 0)
            {
                _activeLabel.text  = "IDLE";
                _progressFill.style.width = Length.Percent(0);
                _progressText.text = "—";
            }
            else
            {
                string key = _researchService.ActiveNodeKey ?? "";
                _activeLabel.text  = key.Contains(':') ? key.Split(':')[1] : key;
                float pct = Mathf.Clamp01((float)done / total) * 100f;
                _progressFill.style.width = Length.Percent(pct);
                _progressText.text = $"{done} / {total}";
            }
            RefreshQueue();
        }

        private void RefreshQueue()
        {
            if (_researchService == null || _researchService.QueueCount == 0)
            { _queueDisplay.text = "—"; return; }

            var parts = new List<string>();
            int skip = _researchService.QueueCount > 1 ? 1 : 0; // active node already shown in bar
            for (int i = skip; i < _researchService.Queue.Count; i++)
            {
                string k = _researchService.Queue[i];
                parts.Add(k.Contains(':') ? k.Split(':')[1] : k);
            }
            _queueDisplay.text = parts.Count > 0 ? string.Join(" → ", parts) : "—";
        }

        // ── Event handlers ────────────────────────────────────────────────────────

        private void HandleProgress(string treeId, string nodeId, int done, int total)
        {
            if (_root.style.display == DisplayStyle.None) return;
            if (treeId != _researchTree?.TreeId) return;

            _activeLabel.text = nodeId;
            float pct = Mathf.Clamp01((float)done / total) * 100f;
            _progressFill.style.width = Length.Percent(pct);
            _progressText.text = $"{done} / {total}";
        }

        private void HandleCompleted(string treeId, string nodeId)
        {
            if (_root.style.display == DisplayStyle.None) return;
            RefreshTierView(_selectedTier);
            RefreshProgress();
        }

        private void HandleQueued(string treeId, string nodeId)
        {
            if (_root.style.display == DisplayStyle.None) return;
            RefreshQueue();
        }

        // ── Formatting ────────────────────────────────────────────────────────────

        private static string FormatTicks(int ticks)
        {
            if (ticks >= 3600) return $"{ticks / 3600}h {(ticks % 3600) / 60}m";
            if (ticks >= 60)   return $"{ticks / 60}m {ticks % 60}s";
            return $"{ticks}s";
        }
    }
}

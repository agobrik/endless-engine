using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using EndlessEngine.Config;
using EndlessEngine.Economy;
using EndlessEngine.SaveAndLoad;
using Debug = UnityEngine.Debug;

namespace EndlessEngine.Upgrade
{
    /// <summary>
    /// Builds and manages the runtime upgrade tree. Implements ISaveStateProvider (order=20)
    /// to persist node ranks across sessions.
    ///
    /// Double-gate build pattern: tree is not readable until BOTH:
    ///   (1) ConfigRegistry.OnConfigsLoaded fires → HandleConfigsLoaded()
    ///   (2) SaveService.OnSaveLoaded fires       → OnAfterLoad()
    ///
    /// IsReady returns false until both gates have fired.
    ///
    /// IncrementNodeRank() is internal — only UpgradeApplicationSystem may call it.
    /// RebuildForPrestige() resets all ranks to 0 on OnPrestigeComplete.
    ///
    /// ADR: ADR-0009 — Upgrade Stat Model
    /// ADR: ADR-0012 — Upgrade Card Selection
    /// </summary>
    public class UpgradeTreeService : MonoBehaviour, ISaveStateProvider, IUpgradeTreeQuery
    {
        // ── ISaveStateProvider ────────────────────────────────────────────────────

        public int ProviderOrder => SaveConstants.SaveProviderOrder.UpgradeTree;

        // ── State ─────────────────────────────────────────────────────────────────

        private Dictionary<string, UpgradeNode> _nodes            = new();
        private bool                            _configsLoaded;
        private bool                            _saveLoaded;
        private Dictionary<string, int>         _pendingSaveState; // held until both gates fire

        /// <summary>True once both OnConfigsLoaded and OnSaveLoaded have fired and tree is built.</summary>
        public bool IsReady => _configsLoaded && _saveLoaded;

        // ── SaveService registration ──────────────────────────────────────────────

        [SerializeField]
        private SaveService _saveService;

        private void Start()
        {
            if (_saveService != null)
                _saveService.RegisterStateProvider(this);

            ConfigRegistry.OnConfigsLoaded += HandleConfigsLoaded;
        }

        private void OnDestroy()
        {
            ConfigRegistry.OnConfigsLoaded -= HandleConfigsLoaded;
        }

        // ── Gate handlers ─────────────────────────────────────────────────────────

        /// <summary>Called when ConfigRegistry.OnConfigsLoaded fires.</summary>
        public void HandleConfigsLoaded()
        {
            _configsLoaded = true;
            TryBuild();
        }

        /// <summary>Called by SaveService after load. ISaveStateProvider implementation.</summary>
        public void OnAfterLoad(SaveData saveData)
        {
            _pendingSaveState = saveData.UpgradeNodeStates ?? new Dictionary<string, int>();
            _saveLoaded       = true;
            TryBuild();
        }

        /// <summary>Called before each save. ISaveStateProvider implementation.</summary>
        public void OnBeforeSave(SaveData saveData)
        {
            saveData.UpgradeNodeStates = _nodes.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.CurrentRank);
        }

        // ── Build logic ───────────────────────────────────────────────────────────

        private void TryBuild()
        {
            if (!_configsLoaded || !_saveLoaded) return;
            BuildTree(_pendingSaveState);
        }

        private void BuildTree(Dictionary<string, int> savedRanks)
        {
            _nodes.Clear();

            foreach (var config in ConfigRegistry.Upgrades)
            {
                if (string.IsNullOrEmpty(config.NodeId))
                {
                    Debug.LogWarning("[UpgradeTreeService] Skipping config node with null/empty NodeId.");
                    continue;
                }

                int savedRank = savedRanks != null && savedRanks.TryGetValue(config.NodeId, out int r) ? r : 0;
                _nodes[config.NodeId] = new UpgradeNode(config, savedRank);
            }

            // Validate: saved keys not present in config
            if (savedRanks != null)
            {
                foreach (var key in savedRanks.Keys)
                {
                    if (!_nodes.ContainsKey(key))
                        Debug.LogWarning($"[UpgradeTreeService] SaveData NodeID '{key}' not found in config — skipping.");
                }
            }
        }

        // ── Public read API ───────────────────────────────────────────────────────

        /// <summary>Returns the node for <paramref name="nodeId"/>, or null if not found.</summary>
        public UpgradeNode GetNode(string nodeId)
            => _nodes.TryGetValue(nodeId, out var node) ? node : null;

        /// <summary>
        /// Returns the cost to purchase the next rank for <paramref name="nodeId"/>.
        /// Formula: Floor(BaseCost × CostScalingFactor ^ CurrentRank).
        /// Returns 0 if node not found.
        /// </summary>
        public long GetNodeCost(string nodeId)
        {
            var node = GetNode(nodeId);
            if (node == null) return 0L;
            return (long)Mathf.Floor(
                node.Config.BaseCost * Mathf.Pow(node.Config.CostScalingFactor, node.CurrentRank));
        }

        /// <summary>Returns true if the node exists, is below MaxRank, meets prerequisites, and passes prestige gate.</summary>
        public bool IsNodeAvailable(string nodeId)
        {
            var node = GetNode(nodeId);
            if (node == null) return false;

            // Max rank check (MaxRank=0 → unlimited)
            if (node.Config.MaxRank > 0 && node.CurrentRank >= node.Config.MaxRank) return false;

            // Prestige gate: requires at least 1 prestige to unlock
            if (node.Config.PrestigeGateRequirement > 0 && _currentPrestigeCount < node.Config.PrestigeGateRequirement)
                return false;

            // Prerequisite check
            if (node.Config.PrerequisiteNodeIDs != null)
            {
                foreach (var prereq in node.Config.PrerequisiteNodeIDs)
                {
                    var prereqNode = GetNode(prereq);
                    if (prereqNode == null || prereqNode.CurrentRank < 1) return false;
                }
            }

            return true;
        }

        /// <summary>Returns all nodes that are currently available for purchase.</summary>
        public List<UpgradeNode> GetAvailableNodes()
            => _nodes.Values.Where(n => IsNodeAvailable(n.Config.NodeId)).ToList();

        // ── Prestige integration ──────────────────────────────────────────────────

        private int _currentPrestigeCount;

        /// <summary>
        /// Called on OnPrestigeComplete. Resets all node ranks to 0.
        /// </summary>
        public void RebuildForPrestige()
        {
            foreach (var node in _nodes.Values)
                node.CurrentRank = 0;
            _currentPrestigeCount++;
        }

        // ── Internal rank write (UpgradeApplicationSystem only) ──────────────────

        /// <summary>
        /// Increments the rank of a node. Internal — only UpgradeApplicationSystem
        /// may call this after a purchase is validated.
        /// </summary>
        internal void IncrementNodeRank(string nodeId)
        {
            if (_nodes.TryGetValue(nodeId, out var node))
                node.CurrentRank++;
        }

        // ── Test injection ────────────────────────────────────────────────────────

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>
        /// Directly triggers the double-gate build with the provided config and save state.
        /// Allows EditMode testing without MonoBehaviour event wiring.
        /// </summary>
        public void InjectForTesting(UpgradeNodeConfigSO[] configs, Dictionary<string, int> savedRanks = null)
        {
            ConfigRegistry.InjectForTesting(upgrades: configs);
            _pendingSaveState = savedRanks ?? new Dictionary<string, int>();
            _configsLoaded    = true;
            _saveLoaded       = true;
            BuildTree(_pendingSaveState);
        }

        /// <summary>Resets tree state for test isolation.</summary>
        public void ResetForTesting()
        {
            _nodes.Clear();
            _configsLoaded    = false;
            _saveLoaded       = false;
            _pendingSaveState = null;
            _currentPrestigeCount = 0;
        }

        /// <summary>Sets prestige count for testing availability gates.</summary>
        public void SetPrestigeCountForTesting(int count) => _currentPrestigeCount = count;

        /// <summary>Fires the configs-loaded gate manually for testing single-gate behaviour.</summary>
        public void FireConfigsLoadedGateForTesting()
        {
            _configsLoaded = true;
            TryBuild();
        }

        /// <summary>Fires the save-loaded gate manually for testing single-gate behaviour.</summary>
        public void FireSaveLoadedGateForTesting(Dictionary<string, int> savedRanks = null)
        {
            _pendingSaveState = savedRanks ?? new Dictionary<string, int>();
            _saveLoaded       = true;
            TryBuild();
        }
#endif
    }

    // ── Runtime node ─────────────────────────────────────────────────────────────

    /// <summary>Runtime state for a single upgrade tree node.</summary>
    public class UpgradeNode
    {
        public readonly UpgradeNodeConfigSO Config;
        public int CurrentRank;

        public UpgradeNode(UpgradeNodeConfigSO config, int savedRank)
        {
            Config      = config;
            CurrentRank = savedRank;
        }
    }
}

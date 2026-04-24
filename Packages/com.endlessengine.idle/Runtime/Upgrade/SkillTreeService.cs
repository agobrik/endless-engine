using System;
using System.Collections.Generic;
using UnityEngine;
using EndlessEngine.Config;
using EndlessEngine.SaveAndLoad;

namespace EndlessEngine.Upgrade
{
    /// <summary>
    /// Reason codes for skill unlock / refund failures.
    /// </summary>
    public enum SkillUnlockFailReason
    {
        AlreadyUnlocked,
        InsufficientPoints,
        PrerequisiteNotMet,
        NodeNotFound
    }

    /// <summary>
    /// Reason codes for skill refund failures.
    /// </summary>
    public enum SkillRefundFailReason
    {
        NotUnlocked,
        NotRefundable,
        NodeNotFound,
        HasDependents // other unlocked nodes require this one
    }

    /// <summary>
    /// Manages skill/talent tree state: unlocking nodes, refunding, point tracking.
    /// Persists unlocked nodes via ISaveStateProvider.
    ///
    /// Supports multiple SkillTreeConfigSO instances (one per tree type).
    /// Node keys are "treeId:nodeId" composite strings.
    ///
    /// Bootstrap wiring:
    ///   skillTreeService.Initialize(treeSOs);
    ///   saveService.RegisterStateProvider(skillTreeService);
    /// </summary>
    public class SkillTreeService : MonoBehaviour, ISaveStateProvider
    {
        // ── ISaveStateProvider ────────────────────────────────────────────────────

        public int ProviderOrder => SaveConstants.SaveProviderOrder.SkillTree;

        // ── Events ────────────────────────────────────────────────────────────────

        /// <summary>Fires after a node is successfully unlocked. Parameters: treeId, nodeId.</summary>
        public static event Action<string, string> OnNodeUnlocked;

        /// <summary>Fires after a node is refunded. Parameters: treeId, nodeId.</summary>
        public static event Action<string, string> OnNodeRefunded;

        /// <summary>Fires when an unlock fails.</summary>
        public static event Action<string, string, SkillUnlockFailReason> OnUnlockFailed;

        /// <summary>Fires when skill points change. Parameter: newTotal.</summary>
        public static event Action<int> OnSkillPointsChanged;

        // ── Runtime state ─────────────────────────────────────────────────────────

        private readonly Dictionary<string, SkillTreeConfigSO> _trees  = new Dictionary<string, SkillTreeConfigSO>();
        private readonly HashSet<string> _unlocked = new HashSet<string>();
        private int _skillPoints;

        private bool _initialized;

        // ── Initialization ────────────────────────────────────────────────────────

        public void Initialize(SkillTreeConfigSO[] trees, int startingPoints = 0)
        {
            _trees.Clear();
            if (trees != null)
                foreach (var t in trees)
                    if (t != null && !string.IsNullOrEmpty(t.TreeId))
                        _trees[t.TreeId] = t;

            _skillPoints = startingPoints;
            _initialized  = true;
        }

        // ── ISaveStateProvider ────────────────────────────────────────────────────

        public void OnBeforeSave(SaveData saveData)
        {
            saveData.UnlockedSkillNodes ??= new HashSet<string>();
            saveData.UnlockedSkillNodes.Clear();
            foreach (var key in _unlocked)
                saveData.UnlockedSkillNodes.Add(key);
            saveData.SkillPoints = _skillPoints;
        }

        public void OnAfterLoad(SaveData saveData)
        {
            _unlocked.Clear();
            if (saveData.UnlockedSkillNodes != null)
                foreach (var key in saveData.UnlockedSkillNodes)
                    _unlocked.Add(key);
            _skillPoints = saveData.SkillPoints;
        }

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>Current skill points available to spend.</summary>
        public int SkillPoints => _skillPoints;

        /// <summary>Adds skill points (e.g. from prestige or milestone reward).</summary>
        public void AddPoints(int amount)
        {
            if (amount <= 0) return;
            _skillPoints += amount;
            OnSkillPointsChanged?.Invoke(_skillPoints);
        }

        /// <summary>Returns true if the given node is unlocked.</summary>
        public bool IsUnlocked(string treeId, string nodeId) =>
            _unlocked.Contains(MakeKey(treeId, nodeId));

        /// <summary>
        /// Attempts to unlock a node. Returns true on success.
        /// Fires OnUnlockFailed with the reason on failure.
        /// </summary>
        public bool TryUnlock(string treeId, string nodeId)
        {
            string key = MakeKey(treeId, nodeId);

            if (_unlocked.Contains(key))
            {
                OnUnlockFailed?.Invoke(treeId, nodeId, SkillUnlockFailReason.AlreadyUnlocked);
                return false;
            }

            if (!_trees.TryGetValue(treeId, out var tree))
            {
                OnUnlockFailed?.Invoke(treeId, nodeId, SkillUnlockFailReason.NodeNotFound);
                return false;
            }

            var node = tree.GetNode(nodeId);
            if (node == null)
            {
                OnUnlockFailed?.Invoke(treeId, nodeId, SkillUnlockFailReason.NodeNotFound);
                return false;
            }

            if (_skillPoints < node.PointCost)
            {
                OnUnlockFailed?.Invoke(treeId, nodeId, SkillUnlockFailReason.InsufficientPoints);
                return false;
            }

            foreach (var prereqId in node.PrerequisiteIds)
            {
                if (!_unlocked.Contains(MakeKey(treeId, prereqId)))
                {
                    OnUnlockFailed?.Invoke(treeId, nodeId, SkillUnlockFailReason.PrerequisiteNotMet);
                    return false;
                }
            }

            _skillPoints -= node.PointCost;
            _unlocked.Add(key);

            OnNodeUnlocked?.Invoke(treeId, nodeId);
            OnSkillPointsChanged?.Invoke(_skillPoints);
            return true;
        }

        /// <summary>
        /// Attempts to refund a node. Returns true on success.
        /// Refunding returns the node's PointCost to the pool.
        /// Fails if other unlocked nodes depend on this one.
        /// </summary>
        public bool TryRefund(string treeId, string nodeId)
        {
            string key = MakeKey(treeId, nodeId);

            if (!_unlocked.Contains(key)) return false;

            if (!_trees.TryGetValue(treeId, out var tree)) return false;

            var node = tree.GetNode(nodeId);
            if (node == null || !node.Refundable) return false;

            // Check no unlocked node depends on this one
            if (HasDependents(treeId, nodeId, tree)) return false;

            _unlocked.Remove(key);
            _skillPoints += node.PointCost;

            OnNodeRefunded?.Invoke(treeId, nodeId);
            OnSkillPointsChanged?.Invoke(_skillPoints);
            return true;
        }

        /// <summary>
        /// Applies the stat effects of all currently unlocked nodes.
        /// Returns a flat list of (targetId, totalValue) per SkillEffectType.
        /// Callers (EconomyService, PrestigeStateManager) should re-query this after any unlock/refund.
        /// </summary>
        public List<SkillEffect> GetAllActiveEffects()
        {
            var results = new List<SkillEffect>();
            foreach (var key in _unlocked)
            {
                var parts = key.Split(':');
                if (parts.Length != 2) continue;
                if (!_trees.TryGetValue(parts[0], out var tree)) continue;
                var node = tree.GetNode(parts[1]);
                if (node?.Effects == null) continue;
                foreach (var effect in node.Effects)
                    results.Add(effect);
            }
            return results;
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static string MakeKey(string treeId, string nodeId) => $"{treeId}:{nodeId}";

        private bool HasDependents(string treeId, string nodeId, SkillTreeConfigSO tree)
        {
            if (tree.Nodes == null) return false;
            foreach (var n in tree.Nodes)
            {
                if (n == null) continue;
                if (!_unlocked.Contains(MakeKey(treeId, n.NodeId))) continue;
                if (n.PrerequisiteIds != null && n.PrerequisiteIds.Contains(nodeId))
                    return true;
            }
            return false;
        }

        // ── Test helpers ──────────────────────────────────────────────────────────

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public static void ClearSubscribersForTesting()
        {
            OnNodeUnlocked      = null;
            OnNodeRefunded      = null;
            OnUnlockFailed      = null;
            OnSkillPointsChanged = null;
        }

        public void InjectPointsForTesting(int points) => _skillPoints = points;

        public void InjectUnlockedForTesting(string treeId, string nodeId) =>
            _unlocked.Add(MakeKey(treeId, nodeId));

        public bool IsUnlockedForTesting(string treeId, string nodeId) =>
            IsUnlocked(treeId, nodeId);
#endif
    }
}

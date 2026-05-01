using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace EndlessEngine.Config
{
    /// <summary>
    /// Validates all 8 canonical SO types after they are loaded from a RealmPackSO.
    /// Returns false and logs field-level errors if any check fails.
    /// Validation runs before ConfigRegistry.Populate() — a failed validation
    /// transitions ConfigLoadingService to ErrorState; OnConfigsLoaded never fires.
    ///
    /// ValidationMode.Error: range violations are fatal (default for shipping builds).
    /// ValidationMode.Warning: range violations log warnings but continue.
    ///
    /// Graph-level checks (ValidateUpgradeGraph):
    ///   • Duplicate NodeIds across the upgrade node set.
    ///   • Missing prerequisite references (orphan refs).
    ///   • Cycle detection via DFS — reports the cycle path on failure.
    /// </summary>
    public static class ConfigValidator
    {
        public enum ValidationMode { Error, Warning }

        /// <summary>
        /// Validates a fully-resolved config set. Logs field-level errors on failure.
        /// Returns true if all checks pass.
        /// </summary>
        public static bool Validate(ResolvedConfigs resolved, ValidationMode mode = ValidationMode.Error)
        {
            bool valid = true;

            valid &= ValidateEnemyStats(resolved.Enemy,  resolved.RealmSlug, mode);
            valid &= ValidateWave(resolved.Wave,         resolved.RealmSlug, mode);
            valid &= ValidateEconomy(resolved.Economy,   resolved.RealmSlug, mode);
            valid &= ValidateUpgrades(resolved.Upgrades, resolved.RealmSlug, mode);
            valid &= ValidatePrestige(resolved.Prestige, resolved.RealmSlug, mode);
            valid &= ValidateRealm(resolved.Realm,       resolved.RealmSlug, mode);
            valid &= ValidatePlayer(resolved.Player,     resolved.RealmSlug, mode);
            valid &= ValidateSchema(resolved.Schema,     resolved.RealmSlug, mode);

            // Graph-level: duplicate IDs, orphan refs, cycles
            valid &= ValidateUpgradeGraph(resolved.Upgrades, resolved.RealmSlug, mode);

            return valid;
        }

        // ── Graph-level validation ───────────────────────────────────────────────

        /// <summary>
        /// Graph-level checks on the upgrade node set:
        ///   1. Duplicate NodeId values.
        ///   2. Prerequisite IDs that reference non-existent nodes (orphan refs).
        ///   3. Cycles in the prerequisite graph (DFS with colour marking).
        ///
        /// Returns true if all checks pass.
        /// </summary>
        public static bool ValidateUpgradeGraph(
            UpgradeNodeConfigSO[] nodes, string slug, ValidationMode mode)
        {
            if (nodes == null || nodes.Length == 0) return true;

            bool ok = true;

            // 1. Build ID set and detect duplicates
            var idSet   = new HashSet<string>(StringComparer.Ordinal);
            var dupSeen = new HashSet<string>(StringComparer.Ordinal);

            foreach (var node in nodes)
            {
                if (node == null || string.IsNullOrEmpty(node.NodeId)) continue;
                if (!idSet.Add(node.NodeId) && dupSeen.Add(node.NodeId))
                {
                    LogIssue($"[Realm: {slug}] Duplicate UpgradeNodeConfigSO.NodeId '{node.NodeId}'.", mode);
                    ok = false;
                }
            }

            // 2. Orphan prerequisite references
            foreach (var node in nodes)
            {
                if (node?.PrerequisiteNodeIDs == null) continue;
                foreach (string prereq in node.PrerequisiteNodeIDs)
                {
                    if (!string.IsNullOrWhiteSpace(prereq) && !idSet.Contains(prereq))
                    {
                        LogIssue($"[Realm: {slug}] UpgradeNode '{node.NodeId}' has prerequisite '{prereq}' " +
                                 "which does not match any NodeId in the set.", mode);
                        ok = false;
                    }
                }
            }

            // 3. Cycle detection — DFS with White/Grey/Black colouring
            // Build adjacency: node → prerequisites (edges point toward dependencies)
            var adj = new Dictionary<string, string[]>(StringComparer.Ordinal);
            foreach (var node in nodes)
            {
                if (node == null || string.IsNullOrEmpty(node.NodeId)) continue;
                adj[node.NodeId] = node.PrerequisiteNodeIDs ?? System.Array.Empty<string>();
            }

            var colour = new Dictionary<string, int>(StringComparer.Ordinal); // 0=white 1=grey 2=black
            var cyclePath = new List<string>();

            foreach (string id in adj.Keys)
            {
                if (!colour.ContainsKey(id) || colour[id] == 0)
                {
                    cyclePath.Clear();
                    if (DfsHasCycle(id, adj, colour, cyclePath))
                    {
                        string cycle = string.Join(" → ", cyclePath);
                        LogIssue($"[Realm: {slug}] Cycle detected in upgrade prerequisite graph: {cycle}.", mode);
                        ok = false;
                        // Continue scanning for additional cycles
                    }
                }
            }

            return ok;
        }

        // Returns true if a cycle is found starting from nodeId.
        // cyclePath contains the cycle trail on return.
        private static bool DfsHasCycle(
            string nodeId,
            Dictionary<string, string[]> adj,
            Dictionary<string, int> colour,
            List<string> path)
        {
            colour[nodeId] = 1; // grey — in current DFS path
            path.Add(nodeId);

            if (adj.TryGetValue(nodeId, out string[] prereqs))
            {
                foreach (string prereq in prereqs)
                {
                    if (string.IsNullOrWhiteSpace(prereq)) continue;
                    if (!adj.ContainsKey(prereq)) continue; // orphan — already reported

                    colour.TryGetValue(prereq, out int c);
                    if (c == 1) // back-edge → cycle
                    {
                        path.Add(prereq); // close the cycle in the display
                        return true;
                    }
                    if (c == 0)
                    {
                        if (DfsHasCycle(prereq, adj, colour, path))
                            return true;
                    }
                }
            }

            colour[nodeId] = 2; // black — fully explored
            path.RemoveAt(path.Count - 1);
            return false;
        }

        // ── Per-SO validators ────────────────────────────────────────────────────

        private static bool ValidateEnemyStats(EnemyStatConfigSO so, string slug, ValidationMode mode)
        {
            bool ok = true;
            ok &= CheckPositive(so.BaseMaxHP,       "EnemyStatConfigSO.BaseMaxHP",       slug, mode, min: 1f);
            ok &= CheckPositive(so.BaseAttackDamage,"EnemyStatConfigSO.BaseAttackDamage", slug, mode, min: 1f);
            ok &= CheckRange(so.MoveSpeed,          "EnemyStatConfigSO.MoveSpeed",        slug, mode, min: 0.5f, max: 20f);
            ok &= CheckPositive(so.AttackInterval,  "EnemyStatConfigSO.AttackInterval",   slug, mode, min: 0.1f);
            ok &= CheckPositive(so.HardCapEnemiesOnScreen,
                                                    "EnemyStatConfigSO.HardCapEnemiesOnScreen", slug, mode, min: 1f);
            return ok;
        }

        private static bool ValidateWave(WaveConfigSO so, string slug, ValidationMode mode)
        {
            bool ok = true;
            ok &= CheckPositive(so.TotalWavesPerRun, "WaveConfigSO.TotalWavesPerRun", slug, mode, min: 1f);
            ok &= CheckPositive(so.BaseEnemyCountPerWave, "WaveConfigSO.BaseEnemyCountPerWave", slug, mode, min: 1f);
            ok &= CheckPositive(so.EliteWaveInterval,"WaveConfigSO.EliteWaveInterval",slug, mode, min: 1f);
            return ok;
        }

        private static bool ValidateEconomy(EconomyConfigSO so, string slug, ValidationMode mode)
        {
            bool ok = true;
            ok &= CheckPositive(so.IdleYieldRateBase,       "EconomyConfigSO.IdleYieldRateBase",       slug, mode, min: 0.001f);
            ok &= CheckPositive(so.BaseMultiplierPerPrestige,"EconomyConfigSO.BaseMultiplierPerPrestige",slug, mode, min: 1f);
            ok &= CheckPositive(so.ResourceHardCap,         "EconomyConfigSO.ResourceHardCap",         slug, mode, min: 1f);
            ok &= CheckPositive(so.OfflineCapHours,         "EconomyConfigSO.OfflineCapHours",         slug, mode, min: 0.1f);
            return ok;
        }

        private static bool ValidateUpgrades(UpgradeNodeConfigSO[] sos, string slug, ValidationMode mode)
        {
            if (sos == null || sos.Length == 0)
            {
                LogIssue($"[Realm: {slug}] UpgradeNodeConfigs is null or empty.", mode);
                return mode == ValidationMode.Warning;
            }
            bool ok = true;
            foreach (var so in sos)
            {
                if (so == null)
                {
                    LogIssue($"[Realm: {slug}] UpgradeNodeConfigs contains a null entry.", mode);
                    ok = mode == ValidationMode.Warning;
                    continue;
                }
                if (string.IsNullOrEmpty(so.NodeId))
                {
                    LogIssue($"[Realm: {slug}] UpgradeNodeConfigSO '{so.name}' has empty NodeId.", mode);
                    ok = mode == ValidationMode.Warning;
                }
            }
            return ok;
        }

        private static bool ValidatePrestige(PrestigeConfigSO so, string slug, ValidationMode mode)
        {
            bool ok = true;
            ok &= CheckPositive(so.BaseMultiplierPerPrestige,"PrestigeConfigSO.BaseMultiplierPerPrestige",slug, mode, min: 1f);
            ok &= CheckPositive(so.MaxPermanentMultiplier,   "PrestigeConfigSO.MaxPermanentMultiplier",   slug, mode, min: 1f);
            return ok;
        }

        private static bool ValidateRealm(RealmIdentityConfigSO so, string slug, ValidationMode mode)
        {
            bool ok = true;
            if (so.ArenaBounds.width <= 0f || so.ArenaBounds.height <= 0f)
            {
                LogIssue($"[Realm: {slug}] RealmIdentityConfigSO.ArenaBounds has zero or negative dimensions (w={so.ArenaBounds.width}, h={so.ArenaBounds.height}).", mode);
                ok = mode == ValidationMode.Warning;
            }
            return ok;
        }

        private static bool ValidatePlayer(PlayerBaseStatConfigSO so, string slug, ValidationMode mode)
        {
            bool ok = true;
            ok &= CheckPositive(so.BaseMaxHP,           "PlayerBaseStatConfigSO.BaseMaxHP",           slug, mode, min: 1f);
            ok &= CheckPositive(so.BaseAttackDamage,    "PlayerBaseStatConfigSO.BaseAttackDamage",    slug, mode, min: 1f);
            ok &= CheckRange(so.BaseCritChance,         "PlayerBaseStatConfigSO.BaseCritChance",      slug, mode, min: 0f, max: 1f);
            ok &= CheckRange(so.BaseCritMultiplier,     "PlayerBaseStatConfigSO.BaseCritMultiplier",  slug, mode, min: 1f, max: 10f);
            ok &= CheckRange(so.BaseMoveSpeed,          "PlayerBaseStatConfigSO.BaseMoveSpeed",       slug, mode, min: 0.5f, max: 20f);
            ok &= CheckPositive(so.BaseAttackInterval,  "PlayerBaseStatConfigSO.BaseAttackInterval",  slug, mode, min: 0.05f);
            return ok;
        }

        private static bool ValidateSchema(SchemaVersionSO so, string slug, ValidationMode mode)
        {
            if (so.CurrentSchemaVersion < 0)
            {
                LogIssue($"[Realm: {slug}] SchemaVersionSO.CurrentSchemaVersion is negative ({so.CurrentSchemaVersion}).", mode);
                return mode == ValidationMode.Warning;
            }
            if (so.MinimumCompatibleVersion > so.CurrentSchemaVersion)
            {
                LogIssue($"[Realm: {slug}] SchemaVersionSO.MinimumCompatibleVersion ({so.MinimumCompatibleVersion}) exceeds CurrentSchemaVersion ({so.CurrentSchemaVersion}).", mode);
                return mode == ValidationMode.Warning;
            }
            return true;
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        private static bool CheckPositive(float value, string fieldName, string slug, ValidationMode mode, float min = 0f)
        {
            if (value < min)
            {
                LogIssue($"[Realm: {slug}] {fieldName} = {value} is below minimum {min}.", mode);
                return mode == ValidationMode.Warning;
            }
            return true;
        }

        private static bool CheckPositive(long value, string fieldName, string slug, ValidationMode mode, float min = 0f)
            => CheckPositive((float)value, fieldName, slug, mode, min);

        private static bool CheckPositive(int value, string fieldName, string slug, ValidationMode mode, float min = 0f)
            => CheckPositive((float)value, fieldName, slug, mode, min);

        private static bool CheckRange(float value, string fieldName, string slug, ValidationMode mode, float min, float max)
        {
            if (value < min || value > max)
            {
                LogIssue($"[Realm: {slug}] {fieldName} = {value} is outside valid range [{min}, {max}].", mode);
                return mode == ValidationMode.Warning;
            }
            return true;
        }

        private static void LogIssue(string message, ValidationMode mode)
        {
            if (mode == ValidationMode.Error)
                Debug.LogError($"[ConfigValidator] {message}");
            else
                Debug.LogWarning($"[ConfigValidator] {message}");
        }
    }
}

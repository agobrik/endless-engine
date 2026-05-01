using System;
using System.Collections.Generic;
using UnityEngine;
using EndlessEngine.Config;
using EndlessEngine.Core;
using EndlessEngine.Economy;
using EndlessEngine.SaveAndLoad;
using EndlessEngine.Statistics;
using EndlessEngine.VFX;

namespace EndlessEngine.Harvest
{
    /// <summary>
    /// Core active-loop service: drives harvest ticks against nodes overlapping the cursor.
    ///
    /// Pipeline per tick:
    ///   1. Read overlapping nodes from HarvestCursor
    ///   2. For each alive node, apply damage = DamagePerTick (scaled by TickRate stat)
    ///   3. HarvestYieldResolver computes gold → EconomyService
    ///   4. HarvestComboTracker accumulates combo points, decays on idle
    ///   5. Events: OnNodeDamaged, OnNodeDepleted, OnYieldAwarded, OnComboChanged
    ///
    /// Implements ISaveStateProvider: persists per-node respawn timers + lifetime stats.
    /// Integrates with StatisticsService for TotalHarvestGold, TotalNodesHarvested, BestCombo.
    /// Integrates with VFXController for floating yield numbers on node hit.
    ///
    /// Attach to the same GameObject as HarvestCursor.
    /// Call Initialize() from bootstrapper before scene starts.
    /// </summary>
    public class HarvestLoopService : MonoBehaviour, ISaveStateProvider
    {
        // ── ISaveStateProvider ────────────────────────────────────────────────────

        public int ProviderOrder => SaveConstants.SaveProviderOrder.Harvest;

        // ── Events ────────────────────────────────────────────────────────────────

        /// <summary>Fired each tick a node is hit. (node, damageDealt, yieldAwarded)</summary>
        public event Action<IHarvestNode, float, float> OnNodeDamaged;

        /// <summary>Fired when a node's HP reaches 0.</summary>
        public event Action<IHarvestNode> OnNodeDepleted;

        /// <summary>Fired each tick gold is awarded. Total across all nodes hit this tick.</summary>
        public event Action<float> OnYieldAwarded;

        /// <summary>Fired when combo multiplier changes. Payload = new multiplier value.</summary>
        public event Action<float> OnComboChanged;

        // ── Statistics stat IDs ───────────────────────────────────────────────────

        public const string StatIdTotalGold      = "harvest.total_gold";
        public const string StatIdTotalNodes      = "harvest.total_nodes_harvested";
        public const string StatIdBestCombo       = "harvest.best_combo_multiplier";

        // ── Dependencies ──────────────────────────────────────────────────────────

        private HarvestCursor       _cursor;
        private HarvestAreaConfigSO _config;
        private EconomyService      _economy;
        private StatisticsService   _statistics;   // optional — null-safe
        private VFXController       _vfx;          // optional — null-safe

        // ── Runtime state ─────────────────────────────────────────────────────────

        private HarvestComboTracker _combo;
        private float               _tickTimer;
        private float               _lastComboMultiplier = 1f;

        // Lifetime session counters (persisted via ISaveStateProvider)
        private long  _totalGoldEarned;
        private int   _totalNodesHarvested;
        private float _bestComboMultiplier = 1f;

        public float ComboMultiplier => _combo?.ComboMultiplier ?? 1f;
        public float ComboPoints     => _combo?.ComboPoints     ?? 0f;

        // ── Initialization ────────────────────────────────────────────────────────

        /// <summary>
        /// Core initialization. statistics and vfx are optional — pass null if not used.
        /// Call from bootstrapper after all services are initialized.
        /// Then call saveService.RegisterStateProvider(this).
        /// </summary>
        public void Initialize(
            HarvestCursor       cursor,
            HarvestAreaConfigSO config,
            EconomyService      economy,
            StatisticsService   statistics = null,
            VFXController       vfx        = null)
        {
            _cursor     = cursor;
            _config     = config;
            _economy    = economy;
            _statistics = statistics;
            _vfx        = vfx;
            _combo      = new HarvestComboTracker(config);
        }

        // ── Unity Lifecycle ───────────────────────────────────────────────────────

        private void Update()
        {
            if (_config == null || _cursor == null) return;

            float dt = Time.deltaTime;

            _combo.Tick(dt);

            float newMul = _combo.ComboMultiplier;
            if (!Mathf.Approximately(newMul, _lastComboMultiplier))
            {
                _lastComboMultiplier = newMul;
                OnComboChanged?.Invoke(newMul);

                if (newMul > _bestComboMultiplier)
                {
                    _bestComboMultiplier = newMul;
                    _statistics?.SetIfHigher(StatIdBestCombo, newMul);
                }
            }

            _tickTimer += dt;
            float interval = ComputeTickInterval();
            if (_tickTimer < interval) return;

            _tickTimer -= interval;
            ExecuteTick();
        }

        // ── Tick ──────────────────────────────────────────────────────────────────

        private void ExecuteTick()
        {
            IReadOnlyList<IHarvestNode> nodes = _cursor.OverlappingNodes;
            if (nodes.Count == 0) return;

            float comboMul      = _combo.ComboMultiplier;
            float totalYield    = 0f;
            float comboAccum    = 0f;
            int   simultaneousN = nodes.Count;

            for (int i = 0; i < nodes.Count; i++)
            {
                IHarvestNode node = nodes[i];
                if (!node.IsAlive) continue;

                float damage  = ComputeDamage(node.Config);
                float applied = node.ApplyDamage(damage);

                float yield;
                if (node.Config.AwardYieldPerTick)
                {
                    yield = HarvestYieldResolver.ResolveTickYield(node.Config, applied, comboMul, simultaneousN);
                }
                else
                {
                    yield = (!node.IsAlive)
                        ? HarvestYieldResolver.ResolveDepletionYield(node.Config, comboMul, simultaneousN)
                        : 0f;
                }

                comboAccum += node.Config.ComboContribution;
                totalYield += yield;

                OnNodeDamaged?.Invoke(node, applied, yield);

                if (!node.IsAlive)
                {
                    _totalNodesHarvested++;
                    _statistics?.Add(StatIdTotalNodes, 1);
                    OnNodeDepleted?.Invoke(node);
                }

                // Floating yield number above the node
                if (yield > 0f)
                    SpawnFloatingYield(yield, node.WorldPosition);
            }

            if (comboAccum > 0f)
                _combo.RecordHit(comboAccum);

            if (totalYield > 0f)
            {
                AwardYield(totalYield);
                OnYieldAwarded?.Invoke(totalYield);
            }
        }

        // ── Private helpers ───────────────────────────────────────────────────────

        private float ComputeTickInterval()
        {
            float bonus = UpgradeApplicationSystem.GetEffectiveStat(StatType.HarvestTickRate);
            float speed = 1f + bonus;
            return _config.BaseTickInterval / Mathf.Max(speed, 0.1f);
        }

        private static float ComputeDamage(HarvestNodeConfigSO config) => config.DamagePerTick;

        private void AwardYield(float amount)
        {
            if (_economy == null) return;
            long gold = (long)Mathf.Max(1f, amount);
            _economy.AddResources(gold);

            _totalGoldEarned += gold;
            _statistics?.Add(StatIdTotalGold, gold);
        }

        private void SpawnFloatingYield(float yield, Vector2 worldPos)
        {
            if (_vfx == null) return;
            long display = (long)Mathf.Max(1f, yield);
            _vfx.SpawnHarvestNumber(display, worldPos);
        }

        // ── ISaveStateProvider ────────────────────────────────────────────────────

        public void OnBeforeSave(SaveData saveData)
        {
            var state = saveData.HarvestState;
            state.NodeStates.Clear();
            state.TotalGoldEarned      = _totalGoldEarned;
            state.TotalNodesHarvested  = _totalNodesHarvested;
            state.BestComboMultiplier  = _bestComboMultiplier;

            // Serialize respawn state for every registered node
            var allNodes = HarvestNodeRegistry.All;
            for (int i = 0; i < allNodes.Count; i++)
            {
                HarvestNode node = allNodes[i];
                if (!node.IsRespawning) continue;

                string key = MakeSaveKey(node, i);
                state.NodeStates[key] = new HarvestNodeSaveEntry
                {
                    IsRespawning            = true,
                    RespawnSecondsRemaining = node.RespawnSecondsRemaining,
                };
            }
        }

        public void OnAfterLoad(SaveData saveData)
        {
            var state = saveData.HarvestState;
            _totalGoldEarned     = state.TotalGoldEarned;
            _totalNodesHarvested = state.TotalNodesHarvested;
            _bestComboMultiplier = Mathf.Max(1f, state.BestComboMultiplier);

            // Restore respawn timers — nodes must already be in the registry (spawned in Start)
            var allNodes = HarvestNodeRegistry.All;
            for (int i = 0; i < allNodes.Count; i++)
            {
                HarvestNode node = allNodes[i];
                string      key  = MakeSaveKey(node, i);

                state.NodeStates.TryGetValue(key, out HarvestNodeSaveEntry entry);
                node.RestoreFromSave(entry); // null entry = node is alive, full HP
            }

            // Push lifetime stats back into StatisticsService
            _statistics?.Add(StatIdTotalGold,  _totalGoldEarned);
            _statistics?.Add(StatIdTotalNodes, _totalNodesHarvested);
            _statistics?.SetIfHigher(StatIdBestCombo, _bestComboMultiplier);
        }

        // ── Public controls ───────────────────────────────────────────────────────

        public void ResetCombo() => _combo?.Reset();

        // ── Private utility ───────────────────────────────────────────────────────

        private static string MakeSaveKey(HarvestNode node, int registryIndex)
            => $"{node.NodeId}_{registryIndex}";

        // ── Test support ──────────────────────────────────────────────────────────

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public void TickForTesting() => ExecuteTick();

        public long  TotalGoldEarnedForTesting     => _totalGoldEarned;
        public int   TotalNodesHarvestedForTesting => _totalNodesHarvested;
        public float BestComboForTesting           => _bestComboMultiplier;
#endif
    }
}

using System;
using UnityEngine;
using EndlessEngine.Economy;
using EndlessEngine.SaveAndLoad;

namespace EndlessEngine.Harvest
{
    /// <summary>
    /// Computes harvest-equivalent gold for time spent offline.
    ///
    /// Formula:
    ///   EffectiveDelta  = Min(offlineDeltaSeconds, OfflineCapHours × 3600)
    ///   HarvestRate     = sum of (BaseYield / RespawnSeconds) for each registered node config
    ///   OfflineHarvest  = Floor(HarvestRate × OfflineEfficiency × EffectiveDelta)
    ///
    /// OfflineEfficiency (0–1) is set in HarvestAreaConfigSO; default 0.3 (30%) —
    /// offline is always less efficient than active play to preserve the active-loop incentive.
    ///
    /// Fires OnOfflineHarvestCalculated once per session (same guard as OfflineTimeCalculator).
    /// Subscribe from a UI controller to show "You harvested X while away."
    ///
    /// Bootstrap wiring:
    ///   harvestOfflineCalc.Initialize(areaConfig, economyService, nodeConfigs);
    ///   saveService.OnSaveLoaded += harvestOfflineCalc.HandleSaveLoaded;
    /// </summary>
    public class HarvestOfflineCalculator : MonoBehaviour
    {
        // ── Events ────────────────────────────────────────────────────────────────

        /// <summary>Fires once per session. Parameters: (goldAwarded, effectiveDeltaSeconds)</summary>
        public static event Action<long, float> OnOfflineHarvestCalculated;

        // ── Inspector ─────────────────────────────────────────────────────────────

        [SerializeField] private SaveService _saveService;

        // ── Dependencies ──────────────────────────────────────────────────────────

        private HarvestAreaConfigSO   _config;
        private EconomyService        _economy;
        private HarvestNodeConfigSO[] _nodeConfigs;

        // ── State ─────────────────────────────────────────────────────────────────

        private bool _hasRun;

        // ── Initialization ────────────────────────────────────────────────────────

        public void Initialize(
            HarvestAreaConfigSO   config,
            EconomyService        economy,
            HarvestNodeConfigSO[] nodeConfigs)
        {
            _config      = config;
            _economy     = economy;
            _nodeConfigs = nodeConfigs;
        }

        // ── Unity Lifecycle ───────────────────────────────────────────────────────

        private void OnEnable()
        {
            if (_saveService != null)
                _saveService.OnSaveLoaded += HandleSaveLoaded;
        }

        private void OnDisable()
        {
            if (_saveService != null)
                _saveService.OnSaveLoaded -= HandleSaveLoaded;
        }

        // ── Core logic ────────────────────────────────────────────────────────────

        public void HandleSaveLoaded(SaveData saveData, bool isNewGame)
        {
            if (_hasRun) return;
            _hasRun = true;

            if (isNewGame || _config == null || _nodeConfigs == null || _nodeConfigs.Length == 0)
            {
                OnOfflineHarvestCalculated?.Invoke(0L, 0f);
                return;
            }

            float deltaSeconds   = Mathf.Max(0f,
                (float)(DateTime.UtcNow - saveData.LastSessionTimestamp).TotalSeconds);
            float capSeconds     = _config.OfflineCapHours * 3600f;
            float effectiveDelta = Mathf.Min(deltaSeconds, capSeconds);

            // Aggregate yield rate: sum of yield-per-second for each node type
            float harvestRate = 0f;
            foreach (var nc in _nodeConfigs)
            {
                if (nc == null || nc.RespawnSeconds <= 0f) continue;
                // One node yields BaseYield every RespawnSeconds
                harvestRate += nc.BaseYield / nc.RespawnSeconds;
            }

            double rawGain = harvestRate * _config.OfflineEfficiency * effectiveDelta;
            long   gain    = (long)Math.Floor(rawGain);

            if (gain > 0 && _economy != null)
                _economy.AddResources(gain);

            OnOfflineHarvestCalculated?.Invoke(gain, effectiveDelta);
        }

        // ── Test support ──────────────────────────────────────────────────────────

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public void InvokeForTesting(SaveData saveData, bool isNewGame)
        {
            _hasRun = false;
            HandleSaveLoaded(saveData, isNewGame);
        }

        public void ResetForTesting() => _hasRun = false;
#endif
    }
}

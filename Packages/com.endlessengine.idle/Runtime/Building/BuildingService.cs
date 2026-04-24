using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using EndlessEngine.Config;
using EndlessEngine.Economy;
using EndlessEngine.SaveAndLoad;

namespace EndlessEngine.Building
{
    /// <summary>
    /// Manages placed buildings: placement, upgrade, removal, passive production.
    ///
    /// Integrates with:
    ///   - EconomyService: placement costs, upgrade costs, production income
    ///   - CurrencyService (optional): for non-gold currencies
    ///   - SaveService: ISaveStateProvider
    ///   - TickEngine.OnTick: production tick (register externally)
    ///
    /// Grid is list-based by default (no spatial enforcement) — spatial
    /// collision can be added in a later sprint.
    /// </summary>
    public class BuildingService : MonoBehaviour, ISaveStateProvider
    {
        public int ProviderOrder => SaveConstants.SaveProviderOrder.Building;

        // ── Static events ─────────────────────────────────────────────────────────

        public static event Action<BuildingInstance>         OnBuildingPlaced;
        public static event Action<BuildingInstance>         OnBuildingUpgraded;
        public static event Action<string>                   OnBuildingRemoved;  // instanceId
        public static event Action<string, long>             OnBuildingProduced; // instanceId, amount
        public static event Action<string, string>           OnPlaceFailed;      // buildingId, reason

        // ── State ─────────────────────────────────────────────────────────────────

        private readonly Dictionary<string, BuildingConfigSO>  _configs   = new Dictionary<string, BuildingConfigSO>();
        private readonly Dictionary<string, BuildingInstance>   _instances = new Dictionary<string, BuildingInstance>();
        private          EconomyService                          _economy;

        // ── Initialization ────────────────────────────────────────────────────────

        public void Initialize(BuildingConfigSO[] configs, EconomyService economy = null)
        {
            _economy = economy;
            _configs.Clear();
            _instances.Clear();

            if (configs != null)
                foreach (var cfg in configs)
                    if (cfg != null && !string.IsNullOrEmpty(cfg.BuildingId))
                        _configs[cfg.BuildingId] = cfg;
        }

        // ── Placement ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Attempt to place a building. Deducts cost and fires OnBuildingPlaced.
        /// </summary>
        /// <param name="buildingId">Must match a registered BuildingConfigSO.</param>
        /// <param name="gridX">Target grid column.</param>
        /// <param name="gridY">Target grid row.</param>
        public PlaceResult TryPlace(string buildingId, int gridX, int gridY)
        {
            if (!_configs.TryGetValue(buildingId, out var config))
            {
                OnPlaceFailed?.Invoke(buildingId, "ConfigNotFound");
                return PlaceResult.Fail("ConfigNotFound");
            }

            if (config.MaxInstances > 0)
            {
                int count = _instances.Values.Count(i => i.BuildingId == buildingId);
                if (count >= config.MaxInstances)
                {
                    OnPlaceFailed?.Invoke(buildingId, "MaxInstancesReached");
                    return PlaceResult.Fail("MaxInstancesReached");
                }
            }

            if (_economy != null && config.PlacementCost > 0)
            {
                if (_economy.CurrentResources < config.PlacementCost)
                {
                    OnPlaceFailed?.Invoke(buildingId, "InsufficientFunds");
                    return PlaceResult.Fail("InsufficientFunds");
                }
                _economy.DeductResources(config.PlacementCost);
            }

            var instance = new BuildingInstance(
                System.Guid.NewGuid().ToString("N"),
                buildingId, gridX, gridY);

            _instances[instance.InstanceId] = instance;
            OnBuildingPlaced?.Invoke(instance);
            return PlaceResult.Ok(instance);
        }

        // ── Upgrade ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Upgrade a building instance to the next tier.
        /// </summary>
        public bool TryUpgrade(string instanceId)
        {
            if (!_instances.TryGetValue(instanceId, out var instance)) return false;
            if (!_configs.TryGetValue(instance.BuildingId, out var config)) return false;

            int nextTier = instance.UpgradeTier + 1;
            if (nextTier > config.UpgradeTiers.Length) return false; // already max tier

            var tierDef = config.UpgradeTiers[nextTier - 1];
            if (_economy != null && tierDef.UpgradeCost > 0)
            {
                if (_economy.CurrentResources < tierDef.UpgradeCost) return false;
                _economy.DeductResources(tierDef.UpgradeCost);
            }

            instance.ApplyUpgrade();
            OnBuildingUpgraded?.Invoke(instance);
            return true;
        }

        // ── Removal ───────────────────────────────────────────────────────────────

        public bool Remove(string instanceId)
        {
            if (!_instances.ContainsKey(instanceId)) return false;
            _instances.Remove(instanceId);
            OnBuildingRemoved?.Invoke(instanceId);
            return true;
        }

        // ── Production Tick ───────────────────────────────────────────────────────

        /// <summary>
        /// Called on TickEngine.OnTick. Accumulates production for all buildings
        /// and deposits into EconomyService.
        /// </summary>
        public void OnTick(float dt)
        {
            if (_economy == null) return;

            long totalGold = 0;
            foreach (var kv in _instances)
            {
                if (!_configs.TryGetValue(kv.Value.BuildingId, out var cfg)) continue;
                if (cfg.ProductionCurrencyId != "gold") continue; // non-gold handled later
                long produced = kv.Value.GetProductionPerTick(cfg);
                if (produced > 0)
                {
                    totalGold += produced;
                    OnBuildingProduced?.Invoke(kv.Key, produced);
                }
            }

            if (totalGold > 0)
                _economy.AddResources(totalGold);
        }

        // ── Query ─────────────────────────────────────────────────────────────────

        public bool CanPlace(string buildingId)
        {
            if (!_configs.TryGetValue(buildingId, out var config)) return false;
            if (_economy != null && _economy.CurrentResources < config.PlacementCost) return false;
            if (config.MaxInstances > 0 &&
                _instances.Values.Count(i => i.BuildingId == buildingId) >= config.MaxInstances)
                return false;
            return true;
        }

        public IReadOnlyDictionary<string, BuildingInstance> GetAllInstances() => _instances;

        public int GetInstanceCount(string buildingId)
            => _instances.Values.Count(i => i.BuildingId == buildingId);

        // ── ISaveStateProvider ────────────────────────────────────────────────────

        public void OnBeforeSave(SaveData saveData)
        {
            saveData.PlacedBuildings ??= new Dictionary<string, BuildingSaveEntry>();
            saveData.PlacedBuildings.Clear();

            foreach (var kv in _instances)
            {
                saveData.PlacedBuildings[kv.Key] = new BuildingSaveEntry
                {
                    BuildingId  = kv.Value.BuildingId,
                    GridX       = kv.Value.GridX,
                    GridY       = kv.Value.GridY,
                    UpgradeTier = kv.Value.UpgradeTier,
                };
            }
        }

        public void OnAfterLoad(SaveData saveData)
        {
            saveData.EnsureDefaults();
            _instances.Clear();

            foreach (var kv in saveData.PlacedBuildings)
            {
                var entry = kv.Value;
                var inst  = new BuildingInstance(kv.Key, entry.BuildingId, entry.GridX, entry.GridY, entry.UpgradeTier);
                _instances[kv.Key] = inst;
            }
        }

        private void OnDestroy()
        {
            ClearSubscribersForTesting();
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public static void ClearSubscribersForTesting()
        {
            OnBuildingPlaced   = null;
            OnBuildingUpgraded = null;
            OnBuildingRemoved  = null;
            OnBuildingProduced = null;
            OnPlaceFailed      = null;
        }
#else
        private static void ClearSubscribersForTesting() { }
#endif
    }

    // ── Result types ─────────────────────────────────────────────────────────────

    public class PlaceResult
    {
        public bool            Success    { get; private set; }
        public string          FailReason { get; private set; }
        public BuildingInstance Instance  { get; private set; }

        public static PlaceResult Ok(BuildingInstance inst)   => new PlaceResult { Success = true,  Instance = inst };
        public static PlaceResult Fail(string reason)          => new PlaceResult { Success = false, FailReason = reason };
    }
}

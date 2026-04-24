using System;
using System.Collections.Generic;
using UnityEngine;
using EndlessEngine.Config;
using EndlessEngine.Economy;
using EndlessEngine.SaveAndLoad;

namespace EndlessEngine.Pet
{
    /// <summary>
    /// Manages the player's pet roster: leveling, equipping, and evolution.
    ///
    /// Integrates with:
    ///   - EconomyService: level-up and evolution costs
    ///   - SaveService: ISaveStateProvider
    ///
    /// Only one pet can be equipped at a time. Passive effects are read via
    /// GetActiveEffects() — callers apply them to stats (same pattern as SkillTreeService).
    /// </summary>
    public class PetService : MonoBehaviour, ISaveStateProvider
    {
        public int ProviderOrder => SaveConstants.SaveProviderOrder.Pet;

        // ── Static events ─────────────────────────────────────────────────────────

        public static event Action<PetConfigSO>         OnPetEquipped;
        public static event Action                       OnPetUnequipped;
        public static event Action<PetConfigSO, int>     OnPetLeveledUp;     // config, new level
        public static event Action<PetConfigSO, PetConfigSO> OnPetEvolved;   // from, to
        public static event Action<string>               OnActionFailed;     // reason

        // ── State ─────────────────────────────────────────────────────────────────

        private readonly Dictionary<string, PetConfigSO> _configs  = new Dictionary<string, PetConfigSO>();
        private readonly Dictionary<string, int>          _levels   = new Dictionary<string, int>();
        private          string                            _equippedPetId = string.Empty;
        private          EconomyService                    _economy;

        // ── Initialization ────────────────────────────────────────────────────────

        public void Initialize(PetConfigSO[] configs, EconomyService economy = null)
        {
            _economy = economy;
            _configs.Clear();
            _levels.Clear();
            _equippedPetId = string.Empty;

            if (configs != null)
                foreach (var cfg in configs)
                    if (cfg != null && !string.IsNullOrEmpty(cfg.PetId))
                    {
                        _configs[cfg.PetId] = cfg;
                        _levels[cfg.PetId]  = 1; // default level 1
                    }
        }

        // ── Equip / Unequip ───────────────────────────────────────────────────────

        public bool TryEquip(string petId)
        {
            if (!_configs.ContainsKey(petId))
            {
                OnActionFailed?.Invoke("PetNotFound");
                return false;
            }

            _equippedPetId = petId;
            OnPetEquipped?.Invoke(_configs[petId]);
            return true;
        }

        public void Unequip()
        {
            _equippedPetId = string.Empty;
            OnPetUnequipped?.Invoke();
        }

        // ── Level Up ─────────────────────────────────────────────────────────────

        public bool TryLevelUp(string petId)
        {
            if (!_configs.TryGetValue(petId, out var config))
            {
                OnActionFailed?.Invoke("PetNotFound");
                return false;
            }

            int currentLevel = GetLevel(petId);
            if (currentLevel >= config.MaxLevel)
            {
                OnActionFailed?.Invoke("AlreadyMaxLevel");
                return false;
            }

            int   costIndex = currentLevel - 1; // 0-based index
            long  cost      = (config.LevelUpCosts != null && costIndex < config.LevelUpCosts.Length)
                              ? config.LevelUpCosts[costIndex]
                              : 0;

            if (_economy != null && cost > 0)
            {
                if (_economy.CurrentResources < cost)
                {
                    OnActionFailed?.Invoke("InsufficientFunds");
                    return false;
                }
                _economy.DeductResources(cost);
            }

            _levels[petId] = currentLevel + 1;
            OnPetLeveledUp?.Invoke(config, _levels[petId]);
            return true;
        }

        // ── Evolution ────────────────────────────────────────────────────────────

        public bool TryEvolve(string petId)
        {
            if (!_configs.TryGetValue(petId, out var config))
            {
                OnActionFailed?.Invoke("PetNotFound");
                return false;
            }

            if (string.IsNullOrEmpty(config.EvolvesToPetId))
            {
                OnActionFailed?.Invoke("NoEvolution");
                return false;
            }

            int currentLevel = GetLevel(petId);
            if (currentLevel < config.EvolveAtLevel)
            {
                OnActionFailed?.Invoke("LevelRequirementNotMet");
                return false;
            }

            if (!_configs.ContainsKey(config.EvolvesToPetId))
            {
                OnActionFailed?.Invoke("EvolutionTargetNotFound");
                return false;
            }

            if (_economy != null && config.EvolutionCost > 0)
            {
                if (_economy.CurrentResources < config.EvolutionCost)
                {
                    OnActionFailed?.Invoke("InsufficientFunds");
                    return false;
                }
                _economy.DeductResources(config.EvolutionCost);
            }

            var evolvedConfig = _configs[config.EvolvesToPetId];

            // Carry over level to new form (capped at evolved config MaxLevel)
            int carryLevel   = Math.Min(currentLevel, evolvedConfig.MaxLevel);
            _levels[config.EvolvesToPetId] = carryLevel;

            // Swap equipped pet if this one was equipped
            if (_equippedPetId == petId)
            {
                _equippedPetId = config.EvolvesToPetId;
                OnPetEquipped?.Invoke(evolvedConfig);
            }

            OnPetEvolved?.Invoke(config, evolvedConfig);
            return true;
        }

        // ── Query ─────────────────────────────────────────────────────────────────

        public int GetLevel(string petId)
            => _levels.TryGetValue(petId, out int l) ? l : 0;

        public bool IsEquipped(string petId) => _equippedPetId == petId;

        public PetConfigSO GetEquippedConfig()
        {
            if (string.IsNullOrEmpty(_equippedPetId)) return null;
            _configs.TryGetValue(_equippedPetId, out var cfg);
            return cfg;
        }

        /// <summary>
        /// All active passive effects from the currently equipped pet at its current level.
        /// Returns empty list if no pet is equipped.
        /// </summary>
        public IReadOnlyList<SkillEffect> GetActiveEffects()
        {
            var results = new List<SkillEffect>();
            if (string.IsNullOrEmpty(_equippedPetId)) return results;
            if (!_configs.TryGetValue(_equippedPetId, out var config)) return results;

            results.AddRange(config.BaseEffects);

            int level = GetLevel(_equippedPetId);
            if (config.LevelBonuses != null)
            {
                for (int i = 0; i < Math.Min(level - 1, config.LevelBonuses.Count); i++)
                    results.AddRange(config.LevelBonuses[i].AdditionalEffects);
            }

            return results;
        }

        // ── ISaveStateProvider ────────────────────────────────────────────────────

        public void OnBeforeSave(SaveData saveData)
        {
            saveData.EquippedPetId = _equippedPetId ?? string.Empty;
            saveData.PetLevels    ??= new System.Collections.Generic.Dictionary<string, int>();
            saveData.PetLevels.Clear();
            foreach (var kv in _levels)
                saveData.PetLevels[kv.Key] = kv.Value;
        }

        public void OnAfterLoad(SaveData saveData)
        {
            saveData.EnsureDefaults();
            _equippedPetId = saveData.EquippedPetId ?? string.Empty;
            _levels.Clear();

            // Reset all known pets to level 1
            foreach (var id in _configs.Keys)
                _levels[id] = 1;

            // Apply saved levels
            if (saveData.PetLevels != null)
                foreach (var kv in saveData.PetLevels)
                    if (_configs.ContainsKey(kv.Key))
                        _levels[kv.Key] = kv.Value;
        }

        private void OnDestroy()
        {
            ClearSubscribersForTesting();
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public static void ClearSubscribersForTesting()
        {
            OnPetEquipped    = null;
            OnPetUnequipped  = null;
            OnPetLeveledUp   = null;
            OnPetEvolved     = null;
            OnActionFailed   = null;
        }
#else
        private static void ClearSubscribersForTesting() { }
#endif
    }
}

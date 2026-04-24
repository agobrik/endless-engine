using System;
using System.Collections.Generic;
using UnityEngine;
using EndlessEngine.Config;
using EndlessEngine.Economy;
using EndlessEngine.Flow;
using EndlessEngine.Input;
using EndlessEngine.SaveAndLoad;

namespace EndlessEngine.Modules
{
    /// <summary>
    /// Module: Zone / Region System
    /// Manages world-space zones that produce gold when active.
    ///
    /// Each zone can run in two modes (set per ZoneConfigSO):
    ///   PassiveMode = true  → yields every tick regardless of cursor
    ///   PassiveMode = false → yields only while cursor is inside the zone
    ///                         (hover multiplier applies in both modes when cursor is inside)
    ///
    /// Zones must be purchased before they activate. Zones can also be upgraded
    /// (each upgrade level multiplies yield by YieldMultiplierPerUpgrade).
    ///
    /// Integrates with ISaveStateProvider to persist unlock and upgrade state.
    ///
    /// Bootstrap wiring:
    ///   var zones = gameObject.AddComponent&lt;ZoneSystem&gt;();
    ///   zones.Initialize(zoneDatabaseSO, economyService, gameFlow, inputProvider, saveNotifier);
    ///   SaveService.RegisterStateProvider(zones);
    /// </summary>
    public class ZoneSystem : MonoBehaviour, ISaveStateProvider
    {
        // ── ISaveStateProvider ────────────────────────────────────────────────────

        public int ProviderOrder => 60; // After Generator (50)

        // ── Events ────────────────────────────────────────────────────────────────

        /// <summary>Fires when a zone is successfully purchased. Parameter: zoneId.</summary>
        public static event Action<string> OnZoneUnlocked;

        /// <summary>Fires when a zone is upgraded. Parameters: (zoneId, newLevel).</summary>
        public static event Action<string, int> OnZoneUpgraded;

        /// <summary>Fires when cursor enters a zone. Parameter: zoneId.</summary>
        public static event Action<string> OnZoneEntered;

        /// <summary>Fires when cursor exits a zone. Parameter: zoneId.</summary>
        public static event Action<string> OnZoneExited;

        // ── Dependencies ──────────────────────────────────────────────────────────

        private ZoneConfigSO[]       _configs;
        private EconomyService       _economy;
        private GameFlowStateMachine _gameFlow;
        private IInputProvider       _input;
        private ISaveNotifier        _saveNotifier;

        // ── Runtime state ─────────────────────────────────────────────────────────

        private readonly Dictionary<string, ZoneRuntimeState> _states =
            new Dictionary<string, ZoneRuntimeState>();
        private readonly Dictionary<string, ZoneConfigSO> _lookup =
            new Dictionary<string, ZoneConfigSO>();

        // cursor tracking
        private readonly HashSet<string> _cursorInsideZones = new HashSet<string>();

        /// <summary>Total gold earned by all zones.</summary>
        public long TotalZoneEarned { get; private set; }

        // ── Initialization ────────────────────────────────────────────────────────

        public void Initialize(
            ZoneConfigSO[]       configs,
            EconomyService       economy,
            GameFlowStateMachine gameFlow,
            IInputProvider       input,
            ISaveNotifier        saveNotifier)
        {
            _configs      = configs;
            _economy      = economy;
            _gameFlow     = gameFlow;
            _input        = input;
            _saveNotifier = saveNotifier;

            _lookup.Clear();
            foreach (var cfg in _configs)
            {
                if (cfg == null || string.IsNullOrEmpty(cfg.ZoneId)) continue;
                _lookup[cfg.ZoneId] = cfg;
                if (!_states.ContainsKey(cfg.ZoneId))
                {
                    _states[cfg.ZoneId] = new ZoneRuntimeState
                    {
                        ZoneId    = cfg.ZoneId,
                        Unlocked  = cfg.UnlockCost == 0 && string.IsNullOrEmpty(cfg.PrerequisiteZoneId),
                        Level     = 0,
                    };
                }
            }
        }

        private void OnEnable()  => TickEngine.OnTick += HandleTick;
        private void OnDisable() => TickEngine.OnTick -= HandleTick;

        // ── Frame Update — cursor tracking ────────────────────────────────────────

        private void Update()
        {
            if (_input == null || _configs == null) return;

            Vector2 worldPos = _input.GetMouseWorldPosition();

            foreach (var cfg in _configs)
            {
                if (cfg == null) continue;
                bool wasInside = _cursorInsideZones.Contains(cfg.ZoneId);
                bool isInside  = IsPointInZone(worldPos, cfg);

                if (isInside && !wasInside)
                {
                    _cursorInsideZones.Add(cfg.ZoneId);
                    OnZoneEntered?.Invoke(cfg.ZoneId);
                }
                else if (!isInside && wasInside)
                {
                    _cursorInsideZones.Remove(cfg.ZoneId);
                    OnZoneExited?.Invoke(cfg.ZoneId);
                }
            }
        }

        // ── Tick Handler ──────────────────────────────────────────────────────────

        private void HandleTick(float effectiveDt)
        {
            if (_configs == null || _economy == null) return;

            float runModifier = GetRunModifier();

            foreach (var cfg in _configs)
            {
                if (cfg == null) continue;
                if (!_states.TryGetValue(cfg.ZoneId, out var state) || !state.Unlocked) continue;

                bool cursorInside = _cursorInsideZones.Contains(cfg.ZoneId);

                // Decide whether to yield this tick
                bool shouldYield = cfg.PassiveMode || cursorInside;
                if (!shouldYield) continue;

                float hoverMult  = cursorInside ? cfg.ActiveHoverMultiplier : 1f;
                float upgradeMult = 1f + cfg.YieldMultiplierPerUpgrade * state.Level;
                float yield      = cfg.YieldPerSecond * hoverMult * upgradeMult * runModifier;

                long earned = (long)(yield * effectiveDt);
                if (earned <= 0) continue;

                _economy.AddResources(earned);
                TotalZoneEarned += earned;
            }
        }

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>
        /// Attempt to purchase/unlock a zone. Deducts cost from economy on success.
        /// </summary>
        public bool TryUnlock(string zoneId)
        {
            if (!_lookup.TryGetValue(zoneId, out var cfg)) return false;
            if (!_states.TryGetValue(zoneId, out var state)) return false;
            if (state.Unlocked) return false;

            // Check prerequisite zone
            if (!string.IsNullOrEmpty(cfg.PrerequisiteZoneId))
            {
                if (!_states.TryGetValue(cfg.PrerequisiteZoneId, out var prereq) || !prereq.Unlocked)
                    return false;
            }

            // Check prestige requirement
            // (prestige count read from SaveData via injected getter in production; 0 = always pass)
            if (cfg.PrestigeRequirement > 0 && _prestigeCountGetter != null
                && _prestigeCountGetter() < cfg.PrestigeRequirement)
                return false;

            if (cfg.UnlockCost > 0 && _economy.CurrentResources < cfg.UnlockCost)
                return false;

            if (cfg.UnlockCost > 0)
                _economy.DeductResources(cfg.UnlockCost);

            state.Unlocked = true;
            _states[zoneId] = state;
            _saveNotifier?.NotifyUpgradePurchased();
            OnZoneUnlocked?.Invoke(zoneId);
            return true;
        }

        /// <summary>
        /// Attempt to upgrade a zone by one level.
        /// Cost = UnlockCost * UpgradeCostScalingFactor^currentLevel.
        /// </summary>
        public bool TryUpgrade(string zoneId)
        {
            if (!_lookup.TryGetValue(zoneId, out var cfg)) return false;
            if (!_states.TryGetValue(zoneId, out var state) || !state.Unlocked) return false;
            if (cfg.MaxUpgradeLevel > 0 && state.Level >= cfg.MaxUpgradeLevel) return false;

            long upgradeCost = (long)(cfg.UnlockCost
                               * Math.Pow(cfg.UpgradeCostScalingFactor, state.Level + 1));
            upgradeCost = Math.Max(upgradeCost, 1);

            if (_economy.CurrentResources < upgradeCost) return false;

            _economy.DeductResources(upgradeCost);
            state.Level++;
            _states[zoneId] = state;
            _saveNotifier?.NotifyUpgradePurchased();
            OnZoneUpgraded?.Invoke(zoneId, state.Level);
            return true;
        }

        /// <summary>Returns the upgrade cost for the next level of a zone.</summary>
        public long GetUpgradeCost(string zoneId)
        {
            if (!_lookup.TryGetValue(zoneId, out var cfg)) return -1;
            if (!_states.TryGetValue(zoneId, out var state)) return -1;
            long cost = (long)(cfg.UnlockCost * Math.Pow(cfg.UpgradeCostScalingFactor, state.Level + 1));
            return Math.Max(cost, 1);
        }

        /// <summary>True if the cursor is currently inside the given zone.</summary>
        public bool IsCursorInZone(string zoneId) => _cursorInsideZones.Contains(zoneId);

        /// <summary>Returns runtime state for a zone. Null if unknown.</summary>
        public ZoneRuntimeState GetState(string zoneId)
            => _states.TryGetValue(zoneId, out var s) ? s : null;

        /// <summary>Read-only view of all zone configs.</summary>
        public IReadOnlyList<ZoneConfigSO> Configs => _configs;

        // ── Prestige count injection (optional) ───────────────────────────────────

        private Func<int> _prestigeCountGetter;

        /// <summary>
        /// Inject a function that returns the current prestige count.
        /// Required only if any zone uses PrestigeRequirement > 0.
        /// </summary>
        public void SetPrestigeCountGetter(Func<int> getter) => _prestigeCountGetter = getter;

        // ── ISaveStateProvider ────────────────────────────────────────────────────

        public void OnBeforeSave(SaveData saveData)
        {
            if (saveData.ZoneStates == null)
                saveData.ZoneStates = new Dictionary<string, ZoneRuntimeState>();
            foreach (var kv in _states)
                saveData.ZoneStates[kv.Key] = kv.Value;
        }

        public void OnAfterLoad(SaveData saveData)
        {
            if (saveData.ZoneStates == null) return;
            foreach (var kv in saveData.ZoneStates)
                if (_states.ContainsKey(kv.Key))
                    _states[kv.Key] = kv.Value;
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static bool IsPointInZone(Vector2 point, ZoneConfigSO cfg)
        {
            switch (cfg.Shape)
            {
                case ZoneShape.Circle:
                    return Vector2.Distance(point, cfg.WorldCenter) <= cfg.Size.x;

                case ZoneShape.Rectangle:
                    float halfW = cfg.Size.x;
                    float halfH = cfg.Size.y;
                    return Mathf.Abs(point.x - cfg.WorldCenter.x) <= halfW
                        && Mathf.Abs(point.y - cfg.WorldCenter.y) <= halfH;

                default:
                    return false;
            }
        }

        private float GetRunModifier()
        {
            if (_gameFlow == null || !_gameFlow.IsInRun) return 1f;
            try { return ConfigRegistry.Run.ActiveRunPassiveModifier; } catch { return 0.5f; }
        }

        // ── Test Support ──────────────────────────────────────────────────────────

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public void UnlockForTesting(string zoneId)
        {
            if (_states.TryGetValue(zoneId, out var s)) { s.Unlocked = true; _states[zoneId] = s; }
        }

        public static void ClearSubscribersForTesting()
        {
            OnZoneUnlocked = null;
            OnZoneUpgraded = null;
            OnZoneEntered  = null;
            OnZoneExited   = null;
        }
#endif
    }

    /// <summary>Runtime + save state for a single zone.</summary>
    [Serializable]
    public class ZoneRuntimeState
    {
        public string ZoneId;
        public bool   Unlocked;
        public int    Level;
    }
}

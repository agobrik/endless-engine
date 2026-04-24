using System;
using System.Collections.Generic;
using UnityEngine;
using EndlessEngine.Config;
using EndlessEngine.SaveAndLoad;

namespace EndlessEngine.Statistics
{
    /// <summary>
    /// Tracks lifetime player statistics (total gold earned, total kills, total prestiges, etc.).
    /// Statistics are keyed by StatDefinitionSO.StatId.
    ///
    /// Two modes per stat (controlled by StatDefinitionSO.IsPeakValue):
    ///   - Counter (default): Add() accumulates the total.
    ///   - Peak:              SetIfHigher() replaces the value when the new value is larger.
    ///
    /// Persists via ISaveStateProvider. Registered as provider order Statistics = 85
    /// (after Milestone).
    ///
    /// Consumers subscribe to OnStatChanged for reactive UI.
    ///
    /// Bootstrap wiring:
    ///   statisticsService.Initialize(statDefs);
    ///   saveService.RegisterStateProvider(statisticsService);
    /// </summary>
    public class StatisticsService : MonoBehaviour, ISaveStateProvider
    {
        // ── ISaveStateProvider ────────────────────────────────────────────────────

        public int ProviderOrder => SaveConstants.SaveProviderOrder.Statistics;

        // ── Events ────────────────────────────────────────────────────────────────

        /// <summary>Fires when any stat value changes. Parameters: statId, newValue.</summary>
        public static event Action<string, double> OnStatChanged;

        // ── Runtime state ─────────────────────────────────────────────────────────

        private readonly Dictionary<string, double>           _values  = new Dictionary<string, double>();
        private readonly Dictionary<string, StatDefinitionSO> _defs    = new Dictionary<string, StatDefinitionSO>();

        // ── Initialization ────────────────────────────────────────────────────────

        /// <summary>
        /// Initializes the service with the given stat definitions.
        /// Call before RegisterStateProvider and LoadAsync.
        /// </summary>
        public void Initialize(StatDefinitionSO[] defs)
        {
            _defs.Clear();
            _values.Clear();
            if (defs == null) return;
            foreach (var d in defs)
                if (d != null && !string.IsNullOrEmpty(d.StatId))
                {
                    _defs[d.StatId] = d;
                    _values[d.StatId] = 0;
                }
        }

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>
        /// Adds delta to the counter stat. Silently ignores unknown stat IDs.
        /// Ignored if IsPeakValue is true for this stat — use SetIfHigher instead.
        /// </summary>
        public void Add(string statId, double delta)
        {
            if (delta <= 0) return;
            if (!_defs.TryGetValue(statId, out var def)) return;
            if (def.IsPeakValue) return;

            _values[statId] = _values.TryGetValue(statId, out var cur) ? cur + delta : delta;
            OnStatChanged?.Invoke(statId, _values[statId]);
        }

        /// <summary>
        /// Updates a peak stat if the new value is higher than the current record.
        /// Silently ignores unknown stat IDs or non-peak stats.
        /// </summary>
        public void SetIfHigher(string statId, double value)
        {
            if (!_defs.TryGetValue(statId, out var def)) return;
            if (!def.IsPeakValue) return;

            double cur = _values.TryGetValue(statId, out var v) ? v : 0;
            if (value <= cur) return;

            _values[statId] = value;
            OnStatChanged?.Invoke(statId, value);
        }

        /// <summary>Returns the current value for the given stat (0 if unknown).</summary>
        public double Get(string statId) =>
            _values.TryGetValue(statId, out var v) ? v : 0;

        /// <summary>Returns a snapshot of all tracked stats.</summary>
        public IReadOnlyDictionary<string, double> GetAll() => _values;

        /// <summary>Returns the display name of a stat, or the statId if not found.</summary>
        public string GetDisplayName(string statId) =>
            _defs.TryGetValue(statId, out var d) ? d.DisplayName : statId;

        // ── ISaveStateProvider ────────────────────────────────────────────────────

        public void OnBeforeSave(SaveData saveData)
        {
            saveData.StatisticsValues ??= new Dictionary<string, double>();
            saveData.StatisticsValues.Clear();
            foreach (var kv in _values)
                saveData.StatisticsValues[kv.Key] = kv.Value;
        }

        public void OnAfterLoad(SaveData saveData)
        {
            if (saveData.StatisticsValues == null) return;
            foreach (var kv in saveData.StatisticsValues)
                if (_defs.ContainsKey(kv.Key))
                    _values[kv.Key] = kv.Value;
        }

        // ── Test helpers ──────────────────────────────────────────────────────────

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public static void ClearSubscribersForTesting() => OnStatChanged = null;

        public void ResetForTesting()
        {
            foreach (var key in new List<string>(_values.Keys))
                _values[key] = 0;
        }
#endif
    }
}

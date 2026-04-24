using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using EndlessEngine.Config;
using EndlessEngine.SaveAndLoad;

namespace EndlessEngine.UnlockLog
{
    /// <summary>
    /// Tracks all discoverable entries (buildings, pets, items, milestones, systems).
    /// Fires OnEntryUnlocked when a new entry is discovered.
    ///
    /// Entries can be unlocked from any system:
    ///   unlockLog.Unlock("ore_t0");
    ///   unlockLog.Unlock("pet_fox");
    ///   unlockLog.Unlock("milestone_wave_50");
    ///
    /// Query:
    ///   unlockLog.IsUnlocked("ore_t0")           → true/false
    ///   unlockLog.GetAll()                        → all known entries with unlock state
    ///   unlockLog.GetUnlocked(UnlockCategory.Pet) → all unlocked pet entries
    /// </summary>
    public class UnlockLogService : MonoBehaviour, ISaveStateProvider
    {
        public int ProviderOrder => SaveConstants.SaveProviderOrder.UnlockLog;

        // ── Static events ─────────────────────────────────────────────────────────

        public static event Action<UnlockEntryConfigSO> OnEntryUnlocked;

        // ── State ─────────────────────────────────────────────────────────────────

        private readonly Dictionary<string, UnlockEntryConfigSO> _configs  = new Dictionary<string, UnlockEntryConfigSO>();
        private readonly HashSet<string>                          _unlocked = new HashSet<string>();

        // ── Initialization ────────────────────────────────────────────────────────

        public void Initialize(UnlockEntryConfigSO[] entries)
        {
            _configs.Clear();
            _unlocked.Clear();

            if (entries != null)
                foreach (var e in entries)
                    if (e != null && !string.IsNullOrEmpty(e.EntryId))
                        _configs[e.EntryId] = e;
        }

        // ── Unlock ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Unlock an entry. Silently ignored if already unlocked or unknown.
        /// Fires OnEntryUnlocked only on first discovery.
        /// </summary>
        public void Unlock(string entryId)
        {
            if (string.IsNullOrEmpty(entryId))       return;
            if (_unlocked.Contains(entryId))          return;
            if (!_configs.ContainsKey(entryId))       return; // unknown entry — not tracked

            _unlocked.Add(entryId);
            OnEntryUnlocked?.Invoke(_configs[entryId]);
        }

        /// <summary>
        /// Register an entry dynamically (for runtime-generated entries without a config).
        /// Config will be null for dynamic entries — callers check IsUnlocked only.
        /// </summary>
        public void UnlockDynamic(string entryId)
        {
            if (string.IsNullOrEmpty(entryId)) return;
            _unlocked.Add(entryId);
        }

        // ── Query ─────────────────────────────────────────────────────────────────

        public bool IsUnlocked(string entryId) => _unlocked.Contains(entryId);

        public int TotalUnlocked => _unlocked.Count;

        public IReadOnlyCollection<string> GetAllUnlockedIds() => _unlocked;

        /// <summary>All defined entries with their unlock state.</summary>
        public IReadOnlyDictionary<string, bool> GetAll()
        {
            var result = new Dictionary<string, bool>(_configs.Count);
            foreach (var id in _configs.Keys)
                result[id] = _unlocked.Contains(id);
            return result;
        }

        /// <summary>Unlocked entries in a specific category (requires config definition).</summary>
        public IReadOnlyList<UnlockEntryConfigSO> GetUnlocked(UnlockCategory category)
            => _configs.Values
                       .Where(e => e.Category == category && _unlocked.Contains(e.EntryId))
                       .ToList();

        /// <summary>
        /// All defined entries visible to the player given unlock state.
        /// Hidden entries are excluded until unlocked (unless IsHiddenUntilUnlocked = false).
        /// </summary>
        public IReadOnlyList<(UnlockEntryConfigSO Config, bool IsUnlocked)> GetVisible()
        {
            var result = new List<(UnlockEntryConfigSO, bool)>();
            foreach (var e in _configs.Values)
            {
                bool unlocked = _unlocked.Contains(e.EntryId);
                if (!unlocked && e.IsHiddenUntilUnlocked) continue;
                result.Add((e, unlocked));
            }
            return result;
        }

        // ── ISaveStateProvider ────────────────────────────────────────────────────

        public void OnBeforeSave(SaveData saveData)
        {
            saveData.UnlockLogEntries ??= new HashSet<string>();
            saveData.UnlockLogEntries.Clear();
            foreach (var id in _unlocked)
                saveData.UnlockLogEntries.Add(id);
        }

        public void OnAfterLoad(SaveData saveData)
        {
            saveData.EnsureDefaults();
            _unlocked.Clear();
            if (saveData.UnlockLogEntries != null)
                foreach (var id in saveData.UnlockLogEntries)
                    _unlocked.Add(id);
        }

        private void OnDestroy()
        {
            ClearSubscribersForTesting();
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public static void ClearSubscribersForTesting()
        {
            OnEntryUnlocked = null;
        }
#else
        private static void ClearSubscribersForTesting() { }
#endif
    }
}

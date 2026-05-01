using System;
using System.Collections.Generic;
using UnityEngine;
using EndlessEngine.SaveAndLoad;
using Debug = UnityEngine.Debug;

namespace EndlessEngine.Unlock
{
    /// <summary>
    /// Evaluates registered IUnlockCondition instances and fires OnEntryUnlocked
    /// the first time each condition becomes true.
    ///
    /// Unlock state persists via SaveData.CompletedMilestones with "unlock:" prefix.
    ///
    /// Bootstrap wiring:
    ///   unlockService.Register(new PrestigeCountUnlockCondition("realm_2", prestigeManager, 3));
    ///   // Call Check() after any event that might satisfy a condition.
    /// </summary>
    public class ConditionalUnlockService : MonoBehaviour, ISaveStateProvider
    {
        public int ProviderOrder => SaveConstants.SaveProviderOrder.Milestone + 15;

        // ── Events ────────────────────────────────────────────────────────────────

        /// <summary>Fires when an entry is unlocked for the first time. Parameter: entryId.</summary>
        public static event Action<string> OnEntryUnlocked;

        // ── State ─────────────────────────────────────────────────────────────────

        private readonly Dictionary<string, IUnlockCondition> _conditions = new();
        private readonly HashSet<string>                      _unlocked   = new();

        // ── Registration ──────────────────────────────────────────────────────────

        /// <summary>
        /// Registers a condition. Safe to call multiple times for the same entryId
        /// (replaces previous registration). Already-unlocked entries are skipped.
        /// </summary>
        public void Register(IUnlockCondition condition)
        {
            if (condition == null || string.IsNullOrEmpty(condition.EntryId)) return;
            _conditions[condition.EntryId] = condition;
        }

        // ── Evaluation ────────────────────────────────────────────────────────────

        /// <summary>
        /// Evaluates all registered conditions. Call after any event that might
        /// satisfy an unlock (prestige, wave complete, purchase, etc.).
        /// </summary>
        public void Check()
        {
            foreach (var kv in _conditions)
            {
                string entryId = kv.Key;
                if (_unlocked.Contains(entryId)) continue;
                if (!kv.Value.IsMet) continue;
                _unlocked.Add(entryId);
                OnEntryUnlocked?.Invoke(entryId);
                Debug.Log($"[ConditionalUnlockService] Unlocked: {entryId}");
            }
        }

        /// <summary>True if the entry has been unlocked.</summary>
        public bool IsUnlocked(string entryId) => _unlocked.Contains(entryId);

        // ── ISaveStateProvider ────────────────────────────────────────────────────

        public void OnBeforeSave(SaveData saveData)
        {
            saveData.CompletedMilestones ??= new HashSet<string>();
            foreach (var id in _unlocked)
                saveData.CompletedMilestones.Add($"unlock:{id}");
        }

        public void OnAfterLoad(SaveData saveData)
        {
            _unlocked.Clear();
            if (saveData.CompletedMilestones == null) return;
            const string prefix = "unlock:";
            foreach (var entry in saveData.CompletedMilestones)
                if (entry.StartsWith(prefix))
                    _unlocked.Add(entry.Substring(prefix.Length));
        }

        // ── Test helpers ──────────────────────────────────────────────────────────

        private void OnDestroy() => ClearSubscribersForTesting();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public static void ClearSubscribersForTesting() => OnEntryUnlocked = null;
        public bool IsUnlockedForTesting(string id) => IsUnlocked(id);
        public void ForceUnlockForTesting(string entryId) => _unlocked.Add(entryId);
#else
        private static void ClearSubscribersForTesting() { }
#endif
    }
}

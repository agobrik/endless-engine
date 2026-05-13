using System;
using System.Collections.Generic;
using UnityEngine;
using EndlessEngine.Economy;
using EndlessEngine.SaveAndLoad;
using Debug = UnityEngine.Debug;

namespace EndlessEngine.Quest
{
    /// <summary>
    /// Engine-level quest service. Tracks active quests, evaluates conditions,
    /// distributes rewards, and persists state via ISaveStateProvider.
    ///
    /// Engine provides: infrastructure (tracking, save, events, reward dispatch).
    /// Game provides: QuestConfigSOs (what quests exist) and IQuestCondition
    ///               implementations (when they're met).
    ///
    /// Bootstrap wiring:
    ///   questService.Initialize(configs, economyService);
    ///   questService.RegisterCondition(new ReachWaveCondition(waveManager, questId, 10));
    ///   // Register all conditions before calling Check() or Load.
    ///
    /// Evaluation: call Check() after any event that might satisfy a condition.
    /// The service is deliberately not event-driven at the engine level — game code
    /// decides when to poll (e.g. on wave complete, on purchase, on prestige).
    /// </summary>
    public class QuestService : MonoBehaviour, ISaveStateProvider
    {
        // ── ISaveStateProvider ────────────────────────────────────────────────────

        public int ProviderOrder => SaveConstants.SaveProviderOrder.Milestone + 5;

        // ── Events ────────────────────────────────────────────────────────────────

        /// <summary>Fires when a quest is completed. Carries the completed config.</summary>
        public static event Action<QuestConfigSO> OnQuestCompleted;

        /// <summary>Fires when a repeatable quest resets and becomes available again.</summary>
#pragma warning disable CS0067
        public static event Action<QuestConfigSO> OnQuestReset;
#pragma warning restore CS0067

        // ── State ─────────────────────────────────────────────────────────────────

        private QuestConfigSO[]                          _configs;
        private EconomyService                           _economy;
        private CurrencyService                          _currency;

        // All registered conditions indexed by ConditionId
        private readonly Dictionary<string, IQuestCondition> _conditions = new Dictionary<string, IQuestCondition>();

        // Completed quest IDs (persisted)
        private readonly HashSet<string> _completed = new HashSet<string>();

        // Repeatable cooldown end times (UTC ticks, not persisted — resets on restart)
        private readonly Dictionary<string, DateTime> _cooldownEnds = new Dictionary<string, DateTime>();

        // Last completion time for scheduled quests (persisted via save key "quest_ts:{id}")
        private readonly Dictionary<string, DateTime> _lastCompleted = new Dictionary<string, DateTime>();

        private bool _initialized;

        // ── Bootstrap ─────────────────────────────────────────────────────────────

        /// <summary>
        /// <paramref name="currency"/> is optional — pass null if no secondary currencies.
        /// </summary>
        public void Initialize(QuestConfigSO[] configs, EconomyService economy, CurrencyService currency = null)
        {
            _configs     = configs ?? Array.Empty<QuestConfigSO>();
            _economy     = economy;
            _currency    = currency;
            _initialized = true;
        }

        /// <summary>
        /// Registers a runtime condition. Call after Initialize() and before the first
        /// Check(). Conditions can be re-registered safely (overwrites prior entry).
        /// </summary>
        public void RegisterCondition(IQuestCondition condition)
        {
            if (condition == null) return;
            _conditions[condition.ConditionId] = condition;
        }

        // ── Evaluation ────────────────────────────────────────────────────────────

        /// <summary>
        /// Evaluates all active quest conditions. Call after any game event that
        /// might satisfy a quest (wave complete, purchase, prestige, etc.).
        /// Completes quests whose all conditions are met and distributes rewards.
        /// </summary>
        public void Check()
        {
            if (!_initialized || _configs == null) return;

            foreach (var config in _configs)
            {
                if (config == null) continue;
                if (!IsAvailable(config)) continue;
                if (AllConditionsMet(config))
                    CompleteQuest(config);
            }
        }

        // ── Query ─────────────────────────────────────────────────────────────────

        /// <summary>True if the quest has been completed at least once this run.</summary>
        public bool IsCompleted(string questId) => _completed.Contains(questId);

        /// <summary>
        /// Returns aggregate 0–1 progress across all conditions of a quest.
        /// Returns 1.0 if already completed.
        /// </summary>
        public float GetProgress(string questId)
        {
            if (_completed.Contains(questId)) return 1f;

            var config = FindConfig(questId);
            if (config == null || config.ConditionIds.Count == 0) return 0f;

            float total = 0f;
            foreach (var id in config.ConditionIds)
            {
                if (_conditions.TryGetValue(id, out var cond))
                    total += cond.Progress;
            }
            return total / config.ConditionIds.Count;
        }

        // ── ISaveStateProvider ────────────────────────────────────────────────────

        public void OnBeforeSave(SaveData saveData)
        {
            saveData.CompletedMilestones ??= new HashSet<string>();
            foreach (var id in _completed)
                saveData.CompletedMilestones.Add($"quest:{id}");
            foreach (var kv in _lastCompleted)
                saveData.CompletedMilestones.Add($"quest_ts:{kv.Key}={kv.Value.Ticks}");
        }

        public void OnAfterLoad(SaveData saveData)
        {
            _completed.Clear();
            _lastCompleted.Clear();
            if (saveData.CompletedMilestones == null) return;
            const string qPrefix  = "quest:";
            const string tsPrefix = "quest_ts:";
            foreach (var entry in saveData.CompletedMilestones)
            {
                if (entry.StartsWith(tsPrefix))
                {
                    var body = entry.Substring(tsPrefix.Length);
                    int sep = body.LastIndexOf('=');
                    if (sep > 0 && long.TryParse(body.Substring(sep + 1), out long ticks))
                        _lastCompleted[body.Substring(0, sep)] = new DateTime(ticks, DateTimeKind.Utc);
                }
                else if (entry.StartsWith(qPrefix))
                {
                    _completed.Add(entry.Substring(qPrefix.Length));
                }
            }
        }

        // ── Private ───────────────────────────────────────────────────────────────

        private bool IsAvailable(QuestConfigSO config)
        {
            if (!config.Repeatable && _completed.Contains(config.QuestId)) return false;

            if (config.Repeatable)
            {
                if (config.ScheduleType != QuestScheduleType.None)
                {
                    // Window-based: available if last completion is before the current window start
                    if (_lastCompleted.TryGetValue(config.QuestId, out var last))
                        if (last >= GetCurrentWindowStart(config.ScheduleType)) return false;
                }
                else if (_cooldownEnds.TryGetValue(config.QuestId, out var until))
                {
                    if (DateTime.UtcNow < until) return false;
                }
            }

            return true;
        }

        private static DateTime GetCurrentWindowStart(QuestScheduleType type)
        {
            var now = DateTime.UtcNow;
            if (type == QuestScheduleType.Daily)
                return now.Date; // today's 00:00 UTC

            // Weekly: this Monday's 00:00 UTC
            int daysSinceMonday = ((int)now.DayOfWeek + 6) % 7; // Monday=0
            return now.Date.AddDays(-daysSinceMonday);
        }

        private bool AllConditionsMet(QuestConfigSO config)
        {
            if (config.ConditionIds.Count == 0) return false;
            foreach (var id in config.ConditionIds)
            {
                if (!_conditions.TryGetValue(id, out var cond) || !cond.IsMet)
                    return false;
            }
            return true;
        }

        private void CompleteQuest(QuestConfigSO config)
        {
            if (!config.Repeatable)
            {
                _completed.Add(config.QuestId);
            }
            else
            {
                if (config.ScheduleType != QuestScheduleType.None)
                    _lastCompleted[config.QuestId] = DateTime.UtcNow;
                else if (config.RepeatCooldownSeconds > 0f)
                    _cooldownEnds[config.QuestId] = DateTime.UtcNow.AddSeconds(config.RepeatCooldownSeconds);
            }

            if (config.GoldReward > 0)
                _economy?.AddResources(config.GoldReward);

            if (!string.IsNullOrEmpty(config.RewardCurrencyId) && config.RewardCurrencyAmount > 0)
                _currency?.Add(config.RewardCurrencyId, config.RewardCurrencyAmount);

            OnQuestCompleted?.Invoke(config);
            Debug.Log($"[QuestService] Quest completed: {config.QuestId}");
        }

        private QuestConfigSO FindConfig(string questId)
        {
            if (_configs == null) return null;
            foreach (var c in _configs)
                if (c != null && c.QuestId == questId) return c;
            return null;
        }

        private void OnDestroy() => ClearSubscribersForTesting();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public static void ClearSubscribersForTesting()
        {
            OnQuestCompleted = null;
        }

        public void InjectCompletedForTesting(string questId) => _completed.Add(questId);
        public bool IsCompletedForTesting(string questId)     => IsCompleted(questId);
        public float GetProgressForTesting(string questId)    => GetProgress(questId);

        /// <summary>
        /// Directly injects a last-completion time for scheduled-quest tests.
        /// Allows simulating whether a quest was completed within the current UTC window.
        /// </summary>
        public void InjectLastCompletedForTesting(string questId, DateTime utcTime)
            => _lastCompleted[questId] = utcTime;

        /// <summary>Reads back the last-completed time for a scheduled quest (for save assertions).</summary>
        public bool TryGetLastCompletedForTesting(string questId, out DateTime utcTime)
            => _lastCompleted.TryGetValue(questId, out utcTime);
#else
        private static void ClearSubscribersForTesting() { }
#endif
    }
}

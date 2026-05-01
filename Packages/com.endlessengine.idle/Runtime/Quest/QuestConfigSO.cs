using System;
using System.Collections.Generic;
using UnityEngine;

namespace EndlessEngine.Quest
{
    /// <summary>UTC reset window for scheduled repeatable quests.</summary>
    public enum QuestScheduleType
    {
        /// <summary>No schedule — uses RepeatCooldownSeconds if Repeatable.</summary>
        None,
        /// <summary>Resets at 00:00 UTC every day.</summary>
        Daily,
        /// <summary>Resets at 00:00 UTC every Monday.</summary>
        Weekly,
    }


    /// <summary>
    /// Designer-facing config for a single quest.
    ///
    /// Conditions are registered at runtime via QuestService.RegisterCondition()
    /// rather than embedded in the SO — this keeps the SO serializable without
    /// forward-references to game code.
    ///
    /// The SO stores identity, display data, rewards, and repeat behavior.
    /// </summary>
    [CreateAssetMenu(fileName = "QuestConfig", menuName = "Endless Engine/Quest/Quest Config")]
    public class QuestConfigSO : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Unique ID. Never change after release — used in save data.")]
        public string QuestId;

        [Tooltip("Player-facing title.")]
        public string DisplayName;

        [Tooltip("Short description shown in the quest UI.")]
        [TextArea(2, 4)] public string Description;

        [Tooltip("Optional icon.")]
        public Sprite Icon;

        [Header("Objectives")]
        [Tooltip("Condition IDs that must all be satisfied to complete this quest. " +
                 "Conditions are registered at runtime via QuestService.RegisterCondition().")]
        public List<string> ConditionIds = new List<string>();

        [Header("Rewards")]
        public double GoldReward;
        public string RewardCurrencyId;
        public double RewardCurrencyAmount;

        [Header("Behavior")]
        [Tooltip("If true, quest resets and can be completed again after each prestige.")]
        public bool RepeatableOnPrestige;

        [Tooltip("If true, quest can be completed multiple times in the same run.")]
        public bool Repeatable;

        [Tooltip("Cooldown in seconds between repeatable completions. 0 = no cooldown.")]
        [Min(0f)] public float RepeatCooldownSeconds;

        [Tooltip("UTC window reset schedule. Daily/Weekly overrides RepeatCooldownSeconds when Repeatable=true.")]
        public QuestScheduleType ScheduleType;
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

namespace EndlessEngine.Config
{
    /// <summary>
    /// Defines a time-gated event (seasonal/rotation).
    ///
    /// Events activate between StartDayOfYear and EndDayOfYear (inclusive).
    /// For non-seasonal events, set RotationCycleHours > 0 for a repeating cadence.
    ///
    /// RewardPool is a list of item ids + drop weights.
    /// EventService does not execute rewards — it fires OnEventActivated and
    /// callers (DropResolver, EconomyService, etc.) handle distribution.
    /// </summary>
    [CreateAssetMenu(menuName = "Endless Engine/Event Schedule", fileName = "EventSchedule")]
    public class EventScheduleConfigSO : ScriptableObject
    {
        [Header("Identity")]
        public string EventId;
        public string DisplayName;
        [TextArea(2, 3)]
        public string Description;

        [Header("Schedule — Calendar")]
        [Range(1, 366)]
        public int StartDayOfYear = 1;
        [Range(1, 366)]
        public int EndDayOfYear   = 365;

        [Header("Schedule — Rotation")]
        /// <summary>Repeat cadence in hours. 0 = no rotation (calendar only).</summary>
        [Min(0)]
        public float RotationCycleHours = 0f;
        /// <summary>Duration of a single rotation window in hours. 0 = unlimited.</summary>
        [Min(0)]
        public float RotationDurationHours = 0f;

        [Header("Rewards")]
        public List<EventRewardEntry> RewardPool = new List<EventRewardEntry>();

        [Header("Modifiers")]
        /// <summary>Passive income multiplier while this event is active (1 = no change).</summary>
        [Min(1f)]
        public float IncomeMultiplier = 1f;
        /// <summary>Research speed multiplier while active (1 = no change).</summary>
        [Min(1f)]
        public float ResearchSpeedMultiplier = 1f;
    }

    [Serializable]
    public class EventRewardEntry
    {
        public string ItemId;
        [Range(1, 100)]
        public int    Weight = 10;
        public int    MinQuantity = 1;
        public int    MaxQuantity = 1;
    }
}

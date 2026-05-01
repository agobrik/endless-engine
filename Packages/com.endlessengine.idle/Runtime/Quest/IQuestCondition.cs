namespace EndlessEngine.Quest
{
    /// <summary>
    /// A single evaluatable condition for a quest objective or completion check.
    ///
    /// Implementations live in game projects (not the engine) because conditions
    /// reference game-specific metrics. The engine provides the interface and
    /// QuestService evaluates all registered conditions via this contract.
    ///
    /// Example implementations:
    ///   ReachWaveCondition    : IQuestCondition  { IsMet => waveManager.Wave >= target }
    ///   EarnGoldCondition     : IQuestCondition  { IsMet => economy.CurrentResources >= target }
    ///   PurchaseCountCondition: IQuestCondition  { IsMet => purchaseTracker.Count >= target }
    ///
    /// Conditions are polled by QuestService each time a relevant event fires —
    /// they do not need to be push-based. Keep IsMet cheap (field comparison).
    /// </summary>
    public interface IQuestCondition
    {
        /// <summary>Unique identifier for this condition. Used in save data and debug logs.</summary>
        string ConditionId { get; }

        /// <summary>Returns true when the condition is currently satisfied.</summary>
        bool IsMet { get; }

        /// <summary>
        /// 0.0 – 1.0 progress towards meeting the condition.
        /// Used for progress bar display. Return IsMet ? 1f : 0f if no partial progress.
        /// </summary>
        float Progress { get; }
    }
}

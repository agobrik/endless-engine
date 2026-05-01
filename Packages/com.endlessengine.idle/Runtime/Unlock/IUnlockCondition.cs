namespace EndlessEngine.Unlock
{
    /// <summary>
    /// Condition evaluated by ConditionalUnlockService to determine
    /// when an entry should be unlocked.
    ///
    /// Implement per entry type (e.g. PrestigeCountUnlockCondition,
    /// WaveReachedUnlockCondition). Register via
    /// conditionalUnlockService.Register(entryId, condition).
    /// </summary>
    public interface IUnlockCondition
    {
        /// <summary>Entry ID this condition guards. Must match UnlockEntryConfigSO.EntryId.</summary>
        string EntryId { get; }

        /// <summary>Returns true when the entry should be unlocked.</summary>
        bool IsMet { get; }
    }
}

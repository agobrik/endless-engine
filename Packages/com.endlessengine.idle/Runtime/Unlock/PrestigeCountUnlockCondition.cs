using EndlessEngine.Prestige;

namespace EndlessEngine.Unlock
{
    /// <summary>Unlocks an entry when PrestigeStateManager.PrestigeCount >= requiredCount.</summary>
    public class PrestigeCountUnlockCondition : IUnlockCondition
    {
        private readonly PrestigeStateManager _prestige;
        private readonly int _required;

        public string EntryId { get; }
        public bool   IsMet   => _prestige != null && _prestige.PrestigeCount >= _required;

        public PrestigeCountUnlockCondition(string entryId, PrestigeStateManager prestige, int requiredCount)
        {
            EntryId   = entryId;
            _prestige = prestige;
            _required = requiredCount;
        }
    }
}

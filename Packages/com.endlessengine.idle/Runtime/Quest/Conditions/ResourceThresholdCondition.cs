using EndlessEngine.Economy;

namespace EndlessEngine.Quest.Conditions
{
    /// <summary>
    /// Quest condition: player's current resources must reach a threshold.
    ///
    /// Usage:
    ///   questService.RegisterCondition(new ResourceThresholdCondition(economy, "earn_1m", 1_000_000.0));
    /// </summary>
    public class ResourceThresholdCondition : IQuestCondition
    {
        private readonly EconomyService _economy;
        private readonly double         _threshold;

        public string ConditionId { get; }

        public ResourceThresholdCondition(EconomyService economy, string conditionId, double threshold)
        {
            _economy   = economy;
            ConditionId = conditionId;
            _threshold  = threshold;
        }

        public bool IsMet => _economy != null && _economy.CurrentResources >= _threshold;

        public float Progress
        {
            get
            {
                if (_economy == null || _threshold <= 0.0) return 0f;
                float p = (float)(_economy.CurrentResources / _threshold);
                return p > 1f ? 1f : p;
            }
        }
    }
}

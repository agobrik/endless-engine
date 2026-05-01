using EndlessEngine.Wave;

namespace EndlessEngine.Quest.Conditions
{
    /// <summary>
    /// Quest condition: current wave number must reach or exceed a target.
    ///
    /// Usage:
    ///   questService.RegisterCondition(new WaveReachedCondition(waveSpawnManager, "reach_wave_10", 10));
    ///
    /// Reads WaveSpawnManager.CurrentWave directly — zero-allocation poll.
    /// </summary>
    public class WaveReachedCondition : IQuestCondition
    {
        private readonly WaveSpawnManager _waveManager;
        private readonly int              _targetWave;

        public string ConditionId { get; }

        public WaveReachedCondition(WaveSpawnManager waveManager, string conditionId, int targetWave)
        {
            _waveManager = waveManager;
            ConditionId  = conditionId;
            _targetWave  = targetWave;
        }

        public bool IsMet => _waveManager != null && _waveManager.CurrentWaveNumber >= _targetWave;

        public float Progress
        {
            get
            {
                if (_waveManager == null || _targetWave <= 0) return 0f;
                float p = (float)_waveManager.CurrentWaveNumber / _targetWave;
                return p > 1f ? 1f : p;
            }
        }
    }
}

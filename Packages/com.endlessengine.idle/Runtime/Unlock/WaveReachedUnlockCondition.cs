using EndlessEngine.Wave;

namespace EndlessEngine.Unlock
{
    /// <summary>Unlocks an entry when WaveSpawnManager.CurrentWaveNumber >= targetWave.</summary>
    public class WaveReachedUnlockCondition : IUnlockCondition
    {
        private readonly WaveSpawnManager _waveManager;
        private readonly int _targetWave;

        public string EntryId { get; }
        public bool   IsMet   => _waveManager != null && _waveManager.CurrentWaveNumber >= _targetWave;

        public WaveReachedUnlockCondition(string entryId, WaveSpawnManager waveManager, int targetWave)
        {
            EntryId      = entryId;
            _waveManager = waveManager;
            _targetWave  = targetWave;
        }
    }
}

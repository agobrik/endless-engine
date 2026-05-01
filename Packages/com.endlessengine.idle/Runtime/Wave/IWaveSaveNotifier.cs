namespace EndlessEngine.Wave
{
    /// <summary>Narrow interface for wave milestone save triggers.</summary>
    public interface IWaveSaveNotifier
    {
        void NotifyWaveMilestone(int waveNumber);
    }
}

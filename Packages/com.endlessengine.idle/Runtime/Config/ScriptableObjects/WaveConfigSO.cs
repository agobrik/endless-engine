using UnityEngine;

namespace EndlessEngine.Config
{
    /// <summary>
    /// Wave progression configuration: enemy count scaling, spawn rate, and milestone intervals.
    /// All wave math uses these values — no hardcoded numbers in WaveSpawnManager.
    ///
    /// ADR: ADR-0011 — Enemy Pool and Wave Scaling
    /// </summary>
    [CreateAssetMenu(fileName = "WaveConfig", menuName = "Endless Engine/Config/Wave")]
    public class WaveConfigSO : ScriptableObject
    {
        [Header("Wave Count")]
        [Tooltip("Total waves before run ends. Set to -1 for infinite (endless) mode.")]
        public int TotalWavesPerRun = -1;

        [Header("Enemy Count Scaling")]
        [Tooltip("Number of enemies in wave 1. Formula: Floor(BaseEnemyCountPerWave × (EnemyCountScalingFactor ^ (WaveNumber - 1)))")]
        public int BaseEnemyCountPerWave = 8;

        [Tooltip("Per-wave multiplier on enemy count. 1.0 = constant; 1.12 = ~2x every 7 waves.")]
        [Range(1f, 2f)]
        public float EnemyCountScalingFactor = 1.12f;

        [Header("Pool & Spawn Rate")]
        [Tooltip("Maximum simultaneous active enemies. Pre-warms the object pool to this capacity.")]
        public int HardCapEnemiesOnScreen = 50;

        [Tooltip("Seconds between individual enemy spawns during trickle. 0.5 = one enemy every 0.5s.")]
        [Range(0.05f, 10f)]
        public float SpawnIntervalSeconds = 0.5f;

        [Header("Wave Transitions")]
        [Tooltip("Seconds to pause between wave clear and next wave start.")]
        [Range(0f, 10f)]
        public float WaveTransitionDelaySeconds = 1.5f;

        [Tooltip("Maximum seconds a wave can run before force-clearing (safety net).")]
        [Range(10f, 600f)]
        public float WaveDurationSeconds = 120f;

        [Header("Upgrade & Save Milestones")]
        [Tooltip("Every N waves triggers OnUpgradeSelectionTriggered. Default: every 3rd wave.")]
        [Range(1, 50)]
        public int UpgradeSelectionWaveInterval = 3;

        [Tooltip("Every N waves triggers a save checkpoint. Default: every 10th wave.")]
        [Range(1, 100)]
        public int WaveSaveMilestoneInterval = 10;

        [Header("Elite & Boss Waves")]
        [Tooltip("Every Nth wave spawns at least one elite enemy.")]
        [Range(1, 100)]
        public int EliteWaveInterval = 5;

        [Tooltip("Stat multiplier applied to elite enemy HP and damage at spawn.")]
        [Range(1f, 10f)]
        public float EliteStatMultiplier = 3f;

        [Tooltip("Every Nth wave spawns one boss (suppresses elite on same wave).")]
        [Range(1, 1000)]
        public int BossWaveInterval = 20;
    }
}

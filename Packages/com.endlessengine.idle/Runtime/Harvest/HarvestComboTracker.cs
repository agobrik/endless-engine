using UnityEngine;
using EndlessEngine.Config;
using EndlessEngine.Core;

namespace EndlessEngine.Harvest
{
    /// <summary>
    /// Tracks the combo meter: accumulates combo points on each harvest tick,
    /// decays them after inactivity, and exposes the current yield multiplier.
    ///
    /// Updated by HarvestLoopService. Does not depend on MonoBehaviour — pure C# class,
    /// fully testable without a scene.
    /// </summary>
    public class HarvestComboTracker
    {
        private readonly HarvestAreaConfigSO _config;

        private float _comboPoints;
        private float _timeSinceLastHarvest;

        public float ComboPoints     => _comboPoints;
        public float ComboMultiplier => ComputeMultiplier();

        public HarvestComboTracker(HarvestAreaConfigSO config)
        {
            _config = config;
        }

        /// <summary>
        /// Call every harvest tick with the total combo contribution from all nodes hit.
        /// </summary>
        public void RecordHit(float comboContribution)
        {
            _timeSinceLastHarvest = 0f;
            _comboPoints += comboContribution;
        }

        /// <summary>
        /// Call every frame with the real Time.deltaTime (or a test-supplied delta).
        /// Applies decay after the inactivity window.
        /// </summary>
        public void Tick(float deltaTime)
        {
            _timeSinceLastHarvest += deltaTime;

            if (_timeSinceLastHarvest > _config.ComboDecayDelay)
            {
                float decayRate = _config.ComboDecayRate
                    * (1f + UpgradeApplicationSystem.GetEffectiveStat(StatType.HarvestComboDecayRate));
                _comboPoints = Mathf.Max(0f, _comboPoints - decayRate * deltaTime);
            }
        }

        public void Reset()
        {
            _comboPoints          = 0f;
            _timeSinceLastHarvest = 0f;
        }

        private float ComputeMultiplier()
        {
            if (_comboPoints <= 0f) return 1f;

            float step   = _config.ComboPointsPerMultiplierStep;
            float raw    = 1f + _comboPoints / step;
            float maxMul = _config.MaxComboMultiplier;

            float upgradeMul = 1f + UpgradeApplicationSystem.GetEffectiveStat(StatType.HarvestComboMultiplier);
            return Mathf.Min(raw * upgradeMul, maxMul);
        }
    }
}

using UnityEngine;
using EndlessEngine.Config;
using EndlessEngine.Core;

namespace EndlessEngine.ClickLoop
{
    /// <summary>
    /// Tracks click combo: accumulates points on each click, decays after inactivity.
    /// Pure C# — no MonoBehaviour dependency, fully unit-testable.
    /// </summary>
    public class ClickComboTracker
    {
        private readonly ClickLoopConfigSO _config;

        private float _comboPoints;
        private float _timeSinceLastClick;

        public float ComboPoints     => _comboPoints;
        public float ComboMultiplier => ComputeMultiplier();

        public ClickComboTracker(ClickLoopConfigSO config) => _config = config;

        public void RecordClick(float contribution)
        {
            _timeSinceLastClick = 0f;
            _comboPoints += contribution;
        }

        public void Tick(float deltaTime)
        {
            _timeSinceLastClick += deltaTime;
            if (_timeSinceLastClick > _config.ComboDecayDelay)
            {
                float rate = _config.ComboDecayRate
                    * (1f + UpgradeApplicationSystem.GetEffectiveStat(StatType.ClickComboDecayRate));
                _comboPoints = Mathf.Max(0f, _comboPoints - rate * deltaTime);
            }
        }

        public void Reset()
        {
            _comboPoints          = 0f;
            _timeSinceLastClick   = 0f;
        }

        private float ComputeMultiplier()
        {
            if (_comboPoints <= 0f) return 1f;
            float raw    = 1f + _comboPoints / _config.ComboPointsPerStep;
            float upgMul = 1f + UpgradeApplicationSystem.GetEffectiveStat(StatType.ClickComboMultiplier);
            return Mathf.Min(raw * upgMul, _config.MaxComboMultiplier);
        }
    }
}

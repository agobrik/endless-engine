using System;
using UnityEngine;
using EndlessEngine.Economy;
using EndlessEngine.SaveAndLoad;

namespace EndlessEngine.ClickLoop
{
    /// <summary>
    /// Credits offline click-loop gold once per session on SaveService.OnSaveLoaded.
    ///
    /// Formula:
    ///   EffectiveDelta  = Min(offlineDeltaSeconds, OfflineCapHours × 3600)
    ///   ClickRate       = sum of (BaseYield / RespawnSeconds) for each target config
    ///   OfflineGold     = Floor(ClickRate × OfflineEfficiency × EffectiveDelta)
    ///
    /// Bootstrap wiring:
    ///   calc.Initialize(loopConfig, economy, targetConfigs);
    ///   saveService.OnSaveLoaded += calc.HandleSaveLoaded;
    /// </summary>
    public class ClickLoopOfflineCalculator : MonoBehaviour
    {
        public static event Action<long, float> OnOfflineClickGainCalculated;

        [SerializeField] private SaveService _saveService;

        private ClickLoopConfigSO    _config;
        private EconomyService       _economy;
        private ClickTargetConfigSO[] _targetConfigs;
        private bool                 _hasRun;

        public void Initialize(
            ClickLoopConfigSO    config,
            EconomyService       economy,
            ClickTargetConfigSO[] targetConfigs)
        {
            _config        = config;
            _economy       = economy;
            _targetConfigs = targetConfigs;
        }

        private void OnEnable()
        {
            if (_saveService != null) _saveService.OnSaveLoaded += HandleSaveLoaded;
        }

        private void OnDisable()
        {
            if (_saveService != null) _saveService.OnSaveLoaded -= HandleSaveLoaded;
        }

        public void HandleSaveLoaded(SaveData saveData, bool isNewGame)
        {
            if (_hasRun) return;
            _hasRun = true;

            if (isNewGame || _config == null || _targetConfigs == null || _targetConfigs.Length == 0)
            {
                OnOfflineClickGainCalculated?.Invoke(0L, 0f);
                return;
            }

            float delta      = Mathf.Max(0f,
                (float)(DateTime.UtcNow - saveData.LastSessionTimestamp).TotalSeconds);
            float effective  = Mathf.Min(delta, _config.OfflineCapHours * 3600f);

            float rate = 0f;
            foreach (var tc in _targetConfigs)
            {
                if (tc == null || tc.RespawnSeconds <= 0f) continue;
                rate += tc.BaseYield / tc.RespawnSeconds;
            }

            long gain = (long)Math.Floor(rate * _config.OfflineEfficiency * effective);
            if (gain > 0 && _economy != null)
                _economy.AddResources(gain);

            OnOfflineClickGainCalculated?.Invoke(gain, effective);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public void InvokeForTesting(SaveData saveData, bool isNewGame)
        {
            _hasRun = false;
            HandleSaveLoaded(saveData, isNewGame);
        }

        public void ResetForTesting() => _hasRun = false;
#endif
    }
}

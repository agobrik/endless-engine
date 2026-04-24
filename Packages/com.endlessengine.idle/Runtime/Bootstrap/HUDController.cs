using TMPro;
using UnityEngine;
using EndlessEngine.Wave;
using EndlessEngine.Economy;
using EndlessEngine.Health;

namespace EndlessEngine.Bootstrap
{
    /// <summary>
    /// Vertical Slice HUD — updates wave number, gold, and player HP display.
    /// </summary>
    public class HUDController : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _waveText;
        [SerializeField] private TextMeshProUGUI _goldText;
        [SerializeField] private TextMeshProUGUI _hpText;

        [SerializeField] private EconomyService _economyService;

        private void OnEnable()
        {
            WaveSpawnManager.OnWaveStarted   += HandleWaveStarted;
            PlayerHealthComponent.OnPlayerHPChanged += HandleHPChanged;
        }

        private void OnDisable()
        {
            WaveSpawnManager.OnWaveStarted   -= HandleWaveStarted;
            PlayerHealthComponent.OnPlayerHPChanged -= HandleHPChanged;
        }

        private void Update()
        {
            if (_goldText != null && _economyService != null)
                _goldText.text = $"Gold: {_economyService.CurrentResources}";
        }

        private void HandleWaveStarted(int waveNumber)
        {
            if (_waveText != null)
                _waveText.text = $"Wave {waveNumber}";
        }

        private void HandleHPChanged(float current, float max)
        {
            if (_hpText != null)
                _hpText.text = $"HP: {Mathf.Floor(current)}/{Mathf.Floor(max)}";
        }
    }
}

using UnityEngine;
using UnityEngine.UI;
using EndlessEngine.Wave;

namespace EndlessEngine.UI
{
    /// <summary>
    /// Displays the current wave number and optionally a progress bar toward the next wave.
    /// Auto-updates on WaveSpawnManager.OnWaveStarted and OnWaveComplete.
    ///
    /// Usage:
    ///   Attach to a HUD GameObject.
    ///   No Initialize() needed — subscribes to static WaveSpawnManager events.
    /// </summary>
    [AddComponentMenu("Endless Engine/UI/Wave Counter")]
    public class WaveCounter : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────────

        [SerializeField] private Text _waveLabel;

        [Tooltip("Format for wave label. {0} = wave number.")]
        [SerializeField] private string _waveLabelFormat = "Wave {0}";

        [Tooltip("Optional progress bar showing enemies cleared this wave.")]
        [SerializeField] private GenericProgressBar _progressBar;

        [Tooltip("Label shown briefly when a wave is cleared.")]
        [SerializeField] private Text _waveClearedLabel;

        [Tooltip("Seconds to show the wave-cleared label before hiding.")]
        [SerializeField] [Range(0f, 5f)] private float _clearedDisplaySeconds = 2f;

        // ── State ─────────────────────────────────────────────────────────────────

        private int   _currentWave = 0;
        private float _clearedTimer;

        // ── Unity lifecycle ───────────────────────────────────────────────────────

        private void OnEnable()
        {
            WaveSpawnManager.OnWaveStarted  += HandleWaveStarted;
            WaveSpawnManager.OnWaveComplete += HandleWaveComplete;
        }

        private void OnDisable()
        {
            WaveSpawnManager.OnWaveStarted  -= HandleWaveStarted;
            WaveSpawnManager.OnWaveComplete -= HandleWaveComplete;
        }

        private void Update()
        {
            if (_clearedTimer > 0f)
            {
                _clearedTimer -= Time.deltaTime;
                if (_clearedTimer <= 0f && _waveClearedLabel != null)
                    _waveClearedLabel.gameObject.SetActive(false);
            }
        }

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>Manually updates the enemy progress (0–1) within the current wave.</summary>
        public void SetEnemyProgress(int killed, int total)
        {
            _progressBar?.SetProgress(killed, total);
        }

        // ── Handlers ──────────────────────────────────────────────────────────────

        private void HandleWaveStarted(int waveNumber)
        {
            _currentWave = waveNumber;
            UpdateLabel();
            _progressBar?.SetNormalized(0f);

            if (_waveClearedLabel != null)
                _waveClearedLabel.gameObject.SetActive(false);
        }

        private void HandleWaveComplete(int waveNumber)
        {
            _progressBar?.SetNormalized(1f);

            if (_waveClearedLabel != null)
            {
                _waveClearedLabel.gameObject.SetActive(true);
                _clearedTimer = _clearedDisplaySeconds;
            }
        }

        private void UpdateLabel()
        {
            if (_waveLabel != null)
                _waveLabel.text = string.Format(_waveLabelFormat, _currentWave);
        }
    }
}

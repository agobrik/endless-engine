using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using EndlessEngine.Config;
using EndlessEngine.Damage;
using EndlessEngine.Economy;
using EndlessEngine.Enemy;
using EndlessEngine.Wave;

namespace EndlessEngine.Audio
{
    /// <summary>
    /// SFX pool and per-frame deduplication audio service.
    ///
    /// Pre-warms a pool of <see cref="AudioSource"/> components at Awake.
    /// Per-frame deduplication prevents the same clip from playing more than once
    /// in a single frame (e.g., 200 simultaneous enemy deaths fire one sound, not 200).
    ///
    /// Subscribes to game events for automatic SFX playback.
    ///
    /// ADR: ADR-0015 — Audio Pool and Mixer Group Strategy
    /// GDD: design/gdd/audio-system.md
    /// Sprint: S4-06
    /// </summary>
    public class AudioService : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────────

        [SerializeField] private AudioMixer   _audioMixer;
        [SerializeField] private AudioConfigSO _config;

        // ── Pool ──────────────────────────────────────────────────────────────────

        private readonly Queue<AudioSource> _sfxPool          = new Queue<AudioSource>(32);
        private readonly HashSet<int>       _playedThisFrame  = new HashSet<int>();

        // ── Lifecycle ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            WarmPool();
        }

        private void OnEnable()
        {
            DamageSystem.OnDamageResolved          += HandleDamageResolved;
            EnemyManager.OnEnemyKilled             += HandleEnemyKilled;
            WaveSpawnManager.OnWaveComplete        += HandleWaveComplete;
            EconomyService.OnUpgradePurchased      += HandleUpgradePurchased;
        }

        private void OnDisable()
        {
            DamageSystem.OnDamageResolved          -= HandleDamageResolved;
            EnemyManager.OnEnemyKilled             -= HandleEnemyKilled;
            WaveSpawnManager.OnWaveComplete        -= HandleWaveComplete;
            EconomyService.OnUpgradePurchased      -= HandleUpgradePurchased;
        }

        private void LateUpdate()
        {
            // Clear dedup set each frame — next frame may play the same clips again
            _playedThisFrame.Clear();
        }

        // ── Pool Setup ────────────────────────────────────────────────────────────

        private void WarmPool()
        {
            if (_config == null) return;

            int size = _config.SFXPoolSize;
            var poolParent = new GameObject("SFX_Pool");
            poolParent.transform.SetParent(transform);

            for (int i = 0; i < size; i++)
            {
                var src = poolParent.AddComponent<AudioSource>();
                src.playOnAwake = false;

                if (_config.SFXMixerGroup != null)
                    src.outputAudioMixerGroup = _config.SFXMixerGroup;

                _sfxPool.Enqueue(src);
            }
        }

        // ── SFX API ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Plays <paramref name="clip"/> from the pool with per-frame deduplication.
        /// Silently drops if pool is exhausted or the same clip already played this frame.
        /// </summary>
        public void PlaySFX(AudioClip clip, float volume = 1f, float pitch = 1f)
        {
            if (clip == null) return;

            int clipId = clip.GetInstanceID();
            if (_playedThisFrame.Contains(clipId)) return; // dedup — first play wins

            if (_sfxPool.Count == 0) return; // pool exhausted — drop silently

            var src   = _sfxPool.Dequeue();
            src.clip  = clip;
            src.volume = Mathf.Clamp01(volume);
            src.pitch  = pitch;
            src.Play();

            _playedThisFrame.Add(clipId);

            float duration = clip.length / Mathf.Max(0.01f, Mathf.Abs(pitch));
            StartCoroutine(RecycleAfterClip(src, duration));
        }

        // ── Volume Control ────────────────────────────────────────────────────────

        /// <summary>
        /// Sets SFX volume on the AudioMixer and persists to PlayerPrefs.
        /// <paramref name="linear"/> in [0, 1]. 0 = silent, 1 = full.
        /// </summary>
        public void SetSFXVolume(float linear)
        {
            if (_audioMixer != null)
                _audioMixer.SetFloat("SFXVolume", LinearToDecibels(linear));
            PlayerPrefs.SetFloat("sfx_volume", linear);
        }

        /// <summary>
        /// Sets Music volume on the AudioMixer and persists to PlayerPrefs.
        /// </summary>
        public void SetMusicVolume(float linear)
        {
            if (_audioMixer != null)
                _audioMixer.SetFloat("MusicVolume", LinearToDecibels(linear));
            PlayerPrefs.SetFloat("music_volume", linear);
        }

        // ── Event Handlers ────────────────────────────────────────────────────────

        private void HandleDamageResolved(DamageHit hit)
        {
            if (_config == null) return;

            // Subscriber order=3 per ADR-0015: after HealthSystem (1) and VFX (2)
            if (hit.AttackerType != AttackerType.Player) return; // only player hits

            AudioClip clip = hit.IsCrit ? _config.HitCritClip : _config.HitNormalClip;
            float vol      = hit.IsCrit ? _config.HitCritVolume : _config.HitNormalVolume;

            PlaySFX(clip, vol, Random.Range(0.9f, 1.1f));
        }

        private void HandleEnemyKilled(EnemyAgent _)
        {
            if (_config == null) return;
            PlaySFX(_config.EnemyDeathClip, _config.EnemyDeathVolume);
        }

        private void HandleWaveComplete(int _)
        {
            if (_config == null) return;
            PlaySFX(_config.WaveCompleteClip, _config.WaveCompleteVolume);
        }

        private void HandleUpgradePurchased(string nodeId, long cost)
        {
            if (_config == null) return;
            PlaySFX(_config.UpgradePurchasedClip, _config.UpgradePurchasedVolume);
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private IEnumerator RecycleAfterClip(AudioSource src, float duration)
        {
            yield return new WaitForSeconds(duration);
            src.Stop();
            src.clip = null;
            _sfxPool.Enqueue(src);
        }

        /// <summary>Converts a linear volume [0,1] to decibels for AudioMixer.SetFloat.</summary>
        public static float LinearToDecibels(float linear)
            => linear > 0.0001f ? 20f * Mathf.Log10(linear) : -80f;

        // ── Test Helpers ──────────────────────────────────────────────────────────

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>Returns current pool count for test assertions.</summary>
        public int PoolCountForTesting => _sfxPool.Count;

        /// <summary>Returns the number of distinct clips played this frame.</summary>
        public int PlayedThisFrameCountForTesting => _playedThisFrame.Count;

        /// <summary>
        /// Exposes PlaySFX for direct unit test calls without wiring full event system.
        /// </summary>
        public void PlaySFXForTesting(AudioClip clip, float volume = 1f, float pitch = 1f)
            => PlaySFX(clip, volume, pitch);

        /// <summary>Calls LateUpdate to clear the dedup set (simulates frame end in tests).</summary>
        public void SimulateFrameEndForTesting() => _playedThisFrame.Clear();
#endif
    }
}

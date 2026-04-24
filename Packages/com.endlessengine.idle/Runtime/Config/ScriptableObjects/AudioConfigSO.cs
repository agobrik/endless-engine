using UnityEngine;
using UnityEngine.Audio;

namespace EndlessEngine.Config
{
    /// <summary>
    /// Audio clip and volume configuration for the SFX pool.
    /// All clips are assigned in the Inspector; no runtime loading.
    ///
    /// ADR: ADR-0015 — Audio Pool and Mixer Group Strategy
    /// GDD: design/gdd/audio-system.md
    /// </summary>
    [CreateAssetMenu(fileName = "AudioConfig", menuName = "Endless Engine/Config/Audio")]
    public class AudioConfigSO : ScriptableObject
    {
        [Header("Hit Sounds")]
        public AudioClip HitNormalClip;
        [Range(0f, 1f)] public float HitNormalVolume = 0.6f;

        public AudioClip HitCritClip;
        [Range(0f, 1f)] public float HitCritVolume   = 0.9f;

        [Header("Kill Sounds")]
        public AudioClip EnemyDeathClip;
        [Range(0f, 1f)] public float EnemyDeathVolume = 0.7f;

        [Header("Wave Sounds")]
        public AudioClip WaveCompleteClip;
        [Range(0f, 1f)] public float WaveCompleteVolume = 0.8f;

        [Header("Upgrade Sounds")]
        public AudioClip UpgradePurchasedClip;
        [Range(0f, 1f)] public float UpgradePurchasedVolume = 0.85f;

        [Header("Pool Settings")]
        [Tooltip("Number of AudioSource components pre-warmed at Awake. Per ADR-0015 default: 32.")]
        [Range(8, 64)] public int SFXPoolSize = 32;

        [Header("Mixer")]
        [Tooltip("SFX AudioMixerGroup — all SFX pool sources route here.")]
        public AudioMixerGroup SFXMixerGroup;
    }
}

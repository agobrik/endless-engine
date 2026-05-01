using System.Collections;
using UnityEngine;
using UnityEngine.Audio;
using EndlessEngine.Config;
using EndlessEngine.Wave;

namespace EndlessEngine.Audio
{
    /// <summary>
    /// State-driven ambient / combat music controller with cross-fade.
    ///
    /// Uses two AudioSource components (A and B) for smooth cross-fades without
    /// instantiation. State transitions trigger a volume cross-fade over
    /// <see cref="AudioConfigSO.MusicCrossFadeDuration"/> seconds.
    ///
    /// States:
    ///   Ambient  — idle / exploration music
    ///   Combat   — battle / wave-in-progress music
    ///   None     — silence (no cross-fade)
    ///
    /// Bootstrap wiring:
    ///   musicController.Initialize(audioConfig);
    ///   musicController.SetState(MusicState.Ambient);
    ///
    /// WaveSpawnManager events are subscribed automatically via OnEnable/OnDisable.
    /// </summary>
    public class MusicController : MonoBehaviour
    {
        public enum MusicState { None, Ambient, Combat }

        // ── Inspector ─────────────────────────────────────────────────────────────

        [Header("Tracks")]
        [Tooltip("Looping clip played during idle / ambient state.")]
        [SerializeField] private AudioClip _ambientClip;

        [Tooltip("Looping clip played during wave / combat state.")]
        [SerializeField] private AudioClip _combatClip;

        // ── State ─────────────────────────────────────────────────────────────────

        private AudioConfigSO  _config;
        private AudioSource    _sourceA;
        private AudioSource    _sourceB;
        private bool           _aIsActive;
        private MusicState     _currentState = MusicState.None;
        private Coroutine      _fadeCoroutine;

        // ── Bootstrap ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Creates the two internal AudioSources and wires the mixer group.
        /// Call once after the AudioMixer is loaded.
        /// </summary>
        public void Initialize(AudioConfigSO config)
        {
            _config = config;
            _sourceA = CreateMusicSource("Music_A", config);
            _sourceB = CreateMusicSource("Music_B", config);
        }

        // ── Unity Lifecycle ───────────────────────────────────────────────────────

        private void OnEnable()
        {
            WaveSpawnManager.OnWaveStarted   += HandleWaveStarted;
            WaveSpawnManager.OnWaveComplete  += HandleWaveComplete;
        }

        private void OnDisable()
        {
            WaveSpawnManager.OnWaveStarted   -= HandleWaveStarted;
            WaveSpawnManager.OnWaveComplete  -= HandleWaveComplete;
        }

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>Current music playback state.</summary>
        public MusicState CurrentState => _currentState;

        /// <summary>
        /// Transitions to a new music state with cross-fade.
        /// No-op if already in the requested state.
        /// </summary>
        public void SetState(MusicState state)
        {
            if (state == _currentState) return;
            _currentState = state;

            AudioClip nextClip = state switch
            {
                MusicState.Ambient => _ambientClip,
                MusicState.Combat  => _combatClip,
                _                  => null,
            };

            CrossFadeTo(nextClip);
        }

        /// <summary>Stops music immediately with no fade.</summary>
        public void StopImmediate()
        {
            if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
            _sourceA.Stop();
            _sourceB.Stop();
            _currentState = MusicState.None;
        }

        // ── Event Handlers ────────────────────────────────────────────────────────

        private void HandleWaveStarted(int _) => SetState(MusicState.Combat);
        private void HandleWaveComplete(int _) => SetState(MusicState.Ambient);

        // ── Cross-Fade ────────────────────────────────────────────────────────────

        private void CrossFadeTo(AudioClip clip)
        {
            if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);

            AudioSource outgoing = _aIsActive ? _sourceA : _sourceB;
            AudioSource incoming = _aIsActive ? _sourceB : _sourceA;
            _aIsActive = !_aIsActive;

            float duration = _config != null ? _config.MusicCrossFadeDuration : 1.5f;
            _fadeCoroutine = StartCoroutine(PerformCrossFade(outgoing, incoming, clip, duration));
        }

        private static IEnumerator PerformCrossFade(
            AudioSource outgoing, AudioSource incoming, AudioClip clip, float duration)
        {
            float outStartVol = outgoing.volume;

            if (clip != null)
            {
                incoming.clip   = clip;
                incoming.loop   = true;
                incoming.volume = 0f;
                incoming.Play();
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t  = Mathf.Clamp01(elapsed / duration);

                outgoing.volume = Mathf.Lerp(outStartVol, 0f, t);
                if (clip != null)
                    incoming.volume = Mathf.Lerp(0f, 1f, t);

                yield return null;
            }

            outgoing.Stop();
            outgoing.clip   = null;
            outgoing.volume = 0f;

            if (clip == null)
            {
                incoming.Stop();
                incoming.volume = 0f;
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private AudioSource CreateMusicSource(string sourceName, AudioConfigSO config)
        {
            var go  = new GameObject(sourceName);
            go.transform.SetParent(transform);
            var src      = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop        = true;
            src.volume      = 0f;
            if (config?.MusicMixerGroup != null)
                src.outputAudioMixerGroup = config.MusicMixerGroup;
            return src;
        }
    }
}

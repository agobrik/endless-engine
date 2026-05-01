using System;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace EndlessEngine.Localization
{
    /// <summary>
    /// Engine-level localization facade. All player-facing strings must go through
    /// LocalizationService.Get(key) — never use raw string literals in gameplay code.
    ///
    /// Backend abstraction (v1.3):
    ///   v1.1 default — DictionaryLocalizationBackend (Resources/Locales/*.json)
    ///   v1.3 optional — UnityLocalizationBackend (Unity Localization Package)
    ///
    ///   Switch backend before calling Initialize():
    ///     LocalizationService.SetBackend(new UnityLocalizationBackend());
    ///     LocalizationService.Initialize("en");
    ///
    ///   Callers never need to change — the API is identical for all backends.
    ///
    /// Key convention: hierarchical dot-notation
    ///   "ui.hud.gold_label"
    ///   "generator.basic.name"
    ///   "milestone.first_prestige.title"
    ///
    /// Missing keys fall back to "[?]{key}" so they are visible in QA.
    ///
    /// Bootstrap wiring:
    ///   LocalizationService.Initialize("en");
    /// </summary>
    public static class LocalizationService
    {
        private static ILocalizationBackend _backend     = new DictionaryLocalizationBackend();
        private static string               _currentLocale = "en";
        private static bool                 _initialized;

        // ── Events ────────────────────────────────────────────────────────────────

        /// <summary>Fires after the locale is changed and new strings are loaded.</summary>
        public static event Action<string> OnLocaleChanged;

        // ── Backend ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Replaces the active backend. Must be called BEFORE Initialize().
        /// Default: DictionaryLocalizationBackend.
        /// </summary>
        public static void SetBackend(ILocalizationBackend backend)
        {
            if (_initialized)
            {
                Debug.LogWarning("[LocalizationService] SetBackend() called after Initialize() — backend change will take effect on next Initialize() call.");
            }
            _backend = backend ?? new DictionaryLocalizationBackend();
        }

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>Current locale code (e.g. "en", "tr", "zh").</summary>
        public static string CurrentLocale => _currentLocale;

        /// <summary>
        /// Initialises and loads strings for the given locale.
        /// Safe to call multiple times — re-initialises if locale differs.
        /// </summary>
        public static void Initialize(string locale = "en")
        {
            _currentLocale = locale;
            _backend.Initialize(locale, () =>
            {
                _initialized = true;
                Debug.Log($"[LocalizationService] Initialized with locale '{locale}' via {_backend.GetType().Name}.");
            });
        }

        /// <summary>Switches to a new locale at runtime. Fires OnLocaleChanged when ready.</summary>
        public static void SetLocale(string locale)
        {
            if (locale == _currentLocale) return;
            _currentLocale = locale;
            _backend.SetLocale(locale, () => OnLocaleChanged?.Invoke(locale));
        }

        /// <summary>
        /// Returns the localized string for the given key.
        /// Falls back to "[?]{key}" if the key is missing or not yet loaded.
        /// </summary>
        public static string Get(string key)
        {
            if (!_initialized)
            {
                Debug.LogWarning($"[LocalizationService] Get('{key}') called before Initialize(). Auto-initializing with 'en'.");
                Initialize("en");
            }

            string value = _backend.Get(key);
            return value ?? $"[?]{key}";
        }

        /// <summary>
        /// Returns the localized string with {0}, {1} … placeholders replaced.
        /// </summary>
        public static string Get(string key, params object[] args)
        {
            string raw = Get(key);
            try { return string.Format(raw, args); }
            catch { return raw; }
        }

        /// <summary>True if the given key exists in the current locale.</summary>
        public static bool HasKey(string key) => _backend.HasKey(key);

        // ── Test Support ──────────────────────────────────────────────────────────

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>Injects strings directly for tests without loading from Resources.</summary>
        public static void InjectForTesting(System.Collections.Generic.Dictionary<string, string> strings, string locale = "en")
        {
            if (_backend is DictionaryLocalizationBackend dict)
                dict.InjectForTesting(strings, locale);
            else
            {
                var fallback = new DictionaryLocalizationBackend();
                fallback.InjectForTesting(strings, locale);
                _backend = fallback;
            }
            _currentLocale = locale;
            _initialized   = true;
        }

        public static void ResetForTesting()
        {
            _backend       = new DictionaryLocalizationBackend();
            _initialized   = false;
            _currentLocale = "en";
        }
#endif
    }
}

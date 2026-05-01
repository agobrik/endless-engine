using System;

namespace EndlessEngine.Localization
{
    /// <summary>
    /// Backend abstraction for LocalizationService.
    ///
    /// v1.1 shipped DictionaryLocalizationBackend (Resources/Locales/*.json).
    /// v1.3 adds UnityLocalizationBackend (Unity Localization Package).
    ///
    /// Game code calls LocalizationService.Get() — it never touches the backend.
    /// Switching backends is a one-line Bootstrap change:
    ///   LocalizationService.SetBackend(new UnityLocalizationBackend());
    ///
    /// Implementations must be thread-safe for Get() (called from UI threads).
    /// Initialize() is called on the main thread before the first Get().
    /// </summary>
    public interface ILocalizationBackend
    {
        /// <summary>Current locale code (e.g. "en", "tr").</summary>
        string CurrentLocale { get; }

        /// <summary>
        /// Initialises the backend for the given locale.
        /// May be synchronous (Dictionary) or async-then-callback (Unity Localization).
        /// <paramref name="onReady"/> fires when Get() is safe to call.
        /// </summary>
        void Initialize(string locale, Action onReady);

        /// <summary>Switches locale. Fires <paramref name="onReady"/> when new strings are loaded.</summary>
        void SetLocale(string locale, Action onReady);

        /// <summary>Returns the string for key, or null if not found.</summary>
        string Get(string key);

        /// <summary>True if the key exists in the current locale.</summary>
        bool HasKey(string key);
    }
}

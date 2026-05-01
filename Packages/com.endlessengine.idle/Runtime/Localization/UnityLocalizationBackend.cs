#if UNITY_LOCALIZATION_ENABLED
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

namespace EndlessEngine.Localization
{
    /// <summary>
    /// ILocalizationBackend backed by Unity Localization Package.
    ///
    /// Activation:
    ///   1. Install com.unity.localization via Package Manager
    ///   2. Add "UNITY_LOCALIZATION_ENABLED" to Scripting Define Symbols
    ///   3. Create a String Table in the Localization Settings asset
    ///      (named "GameStrings" by default — override via constructor)
    ///   4. Bootstrap:
    ///        LocalizationService.SetBackend(new UnityLocalizationBackend());
    ///        LocalizationService.Initialize("en");
    ///
    /// The UnityLocalizationBackend reads from a named String Table Collection.
    /// Keys must match those in en.json (same dot-notation convention).
    ///
    /// Async note: Unity Localization loads tables asynchronously.
    /// Initialize() starts the load; onReady fires when the table is available.
    /// Get() returns null (falls back to "[?]key" in LocalizationService) until ready.
    ///
    /// Locale change: SetLocale() triggers Unity's built-in locale switch.
    /// LocalizationSettings.SelectedLocaleChanged fires automatically — this backend
    /// listens to it and calls the supplied onReady when the new table loads.
    /// </summary>
    public class UnityLocalizationBackend : ILocalizationBackend
    {
        private readonly string _tableName;
        private StringTable     _table;
        private string          _currentLocale = "en";
        private bool            _ready;
        private Action          _pendingOnReady;

        public UnityLocalizationBackend(string tableName = "GameStrings")
        {
            _tableName = tableName;
        }

        public string CurrentLocale => _currentLocale;

        public void Initialize(string locale, Action onReady)
        {
            _currentLocale  = locale;
            _pendingOnReady = onReady;

            LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;

            // Kick off async initialization
            LocalizationSettings.InitializationOperation.Completed += _ =>
            {
                SetLocaleInternal(locale, onReady);
            };
        }

        public void SetLocale(string locale, Action onReady)
        {
            if (locale == _currentLocale) { onReady?.Invoke(); return; }
            _currentLocale = locale;
            SetLocaleInternal(locale, onReady);
        }

        public string Get(string key)
        {
            if (_table == null) return null;
            var entry = _table.GetEntry(key);
            return entry?.GetLocalizedString();
        }

        public bool HasKey(string key)
        {
            if (_table == null) return false;
            return _table.GetEntry(key) != null;
        }

        // ── Private ───────────────────────────────────────────────────────────────

        private void SetLocaleInternal(string locale, Action onReady)
        {
            // Find the Locale asset matching the locale code
            var locales = LocalizationSettings.AvailableLocales.Locales;
            Locale target = null;
            foreach (var l in locales)
                if (l.Identifier.Code == locale) { target = l; break; }

            if (target == null)
            {
                Debug.LogWarning($"[UnityLocalizationBackend] Locale '{locale}' not found in LocalizationSettings.");
                onReady?.Invoke();
                return;
            }

            LocalizationSettings.SelectedLocale = target;

            // Load the string table for this locale
            var op = LocalizationSettings.StringDatabase.GetTableAsync(_tableName);
            op.Completed += handle =>
            {
                if (handle.Status == UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded)
                {
                    _table = handle.Result;
                    _ready = true;
                    Debug.Log($"[UnityLocalizationBackend] Table '{_tableName}' loaded for locale '{locale}'.");
                }
                else
                {
                    Debug.LogError($"[UnityLocalizationBackend] Failed to load table '{_tableName}' for locale '{locale}'.");
                }
                onReady?.Invoke();
            };
        }

        private void OnLocaleChanged(Locale locale)
        {
            _currentLocale = locale?.Identifier.Code ?? "en";
            _ready = false;
            SetLocaleInternal(_currentLocale, null);
        }
    }
}
#endif

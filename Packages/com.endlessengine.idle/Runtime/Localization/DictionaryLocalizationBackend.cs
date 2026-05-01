using System;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using Debug = UnityEngine.Debug;

namespace EndlessEngine.Localization
{
    /// <summary>
    /// ILocalizationBackend backed by Resources/Locales/{locale}.json.
    /// This is the v1.1 implementation extracted into the backend pattern.
    /// Synchronous — onReady fires immediately during Initialize().
    /// </summary>
    public class DictionaryLocalizationBackend : ILocalizationBackend
    {
        private Dictionary<string, string> _strings = new Dictionary<string, string>();
        private string _currentLocale = "en";

        public string CurrentLocale => _currentLocale;

        public void Initialize(string locale, Action onReady)
        {
            _currentLocale = locale;
            Load(locale);
            onReady?.Invoke();
        }

        public void SetLocale(string locale, Action onReady)
        {
            if (locale == _currentLocale) { onReady?.Invoke(); return; }
            _currentLocale = locale;
            Load(locale);
            onReady?.Invoke();
        }

        public string Get(string key)
            => _strings.TryGetValue(key, out var v) ? v : null;

        public bool HasKey(string key) => _strings.ContainsKey(key);

        private void Load(string locale)
        {
            _strings.Clear();
            var asset = Resources.Load<TextAsset>($"Locales/{locale}");
            if (asset == null)
            {
                Debug.LogWarning($"[DictionaryLocalizationBackend] Locale file 'Resources/Locales/{locale}.json' not found.");
                return;
            }
            try
            {
                var parsed = JsonConvert.DeserializeObject<Dictionary<string, string>>(asset.text);
                if (parsed != null)
                    foreach (var kv in parsed)
                        _strings[kv.Key] = kv.Value;

                Debug.Log($"[DictionaryLocalizationBackend] Loaded locale '{locale}': {_strings.Count} strings.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DictionaryLocalizationBackend] Parse error for locale '{locale}': {ex.Message}");
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public void InjectForTesting(Dictionary<string, string> strings, string locale = "en")
        {
            _strings       = new Dictionary<string, string>(strings);
            _currentLocale = locale;
        }
#endif
    }
}

using System;
using System.Text;
using UnityEngine;
using EndlessEngine.SaveAndLoad;

namespace EndlessEngine.Export
{
    /// <summary>
    /// Export/Import save state as a base64-encoded string (build code).
    ///
    /// Build codes are safe to share via clipboard or text — they are
    /// base64-encoded JSON of SaveData. Import validates structure before applying.
    ///
    /// Security note: Build codes are player-facing and can be edited.
    /// Never trust imported data for server-authoritative stats.
    /// Local games only — no server validation.
    /// </summary>
    public class ExportService : MonoBehaviour
    {
        public static event Action<string>  OnExportComplete; // build code
        public static event Action<string>  OnImportComplete; // build code
        public static event Action<string>  OnImportFailed;   // reason

        private SaveService _saveService;

        public void Initialize(SaveService saveService)
        {
            _saveService = saveService;
        }

        // ── Export ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Exports the currently loaded save. Convenience wrapper over <see cref="ExportToCode"/>.
        /// Returns empty string if SaveService has no loaded data.
        /// </summary>
        public string ExportCurrentSave()
        {
            var saveData = _saveService?.GetCurrentSaveData();
            if (saveData == null)
            {
                Debug.LogWarning("[ExportService] No loaded save data to export.");
                return string.Empty;
            }
            return ExportToCode(saveData);
        }

        /// <summary>
        /// Serialize current SaveData to a build code string.
        /// Copies to system clipboard automatically.
        /// </summary>
        public string ExportToCode(SaveData saveData)
        {
            if (saveData == null)
            {
                Debug.LogWarning("[ExportService] SaveData is null — cannot export.");
                return string.Empty;
            }

            string json      = Newtonsoft.Json.JsonConvert.SerializeObject(saveData);
            string buildCode = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

            GUIUtility.systemCopyBuffer = buildCode;
            OnExportComplete?.Invoke(buildCode);
            Debug.Log($"[ExportService] Build code copied to clipboard ({buildCode.Length} chars).");
            return buildCode;
        }

        // ── Import ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Deserialize a build code to SaveData and validate its structure.
        /// Does NOT auto-apply — returns the decoded SaveData for caller to decide.
        /// </summary>
        public bool TryImportFromCode(string buildCode, out SaveData saveData)
        {
            saveData = null;
            if (string.IsNullOrWhiteSpace(buildCode))
            {
                OnImportFailed?.Invoke("EmptyCode");
                return false;
            }

            try
            {
                string json = Encoding.UTF8.GetString(Convert.FromBase64String(buildCode));
                saveData = Newtonsoft.Json.JsonConvert.DeserializeObject<SaveData>(json);
                if (saveData == null)
                {
                    OnImportFailed?.Invoke("NullAfterDeserialize");
                    return false;
                }

                saveData.EnsureDefaults();

                if (saveData.SchemaVersion < 0)
                {
                    OnImportFailed?.Invoke("InvalidSchemaVersion");
                    saveData = null;
                    return false;
                }

                OnImportComplete?.Invoke(buildCode);
                Debug.Log($"[ExportService] Import successful — schema v{saveData.SchemaVersion}.");
                return true;
            }
            catch (FormatException)
            {
                OnImportFailed?.Invoke("InvalidBase64");
                return false;
            }
            catch (Exception e)
            {
                OnImportFailed?.Invoke($"DeserializeError: {e.Message}");
                return false;
            }
        }

        private void OnDestroy() => ClearSubscribersForTesting();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public static void ClearSubscribersForTesting()
        {
            OnExportComplete = null;
            OnImportComplete = null;
            OnImportFailed   = null;
        }
#else
        private static void ClearSubscribersForTesting() { }
#endif
    }
}

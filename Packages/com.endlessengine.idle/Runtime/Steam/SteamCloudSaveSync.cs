using System;
using System.Text;
using UnityEngine;
using EndlessEngine.SaveAndLoad;
using static EndlessEngine.SaveAndLoad.CloudSaveMergeStrategy;
using Debug = UnityEngine.Debug;

namespace EndlessEngine.Steam
{
    /// <summary>
    /// Syncs SaveService saves to Steam Remote Storage (Steam Cloud).
    ///
    /// Strategy: local save is authoritative. Steam Cloud is a backup copy.
    /// On load: if Steam cloud file is newer than local, offer to restore it
    ///          (fires OnCloudSaveNewerThanLocal — game decides whether to apply).
    /// On save: upload primary JSON to Steam Cloud in the background.
    ///
    /// HMAC signature is uploaded alongside the save so integrity is preserved
    /// across cloud restore. The signature file uses the same naming convention
    /// as the local sidecar: {filename}.sig
    ///
    /// Bootstrap wiring:
    ///   cloudSync.Initialize(saveService, steamService);
    ///   // Call after SteamService.Initialize() and before SaveService.LoadAsync().
    /// </summary>
    public class SteamCloudSaveSync : MonoBehaviour
    {
        private const string CloudSaveFile    = SaveConstants.PrimaryFile;
        private const string CloudSigFile     = SaveConstants.PrimaryFile + ".sig";

        // ── Events ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Fires when a cloud save exists and its timestamp is newer than the local save.
        /// Subscribe to show a "Restore from cloud?" dialog — call RestoreFromCloud()
        /// if the player confirms, or ignore to keep the local save.
        /// </summary>
        public static event Action OnCloudSaveNewerThanLocal;

        // ── State ─────────────────────────────────────────────────────────────────

        private SaveService           _saveService;
        private ISteamService         _steam;
        private bool                  _uploadPending;
        private CloudSaveMergeStrategy _mergeStrategy = TakeHigherPrestigeCloudMergeStrategy.Instance;

        // ── Bootstrap ─────────────────────────────────────────────────────────────

        public void Initialize(SaveService saveService, ISteamService steam,
            CloudSaveMergeStrategy mergeStrategy = null)
        {
            _saveService   = saveService;
            _steam         = steam ?? NullSteamService.Instance;
            _mergeStrategy = mergeStrategy ?? TakeHigherPrestigeCloudMergeStrategy.Instance;
        }

        // ── Unity Lifecycle ───────────────────────────────────────────────────────

        private void OnEnable()
        {
            if (_saveService != null)
                _saveService.OnSaveCompleted += HandleSaveCompleted;
        }

        private void OnDisable()
        {
            if (_saveService != null)
                _saveService.OnSaveCompleted -= HandleSaveCompleted;
        }

        // ── Cloud check on startup ────────────────────────────────────────────────

        /// <summary>
        /// Checks whether a newer cloud save exists. Call once before LoadAsync().
        /// Fires OnCloudSaveNewerThanLocal if a restoration candidate is found.
        /// </summary>
        public void CheckForNewerCloudSave()
        {
            if (!_steam.IsAvailable) return;
            if (!_steam.CloudSaveExists(CloudSaveFile)) return;

            // Read cloud save timestamp and compare to local
            _steam.CloudSaveRead(CloudSaveFile, cloudBytes =>
            {
                if (cloudBytes == null) return;

                try
                {
                    string json      = Encoding.UTF8.GetString(cloudBytes);
                    var    cloudData = Newtonsoft.Json.JsonConvert.DeserializeObject<SaveData>(json);
                    if (cloudData == null) return;

                    var localData  = _saveService.GetCurrentSaveData();
                    var resolved   = _mergeStrategy.Resolve(localData, cloudData);
                    bool cloudWon  = !ReferenceEquals(resolved, localData) &&
                                    (resolved == cloudData ||
                                     (resolved.LastSessionTimestamp > (localData?.LastSessionTimestamp ?? DateTime.MinValue)));

                    if (cloudWon)
                    {
                        Debug.Log($"[SteamCloudSaveSync] Merge strategy chose cloud save " +
                                  $"(prestige cloud={cloudData.PrestigeCount} local={localData?.PrestigeCount ?? 0}).");
                        OnCloudSaveNewerThanLocal?.Invoke();
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[SteamCloudSaveSync] Could not parse cloud save for comparison: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// Downloads the cloud save and applies it via SaveService.ApplyImportedSaveData().
        /// Call in response to the player confirming a cloud restore prompt.
        /// </summary>
        public void RestoreFromCloud(Action<bool> onComplete = null)
        {
            if (!_steam.IsAvailable) { onComplete?.Invoke(false); return; }

            _steam.CloudSaveRead(CloudSaveFile, bytes =>
            {
                if (bytes == null) { onComplete?.Invoke(false); return; }

                try
                {
                    string   json = Encoding.UTF8.GetString(bytes);
                    SaveData data = Newtonsoft.Json.JsonConvert.DeserializeObject<SaveData>(json);
                    if (data == null) { onComplete?.Invoke(false); return; }

                    _saveService.ApplyImportedSaveData(data);
                    Debug.Log("[SteamCloudSaveSync] Restored save from Steam Cloud.");
                    onComplete?.Invoke(true);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[SteamCloudSaveSync] RestoreFromCloud failed: {ex.Message}");
                    onComplete?.Invoke(false);
                }
            });
        }

        // ── Upload on save ────────────────────────────────────────────────────────

        private void HandleSaveCompleted(bool success)
        {
            if (!success || !_steam.IsAvailable) return;

            // Read the just-written local file and push to cloud
            string localPath = System.IO.Path.Combine(
                UnityEngine.Application.persistentDataPath, CloudSaveFile);
            string sigPath   = localPath + ".sig";

            if (!System.IO.File.Exists(localPath)) return;

            try
            {
                byte[] saveBytes = System.IO.File.ReadAllBytes(localPath);
                _steam.CloudSaveWrite(CloudSaveFile, saveBytes, ok =>
                {
                    if (!ok) Debug.LogWarning("[SteamCloudSaveSync] Failed to upload save to Steam Cloud.");
                });

                // Upload signature alongside save
                if (System.IO.File.Exists(sigPath))
                {
                    byte[] sigBytes = System.IO.File.ReadAllBytes(sigPath);
                    _steam.CloudSaveWrite(CloudSigFile, sigBytes);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SteamCloudSaveSync] Cloud upload read failed: {ex.Message}");
            }
        }
    }
}

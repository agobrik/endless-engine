using System;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace EndlessEngine.SaveAndLoad
{
    /// <summary>
    /// Determines how to resolve a conflict between a local save and a cloud save.
    ///
    /// Used by SteamCloudSaveSync (and future cloud providers) when both saves exist
    /// and neither is obviously the winner.
    ///
    /// The default strategy (TakeNewer) picks whichever has the higher
    /// LastSessionTimestamp. Game code can override to implement custom merge logic
    /// (e.g. take higher prestige count, or merge specific fields).
    /// </summary>
    public abstract class CloudSaveMergeStrategy
    {
        /// <summary>Resolves the conflict between the local and cloud SaveData.</summary>
        /// <param name="local">Current on-disk save.</param>
        /// <param name="cloud">Downloaded cloud save.</param>
        /// <returns>The SaveData that should be used as the authoritative state.</returns>
        public abstract SaveData Resolve(SaveData local, SaveData cloud);

        /// <summary>
        /// Fires when Resolve() returns the cloud save so callers can show a notification.
        /// Not raised by the strategy itself — SteamCloudSaveSync raises it.
        /// </summary>
        public static event Action<CloudMergeResult> OnMergeCompleted;

        internal static void RaiseMergeCompleted(CloudMergeResult result)
            => OnMergeCompleted?.Invoke(result);
    }

    public enum CloudMergeResult
    {
        UsedLocal,
        UsedCloud,
        Merged,
    }

    // ── Built-in strategies ───────────────────────────────────────────────────────

    /// <summary>Picks the save with the higher LastSessionTimestamp. Tie → local wins.</summary>
    public sealed class TakeNewerCloudMergeStrategy : CloudSaveMergeStrategy
    {
        public static readonly TakeNewerCloudMergeStrategy Instance = new();
        private TakeNewerCloudMergeStrategy() { }

        public override SaveData Resolve(SaveData local, SaveData cloud)
        {
            bool cloudNewer = cloud != null && (local == null ||
                              cloud.LastSessionTimestamp > local.LastSessionTimestamp);
            var result = cloudNewer ? cloud : local;
            var which  = cloudNewer ? CloudMergeResult.UsedCloud : CloudMergeResult.UsedLocal;
            RaiseMergeCompleted(which);
            Debug.Log($"[CloudMerge] TakeNewer → {which}");
            return result;
        }
    }

    /// <summary>
    /// Picks the save with the higher PrestigeCount.
    /// If equal, falls back to TakeNewerCloudMergeStrategy.
    /// Useful for idle games where prestige is the primary progress metric.
    /// </summary>
    public sealed class TakeHigherPrestigeCloudMergeStrategy : CloudSaveMergeStrategy
    {
        public static readonly TakeHigherPrestigeCloudMergeStrategy Instance = new();
        private TakeHigherPrestigeCloudMergeStrategy() { }

        public override SaveData Resolve(SaveData local, SaveData cloud)
        {
            if (cloud == null) { RaiseMergeCompleted(CloudMergeResult.UsedLocal); return local; }
            if (local == null) { RaiseMergeCompleted(CloudMergeResult.UsedCloud); return cloud; }

            if (cloud.PrestigeCount > local.PrestigeCount)
            {
                RaiseMergeCompleted(CloudMergeResult.UsedCloud);
                Debug.Log($"[CloudMerge] TakeHigherPrestige → cloud (prestige {cloud.PrestigeCount} > {local.PrestigeCount})");
                return cloud;
            }
            if (local.PrestigeCount > cloud.PrestigeCount)
            {
                RaiseMergeCompleted(CloudMergeResult.UsedLocal);
                return local;
            }
            // Equal prestige — fall back to newer timestamp
            return TakeNewerCloudMergeStrategy.Instance.Resolve(local, cloud);
        }
    }

    /// <summary>
    /// Field-level merge: takes the maximum across key numeric fields.
    /// - PrestigeCount:      max
    /// - CurrentResources:   max
    /// - TotalWavesCleared:  max (if field exists)
    /// - CompletedMilestones: union
    /// - Schema version:     max (ensures forward migration)
    /// </summary>
    public sealed class MaxFieldCloudMergeStrategy : CloudSaveMergeStrategy
    {
        public static readonly MaxFieldCloudMergeStrategy Instance = new();
        private MaxFieldCloudMergeStrategy() { }

        public override SaveData Resolve(SaveData local, SaveData cloud)
        {
            if (cloud == null) { RaiseMergeCompleted(CloudMergeResult.UsedLocal); return local; }
            if (local == null) { RaiseMergeCompleted(CloudMergeResult.UsedCloud); return cloud; }

            // Start from the newer save as the base, then take max fields
            var newer = cloud.LastSessionTimestamp >= local.LastSessionTimestamp ? cloud : local;
            var older = cloud.LastSessionTimestamp >= local.LastSessionTimestamp ? local : cloud;

            var merged = new SaveData();
            CopyFrom(merged, newer);

            // Max numeric fields
            merged.PrestigeCount      = Math.Max(newer.PrestigeCount,     older.PrestigeCount);
            merged.CurrentResources   = Math.Max(newer.CurrentResources,  older.CurrentResources);
            merged.SchemaVersion      = Math.Max(newer.SchemaVersion,     older.SchemaVersion);

            // Union milestones
            if (older.CompletedMilestones != null && merged.CompletedMilestones != null)
                foreach (var m in older.CompletedMilestones)
                    merged.CompletedMilestones.Add(m);

            RaiseMergeCompleted(CloudMergeResult.Merged);
            Debug.Log($"[CloudMerge] MaxField → merged (prestige={merged.PrestigeCount})");
            return merged;
        }

        private static void CopyFrom(SaveData dst, SaveData src)
        {
            // Shallow copy: start from JSON round-trip to avoid shared reference issues
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(src);
            var copy = Newtonsoft.Json.JsonConvert.DeserializeObject<SaveData>(json);
            if (copy == null) return;
            dst.PrestigeCount            = copy.PrestigeCount;
            dst.CurrentResources         = copy.CurrentResources;
            dst.SchemaVersion            = copy.SchemaVersion;
            dst.LastSessionTimestamp     = copy.LastSessionTimestamp;
            dst.CompletedMilestones      = copy.CompletedMilestones ?? new System.Collections.Generic.HashSet<string>();
            dst.NumberBackendName        = copy.NumberBackendName;
            dst.CurrentResourcesMantissa = copy.CurrentResourcesMantissa;
            dst.CurrentResourcesExponent = copy.CurrentResourcesExponent;
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace EndlessEngine.Feature
{
    /// <summary>
    /// Static feature flag service for A/B testing and staged rollouts.
    ///
    /// Flags are string-keyed booleans or string values. The active IFeatureFlagProvider
    /// supplies the flag values; the default is LocalFeatureFlagProvider which reads
    /// from a dictionary set at startup.
    ///
    /// Bootstrap wiring — static flag overrides (local/editor):
    ///   FeatureFlagService.SetFlag("new_meta_ui", true);
    ///   FeatureFlagService.SetFlag("ab_prestige_flow", "variant_b");
    ///
    /// Bootstrap wiring — remote provider:
    ///   FeatureFlagService.SetProvider(new RemoteConfigFeatureFlagProvider(remoteConfig));
    /// </summary>
    public static class FeatureFlagService
    {
        private static IFeatureFlagProvider _provider = LocalFeatureFlagProvider.Instance;

        // ── Provider ──────────────────────────────────────────────────────────────

        public static void SetProvider(IFeatureFlagProvider provider)
            => _provider = provider ?? LocalFeatureFlagProvider.Instance;

        // ── Boolean flags ─────────────────────────────────────────────────────────

        /// <summary>Returns the boolean value of a flag. Returns defaultValue if not set.</summary>
        public static bool IsEnabled(string flagKey, bool defaultValue = false)
        {
            try { return _provider.GetBool(flagKey, defaultValue); }
            catch (Exception ex)
            {
                Debug.LogError($"[FeatureFlagService] IsEnabled '{flagKey}' failed: {ex.Message}");
                return defaultValue;
            }
        }

        // ── String flags ──────────────────────────────────────────────────────────

        /// <summary>Returns the string value of a flag. Returns defaultValue if not set.</summary>
        public static string GetVariant(string flagKey, string defaultValue = "control")
        {
            try { return _provider.GetString(flagKey, defaultValue); }
            catch (Exception ex)
            {
                Debug.LogError($"[FeatureFlagService] GetVariant '{flagKey}' failed: {ex.Message}");
                return defaultValue;
            }
        }

        // ── Convenience: local overrides (tests / editor / staging) ──────────────

        public static void SetFlag(string flagKey, bool value)
            => LocalFeatureFlagProvider.Instance.SetBool(flagKey, value);

        public static void SetFlag(string flagKey, string value)
            => LocalFeatureFlagProvider.Instance.SetString(flagKey, value);

        public static void ClearFlags() => LocalFeatureFlagProvider.Instance.Clear();
    }

    // ── Provider interface ────────────────────────────────────────────────────────

    public interface IFeatureFlagProvider
    {
        bool   GetBool(string flagKey, bool defaultValue);
        string GetString(string flagKey, string defaultValue);
    }

    // ── Default local provider ────────────────────────────────────────────────────

    /// <summary>In-memory flag store. Used as the default and for local overrides.</summary>
    public sealed class LocalFeatureFlagProvider : IFeatureFlagProvider
    {
        public static readonly LocalFeatureFlagProvider Instance = new();
        private LocalFeatureFlagProvider() { }

        private readonly Dictionary<string, string> _flags = new();

        public void SetBool(string key, bool value)   => _flags[key] = value ? "true" : "false";
        public void SetString(string key, string value) => _flags[key] = value ?? "";
        public void Clear() => _flags.Clear();

        public bool GetBool(string key, bool defaultValue)
        {
            if (!_flags.TryGetValue(key, out var v)) return defaultValue;
            return v == "true" || v == "1";
        }

        public string GetString(string key, string defaultValue)
            => _flags.TryGetValue(key, out var v) ? v : defaultValue;
    }

    /// <summary>Standard feature flag key constants.</summary>
    public static class FeatureFlags
    {
        public const string NewMetaUI          = "new_meta_ui";
        public const string PrestigeFlowVariant = "ab_prestige_flow";
        public const string TraitSystemEnabled  = "trait_system_enabled";
        public const string CloudSaveEnabled    = "cloud_save_enabled";
        public const string IapEnabled          = "iap_enabled";
    }
}

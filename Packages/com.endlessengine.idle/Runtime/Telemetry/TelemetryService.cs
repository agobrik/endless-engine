using System.Collections.Generic;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace EndlessEngine.Telemetry
{
    /// <summary>
    /// Engine-level telemetry facade. Routes Track() calls to the active ITelemetryProvider.
    ///
    /// Bootstrap wiring:
    ///   TelemetryService.SetProvider(new MyAnalyticsProvider());
    ///
    /// Defaults to NullTelemetryProvider — no data sent until a provider is injected.
    /// All methods are null-safe and never throw.
    /// </summary>
    public static class TelemetryService
    {
        private static ITelemetryProvider _provider = NullTelemetryProvider.Instance;

        /// <summary>Replaces the active provider. Call from Bootstrap before first Track().</summary>
        public static void SetProvider(ITelemetryProvider provider)
        {
            _provider = provider ?? NullTelemetryProvider.Instance;
            Debug.Log($"[TelemetryService] Provider set: {_provider.GetType().Name}");
        }

        /// <summary>Tracks an event with no additional properties.</summary>
        public static void Track(string eventName)
        {
            try { _provider.Track(eventName); }
            catch (System.Exception ex)
            { Debug.LogError($"[TelemetryService] Track '{eventName}' failed: {ex.Message}"); }
        }

        /// <summary>Tracks an event with key-value properties.</summary>
        public static void Track(string eventName, Dictionary<string, object> properties)
        {
            try { _provider.Track(eventName, properties); }
            catch (System.Exception ex)
            { Debug.LogError($"[TelemetryService] Track '{eventName}' failed: {ex.Message}"); }
        }

        /// <summary>Sets a persistent player property on the analytics backend.</summary>
        public static void SetPlayerProperty(string key, object value)
        {
            try { _provider.SetPlayerProperty(key, value); }
            catch (System.Exception ex)
            { Debug.LogError($"[TelemetryService] SetPlayerProperty '{key}' failed: {ex.Message}"); }
        }

        /// <summary>Returns true if the active provider is the no-op NullTelemetryProvider.</summary>
        public static bool IsNullProvider => _provider is NullTelemetryProvider;
    }
}

using System.Collections.Generic;

namespace EndlessEngine.Telemetry
{
    /// <summary>
    /// No-op ITelemetryProvider. Default until a real backend is configured.
    /// All calls are silently discarded — zero overhead, zero side effects.
    /// </summary>
    public sealed class NullTelemetryProvider : ITelemetryProvider
    {
        public static readonly NullTelemetryProvider Instance = new NullTelemetryProvider();

        public void Track(string eventName, Dictionary<string, object> properties = null) { }
        public void SetPlayerProperty(string key, object value) { }
    }
}

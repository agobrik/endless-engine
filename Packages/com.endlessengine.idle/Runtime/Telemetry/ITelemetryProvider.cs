namespace EndlessEngine.Telemetry
{
    /// <summary>
    /// Abstraction over analytics backends (GameAnalytics, Unity Analytics, custom).
    /// Engine fires events via TelemetryService.Track(). The active provider routes them.
    ///
    /// Default provider is NullTelemetryProvider (no-op).
    /// Replace at Bootstrap with a concrete implementation per game.
    ///
    /// Event names follow the convention: category.action[.detail]
    /// e.g. "session.started", "prestige.triggered", "generator.purchased"
    /// </summary>
    public interface ITelemetryProvider
    {
        /// <summary>
        /// Tracks a named event with optional key-value properties.
        /// Implementations must be null-safe and never throw.
        /// </summary>
        void Track(string eventName, System.Collections.Generic.Dictionary<string, object> properties = null);

        /// <summary>Sets a persistent player property (e.g. "prestige_count": 5).</summary>
        void SetPlayerProperty(string key, object value);
    }

    /// <summary>Standard event name constants. Extend per-game as needed.</summary>
    public static class TelemetryEvents
    {
        public const string SessionStarted        = "session.started";
        public const string SessionEnded          = "session.ended";
        public const string PrestigeTriggered     = "prestige.triggered";
        public const string AscensionTriggered    = "ascension.triggered";
        public const string GeneratorPurchased    = "generator.purchased";
        public const string UpgradePurchased      = "upgrade.purchased";
        public const string MilestoneReached      = "milestone.reached";
        public const string WaveCompleted         = "wave.completed";
        public const string OfflineReturnLarge    = "offline.return.large";   // > 30 min offline
        public const string ChurnSuspected        = "churn.suspected";        // > 15 min inactive
    }
}

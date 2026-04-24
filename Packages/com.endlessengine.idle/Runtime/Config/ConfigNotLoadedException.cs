using System;

namespace EndlessEngine.Config
{
    /// <summary>
    /// Thrown when a ConfigRegistry accessor is called before OnConfigsLoaded fires.
    /// Only thrown in UNITY_EDITOR or DEVELOPMENT_BUILD — production boot sequencing
    /// prevents early access; this guard exists to catch developer mistakes.
    ///
    /// ADR: ADR-0003 — ConfigRegistry Static Service Locator
    /// </summary>
    public class ConfigNotLoadedException : Exception
    {
        /// <summary>
        /// Creates a ConfigNotLoadedException for the named accessor.
        /// </summary>
        /// <param name="accessorName">
        /// Name of the ConfigRegistry property that was accessed before load
        /// (e.g., "Enemy", "Wave", "Upgrades").
        /// </param>
        public ConfigNotLoadedException(string accessorName)
            : base($"ConfigRegistry.{accessorName} was accessed before OnConfigsLoaded fired. " +
                   "Ensure all gameplay systems subscribe to ConfigRegistry.OnConfigsLoaded " +
                   "before reading config properties, or use [DefaultExecutionOrder] to run " +
                   "after ConfigLoadingService.")
        {
        }
    }
}

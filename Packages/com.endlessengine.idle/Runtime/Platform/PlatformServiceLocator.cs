namespace EndlessEngine.Platform
{
    /// <summary>
    /// Service locator for IPlatformService.
    ///
    /// Single global access point — avoids threading through IPlatformService as a
    /// constructor argument to every system that needs it (AudioService, UIControllers, etc.).
    ///
    /// Default: NullPlatformService (safe no-op until Bootstrap configures a real backend).
    ///
    /// Bootstrap wiring:
    ///   PlatformServiceLocator.Set(new SteamPlatformService(steamService));
    ///
    /// Usage:
    ///   PlatformServiceLocator.Current.UnlockAchievement("ACH_FIRST_PRESTIGE");
    /// </summary>
    public static class PlatformServiceLocator
    {
        private static IPlatformService _current = NullPlatformService.Instance;

        /// <summary>The active platform service. Never null.</summary>
        public static IPlatformService Current => _current;

        /// <summary>Replaces the active service. Pass null to reset to NullPlatformService.</summary>
        public static void Set(IPlatformService service)
        {
            _current = service ?? NullPlatformService.Instance;
        }

        /// <summary>Resets to NullPlatformService. Call in test TearDown.</summary>
        public static void Reset() => _current = NullPlatformService.Instance;
    }
}

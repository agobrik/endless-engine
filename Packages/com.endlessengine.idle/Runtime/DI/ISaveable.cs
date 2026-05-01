using EndlessEngine.SaveAndLoad;

namespace EndlessEngine.DI
{
    /// <summary>
    /// Unified save/load interface for DI-managed services.
    ///
    /// Services that implement ISaveable are automatically registered with
    /// SaveService by the DI container. This replaces the manual
    /// saveService.RegisterStateProvider(myService) calls in Bootstrap.
    ///
    /// Implement this in addition to (or instead of) ISaveStateProvider when
    /// a service is managed by the DI container.
    ///
    /// ProviderOrder determines the sequence in which services are serialized.
    /// Use SaveConstants.SaveProviderOrder constants for well-known services.
    /// </summary>
    public interface ISaveable : ISaveStateProvider
    {
        // ISaveStateProvider already defines:
        //   int  ProviderOrder { get; }
        //   void OnBeforeSave(SaveData saveData);
        //   void OnAfterLoad(SaveData saveData);
    }
}

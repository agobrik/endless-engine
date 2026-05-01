using System;
using UnityEngine;

namespace EndlessEngine.Config
{
    /// <summary>
    /// Abstracts config loading so that runtime (Addressables), test (direct SO injection),
    /// and future hot-reload backends all satisfy the same interface.
    ///
    /// Current implementations:
    ///   AddressablesConfigProvider — production, loads from Addressables async
    ///   DirectConfigProvider       — vertical-slice / tests, takes SOs directly
    ///
    /// v1.3 will add FileWatcherConfigProvider for editor hot-reload: watches
    /// JSON override files and re-fires OnConfigReloaded when they change.
    ///
    /// Bootstrap pattern:
    ///   _configProvider.Load(onComplete: () => { /* all systems ready */ });
    /// </summary>
    public interface IConfigProvider
    {
        /// <summary>Fires when configs are fully loaded and ConfigRegistry is populated.</summary>
        event Action OnConfigsLoaded;

        /// <summary>
        /// Fires when a config is hot-reloaded at runtime.
        /// <typeparamref name="ScriptableObject"/> is the updated SO.
        /// Only fired by implementations that support hot-reload (v1.3+).
        /// </summary>
        event Action<ScriptableObject> OnConfigReloaded;

        /// <summary>True after a successful Load() call completes.</summary>
        bool IsLoaded { get; }

        /// <summary>
        /// Begins loading configs. Fires OnConfigsLoaded on completion.
        /// Calling Load() again after IsLoaded=true is a no-op.
        /// </summary>
        void Load();

        /// <summary>
        /// Returns the config of type T. Throws ConfigNotLoadedException if Load()
        /// has not completed. Returns null if no config of that type was registered.
        /// </summary>
        T Get<T>() where T : ScriptableObject;
    }
}

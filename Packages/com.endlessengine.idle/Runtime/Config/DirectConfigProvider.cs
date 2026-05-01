using System;
using System.Collections.Generic;
using UnityEngine;

namespace EndlessEngine.Config
{
    /// <summary>
    /// IConfigProvider backed by ScriptableObjects assigned directly in code or Inspector.
    /// Used by: vertical-slice Bootstrap, EditMode tests, PlayMode tests.
    ///
    /// Usage:
    ///   var provider = new DirectConfigProvider();
    ///   provider.Register(myEconomyConfig);
    ///   provider.Register(myWaveConfig);
    ///   provider.Load(); // synchronous, fires OnConfigsLoaded immediately
    ///   T cfg = provider.Get<T>();
    /// </summary>
    public class DirectConfigProvider : IConfigProvider
    {
        public event Action                    OnConfigsLoaded;
        public event Action<ScriptableObject>  OnConfigReloaded;

        public bool IsLoaded { get; private set; }

        private readonly Dictionary<Type, ScriptableObject> _configs = new Dictionary<Type, ScriptableObject>();

        /// <summary>Registers a config SO. Must be called before Load().</summary>
        public void Register<T>(T config) where T : ScriptableObject
        {
            if (config != null)
                _configs[typeof(T)] = config;
        }

        /// <summary>Registers a config under the given type key (for interface/base-class lookup).</summary>
        public void Register(Type type, ScriptableObject config)
        {
            if (config != null)
                _configs[type] = config;
        }

        public void Load()
        {
            if (IsLoaded) return;
            IsLoaded = true;
            OnConfigsLoaded?.Invoke();
        }

        public T Get<T>() where T : ScriptableObject
        {
            if (!IsLoaded) throw new ConfigNotLoadedException(typeof(T).Name);

            if (_configs.TryGetValue(typeof(T), out var so))
                return so as T;

            return null;
        }

        // Suppress unused event warning — implementations may not fire this
        protected void FireConfigReloaded(ScriptableObject so) => OnConfigReloaded?.Invoke(so);
    }
}

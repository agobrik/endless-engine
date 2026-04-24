using System;
using System.Collections.Generic;
using EndlessEngine.Config;

namespace EndlessEngine.SaveAndLoad
{
    /// <summary>
    /// Creates a new-game <see cref="SaveData"/> with correct defaults.
    /// Reads initial values from <see cref="ConfigRegistry"/> — must only be called
    /// after <see cref="ConfigRegistry.OnConfigsLoaded"/> fires.
    ///
    /// ADR: ADR-0002 — Save Serialization Format and Atomic Write Pattern
    /// </summary>
    public static class SaveDataFactory
    {
        /// <summary>
        /// Creates a fully-initialized <see cref="SaveData"/> for a new game.
        /// All fields are at their canonical starting values.
        /// </summary>
        public static SaveData CreateNewGame()
        {
            int schemaVersion = 0;
            try { schemaVersion = ConfigRegistry.Schema.CurrentSchemaVersion; } catch { }

            float baseMultiplier = 1.5f;
            try { baseMultiplier = ConfigRegistry.Prestige.BaseMultiplierPerPrestige; } catch { }

            string realmSlug = "default";
            try { realmSlug = ConfigRegistry.Realm.RealmSlug; } catch { }

            return new SaveData
            {
                SchemaVersion              = schemaVersion,
                LastSessionTimestamp       = DateTime.UtcNow,
                SessionDurationSeconds     = 0f,

                CurrentResources           = 0L,

                UpgradeNodeStates          = new Dictionary<string, int>(),
                GeneratorStates            = new Dictionary<string, EndlessEngine.Generator.GeneratorState>(),

                PrestigeCount              = 0,
                BaseMultiplierPerPrestige  = baseMultiplier,

                WaveNumber                 = 1,
                CurrentRunState            = RunState.Active,

                CurrentRealmSlug           = realmSlug,
                UnlockedRealmSlugs         = new System.Collections.Generic.List<string> { realmSlug },

                PrestigeInProgress                = false,
                PrePrestigeResources              = 0L,
                PrePrestigeUpgradeNodeStates      = new Dictionary<string, int>(),
                PrePrestigeWaveNumber             = 0,
            };
        }
    }

    /// <summary>
    /// Valid values for <see cref="SaveData.CurrentRunState"/>.
    /// Serialized as string in JSON for forward-compat.
    /// </summary>
    public static class RunState
    {
        /// <summary>Player is in an active run.</summary>
        public const string Active = "Active";

        /// <summary>Player is offline or in idle recovery between runs.</summary>
        public const string IdleRecovery = "IdleRecovery";
    }
}

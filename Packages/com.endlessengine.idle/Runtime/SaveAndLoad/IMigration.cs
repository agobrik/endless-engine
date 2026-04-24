namespace EndlessEngine.SaveAndLoad
{
    /// <summary>
    /// Represents a single schema version increment for save data migration.
    /// Each migration mutates a <see cref="SaveData"/> instance in-place from
    /// <see cref="FromVersion"/> to <see cref="ToVersion"/>.
    ///
    /// Rules:
    /// - Always set explicit defaults for new fields; never rely on Newtonsoft null-on-missing.
    /// - Do not modify fields that were not added/changed in this version step.
    ///
    /// ADR: ADR-0002 — Save Serialization Format and Atomic Write Pattern
    /// </summary>
    public interface IMigration
    {
        /// <summary>The schema version this migration reads from.</summary>
        int FromVersion { get; }

        /// <summary>The schema version this migration produces.</summary>
        int ToVersion { get; }

        /// <summary>Mutates <paramref name="data"/> in-place to match <see cref="ToVersion"/> schema.</summary>
        void Migrate(SaveData data);
    }
}

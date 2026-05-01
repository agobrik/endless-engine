using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace EndlessEngine.SaveAndLoad
{
    /// <summary>
    /// Applies a chain of <see cref="IMigration"/> steps to advance
    /// <see cref="SaveData.SchemaVersion"/> from its current value to
    /// <paramref name="targetVersion"/>.
    ///
    /// Call <see cref="IsSaveCompatible"/> before <see cref="Apply"/> to guard
    /// against saves that are too old (below MinCompatibleVersion) or from a
    /// future schema version (downgrade scenario).
    ///
    /// ADR: ADR-0002 Rules 18–20
    /// </summary>
    public class MigrationPipeline
    {
        private readonly List<IMigration> _migrations;

        /// <param name="migrations">All registered migrations, in any order. Sorted internally by <see cref="IMigration.FromVersion"/>.</param>
        public MigrationPipeline(IEnumerable<IMigration> migrations)
        {
            _migrations = migrations.OrderBy(m => m.FromVersion).ToList();
        }

        /// <summary>
        /// Returns true when <paramref name="saveVersion"/> is within the acceptable range:
        /// <c>minCompatibleVersion ≤ saveVersion ≤ currentVersion</c>.
        /// </summary>
        public static bool IsSaveCompatible(int saveVersion, int minCompatibleVersion, int currentVersion)
            => saveVersion >= minCompatibleVersion && saveVersion <= currentVersion;

        /// <summary>
        /// Applies migrations in sequence until <c>data.SchemaVersion == targetVersion</c>.
        /// Throws <see cref="MissingMigrationException"/> if a required step is absent.
        /// </summary>
        public void Apply(SaveData data, int targetVersion)
        {
            while (data.SchemaVersion < targetVersion)
            {
                var migration = _migrations.FirstOrDefault(m => m.FromVersion == data.SchemaVersion);
                if (migration == null)
                    throw new MissingMigrationException(data.SchemaVersion);

                migration.Migrate(data);
                data.SchemaVersion = migration.ToVersion;
                Debug.Log($"[MigrationPipeline] Migrated save from v{migration.FromVersion} to v{migration.ToVersion}.");
            }
        }
    }

    /// <summary>
    /// Thrown when <see cref="MigrationPipeline.Apply"/> cannot find a migration
    /// for the current <see cref="SaveData.SchemaVersion"/>.
    /// </summary>
    public class MissingMigrationException : Exception
    {
        public MissingMigrationException(int fromVersion)
            : base($"No IMigration registered for schema version {fromVersion}. Add a Migration_{fromVersion}_to_{fromVersion + 1} class.") { }
    }
}

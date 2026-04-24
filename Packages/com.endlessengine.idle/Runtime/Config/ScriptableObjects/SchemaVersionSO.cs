using UnityEngine;

namespace EndlessEngine.Config
{
    /// <summary>
    /// Defines the current save data schema version and migration compatibility window.
    /// Used by SaveService to determine whether a save file can be loaded or migrated.
    /// </summary>
    [CreateAssetMenu(fileName = "SchemaVersion", menuName = "Endless Engine/Config/Schema Version")]
    public class SchemaVersionSO : ScriptableObject
    {
        [Tooltip("Current schema version. Increment by 1 for every SaveData field change.")]
        public int CurrentSchemaVersion = 0;

        [Tooltip("Oldest schema version that can be migrated to current. Saves below this force a new game.")]
        public int MinimumCompatibleVersion = 0;
    }
}

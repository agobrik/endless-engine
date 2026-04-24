using UnityEngine;

namespace EndlessEngine.Config
{
    /// <summary>
    /// Central ordered list of all zone definitions.
    /// Analogous to GeneratorDatabaseSO — one asset holds all ZoneConfigSO references.
    /// ZoneSystem reads this on Initialize().
    /// </summary>
    [CreateAssetMenu(fileName = "ZoneDatabase",
                     menuName = "Endless Engine/Modules/Zone Database")]
    public class ZoneDatabaseSO : ScriptableObject
    {
        [Tooltip("All zones, ordered by unlock sequence.")]
        public ZoneConfigSO[] Zones;
    }
}

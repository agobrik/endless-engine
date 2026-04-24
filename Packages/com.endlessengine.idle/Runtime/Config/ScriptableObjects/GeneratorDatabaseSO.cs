using UnityEngine;

namespace EndlessEngine.Config
{
    /// <summary>
    /// Central database of all generator definitions.
    /// A single asset holds all GeneratorConfigSO references in order.
    /// Bootstrap reads this instead of individual GeneratorConfigSO references,
    /// keeping the Inspector clean as generator count grows.
    /// </summary>
    [CreateAssetMenu(fileName = "GeneratorDatabase", menuName = "Endless Engine/Config/Generator Database")]
    public class GeneratorDatabaseSO : ScriptableObject
    {
        [Tooltip("All generator types, ordered by unlock sequence.")]
        public GeneratorConfigSO[] Generators;
    }
}

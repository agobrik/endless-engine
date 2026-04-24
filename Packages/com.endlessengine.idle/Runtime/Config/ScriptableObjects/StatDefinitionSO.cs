using UnityEngine;

namespace EndlessEngine.Config
{
    /// <summary>
    /// Defines a tracked game statistic (lifetime counter or peak value).
    ///
    /// Create via: Tools → Endless Engine → Create Stat Definition
    /// </summary>
    [CreateAssetMenu(
        menuName = "Endless Engine/Statistics/Stat Definition",
        fileName = "StatDefinition")]
    public class StatDefinitionSO : ScriptableObject
    {
        [Tooltip("Unique stat key. Never change after release.")]
        public string StatId = "";

        [Tooltip("Player-facing display name.")]
        public string DisplayName = "";

        [Tooltip("Short description shown in the statistics screen.")]
        public string Description = "";

        [Tooltip("If true, records the maximum value ever seen rather than a running total.")]
        public bool IsPeakValue = false;
    }
}

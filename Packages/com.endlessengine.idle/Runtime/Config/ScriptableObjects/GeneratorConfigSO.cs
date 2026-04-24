using UnityEngine;

namespace EndlessEngine.Config
{
    /// <summary>
    /// Defines a single idle generator type (e.g. "Basic Producer", "Solar Array").
    /// Each purchased copy contributes BaseYieldPerSecond to passive income.
    ///
    /// Cost of the Nth copy = BaseCost * (CostScalingFactor ^ N).
    /// </summary>
    [CreateAssetMenu(fileName = "GeneratorConfig", menuName = "Endless Engine/Config/Generator Config")]
    public class GeneratorConfigSO : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Unique stable ID. Never change after first save.")]
        public string GeneratorId = "generator_basic";

        [Tooltip("Display name shown in the Generator Screen.")]
        public string DisplayName = "Basic Producer";

        [TextArea(2, 4)]
        [Tooltip("Short description shown in the UI.")]
        public string Description = "Generates gold passively.";

        [Header("Economy")]
        [Tooltip("Gold produced per second per owned copy (before upgrades).")]
        [Min(0f)]
        public float BaseYieldPerSecond = 1f;

        [Tooltip("Cost of the first copy.")]
        [Min(1)]
        public long BaseCost = 100;

        [Tooltip("Cost multiplier per additional copy. 1.15 = 15% more expensive each time.")]
        [Range(1f, 2f)]
        public float CostScalingFactor = 1.15f;

        [Tooltip("Maximum number of copies the player can own. -1 = unlimited.")]
        public int MaxCount = -1;

        [Header("Unlock")]
        [Tooltip("How many of the previous generator must be owned before this one is available. 0 = always unlocked.")]
        [Min(0)]
        public int UnlockRequirement = 0;

        [Tooltip("The generator that must reach UnlockRequirement count before this unlocks. Null = no prerequisite.")]
        public GeneratorConfigSO UnlockPrerequisite;

        /// <summary>Cost to buy the Nth copy (0-indexed: 0 = first copy).</summary>
        public long CostForCopy(int copyIndex)
        {
            if (copyIndex < 0) return BaseCost;
            return (long)(BaseCost * System.Math.Pow(CostScalingFactor, copyIndex));
        }
    }
}

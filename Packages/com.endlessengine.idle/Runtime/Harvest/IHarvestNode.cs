using UnityEngine;

namespace EndlessEngine.Harvest
{
    /// <summary>
    /// Contract for any world object that can be harvested by the cursor.
    /// Implemented by HarvestNode MonoBehaviour. Decoupled so tests can mock it.
    /// </summary>
    public interface IHarvestNode
    {
        string NodeId { get; }
        HarvestNodeConfigSO Config { get; }

        bool IsAlive { get; }
        float CurrentHP { get; }

        /// <summary>
        /// Apply harvest damage. Returns actual damage applied (clamped to remaining HP).
        /// If HP reaches 0, triggers depletion internally (VFX, respawn timer).
        /// </summary>
        float ApplyDamage(float amount);

        /// <summary>World-space position of the node centre.</summary>
        Vector2 WorldPosition { get; }
    }
}

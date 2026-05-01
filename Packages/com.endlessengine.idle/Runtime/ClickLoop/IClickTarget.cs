using UnityEngine;

namespace EndlessEngine.ClickLoop
{
    /// <summary>
    /// Contract for any world object that can be damaged by player clicks.
    /// Implemented by ClickTarget MonoBehaviour. Decoupled for testability.
    /// </summary>
    public interface IClickTarget
    {
        string              TargetId  { get; }
        ClickTargetConfigSO Config    { get; }
        bool                IsAlive   { get; }
        float               CurrentHP { get; }
        Vector2             WorldPosition { get; }

        /// <summary>
        /// Apply click damage. Returns actual damage applied (clamped to remaining HP).
        /// Triggers destruction internally (VFX, respawn timer) when HP reaches 0.
        /// </summary>
        float ApplyDamage(float amount);
    }
}

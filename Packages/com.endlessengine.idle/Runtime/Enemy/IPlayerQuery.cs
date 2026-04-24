using UnityEngine;

namespace EndlessEngine.Enemy
{
    /// <summary>
    /// Read-only interface EnemyManager uses to query player state.
    /// Decouples Enemy AI from the concrete PlayerHealthComponent.
    ///
    /// ADR: ADR-0006 — Enemy Update Loop
    /// </summary>
    public interface IPlayerQuery
    {
        /// <summary>Current world position of the player character.</summary>
        Vector2 Position { get; }

        /// <summary>True when the player is in IdleRecovery — enemies should pause.</summary>
        bool IsInIdleRecovery { get; }
    }
}

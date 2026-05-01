using System;

namespace EndlessEngine.DI
{
    /// <summary>
    /// Abstracts the tick event source so ITickable services and tests can be
    /// driven without depending on TickEngine (a MonoBehaviour) directly.
    ///
    /// TickEngine implements ITickSource. Tests use MockTickSource to fire ticks
    /// on demand without needing a running MonoBehaviour.
    ///
    /// The DI container registers TickEngine as ITickSource. Services receive
    /// ITickSource via constructor injection and call Subscribe/Unsubscribe in
    /// Initialize/Dispose instead of subscribing to TickEngine.OnTick directly.
    ///
    /// ADR: Accepted from Opus 4.7 review — ITickSource in DI sprint (v1.2).
    /// </summary>
    public interface ITickSource
    {
        /// <summary>Subscribe a callback to receive tick events.</summary>
        void Subscribe(Action<float> onTick);

        /// <summary>Unsubscribe a previously registered callback.</summary>
        void Unsubscribe(Action<float> onTick);

        /// <summary>True when the tick source is currently paused.</summary>
        bool IsPaused { get; }

        /// <summary>Current time scale multiplier.</summary>
        float TimeScale { get; }
    }
}

using System;

namespace EndlessEngine.DI
{
    /// <summary>
    /// Test implementation of ITickSource. Fires ticks on demand via FireTick().
    /// Use in EditMode tests and PlayMode tests where a running TickEngine MonoBehaviour
    /// is not available or desirable.
    /// </summary>
    public class MockTickSource : ITickSource
    {
        private Action<float> _subscribers;

        public bool  IsPaused  { get; set; } = false;
        public float TimeScale { get; set; } = 1f;

        public void Subscribe(Action<float> onTick)   => _subscribers += onTick;
        public void Unsubscribe(Action<float> onTick) => _subscribers -= onTick;

        /// <summary>Fires one tick with the given delta time. For test use only.</summary>
        public void FireTick(float deltaTime = 1f)
        {
            if (!IsPaused) _subscribers?.Invoke(deltaTime);
        }

        /// <summary>Clears all subscribers. Call in test TearDown.</summary>
        public void ClearSubscribers() => _subscribers = null;
    }
}

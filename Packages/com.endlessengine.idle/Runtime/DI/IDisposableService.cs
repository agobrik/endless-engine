namespace EndlessEngine.DI
{
    /// <summary>
    /// Marks a DI-managed service that holds unmanaged resources or event subscriptions
    /// that must be explicitly released when the container is torn down.
    ///
    /// Named IDisposableService to avoid collision with System.IDisposable.
    /// The DI container calls Dispose() on all registered IDisposableService instances
    /// in reverse-registration order during scene teardown or container shutdown.
    ///
    /// Typical uses:
    /// - Unsubscribe from static events (prevents memory leaks in multi-scene games)
    /// - Release native handles or unmanaged memory
    /// - Cancel pending async operations
    /// </summary>
    public interface IDisposableService
    {
        void Dispose();
    }
}

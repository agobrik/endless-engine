namespace EndlessEngine.DI
{
    /// <summary>
    /// Marks a service that requires a deterministic initialization call after
    /// all dependencies are resolved. The DI container calls Initialize() on all
    /// registered IInitializable instances in registration order after the container
    /// is built — before the first frame Update runs.
    ///
    /// Rules:
    /// - Initialize() must be idempotent (safe to call twice, second call no-ops).
    /// - Never call other services in a constructor — only in Initialize().
    /// - Dependencies may be injected via constructor or [Inject] field before
    ///   Initialize() fires.
    /// </summary>
    public interface IInitializable
    {
        void Initialize();
    }
}

using System.Collections;

namespace EndlessEngine.Modules
{
    /// <summary>
    /// Core lifecycle interface for all Endless Engine modules.
    ///
    /// Modules must be registered with ModuleRegistry before use.
    /// The registry controls initialization order, tick scheduling, and shutdown.
    ///
    /// Implementation contract:
    ///   - Init is called once after all modules are registered.
    ///   - Tick is called on TickEngine.OnTick (if ReceivesTick returns true).
    ///   - Shutdown is called on scene unload or explicit registry disposal.
    /// </summary>
    public interface IIdleModule
    {
        /// <summary>
        /// Unique identifier for this module. Used for dependency resolution and logging.
        /// Convention: lowercase-hyphenated, e.g. "economy", "skill-tree", "research"
        /// </summary>
        string ModuleId { get; }

        /// <summary>
        /// IDs of modules that must be initialized before this one.
        /// Circular dependencies will throw at registration time.
        /// </summary>
        string[] Dependencies { get; }

        /// <summary>
        /// Initialization order within the same dependency tier (lower = earlier).
        /// Modules with the same tier use Dependencies for ordering.
        /// </summary>
        int InitOrder { get; }

        /// <summary>
        /// Whether this module receives TickEngine ticks via OnTick(float dt).
        /// Set to false for data-only modules to avoid unnecessary tick overhead.
        /// </summary>
        bool ReceivesTick { get; }

        /// <summary>
        /// Initialize this module. Called by ModuleRegistry after dependencies are ready.
        /// </summary>
        /// <returns>Coroutine for async init (yield return null if synchronous).</returns>
        IEnumerator Init();

        /// <summary>
        /// Per-tick update. Called by TickEngine when ReceivesTick is true.
        /// </summary>
        /// <param name="dt">Delta time in seconds (TickEngine.TickInterval × TimeScale).</param>
        void Tick(float dt);

        /// <summary>
        /// Clean up resources, unsubscribe events. Called on shutdown.
        /// </summary>
        void Shutdown();
    }
}

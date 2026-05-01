#if VCONTAINER_ENABLED
using System.Collections.Generic;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using EndlessEngine.SaveAndLoad;
using EndlessEngine.Flow;
using EndlessEngine.Config;
using EndlessEngine.Economy.Math;

namespace EndlessEngine.DI
{
    /// <summary>
    /// Base LifetimeScope for Endless Engine games using VContainer.
    ///
    /// Activation:
    ///   1. Install VContainer via Package Manager (com.cysharp.vcontainer)
    ///   2. Add "VCONTAINER_ENABLED" to Player Settings → Scripting Define Symbols
    ///   3. Subclass GameLifetimeScope in your game project:
    ///      public class MyGameScope : GameLifetimeScope { protected override void Configure(IContainerBuilder b) { ... } }
    ///   4. Attach to a GameObject in your Bootstrap scene
    ///
    /// Auto-registration:
    ///   RegisterEngineService() handles IInitializable, ITickable, ISaveable,
    ///   IDisposableService automatically — no manual wiring needed.
    ///
    /// Pattern:
    ///   Override Configure() in your game scope, call base.Configure(builder)
    ///   first, then register game-specific services.
    /// </summary>
    public abstract class GameLifetimeScope : LifetimeScope
    {
        [Header("Engine Core (assign in Inspector)")]
        [SerializeField] protected TickEngine    _tickEngine;
        [SerializeField] protected SaveService   _saveService;
        [SerializeField] protected EconomyConfigSO _economyConfig;

        // Collected during Configure — processed in PostInitialize
        private readonly List<IInitializable>    _initializables  = new List<IInitializable>();
        private readonly List<ITickable>         _tickables       = new List<ITickable>();
        private readonly List<ISaveable>         _saveables       = new List<ISaveable>();
        private readonly List<IDisposableService> _disposables    = new List<IDisposableService>();

        // ── VContainer lifecycle ──────────────────────────────────────────────────

        protected override void Configure(IContainerBuilder builder)
        {
            // Configure numeric backend first — must precede any service registration
            if (_economyConfig != null)
                BigNumberFactory.Configure(_economyConfig.NumberBackend);

            // Register core engine services
            if (_tickEngine  != null) builder.RegisterComponent(_tickEngine).As<ITickSource>();
            if (_saveService != null) builder.RegisterComponent(_saveService);

            // Game-specific registrations happen in subclass override
        }

        protected override void Awake()
        {
            base.Awake();

            // After container is built, collect and process lifecycle registrations
            var container = Container;

            foreach (var init in container.Resolve<IEnumerable<IInitializable>>())
                _initializables.Add(init);

            foreach (var tick in container.Resolve<IEnumerable<ITickable>>())
                _tickables.Add(tick);

            foreach (var save in container.Resolve<IEnumerable<ISaveable>>())
                _saveables.Add(save);

            foreach (var disp in container.Resolve<IEnumerable<IDisposableService>>())
                _disposables.Add(disp);

            // Register tickables with the tick source
            if (_tickEngine != null)
                foreach (var t in _tickables)
                    _tickEngine.Subscribe(t.Tick);

            // Register saveables with the save service
            if (_saveService != null)
                foreach (var s in _saveables)
                    _saveService.RegisterStateProvider(s);

            // Initialize all services in registration order
            foreach (var init in _initializables)
                init.Initialize();
        }

        protected virtual void OnDestroy()
        {
            // Unsubscribe tickables
            if (_tickEngine != null)
                foreach (var t in _tickables)
                    _tickEngine.Unsubscribe(t.Tick);

            // Dispose in reverse order
            for (int i = _disposables.Count - 1; i >= 0; i--)
                _disposables[i].Dispose();
        }
    }
}
#endif

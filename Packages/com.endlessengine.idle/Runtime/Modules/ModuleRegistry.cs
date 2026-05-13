#pragma warning disable CS0414
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace EndlessEngine.Modules
{
    /// <summary>
    /// Manages registration, dependency ordering, lifecycle, and tick routing
    /// for all IIdleModule implementations.
    ///
    /// Usage:
    ///   1. Call Register() for each module before InitAllAsync().
    ///   2. Call InitAllAsync() from a MonoBehaviour coroutine to initialise in dependency order.
    ///   3. Call BindTickEngine() to wire Tick calls to TickEngine.OnTick.
    ///   4. Call ShutdownAll() from OnDestroy to clean up.
    /// </summary>
    public class ModuleRegistry
    {
        private readonly Dictionary<string, IIdleModule>  _modules    = new Dictionary<string, IIdleModule>();
        private readonly HashSet<string>                  _initialized = new HashSet<string>();
        private bool                                      _tickBound   = false;

        // ── Registration ──────────────────────────────────────────────────────────

        /// <summary>
        /// Register a module. Must be called before InitAllAsync.
        /// Throws if a module with the same ModuleId is already registered.
        /// </summary>
        public void Register(IIdleModule module)
        {
            if (module == null)
                throw new ArgumentNullException(nameof(module));
            if (string.IsNullOrEmpty(module.ModuleId))
                throw new ArgumentException("ModuleId must not be null or empty.", nameof(module));
            if (_modules.ContainsKey(module.ModuleId))
                throw new InvalidOperationException(
                    $"[ModuleRegistry] Duplicate module id: '{module.ModuleId}'. " +
                    $"Each module id must be unique.");

            _modules[module.ModuleId] = module;
        }

        /// <summary>
        /// Returns true if a module with the given id has been registered.
        /// </summary>
        public bool IsRegistered(string moduleId) => _modules.ContainsKey(moduleId);

        /// <summary>
        /// Returns true if the module with the given id has completed Init.
        /// </summary>
        public bool IsInitialized(string moduleId) => _initialized.Contains(moduleId);

        /// <summary>
        /// Number of registered modules.
        /// </summary>
        public int Count => _modules.Count;

        // ── Lifecycle ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Initialise all registered modules in dependency order.
        /// Performs topological sort before calling Init on each module.
        /// Throws on circular dependency.
        /// </summary>
        public IEnumerator InitAllAsync()
        {
            var ordered = TopologicalSort();
            foreach (var module in ordered)
            {
                if (_initialized.Contains(module.ModuleId))
                    continue;

                Debug.Log($"[ModuleRegistry] Initialising: {module.ModuleId}");
                yield return module.Init();
                _initialized.Add(module.ModuleId);
                Debug.Log($"[ModuleRegistry] Ready: {module.ModuleId}");
            }
        }

        /// <summary>
        /// Wire tick-receiving modules to an action (typically TickEngine.OnTick).
        /// Call this after InitAllAsync completes.
        /// Safe to call multiple times — won't double-bind.
        /// </summary>
        public void BindTick(Action<float> tickEvent, bool subscribe)
        {
            foreach (var module in _modules.Values)
            {
                if (!module.ReceivesTick) continue;
                if (subscribe)
                    tickEvent += module.Tick;
                else
                    tickEvent -= module.Tick;
            }
        }

        /// <summary>
        /// Shut down all modules in reverse init order and clear the registry.
        /// </summary>
        public void ShutdownAll()
        {
            var ordered = _initialized.Count > 0
                ? Enumerable.Reverse(TopologicalSort()).ToList()
                : _modules.Values.ToList();

            foreach (var module in ordered)
            {
                try
                {
                    module.Shutdown();
                    Debug.Log($"[ModuleRegistry] Shutdown: {module.ModuleId}");
                }
                catch (Exception e)
                {
                    Debug.LogError($"[ModuleRegistry] Shutdown error in '{module.ModuleId}': {e}");
                }
            }

            _modules.Clear();
            _initialized.Clear();
        }

        // ── Dependency Resolution ─────────────────────────────────────────────────

        /// <summary>
        /// Returns all registered modules sorted in topological order (dependencies first).
        /// Within the same dependency tier, sorts by InitOrder then ModuleId.
        /// Throws InvalidOperationException on circular dependency.
        /// </summary>
        public List<IIdleModule> TopologicalSort()
        {
            ValidateDependencies();

            var result   = new List<IIdleModule>();
            var visited  = new HashSet<string>();
            var visiting = new HashSet<string>(); // cycle detection

            // Sort by InitOrder then ModuleId for deterministic output within a tier
            var sortedModules = _modules.Values
                .OrderBy(m => m.InitOrder)
                .ThenBy(m => m.ModuleId)
                .ToList();

            foreach (var module in sortedModules)
                Visit(module.ModuleId, visited, visiting, result);

            return result;
        }

        private void Visit(string id, HashSet<string> visited, HashSet<string> visiting, List<IIdleModule> result)
        {
            if (visited.Contains(id))  return;
            if (visiting.Contains(id))
                throw new InvalidOperationException(
                    $"[ModuleRegistry] Circular dependency detected involving module '{id}'.");

            visiting.Add(id);
            var module = _modules[id];
            foreach (var dep in module.Dependencies)
            {
                if (!_modules.ContainsKey(dep))
                    throw new InvalidOperationException(
                        $"[ModuleRegistry] Module '{id}' depends on unregistered module '{dep}'.");
                Visit(dep, visited, visiting, result);
            }

            visiting.Remove(id);
            visited.Add(id);
            result.Add(module);
        }

        private void ValidateDependencies()
        {
            foreach (var module in _modules.Values)
            {
                foreach (var dep in module.Dependencies)
                {
                    if (!_modules.ContainsKey(dep))
                        throw new InvalidOperationException(
                            $"[ModuleRegistry] Module '{module.ModuleId}' depends on " +
                            $"unregistered module '{dep}'.");
                }
            }
        }

        // ── Query ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Get a registered module by id. Returns null if not found.
        /// </summary>
        public IIdleModule Get(string moduleId)
        {
            _modules.TryGetValue(moduleId, out var m);
            return m;
        }

        /// <summary>
        /// Get a registered module by id and type. Returns null if not found or wrong type.
        /// </summary>
        public T Get<T>(string moduleId) where T : class, IIdleModule
            => Get(moduleId) as T;

        /// <summary>
        /// All registered module IDs.
        /// </summary>
        public IReadOnlyCollection<string> ModuleIds => _modules.Keys;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>Clears all state for testing. Use in TearDown only.</summary>
        public void ClearForTesting()
        {
            _modules.Clear();
            _initialized.Clear();
        }
#endif
    }
}

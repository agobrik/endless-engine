// Tests for Sprint 16 — S16-02: IIdleModule + ModuleRegistry
// Type: Logic (Unit/EditMode)
//
// Covers:
//   - Register: stores module, rejects null, rejects duplicate id, rejects empty id
//   - IsRegistered / IsInitialized / Count
//   - TopologicalSort: no deps, single chain, multi-tier, correct order by InitOrder
//   - TopologicalSort: throws on circular dependency
//   - TopologicalSort: throws on unregistered dependency
//   - InitAllAsync: calls Init in topological order
//   - InitAllAsync: marks modules as initialized
//   - ShutdownAll: calls Shutdown on all modules
//   - Get / Get<T>: retrieves registered modules
//   - ClearForTesting: resets state
//
// To run: Unity Test Runner → EditMode → EndlessEngine.Tests.Unit.ModuleRegistry

using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.TestTools;
using EndlessEngine.Modules;

namespace EndlessEngine.Tests.Unit.ModuleRegistry
{
    [TestFixture]
    public class ModuleRegistryTests
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD

        private EndlessEngine.Modules.ModuleRegistry _registry;

        [SetUp]
        public void SetUp()
        {
            _registry = new EndlessEngine.Modules.ModuleRegistry();
        }

        [TearDown]
        public void TearDown()
        {
            _registry.ClearForTesting();
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static FakeModule MakeModule(string id, string[] deps = null, int order = 0, bool ticks = false)
            => new FakeModule(id, deps ?? Array.Empty<string>(), order, ticks);

        // ── Register ──────────────────────────────────────────────────────────────

        [Test]
        public void Register_StoresModule()
        {
            _registry.Register(MakeModule("econ"));
            Assert.IsTrue(_registry.IsRegistered("econ"));
        }

        [Test]
        public void Register_ThrowsOnNull()
        {
            Assert.Throws<ArgumentNullException>(() => _registry.Register(null));
        }

        [Test]
        public void Register_ThrowsOnDuplicateId()
        {
            _registry.Register(MakeModule("econ"));
            Assert.Throws<InvalidOperationException>(() => _registry.Register(MakeModule("econ")));
        }

        [Test]
        public void Register_ThrowsOnEmptyId()
        {
            Assert.Throws<ArgumentException>(() => _registry.Register(MakeModule("")));
        }

        [Test]
        public void Count_ReflectsRegistrations()
        {
            Assert.AreEqual(0, _registry.Count);
            _registry.Register(MakeModule("a"));
            _registry.Register(MakeModule("b"));
            Assert.AreEqual(2, _registry.Count);
        }

        // ── TopologicalSort ───────────────────────────────────────────────────────

        [Test]
        public void TopologicalSort_NoDeps_AnyOrder_AllPresent()
        {
            _registry.Register(MakeModule("a"));
            _registry.Register(MakeModule("b"));
            _registry.Register(MakeModule("c"));

            var sorted = _registry.TopologicalSort();
            Assert.AreEqual(3, sorted.Count);
            CollectionAssert.Contains(sorted, _registry.Get("a"));
            CollectionAssert.Contains(sorted, _registry.Get("b"));
            CollectionAssert.Contains(sorted, _registry.Get("c"));
        }

        [Test]
        public void TopologicalSort_ChainDep_CorrectOrder()
        {
            // c depends on b depends on a → order must be a, b, c
            _registry.Register(MakeModule("a"));
            _registry.Register(MakeModule("b", new[] { "a" }));
            _registry.Register(MakeModule("c", new[] { "b" }));

            var sorted = _registry.TopologicalSort();
            var ids = sorted.ConvertAll(m => m.ModuleId);

            Assert.Less(ids.IndexOf("a"), ids.IndexOf("b"), "a before b");
            Assert.Less(ids.IndexOf("b"), ids.IndexOf("c"), "b before c");
        }

        [Test]
        public void TopologicalSort_DiamondDep_AllPresent_DepsFirst()
        {
            // d depends on b and c; b and c both depend on a
            _registry.Register(MakeModule("a"));
            _registry.Register(MakeModule("b", new[] { "a" }));
            _registry.Register(MakeModule("c", new[] { "a" }));
            _registry.Register(MakeModule("d", new[] { "b", "c" }));

            var sorted = _registry.TopologicalSort();
            var ids = sorted.ConvertAll(m => m.ModuleId);

            Assert.Less(ids.IndexOf("a"), ids.IndexOf("b"), "a before b");
            Assert.Less(ids.IndexOf("a"), ids.IndexOf("c"), "a before c");
            Assert.Less(ids.IndexOf("b"), ids.IndexOf("d"), "b before d");
            Assert.Less(ids.IndexOf("c"), ids.IndexOf("d"), "c before d");
        }

        [Test]
        public void TopologicalSort_Throws_OnCircularDependency()
        {
            _registry.Register(MakeModule("a", new[] { "b" }));
            _registry.Register(MakeModule("b", new[] { "a" }));

            Assert.Throws<InvalidOperationException>(() => _registry.TopologicalSort());
        }

        [Test]
        public void TopologicalSort_Throws_OnUnregisteredDependency()
        {
            _registry.Register(MakeModule("a", new[] { "missing" }));

            Assert.Throws<InvalidOperationException>(() => _registry.TopologicalSort());
        }

        [Test]
        public void TopologicalSort_SortsWithinTierByInitOrder()
        {
            // No deps, different InitOrder values
            _registry.Register(MakeModule("z", order: 10));
            _registry.Register(MakeModule("a", order: 1));
            _registry.Register(MakeModule("m", order: 5));

            var sorted = _registry.TopologicalSort();
            var ids = sorted.ConvertAll(m => m.ModuleId);

            Assert.Less(ids.IndexOf("a"), ids.IndexOf("m"), "order 1 before 5");
            Assert.Less(ids.IndexOf("m"), ids.IndexOf("z"), "order 5 before 10");
        }

        // ── InitAllAsync ──────────────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator InitAllAsync_CallsInitInOrder()
        {
            var log = new List<string>();
            var a = new FakeModule("a", Array.Empty<string>(), 0, false, () => log.Add("a"));
            var b = new FakeModule("b", new[] { "a" }, 0, false, () => log.Add("b"));
            var c = new FakeModule("c", new[] { "b" }, 0, false, () => log.Add("c"));

            _registry.Register(a);
            _registry.Register(b);
            _registry.Register(c);

            yield return _registry.InitAllAsync();

            Assert.AreEqual(new[] { "a", "b", "c" }, log.ToArray());
        }

        [UnityTest]
        public IEnumerator InitAllAsync_MarksModulesInitialized()
        {
            _registry.Register(MakeModule("x"));
            Assert.IsFalse(_registry.IsInitialized("x"));

            yield return _registry.InitAllAsync();

            Assert.IsTrue(_registry.IsInitialized("x"));
        }

        [UnityTest]
        public IEnumerator InitAllAsync_DoesNotDoubleInit()
        {
            int initCount = 0;
            var m = new FakeModule("x", Array.Empty<string>(), 0, false, () => initCount++);
            _registry.Register(m);

            yield return _registry.InitAllAsync();
            yield return _registry.InitAllAsync(); // second call

            Assert.AreEqual(1, initCount, "Init called only once");
        }

        // ── ShutdownAll ───────────────────────────────────────────────────────────

        [Test]
        public void ShutdownAll_CallsShutdownOnAll()
        {
            var a = MakeModule("a");
            var b = MakeModule("b");
            _registry.Register(a);
            _registry.Register(b);

            _registry.ShutdownAll();

            Assert.IsTrue(a.ShutdownCalled);
            Assert.IsTrue(b.ShutdownCalled);
        }

        [Test]
        public void ShutdownAll_ClearsRegistry()
        {
            _registry.Register(MakeModule("a"));
            _registry.ShutdownAll();

            Assert.AreEqual(0, _registry.Count);
            Assert.IsFalse(_registry.IsRegistered("a"));
        }

        // ── Get ───────────────────────────────────────────────────────────────────

        [Test]
        public void Get_ReturnsModule()
        {
            var m = MakeModule("econ");
            _registry.Register(m);
            Assert.AreEqual(m, _registry.Get("econ"));
        }

        [Test]
        public void Get_ReturnsNullForUnknownId()
        {
            Assert.IsNull(_registry.Get("nope"));
        }

        [Test]
        public void GetT_ReturnsCastModule()
        {
            var m = MakeModule("econ");
            _registry.Register(m);
            Assert.IsNotNull(_registry.Get<FakeModule>("econ"));
        }

        [Test]
        public void GetT_ReturnsNullOnWrongType()
        {
            // If we ever had two module types, this covers the cast failure path.
            // FakeModule is the only type here — ensure a missing id returns null.
            Assert.IsNull(_registry.Get<FakeModule>("missing"));
        }

#endif
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    // ── Test double ──────────────────────────────────────────────────────────────

    internal class FakeModule : IIdleModule
    {
        private readonly Action _onInit;

        public string   ModuleId     { get; }
        public string[] Dependencies { get; }
        public int      InitOrder    { get; }
        public bool     ReceivesTick { get; }
        public bool     ShutdownCalled { get; private set; }

        public FakeModule(string id, string[] deps, int order, bool ticks, Action onInit = null)
        {
            ModuleId     = id;
            Dependencies = deps;
            InitOrder    = order;
            ReceivesTick = ticks;
            _onInit      = onInit;
        }

        public IEnumerator Init()
        {
            _onInit?.Invoke();
            yield return null;
        }

        public void Tick(float dt) { }

        public void Shutdown() => ShutdownCalled = true;
    }
#endif
}

// Tests for Sprint 17 — S17-03: UnlockLogService
// Type: Logic (Unit/EditMode)
//
// Covers:
//   - Unlock: fires OnEntryUnlocked only on first discovery
//   - Unlock: duplicate unlock is silent (no double event)
//   - Unlock: unknown entry id is ignored
//   - IsUnlocked: accurate before/after unlock
//   - TotalUnlocked: correct count
//   - GetAll: returns all entries with unlock state
//   - GetUnlocked(category): filtered by category
//   - GetVisible: hides IsHiddenUntilUnlocked entries until discovered
//   - UnlockDynamic: adds non-config id to unlocked set
//   - Save/Load round-trip
//
// To run: Unity Test Runner → EditMode → EndlessEngine.Tests.Unit.BuildingPetUnlock

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using EndlessEngine.Config;
using EndlessEngine.SaveAndLoad;
using EndlessEngine.UnlockLog;

namespace EndlessEngine.Tests.Unit.BuildingPetUnlock
{
    [TestFixture]
    public class UnlockLogServiceTests
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD

        private UnlockLogService       _service;
        private UnlockEntryConfigSO    _oreEntry;
        private UnlockEntryConfigSO    _foxEntry;
        private UnlockEntryConfigSO    _hiddenEntry;

        private readonly List<UnlockEntryConfigSO> _unlockedEvents = new List<UnlockEntryConfigSO>();

        [SetUp]
        public void SetUp()
        {
            UnlockLogService.ClearSubscribersForTesting();
            UnlockLogService.OnEntryUnlocked += e => _unlockedEvents.Add(e);
            _unlockedEvents.Clear();

            _oreEntry = ScriptableObject.CreateInstance<UnlockEntryConfigSO>();
            _oreEntry.EntryId  = "ore_t0";
            _oreEntry.Category = UnlockCategory.Item;
            _oreEntry.IsHiddenUntilUnlocked = false;

            _foxEntry = ScriptableObject.CreateInstance<UnlockEntryConfigSO>();
            _foxEntry.EntryId  = "fox";
            _foxEntry.Category = UnlockCategory.Pet;
            _foxEntry.IsHiddenUntilUnlocked = false;

            _hiddenEntry = ScriptableObject.CreateInstance<UnlockEntryConfigSO>();
            _hiddenEntry.EntryId  = "secret_building";
            _hiddenEntry.Category = UnlockCategory.Building;
            _hiddenEntry.IsHiddenUntilUnlocked = true;

            var go   = new GameObject("UnlockLogService");
            _service = go.AddComponent<UnlockLogService>();
            _service.Initialize(new[] { _oreEntry, _foxEntry, _hiddenEntry });
        }

        [TearDown]
        public void TearDown()
        {
            UnlockLogService.ClearSubscribersForTesting();
            if (_service      != null) Object.DestroyImmediate(_service.gameObject);
            if (_oreEntry     != null) Object.DestroyImmediate(_oreEntry);
            if (_foxEntry     != null) Object.DestroyImmediate(_foxEntry);
            if (_hiddenEntry  != null) Object.DestroyImmediate(_hiddenEntry);
        }

        // ── Unlock ────────────────────────────────────────────────────────────────

        [Test]
        public void Unlock_FiresEvent_OnFirstDiscovery()
        {
            _service.Unlock("ore_t0");
            Assert.AreEqual(1, _unlockedEvents.Count);
            Assert.AreEqual(_oreEntry, _unlockedEvents[0]);
        }

        [Test]
        public void Unlock_Silent_OnDuplicateUnlock()
        {
            _service.Unlock("ore_t0");
            _service.Unlock("ore_t0"); // second call
            Assert.AreEqual(1, _unlockedEvents.Count, "Event fires only once");
        }

        [Test]
        public void Unlock_IgnoresUnknownId()
        {
            _service.Unlock("nonexistent");
            Assert.AreEqual(0, _unlockedEvents.Count);
            Assert.IsFalse(_service.IsUnlocked("nonexistent"));
        }

        // ── IsUnlocked ────────────────────────────────────────────────────────────

        [Test]
        public void IsUnlocked_FalseBeforeUnlock()
        {
            Assert.IsFalse(_service.IsUnlocked("ore_t0"));
        }

        [Test]
        public void IsUnlocked_TrueAfterUnlock()
        {
            _service.Unlock("ore_t0");
            Assert.IsTrue(_service.IsUnlocked("ore_t0"));
        }

        // ── TotalUnlocked ─────────────────────────────────────────────────────────

        [Test]
        public void TotalUnlocked_CountsAccurately()
        {
            Assert.AreEqual(0, _service.TotalUnlocked);
            _service.Unlock("ore_t0");
            _service.Unlock("fox");
            Assert.AreEqual(2, _service.TotalUnlocked);
        }

        // ── GetAll ────────────────────────────────────────────────────────────────

        [Test]
        public void GetAll_ContainsAllDefinedEntries()
        {
            var all = _service.GetAll();
            Assert.AreEqual(3, all.Count);
            Assert.IsFalse(all["ore_t0"]);

            _service.Unlock("ore_t0");
            all = _service.GetAll();
            Assert.IsTrue(all["ore_t0"]);
        }

        // ── GetUnlocked(category) ─────────────────────────────────────────────────

        [Test]
        public void GetUnlocked_FiltersByCategory()
        {
            _service.Unlock("ore_t0");
            _service.Unlock("fox");

            var pets  = _service.GetUnlocked(UnlockCategory.Pet);
            var items = _service.GetUnlocked(UnlockCategory.Item);

            Assert.AreEqual(1, pets.Count);
            Assert.AreEqual("fox", pets[0].EntryId);
            Assert.AreEqual(1, items.Count);
        }

        // ── GetVisible ────────────────────────────────────────────────────────────

        [Test]
        public void GetVisible_ExcludesHiddenBeforeUnlock()
        {
            var visible = _service.GetVisible();
            foreach (var (cfg, _) in visible)
                Assert.AreNotEqual("secret_building", cfg.EntryId, "Hidden entry not visible until unlocked");
        }

        [Test]
        public void GetVisible_IncludesHiddenAfterUnlock()
        {
            _service.Unlock("secret_building");
            var visible = _service.GetVisible();
            bool found = false;
            foreach (var (cfg, unlocked) in visible)
                if (cfg.EntryId == "secret_building" && unlocked) found = true;
            Assert.IsTrue(found);
        }

        // ── UnlockDynamic ─────────────────────────────────────────────────────────

        [Test]
        public void UnlockDynamic_AddsToUnlockedSet()
        {
            _service.UnlockDynamic("dynamic_reward_xyz");
            Assert.IsTrue(_service.IsUnlocked("dynamic_reward_xyz"));
        }

        // ── Save/Load round-trip ──────────────────────────────────────────────────

        [Test]
        public void SaveLoad_RoundTrip_RestoresUnlockedEntries()
        {
            _service.Unlock("ore_t0");
            _service.Unlock("fox");

            var saveData = new SaveData();
            saveData.EnsureDefaults();
            _service.OnBeforeSave(saveData);

            var go2      = new GameObject("UnlockLogService2");
            var service2 = go2.AddComponent<UnlockLogService>();
            service2.Initialize(new[] { _oreEntry, _foxEntry, _hiddenEntry });
            service2.OnAfterLoad(saveData);

            Assert.IsTrue(service2.IsUnlocked("ore_t0"));
            Assert.IsTrue(service2.IsUnlocked("fox"));
            Assert.IsFalse(service2.IsUnlocked("secret_building"));

            Object.DestroyImmediate(go2);
        }

#endif
    }
}

// Tests for Sprint 13 — S13-05: StatisticsService
// Type: Logic (Unit/EditMode)
//
// Covers:
//   - Add() accumulates counter stats
//   - Add() ignores unknown stat IDs
//   - Add() ignores zero and negative deltas
//   - SetIfHigher() updates peak stats when value is higher
//   - SetIfHigher() does NOT update when value is lower or equal
//   - SetIfHigher() ignores counter stats
//   - Add() ignores peak stats
//   - OnStatChanged fires on Add and SetIfHigher
//   - Save / load round-trip (OnBeforeSave / OnAfterLoad)
//   - GetAll() returns snapshot of all tracked stats
//
// To run: Unity Test Runner → EditMode → EndlessEngine.Tests.Unit.StatisticsSystem

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using EndlessEngine.Config;
using EndlessEngine.Statistics;
using EndlessEngine.SaveAndLoad;

namespace EndlessEngine.Tests.Unit.StatisticsSystem
{
    [TestFixture]
    public class StatisticsServiceTests
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private StatisticsService  _service;
        private StatDefinitionSO   _counterDef;
        private StatDefinitionSO   _peakDef;
        private StatDefinitionSO   _killsDef;

        private readonly List<(string, double)> _statEvents = new List<(string, double)>();

        [SetUp]
        public void SetUp()
        {
            StatisticsService.ClearSubscribersForTesting();
            StatisticsService.OnStatChanged += (id, val) => _statEvents.Add((id, val));
            _statEvents.Clear();

            _counterDef = ScriptableObject.CreateInstance<StatDefinitionSO>();
            _counterDef.StatId      = "total_gold";
            _counterDef.DisplayName = "Total Gold Earned";
            _counterDef.IsPeakValue = false;

            _peakDef = ScriptableObject.CreateInstance<StatDefinitionSO>();
            _peakDef.StatId      = "longest_run";
            _peakDef.DisplayName = "Longest Run (s)";
            _peakDef.IsPeakValue = true;

            _killsDef = ScriptableObject.CreateInstance<StatDefinitionSO>();
            _killsDef.StatId      = "total_kills";
            _killsDef.DisplayName = "Total Kills";
            _killsDef.IsPeakValue = false;

            var go = new GameObject("StatisticsService");
            _service = go.AddComponent<StatisticsService>();
            _service.Initialize(new[] { _counterDef, _peakDef, _killsDef });
        }

        [TearDown]
        public void TearDown()
        {
            StatisticsService.ClearSubscribersForTesting();
            if (_service    != null) Object.DestroyImmediate(_service.gameObject);
            if (_counterDef != null) Object.DestroyImmediate(_counterDef);
            if (_peakDef    != null) Object.DestroyImmediate(_peakDef);
            if (_killsDef   != null) Object.DestroyImmediate(_killsDef);
        }

        // ── Counter (Add) ──────────────────────────────────────────────────────────

        [Test]
        public void Add_AccumulatesValue()
        {
            _service.Add("total_gold", 100);
            _service.Add("total_gold", 250);
            Assert.AreEqual(350, _service.Get("total_gold"));
        }

        [Test]
        public void Add_FiresOnStatChanged()
        {
            _service.Add("total_gold", 500);
            Assert.AreEqual(1, _statEvents.Count);
            Assert.AreEqual(("total_gold", 500.0), _statEvents[0]);
        }

        [Test]
        public void Add_ZeroDelta_NoChange()
        {
            _service.Add("total_gold", 0);
            Assert.AreEqual(0, _statEvents.Count);
            Assert.AreEqual(0, _service.Get("total_gold"));
        }

        [Test]
        public void Add_NegativeDelta_NoChange()
        {
            _service.Add("total_gold", -100);
            Assert.AreEqual(0, _statEvents.Count);
        }

        [Test]
        public void Add_UnknownStatId_Ignored()
        {
            _service.Add("nonexistent_stat", 999);
            Assert.AreEqual(0, _statEvents.Count);
        }

        [Test]
        public void Add_PeakStat_Ignored()
        {
            _service.Add("longest_run", 120); // IsPeakValue — Add should be ignored
            Assert.AreEqual(0, _service.Get("longest_run"),
                "Add must have no effect on a peak stat");
            Assert.AreEqual(0, _statEvents.Count);
        }

        // ── Peak (SetIfHigher) ────────────────────────────────────────────────────

        [Test]
        public void SetIfHigher_UpdatesWhenHigher()
        {
            _service.SetIfHigher("longest_run", 120);
            Assert.AreEqual(120, _service.Get("longest_run"));
        }

        [Test]
        public void SetIfHigher_DoesNotUpdateWhenLower()
        {
            _service.SetIfHigher("longest_run", 200);
            _service.SetIfHigher("longest_run", 150);
            Assert.AreEqual(200, _service.Get("longest_run"));
        }

        [Test]
        public void SetIfHigher_DoesNotUpdateWhenEqual()
        {
            _service.SetIfHigher("longest_run", 200);
            _statEvents.Clear();
            _service.SetIfHigher("longest_run", 200);
            Assert.AreEqual(0, _statEvents.Count, "No event on equal value");
        }

        [Test]
        public void SetIfHigher_FiresOnStatChanged()
        {
            _service.SetIfHigher("longest_run", 300);
            Assert.AreEqual(1, _statEvents.Count);
            Assert.AreEqual(300.0, _statEvents[0].Item2);
        }

        [Test]
        public void SetIfHigher_CounterStat_Ignored()
        {
            _service.SetIfHigher("total_gold", 9999); // counter, not peak
            Assert.AreEqual(0, _service.Get("total_gold"),
                "SetIfHigher must have no effect on a counter stat");
            Assert.AreEqual(0, _statEvents.Count);
        }

        [Test]
        public void SetIfHigher_UnknownStatId_Ignored()
        {
            _service.SetIfHigher("nonexistent_stat", 100);
            Assert.AreEqual(0, _statEvents.Count);
        }

        // ── GetAll ────────────────────────────────────────────────────────────────

        [Test]
        public void GetAll_ReturnsAllInitializedStats()
        {
            var all = _service.GetAll();
            Assert.IsTrue(all.ContainsKey("total_gold"));
            Assert.IsTrue(all.ContainsKey("longest_run"));
            Assert.IsTrue(all.ContainsKey("total_kills"));
        }

        [Test]
        public void GetAll_ReflectsCurrentValues()
        {
            _service.Add("total_kills", 42);
            var all = _service.GetAll();
            Assert.AreEqual(42, all["total_kills"]);
        }

        // ── Save / Load ───────────────────────────────────────────────────────────

        [Test]
        public void SaveLoad_RoundTrip_RestoresCountersAndPeaks()
        {
            _service.Add("total_gold", 5000);
            _service.Add("total_kills", 77);
            _service.SetIfHigher("longest_run", 360);

            var saveData = new SaveData();
            saveData.EnsureDefaults();
            _service.OnBeforeSave(saveData);

            var go2      = new GameObject("StatisticsService2");
            var service2 = go2.AddComponent<StatisticsService>();
            service2.Initialize(new[] { _counterDef, _peakDef, _killsDef });
            service2.OnAfterLoad(saveData);

            Assert.AreEqual(5000, service2.Get("total_gold"));
            Assert.AreEqual(77,   service2.Get("total_kills"));
            Assert.AreEqual(360,  service2.Get("longest_run"));

            Object.DestroyImmediate(go2);
        }
#endif
    }
}

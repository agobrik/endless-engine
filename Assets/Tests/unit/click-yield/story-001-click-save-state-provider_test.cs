// Tests for Sprint 6 — S6-02: ClickYieldService ISaveStateProvider
// Type: Logic (Unit/EditMode)
//
// Verifies that ClickYieldService correctly implements ISaveStateProvider:
//   - OnBeforeSave writes TotalClickEarned and AutoClickRateOverride to SaveData
//   - OnAfterLoad restores TotalClickEarned and AutoClickRateOverride from SaveData
//
// To run: Unity Test Runner → EditMode → EndlessEngine.Tests.Unit.ClickYield

using NUnit.Framework;
using UnityEngine;
using EndlessEngine.Modules;
using EndlessEngine.SaveAndLoad;

namespace EndlessEngine.Tests.Unit.ClickYield
{
    /// <summary>
    /// Unit tests for ClickYieldService ISaveStateProvider — Sprint 6 S6-02.
    /// </summary>
    [TestFixture]
    public class ClickSaveStateProviderTests
    {
        private ClickYieldService _service;

        [SetUp]
        public void SetUp()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var go = new GameObject("ClickYieldServiceTest");
            _service = go.AddComponent<ClickYieldService>();
            // Initialize with nulls — no economy needed for save/load tests
            _service.Initialize(config: null, economy: null);
#endif
        }

        [TearDown]
        public void TearDown()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            ClickYieldService.ClearSubscribersForTesting();
            if (_service != null)
                Object.DestroyImmediate(_service.gameObject);
#endif
        }

        // ── ProviderOrder ─────────────────────────────────────────────────────────

        [Test]
        [Description("ProviderOrder is SaveConstants.SaveProviderOrder.Click (60).")]
        public void ProviderOrder_IsClickOrder()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Assert.AreEqual(SaveConstants.SaveProviderOrder.Click, _service.ProviderOrder);
#endif
        }

        // ── OnBeforeSave ──────────────────────────────────────────────────────────

        [Test]
        [Description("OnBeforeSave: writes TotalClickEarned to saveData.ClickState.")]
        public void OnBeforeSave_WritesTotalClickEarned()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Simulate some clicks earning gold by injecting earned value
            // (ClickYieldService.TotalClickEarned is private set, incremented in ProcessClick)
            // We verify via save round-trip: inject save, check total, re-save, verify
            var loadData = new SaveData
            {
                ClickState = new ClickModuleSaveState { TotalClickEarned = 12345L }
            };
            _service.OnAfterLoad(loadData);

            var saveData = new SaveData();
            _service.OnBeforeSave(saveData);

            Assert.IsNotNull(saveData.ClickState);
            Assert.AreEqual(12345L, saveData.ClickState.TotalClickEarned);
#endif
        }

        [Test]
        [Description("OnBeforeSave: writes AutoClickRateOverride to saveData.ClickState.")]
        public void OnBeforeSave_WritesAutoClickRateOverride()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _service.SetAutoClickRate(3.5f);

            var saveData = new SaveData();
            _service.OnBeforeSave(saveData);

            Assert.IsNotNull(saveData.ClickState);
            Assert.AreEqual(3.5f, saveData.ClickState.AutoClickRateOverride, 0.001f);
#endif
        }

        [Test]
        [Description("OnBeforeSave: initializes ClickState if saveData.ClickState is null.")]
        public void OnBeforeSave_NullClickState_InitializesIt()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var saveData = new SaveData { ClickState = null };
            Assert.DoesNotThrow(() => _service.OnBeforeSave(saveData));
            Assert.IsNotNull(saveData.ClickState);
#endif
        }

        // ── OnAfterLoad ───────────────────────────────────────────────────────────

        [Test]
        [Description("OnAfterLoad: restores TotalClickEarned from saveData.ClickState.")]
        public void OnAfterLoad_RestoresTotalClickEarned()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var saveData = new SaveData
            {
                ClickState = new ClickModuleSaveState { TotalClickEarned = 99999L }
            };
            _service.OnAfterLoad(saveData);

            // Verify via round-trip save
            var writtenBack = new SaveData();
            _service.OnBeforeSave(writtenBack);
            Assert.AreEqual(99999L, writtenBack.ClickState.TotalClickEarned);
#endif
        }

        [Test]
        [Description("OnAfterLoad: restores AutoClickRateOverride when > 0.")]
        public void OnAfterLoad_RestoresAutoClickRateOverride_WhenPositive()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var saveData = new SaveData
            {
                ClickState = new ClickModuleSaveState { AutoClickRateOverride = 5.0f }
            };
            _service.OnAfterLoad(saveData);

            Assert.AreEqual(5.0f, _service.AutoClickRate, 0.001f);
#endif
        }

        [Test]
        [Description("OnAfterLoad: does not override AutoClickRate when AutoClickRateOverride = 0.")]
        public void OnAfterLoad_ZeroAutoClickRateOverride_DoesNotOverride()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _service.SetAutoClickRate(2.0f); // set a non-zero rate first
            var saveData = new SaveData
            {
                ClickState = new ClickModuleSaveState { AutoClickRateOverride = 0f }
            };
            _service.OnAfterLoad(saveData);

            // Rate should remain 2.0 (override of 0 means "don't override")
            Assert.AreEqual(2.0f, _service.AutoClickRate, 0.001f,
                "Zero AutoClickRateOverride must not reset the existing rate");
#endif
        }

        [Test]
        [Description("OnAfterLoad: null saveData.ClickState → no crash, no state change.")]
        public void OnAfterLoad_NullClickState_NoCrash()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var saveData = new SaveData { ClickState = null };
            Assert.DoesNotThrow(() => _service.OnAfterLoad(saveData));
#endif
        }
    }
}

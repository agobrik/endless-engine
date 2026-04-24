// Tests for Sprint 18 — S18-03: ExportService
// Type: Logic (Unit/EditMode)
//
// Covers:
//   - ExportToCode: returns non-empty base64 string
//   - ExportToCode: null SaveData returns empty string (no throw)
//   - TryImportFromCode: roundtrip — exported code imports cleanly
//   - TryImportFromCode: empty/null code fires OnImportFailed with EmptyCode
//   - TryImportFromCode: invalid base64 fires OnImportFailed with InvalidBase64
//   - TryImportFromCode: preserves key SaveData fields after roundtrip
//   - EnsureDefaults called after import (no null collections)
//
// To run: Unity Test Runner → EditMode → EndlessEngine.Tests.Unit.EventsLeaderboardExport

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using EndlessEngine.Export;
using EndlessEngine.SaveAndLoad;

namespace EndlessEngine.Tests.Unit.EventsLeaderboardExport
{
    [TestFixture]
    public class ExportServiceTests
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD

        private ExportService _service;
        private readonly List<string> _failedReasons = new List<string>();

        [SetUp]
        public void SetUp()
        {
            ExportService.ClearSubscribersForTesting();
            ExportService.OnImportFailed += r => _failedReasons.Add(r);
            _failedReasons.Clear();

            var go   = new GameObject("ExportService");
            _service = go.AddComponent<ExportService>();
            _service.Initialize(null); // no save service needed for unit test
        }

        [TearDown]
        public void TearDown()
        {
            ExportService.ClearSubscribersForTesting();
            if (_service != null) Object.DestroyImmediate(_service.gameObject);
        }

        // ── ExportToCode ──────────────────────────────────────────────────────────

        [Test]
        public void ExportToCode_ReturnsNonEmptyString()
        {
            var saveData = new SaveData();
            saveData.EnsureDefaults();
            saveData.CurrentResources = 9999;

            string code = _service.ExportToCode(saveData);
            Assert.IsNotEmpty(code);
        }

        [Test]
        public void ExportToCode_NullSaveData_ReturnsEmpty()
        {
            string code = _service.ExportToCode(null);
            Assert.IsEmpty(code);
        }

        // ── TryImportFromCode ─────────────────────────────────────────────────────

        [Test]
        public void TryImportFromCode_Roundtrip_Success()
        {
            var saveData = new SaveData();
            saveData.EnsureDefaults();
            saveData.CurrentResources = 12345;
            saveData.PrestigeCount    = 3;

            string code = _service.ExportToCode(saveData);
            bool result = _service.TryImportFromCode(code, out SaveData imported);

            Assert.IsTrue(result);
            Assert.IsNotNull(imported);
            Assert.AreEqual(12345, imported.CurrentResources);
            Assert.AreEqual(3,     imported.PrestigeCount);
        }

        [Test]
        public void TryImportFromCode_EmptyCode_Fails_WithEmptyCodeReason()
        {
            bool result = _service.TryImportFromCode("", out _);
            Assert.IsFalse(result);
            Assert.AreEqual(1, _failedReasons.Count);
            Assert.AreEqual("EmptyCode", _failedReasons[0]);
        }

        [Test]
        public void TryImportFromCode_NullCode_Fails_WithEmptyCodeReason()
        {
            bool result = _service.TryImportFromCode(null, out _);
            Assert.IsFalse(result);
            Assert.AreEqual("EmptyCode", _failedReasons[0]);
        }

        [Test]
        public void TryImportFromCode_InvalidBase64_Fails_WithInvalidBase64Reason()
        {
            bool result = _service.TryImportFromCode("!!!not_base64!!!", out _);
            Assert.IsFalse(result);
            Assert.AreEqual("InvalidBase64", _failedReasons[0]);
        }

        [Test]
        public void TryImportFromCode_EnsuresDefaultsAfterImport()
        {
            // Create a save with no collection fields (pre-EnsureDefaults state)
            var saveData = new SaveData { CurrentResources = 500 };
            // Don't call EnsureDefaults — simulate old save file

            string code = _service.ExportToCode(saveData);
            _service.TryImportFromCode(code, out SaveData imported);

            // After import, collections should not be null
            Assert.IsNotNull(imported.UpgradeNodeStates);
            Assert.IsNotNull(imported.GeneratorStates);
        }

#endif
    }
}

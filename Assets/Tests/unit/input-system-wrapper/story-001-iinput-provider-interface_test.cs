// Tests for Story S1-13: Input System Wrapper — IInputProvider Interface and MockInputProvider
// Type: Logic (Unit/EditMode)
// Story: production/epics/input-system-wrapper/story-001-iinput-provider-interface.md
//
// These tests verify:
//   (1) IInputProvider interface has all four required methods and the event
//   (2) MockInputProvider implements IInputProvider correctly
//   (3) MockInputProvider.SimulatePausePress fires OnPausePressed
//   (4) AC-ISW-04: No gameplay code references UnityEngine.InputSystem directly
//
// To run: Unity Test Runner → EditMode → EndlessEngine.Tests.Unit.InputSystemWrapper

using System;
using NUnit.Framework;
using UnityEngine;
using EndlessEngine.Input;

namespace EndlessEngine.Tests.Unit.InputSystemWrapper
{
    /// <summary>
    /// Unit tests for IInputProvider and MockInputProvider (S1-13 / Story 001).
    /// </summary>
    [TestFixture]
    public class IInputProviderTests
    {
        // ── Interface contract ────────────────────────────────────────────────────

        [Test]
        [Description("IInputProvider has GetMoveVector() returning Vector2.")]
        public void IInputProvider_HasGetMoveVector()
        {
            var method = typeof(IInputProvider).GetMethod("GetMoveVector");
            Assert.IsNotNull(method, "IInputProvider must declare GetMoveVector()");
            Assert.AreEqual(typeof(Vector2), method.ReturnType,
                "GetMoveVector must return Vector2");
        }

        [Test]
        [Description("IInputProvider has GetConfirmPressed(), GetCancelPressed(), GetPausePressed() returning bool.")]
        public void IInputProvider_HasBoolMethods()
        {
            foreach (var name in new[] { "GetConfirmPressed", "GetCancelPressed", "GetPausePressed" })
            {
                var method = typeof(IInputProvider).GetMethod(name);
                Assert.IsNotNull(method, $"IInputProvider must declare {name}()");
                Assert.AreEqual(typeof(bool), method.ReturnType,
                    $"{name} must return bool");
            }
        }

        [Test]
        [Description("IInputProvider has OnPausePressed event of type Action.")]
        public void IInputProvider_HasOnPausePressedEvent()
        {
            var ev = typeof(IInputProvider).GetEvent("OnPausePressed");
            Assert.IsNotNull(ev, "IInputProvider must declare OnPausePressed event");
            Assert.AreEqual(typeof(Action), ev.EventHandlerType,
                "OnPausePressed must be of type Action");
        }

        // ── MockInputProvider implementation ──────────────────────────────────────

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [Test]
        [Description("MockInputProvider implements IInputProvider.")]
        public void MockInputProvider_ImplementsIInputProvider()
        {
            Assert.IsTrue(typeof(IInputProvider).IsAssignableFrom(typeof(MockInputProvider)),
                "MockInputProvider must implement IInputProvider");
        }

        [Test]
        [Description("MockInputProvider.GetMoveVector returns set MoveVector value.")]
        public void MockInputProvider_GetMoveVector_ReturnsSetValue()
        {
            var mock = new MockInputProvider { MoveVector = Vector2.right };
            Assert.AreEqual(Vector2.right, mock.GetMoveVector());
        }

        [Test]
        [Description("MockInputProvider.GetMoveVector returns zero by default.")]
        public void MockInputProvider_GetMoveVector_DefaultIsZero()
        {
            var mock = new MockInputProvider();
            Assert.AreEqual(Vector2.zero, mock.GetMoveVector());
        }

        [Test]
        [Description("MockInputProvider.GetConfirmPressed returns set value.")]
        public void MockInputProvider_GetConfirmPressed_ReturnsSetValue()
        {
            var mock = new MockInputProvider { ConfirmPressed = true };
            Assert.IsTrue(mock.GetConfirmPressed());
        }

        [Test]
        [Description("MockInputProvider.GetCancelPressed returns set value.")]
        public void MockInputProvider_GetCancelPressed_ReturnsSetValue()
        {
            var mock = new MockInputProvider { CancelPressed = true };
            Assert.IsTrue(mock.GetCancelPressed());
        }

        [Test]
        [Description("MockInputProvider.GetPausePressed returns set value.")]
        public void MockInputProvider_GetPausePressed_ReturnsSetValue()
        {
            var mock = new MockInputProvider { PausePressed = true };
            Assert.IsTrue(mock.GetPausePressed());
        }

        [Test]
        [Description("MockInputProvider.SimulatePausePress fires OnPausePressed event.")]
        public void MockInputProvider_SimulatePausePress_FiresEvent()
        {
            var mock = new MockInputProvider();
            int fireCount = 0;
            mock.OnPausePressed += () => fireCount++;

            mock.SimulatePausePress();

            Assert.AreEqual(1, fireCount, "OnPausePressed must fire once on SimulatePausePress");
        }

        [Test]
        [Description("MockInputProvider.SimulatePausePress with no subscribers does not throw.")]
        public void MockInputProvider_SimulatePausePress_NoSubscribers_NoThrow()
        {
            var mock = new MockInputProvider();
            Assert.DoesNotThrow(() => mock.SimulatePausePress(),
                "SimulatePausePress must not throw when no subscribers are registered");
        }

        [Test]
        [Description("MockInputProvider can be used via IInputProvider reference (dependency injection pattern).")]
        public void MockInputProvider_UsedViaInterface_WorksCorrectly()
        {
            IInputProvider input = new MockInputProvider
            {
                MoveVector     = new Vector2(0.5f, 0.5f),
                ConfirmPressed = true,
            };

            Assert.AreEqual(new Vector2(0.5f, 0.5f), input.GetMoveVector());
            Assert.IsTrue(input.GetConfirmPressed());
            Assert.IsFalse(input.GetCancelPressed());
        }
#endif

        // ── AC-ISW-04: No direct InputSystem imports in gameplay code ─────────────

        [Test]
        [Description("AC-ISW-04: No gameplay Runtime .cs file imports UnityEngine.InputSystem (except InputProviderUnity.cs).")]
        public void RuntimeCode_NoDirectInputSystemImports()
        {
#if UNITY_EDITOR
            // Scan all .cs files under the package Runtime/ directory for forbidden import
            // Scripts were migrated from Assets/Scripts/Runtime/ to the UPM package in Sprint 20.
            string projectRoot  = System.IO.Path.GetDirectoryName(UnityEngine.Application.dataPath);
            string runtimePath  = System.IO.Path.Combine(
                projectRoot, "Packages", "com.endlessengine.idle", "Runtime");

            if (!System.IO.Directory.Exists(runtimePath))
            {
                Assert.Inconclusive($"Runtime path not found: {runtimePath}");
                return;
            }

            var csFiles = System.IO.Directory.GetFiles(runtimePath, "*.cs",
                System.IO.SearchOption.AllDirectories);

            foreach (var file in csFiles)
            {
                // InputProviderUnity is the sole allowed importer
                if (file.EndsWith("InputProviderUnity.cs",
                    System.StringComparison.OrdinalIgnoreCase))
                    continue;

                string content = System.IO.File.ReadAllText(file);
                bool hasForbiddenImport =
                    content.Contains("using UnityEngine.InputSystem") ||
                    content.Contains("InputSystem.") ||
                    // Legacy Input class usage
                    content.Contains("UnityEngine.Input.");

                if (hasForbiddenImport)
                    Assert.Fail($"File '{System.IO.Path.GetFileName(file)}' contains a direct Input System reference. " +
                                "Use IInputProvider instead (ADR-0007).");
            }
#else
            Assert.Inconclusive("AC-ISW-04 filesystem scan requires UNITY_EDITOR — run in EditMode.");
#endif
        }
    }
}

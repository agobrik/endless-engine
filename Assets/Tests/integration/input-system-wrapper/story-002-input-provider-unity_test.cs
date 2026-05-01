// Tests for Story S1-16: Input System — InputProviderUnity Integration
// Type: Integration (EditMode)
// Story: production/epics/input-system-wrapper/story-002-input-provider-unity.md
//
// These tests verify:
//   (1) AC-ISW-01: WASD diagonal → GetMoveVector() normalized to ~(0.707, 0.707)
//   (2) AC-ISW-02: Gamepad left stick full-right → GetMoveVector() = (1, 0)
//   (3) AC-ISW-03: Pause debounce — OnPausePressed fires once per window, not on rapid repeat
//
// Requires: Unity Input System package with test support (com.unity.inputsystem)
// To run: Unity Test Runner → EditMode → EndlessEngine.Tests.Integration.InputSystemWrapper
//
// NOTE: InputTestFixture requires the Input System package's test assembly.
//   Add "com.unity.inputsystem" to testables in Packages/manifest.json if tests
//   cannot resolve InputTestFixture.
//
// NOTE on InputSystem.Update():
//   In EditMode [Test]s, InputTestFixture uses InputTestRuntime (not NativeInputRuntime),
//   so yield return null does NOT trigger InputSystem.Update(). We call it explicitly
//   after Press()/Set() so queued events are processed before ReadValue()/callback checks.

using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using EndlessEngine.Input;

namespace EndlessEngine.Tests.Integration.InputSystemWrapper
{
    /// <summary>
    /// Integration tests for InputProviderUnity — simulates real Unity Input System
    /// hardware events via InputTestFixture and verifies IInputProvider contract.
    /// </summary>
    [TestFixture]
    public class InputProviderUnityTests : InputTestFixture
    {
        private GameObject         _go;
        private InputProviderUnity _provider;

        // Simulated devices
        private Keyboard _keyboard;
        private Gamepad  _gamepad;

        // ── SetUp / TearDown ──────────────────────────────────────────────────────

        [SetUp]
        public override void Setup()
        {
            base.Setup();

            // Register simulated devices first so binding resolution picks them up
            _keyboard = InputSystem.AddDevice<Keyboard>();
            _gamepad  = InputSystem.AddDevice<Gamepad>();

            // Build inline asset after devices exist; map.Enable() inside resolves
            // bindings against the already-registered simulated devices.
            var actions = BuildFallbackInputActions();

            // [RequireComponent] forces PlayerInput onto the GO, but we do NOT assign
            // actions to PlayerInput — it would Instantiate() a clone, giving us a
            // different object from the one InputTestFixture drives.
            _go = new GameObject("InputProviderUnityTest");
            _go.AddComponent<PlayerInput>();

            _provider = _go.AddComponent<InputProviderUnity>();
            // Bind directly to the raw asset — the same object Press()/Set() drive.
            _provider.RebindActionsForTesting(actions);
        }

        [TearDown]
        public override void TearDown()
        {
            if (_go != null)
                UnityEngine.Object.DestroyImmediate(_go);
            base.TearDown();
        }

        // ── AC-ISW-01: WASD Diagonal Normalization ────────────────────────────────

        [Test]
        [Description("AC-ISW-01: W+D simultaneously → GetMoveVector() is approximately (0.707, 0.707)")]
        public void GetMoveVector_WAndDHeld_ReturnsNormalizedDiagonal()
        {
            Press(_keyboard.wKey);
            Press(_keyboard.dKey);
            InputSystem.Update(); // flush queued events

            Vector2 move = _provider.GetMoveVector();

            Assert.AreEqual(1f, move.magnitude, 0.05f,
                "GetMoveVector must return a normalized vector");
            Assert.AreEqual(0.707f, move.x, 0.05f,
                "X component of W+D diagonal must be ≈ 0.707");
            Assert.AreEqual(0.707f, move.y, 0.05f,
                "Y component of W+D diagonal must be ≈ 0.707");

            Release(_keyboard.wKey);
            Release(_keyboard.dKey);
        }

        [Test]
        [Description("AC-ISW-01 edge case: only W held → GetMoveVector() = (0, 1)")]
        public void GetMoveVector_OnlyWHeld_ReturnsUp()
        {
            Press(_keyboard.wKey);
            InputSystem.Update();

            Vector2 move = _provider.GetMoveVector();

            Assert.AreEqual(0f, move.x, 0.05f, "X must be 0 when only W is held");
            Assert.AreEqual(1f, move.y, 0.05f, "Y must be 1 when only W is held");

            Release(_keyboard.wKey);
        }

        [Test]
        [Description("AC-ISW-01 edge case: no input → GetMoveVector() = Vector2.zero")]
        public void GetMoveVector_NoInput_ReturnsZero()
        {
            InputSystem.Update();

            Vector2 move = _provider.GetMoveVector();

            Assert.AreEqual(Vector2.zero, move,
                "GetMoveVector must return zero when no movement input is active");
        }

        // ── AC-ISW-02: Gamepad Stick ──────────────────────────────────────────────

        [Test]
        [Description("AC-ISW-02: Gamepad left stick full-right → GetMoveVector() = (1, 0)")]
        public void GetMoveVector_GamepadFullRight_ReturnsOneZero()
        {
            Set(_gamepad.leftStick, new Vector2(1f, 0f));
            InputSystem.Update();

            Vector2 move = _provider.GetMoveVector();

            Assert.AreEqual(1f, move.x, 0.05f, "X must be ≈ 1 for full-right stick");
            Assert.AreEqual(0f, move.y, 0.05f, "Y must be ≈ 0 for full-right stick");

            Set(_gamepad.leftStick, Vector2.zero);
        }

        // ── AC-ISW-03: Pause Debounce ─────────────────────────────────────────────

        [Test]
        [Description("AC-ISW-03: Escape at t=0 fires OnPausePressed; second Escape within 0.2s does NOT fire")]
        public void OnPausePressed_RapidEscape_FiresOnlyOnceInDebounceWindow()
        {
            int pauseCount = 0;
            _provider.OnPausePressed += () => pauseCount++;

            // First press at simulated t=1.0
            currentTime = 1.0;
            Press(_keyboard.escapeKey);
            InputSystem.Update();
            Release(_keyboard.escapeKey);
            InputSystem.Update();

            int countAfterFirst = pauseCount;

            // Second press 0.05s later — still within the 0.2s debounce window
            currentTime = 1.05;
            Press(_keyboard.escapeKey);
            InputSystem.Update();
            Release(_keyboard.escapeKey);
            InputSystem.Update();

            int countAfterSecond = pauseCount;

            Assert.AreEqual(1, countAfterFirst,
                "First Escape press must fire OnPausePressed");
            Assert.AreEqual(1, countAfterSecond,
                "Second Escape within debounce window must NOT fire OnPausePressed");
        }

        [Test]
        [Description("AC-ISW-03: Escape after debounce window (>0.2s) fires again")]
        public void OnPausePressed_EscapeAfterDebounceWindow_FiresAgain()
        {
            int pauseCount = 0;
            _provider.OnPausePressed += () => pauseCount++;

            // First press at simulated t=1.0
            currentTime = 1.0;
            Press(_keyboard.escapeKey);
            InputSystem.Update();
            Release(_keyboard.escapeKey);
            InputSystem.Update();

            // Advance past debounce window (0.2s + buffer) and press again
            currentTime = 1.3;
            Press(_keyboard.escapeKey);
            InputSystem.Update();
            Release(_keyboard.escapeKey);
            InputSystem.Update();

            Assert.AreEqual(2, pauseCount,
                "OnPausePressed must fire again after debounce window expires");
        }

        // ── Helper: Fallback Actions Asset ────────────────────────────────────────

        private static InputActionAsset BuildFallbackInputActions()
        {
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            var map   = asset.AddActionMap("Player");

            // Move — 2D composite WASD + gamepad stick
            var moveAction = map.AddAction("Move", InputActionType.Value);
            moveAction.expectedControlType = "Vector2";
            var wasdComposite = moveAction.AddCompositeBinding("2DVector");
            wasdComposite.With("Up",    "<Keyboard>/w");
            wasdComposite.With("Down",  "<Keyboard>/s");
            wasdComposite.With("Left",  "<Keyboard>/a");
            wasdComposite.With("Right", "<Keyboard>/d");
            moveAction.AddBinding("<Gamepad>/leftStick");

            // Confirm
            var confirmAction = map.AddAction("Confirm", InputActionType.Button);
            confirmAction.AddBinding("<Keyboard>/space");
            confirmAction.AddBinding("<Keyboard>/enter");
            confirmAction.AddBinding("<Gamepad>/buttonSouth");

            // Cancel
            var cancelAction = map.AddAction("Cancel", InputActionType.Button);
            cancelAction.AddBinding("<Keyboard>/escape");
            cancelAction.AddBinding("<Gamepad>/buttonEast");

            // Pause
            var pauseAction = map.AddAction("Pause", InputActionType.Button);
            pauseAction.AddBinding("<Keyboard>/escape");
            pauseAction.AddBinding("<Gamepad>/start");

            map.Enable();
            return asset;
        }
    }
}

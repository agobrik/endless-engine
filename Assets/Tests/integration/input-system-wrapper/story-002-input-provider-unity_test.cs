// Tests for Story S1-16: Input System — InputProviderUnity Integration
// Type: Integration (PlayMode)
// Story: production/epics/input-system-wrapper/story-002-input-provider-unity.md
//
// These tests verify:
//   (1) AC-ISW-01: WASD diagonal → GetMoveVector() normalized to ~(0.707, 0.707)
//   (2) AC-ISW-02: Gamepad left stick full-right → GetMoveVector() = (1, 0)
//   (3) AC-ISW-03: Pause debounce — OnPausePressed fires once per window, not on rapid repeat
//
// Requires: Unity Input System package with test support (com.unity.inputsystem)
// To run: Unity Test Runner → PlayMode → EndlessEngine.Tests.Integration.InputSystemWrapper
//
// NOTE: InputTestFixture requires the Input System package's test assembly.
//   Add "com.unity.inputsystem" to testables in Packages/manifest.json if tests
//   cannot resolve InputTestFixture.

using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
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
        private GameObject        _go;
        private InputProviderUnity _provider;
        private PlayerInput        _playerInput;

        // Simulated devices
        private Keyboard _keyboard;
        private Gamepad  _gamepad;

        // ── SetUp / TearDown ──────────────────────────────────────────────────────

        [SetUp]
        public override void Setup()
        {
            base.Setup();

            // Register simulated devices
            _keyboard = InputSystem.AddDevice<Keyboard>();
            _gamepad  = InputSystem.AddDevice<Gamepad>();

            // Build a GameObject with PlayerInput wired to our Actions asset
            _go = new GameObject("InputProviderUnityTest");

            _playerInput = _go.AddComponent<PlayerInput>();

            // Load the Input Actions asset from the project
            var actions = Resources.Load<InputActionAsset>("Input/GameInputActions");
            if (actions == null)
            {
                // Fallback: build the asset inline so tests can run without the asset on the path
                actions = BuildFallbackInputActions();
            }
            _playerInput.actions = actions;

            _provider = _go.AddComponent<InputProviderUnity>();
        }

        [TearDown]
        public override void TearDown()
        {
            if (_go != null)
                UnityEngine.Object.DestroyImmediate(_go);
            base.TearDown();
        }

        // ── AC-ISW-01: WASD Diagonal Normalization ────────────────────────────────

        [UnityTest]
        [Description("AC-ISW-01: W+D simultaneously → GetMoveVector() is approximately (0.707, 0.707)")]
        public IEnumerator GetMoveVector_WAndDHeld_ReturnsNormalizedDiagonal()
        {
            // Arrange — hold W and D
            Press(_keyboard.wKey);
            Press(_keyboard.dKey);
            yield return null; // one frame to let Input System process

            // Act
            Vector2 move = _provider.GetMoveVector();

            // Assert — magnitude ≈ 1.0, direction ≈ (0.707, 0.707)
            Assert.AreEqual(1f, move.magnitude, 0.05f,
                "GetMoveVector must return a normalized vector");
            Assert.AreEqual(0.707f, move.x, 0.05f,
                "X component of W+D diagonal must be ≈ 0.707");
            Assert.AreEqual(0.707f, move.y, 0.05f,
                "Y component of W+D diagonal must be ≈ 0.707");

            // Cleanup
            Release(_keyboard.wKey);
            Release(_keyboard.dKey);
        }

        [UnityTest]
        [Description("AC-ISW-01 edge case: only W held → GetMoveVector() = (0, 1)")]
        public IEnumerator GetMoveVector_OnlyWHeld_ReturnsUp()
        {
            // Arrange
            Press(_keyboard.wKey);
            yield return null;

            // Act
            Vector2 move = _provider.GetMoveVector();

            // Assert
            Assert.AreEqual(0f,  move.x, 0.05f, "X must be 0 when only W is held");
            Assert.AreEqual(1f,  move.y, 0.05f, "Y must be 1 when only W is held");

            Release(_keyboard.wKey);
        }

        [UnityTest]
        [Description("AC-ISW-01 edge case: no input → GetMoveVector() = Vector2.zero")]
        public IEnumerator GetMoveVector_NoInput_ReturnsZero()
        {
            yield return null;

            Vector2 move = _provider.GetMoveVector();

            Assert.AreEqual(Vector2.zero, move,
                "GetMoveVector must return zero when no movement input is active");
        }

        // ── AC-ISW-02: Gamepad Stick ──────────────────────────────────────────────

        [UnityTest]
        [Description("AC-ISW-02: Gamepad left stick full-right → GetMoveVector() = (1, 0)")]
        public IEnumerator GetMoveVector_GamepadFullRight_ReturnsOneZero()
        {
            // Arrange — push left stick fully to the right
            Set(_gamepad.leftStick, new Vector2(1f, 0f));
            yield return null;

            // Act
            Vector2 move = _provider.GetMoveVector();

            // Assert — after normalization + deadzone, should be (1, 0)
            Assert.AreEqual(1f, move.x, 0.05f, "X must be ≈ 1 for full-right stick");
            Assert.AreEqual(0f, move.y, 0.05f, "Y must be ≈ 0 for full-right stick");

            Set(_gamepad.leftStick, Vector2.zero);
        }

        // ── AC-ISW-03: Pause Debounce ─────────────────────────────────────────────

        [UnityTest]
        [Description("AC-ISW-03: Escape at t=0 fires OnPausePressed; second Escape within 0.2s does NOT fire")]
        public IEnumerator OnPausePressed_RapidEscape_FiresOnlyOnceInDebounceWindow()
        {
            // Arrange
            int pauseCount = 0;
            _provider.OnPausePressed += () => pauseCount++;

            // Act — first press at t=0
            Press(_keyboard.escapeKey);
            yield return null; // Update runs, debounce timer set to 0.2s
            Release(_keyboard.escapeKey);

            int countAfterFirst = pauseCount;

            // Second press within debounce (0.05s elapsed — well within 0.2s)
            yield return new WaitForSeconds(0.05f);
            Press(_keyboard.escapeKey);
            yield return null;
            Release(_keyboard.escapeKey);

            int countAfterSecond = pauseCount;

            // Assert — first press fired, second was suppressed
            Assert.AreEqual(1, countAfterFirst,
                "First Escape press must fire OnPausePressed");
            Assert.AreEqual(1, countAfterSecond,
                "Second Escape within debounce window must NOT fire OnPausePressed");
        }

        [UnityTest]
        [Description("AC-ISW-03: Escape after debounce window (>0.2s) fires again")]
        public IEnumerator OnPausePressed_EscapeAfterDebounceWindow_FiresAgain()
        {
            // Arrange
            int pauseCount = 0;
            _provider.OnPausePressed += () => pauseCount++;

            // First press
            Press(_keyboard.escapeKey);
            yield return null;
            Release(_keyboard.escapeKey);

            // Wait for debounce to expire (0.2s + small buffer)
            yield return new WaitForSeconds(0.25f);

            // Second press after window
            Press(_keyboard.escapeKey);
            yield return null;
            Release(_keyboard.escapeKey);

            // Assert — both presses fired
            Assert.AreEqual(2, pauseCount,
                "OnPausePressed must fire again after debounce window expires");
        }

        // ── Helper: Fallback Actions Asset ────────────────────────────────────────

        /// <summary>
        /// Builds a minimal InputActionAsset inline when the project's
        /// <c>Assets/Input/GameInputActions.inputactions</c> is not loadable via
        /// <see cref="Resources.Load{T}"/> (i.e., the asset is not in a Resources folder).
        /// This ensures tests can run in CI without the full asset pipeline.
        /// </summary>
        private static InputActionAsset BuildFallbackInputActions()
        {
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            var map   = asset.AddActionMap("Player");

            // Move — 2D composite WASD
            var moveAction = map.AddAction("Move", InputActionType.Value);
            moveAction.expectedControlType = "Vector2";
            var wasdComposite = moveAction.AddCompositeBinding("2DVector(mode=2)"); // mode=2 = DigitalNormalized
            wasdComposite.With("Up",    "<Keyboard>/w",    "Keyboard&Mouse");
            wasdComposite.With("Down",  "<Keyboard>/s",    "Keyboard&Mouse");
            wasdComposite.With("Left",  "<Keyboard>/a",    "Keyboard&Mouse");
            wasdComposite.With("Right", "<Keyboard>/d",    "Keyboard&Mouse");
            moveAction.AddBinding("<Gamepad>/leftStick", processors: "StickDeadzone,NormalizeVector2",
                groups: "Gamepad");

            // Confirm
            var confirmAction = map.AddAction("Confirm", InputActionType.Button);
            confirmAction.AddBinding("<Keyboard>/space",        groups: "Keyboard&Mouse");
            confirmAction.AddBinding("<Keyboard>/enter",        groups: "Keyboard&Mouse");
            confirmAction.AddBinding("<Gamepad>/buttonSouth",   groups: "Gamepad");

            // Cancel
            var cancelAction = map.AddAction("Cancel", InputActionType.Button);
            cancelAction.AddBinding("<Keyboard>/escape",        groups: "Keyboard&Mouse");
            cancelAction.AddBinding("<Gamepad>/buttonEast",     groups: "Gamepad");

            // Pause
            var pauseAction = map.AddAction("Pause", InputActionType.Button);
            pauseAction.AddBinding("<Keyboard>/escape",         groups: "Keyboard&Mouse");
            pauseAction.AddBinding("<Gamepad>/start",           groups: "Gamepad");

            map.Enable();
            return asset;
        }
    }
}
